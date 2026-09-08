using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSConfiguration.Core.Internal;

/// <summary>
/// Base class for configuration providers that load AWS-backed key/value pairs and optionally
/// poll for changes in the background. Owns the load/reload lifecycle, change detection,
/// polling machinery, and duplicate-key handling so concrete providers only implement fetching.
/// </summary>
public abstract class PollingConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly ILogger? _logger;
    private HashSet<(string, string?)> _loadedValues = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingConfigurationProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    protected PollingConfigurationProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Describes the resource being loaded, used to compose log messages.
    /// Example: "secrets from AWS Secrets Manager".
    /// </summary>
    protected abstract string ResourceDescription { get; }

    /// <summary>
    /// The noun used for polling log messages.
    /// Example: "secret".
    /// </summary>
    protected abstract string ResourceNoun { get; }

    /// <summary>
    /// The advisory sentence appended to the duplicate-key exception message,
    /// directing users to the filter/key-generator options that must be adjusted.
    /// </summary>
    protected abstract string DuplicateKeyOptionsHint { get; }

    /// <summary>
    /// The time that should be waited before refreshing values.
    /// If null, values will not be refreshed.
    /// </summary>
    protected abstract TimeSpan? PollingInterval { get; }

    /// <summary>
    /// Fetches the current set of configuration values from the backing AWS service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of configuration key/value pairs.</returns>
    protected abstract Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads the configuration data from the backing AWS service.
    /// </summary>
    public override void Load()
    {
        // Note: Using GetAwaiter().GetResult() is required here because the ConfigurationProvider.Load()
        // method must be synchronous, but AWS SDK operations are async-only. This follows the same
        // pattern used by other configuration providers that integrate with async-only services.
        // The ConfigureAwait(false) helps prevent deadlocks in synchronization contexts.
        if (_logger != null)
        {
            _logger.Time($"Loading {ResourceDescription}", () =>
            {
                LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            });
        }
        else
        {
            LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Forces a reload of the configuration data from the backing AWS service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ForceReloadAsync(CancellationToken cancellationToken)
    {
        return ReloadAsync(cancellationToken);
    }

    /// <summary>
    /// Releases all resources used by the provider, stopping any active polling.
    /// </summary>
    public void Dispose()
    {
        _cancellationToken?.Cancel();

        try
        {
            _pollingTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _cancellationToken?.Dispose();
        _cancellationToken = null;
        _pollingTask = null;
    }

    /// <summary>
    /// Adds a configuration value, warning if the generated key was already added.
    /// </summary>
    /// <param name="configuration">The set of configuration values being built.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    protected void AddConfigurationValue(HashSet<(string, string?)> configuration, string key, string? value)
    {
        if (!configuration.Add((key, value)))
        {
            _logger?.Warning("Duplicate configuration key '{ConfigurationKey}' was generated more than once with an identical value; the extra entry was ignored", key);
        }
    }

    private async Task LoadAsync()
    {
        var values = await FetchConfigurationAsync(default).ConfigureAwait(false);

        // Build the data before committing any state so a duplicate-key failure
        // cannot leave _loadedValues out of sync with the data that was loaded.
        Data = BuildData(values);
        _loadedValues = values;

        if (PollingInterval.HasValue)
        {
            await StopPollingAsync().ConfigureAwait(false);

            _cancellationToken = new CancellationTokenSource();
            _pollingTask = PollForChangesAsync(PollingInterval.Value, _cancellationToken.Token);
        }
    }

    private async Task StopPollingAsync()
    {
        if (_cancellationToken is null && _pollingTask is null)
        {
            return;
        }

        _cancellationToken?.Cancel();

        try
        {
            await _pollingTask!.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the poller was cancelled during shutdown or a restart.
        }

        _cancellationToken?.Dispose();
        _cancellationToken = null;
        _pollingTask = null;
    }

    private async Task PollForChangesAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        _logger?.Information("Starting {ResourceNoun} polling with interval {PollingInterval}", ResourceNoun, interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            try
            {
                _logger?.Debug("Polling for {ResourceNoun} changes", ResourceNoun);
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - break without logging
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error during {ResourceNoun} polling, will retry in {PollingInterval}", ResourceNoun, interval);
            }
        }

        _logger?.Information("{ResourceNoun} polling stopped", ResourceNoun);
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (_logger != null)
        {
            await _logger.TimeAsync($"Reloading {ResourceDescription}",
                () => ReloadCoreAsync(cancellationToken)).ConfigureAwait(false);
        }
        else
        {
            await ReloadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReloadCoreAsync(CancellationToken cancellationToken)
    {
        var oldValues = _loadedValues;

        var newValues = await FetchConfigurationAsync(cancellationToken).ConfigureAwait(false);

        if (oldValues.SetEquals(newValues))
        {
            _logger?.Debug("No {ResourceNoun} changes detected", ResourceNoun);
            return;
        }

        // Build the data before committing any state so a duplicate-key failure
        // leaves _loadedValues unchanged and the next poll retries the reload.
        var data = BuildData(newValues);

        _loadedValues = newValues;
        Data = data;
        OnReload();

        var addedCount = newValues.Except(oldValues).Count();
        var removedCount = oldValues.Except(newValues).Count();
        _logger?.Information("{ResourceNoun} changes detected and reloaded. {AddedCount} added, {RemovedCount} removed",
            ResourceNoun, addedCount, removedCount);
    }

    private Dictionary<string, string?> BuildData(HashSet<(string, string?)> values)
    {
        var data = new Dictionary<string, string?>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (data.TryGetValue(key, out var existingValue))
            {
                if (string.Equals(existingValue, value, StringComparison.Ordinal))
                {
                    _logger?.Warning("Duplicate configuration key '{ConfigurationKey}' was generated more than once with an identical value; the extra entry was ignored", key);
                    continue;
                }

                throw new InvalidOperationException(
                    $"Configuration key '{key}' was generated more than once with different values (keys are case-insensitive). " +
                    DuplicateKeyOptionsHint);
            }

            data[key] = value;
        }

        return data;
    }
}
