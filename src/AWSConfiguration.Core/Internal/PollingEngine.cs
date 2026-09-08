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
/// Internal engine that owns the load/reload lifecycle, change detection, polling machinery,
/// and duplicate-key handling for AWS-backed configuration providers. Concrete providers
/// compose this engine (instead of inheriting from it) so their public API surface only
/// exposes Microsoft abstractions.
/// </summary>
internal sealed class PollingEngine : IDisposable
{
    private readonly ILogger? _logger;
    private readonly string _resourceDescription;
    private readonly string _resourceNoun;
    private readonly string _duplicateKeyOptionsHint;
    private readonly Func<CancellationToken, Task<HashSet<(string, string?)>>> _fetchConfiguration;
    private readonly Func<TimeSpan?> _pollingInterval;
    private readonly Action<Dictionary<string, string?>, bool> _commitData;
    private HashSet<(string, string?)> _loadedValues = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingEngine"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <param name="resourceDescription">Describes the resource being loaded, used to compose log messages (example: "secrets from AWS Secrets Manager").</param>
    /// <param name="resourceNoun">The noun used for polling log messages (example: "secret").</param>
    /// <param name="duplicateKeyOptionsHint">The advisory sentence appended to the duplicate-key exception message.</param>
    /// <param name="fetchConfiguration">Fetches the current set of configuration values from the backing AWS service.</param>
    /// <param name="pollingInterval">Returns the time to wait before refreshing values, or null to disable polling.</param>
    /// <param name="commitData">Commits built configuration data. Receives the data and a flag indicating whether a change token should be published (reload) or not (initial load).</param>
    public PollingEngine(
        ILogger? logger,
        string resourceDescription,
        string resourceNoun,
        string duplicateKeyOptionsHint,
        Func<CancellationToken, Task<HashSet<(string, string?)>>> fetchConfiguration,
        Func<TimeSpan?> pollingInterval,
        Action<Dictionary<string, string?>, bool> commitData)
    {
        _logger = logger;
        _resourceDescription = resourceDescription;
        _resourceNoun = resourceNoun;
        _duplicateKeyOptionsHint = duplicateKeyOptionsHint;
        _fetchConfiguration = fetchConfiguration;
        _pollingInterval = pollingInterval;
        _commitData = commitData;
    }

    /// <summary>
    /// Adds a configuration value, warning if the generated key was already added.
    /// </summary>
    /// <param name="configuration">The set of configuration values being built.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    public void AddConfigurationValue(HashSet<(string, string?)> configuration, string key, string? value)
    {
        if (!configuration.Add((key, value)))
        {
            _logger?.Warning("Duplicate configuration key '{ConfigurationKey}' was generated more than once with an identical value; the extra entry was ignored", key);
        }
    }

    /// <summary>
    /// Loads the configuration data from the backing AWS service.
    /// </summary>
    public void Load()
    {
        // Note: Using GetAwaiter().GetResult() is required here because the ConfigurationProvider.Load()
        // method must be synchronous, but AWS SDK operations are async-only. This follows the same
        // pattern used by other configuration providers that integrate with async-only services.
        // The ConfigureAwait(false) helps prevent deadlocks in synchronization contexts.
        if (_logger != null)
        {
            _logger.Time($"Loading {_resourceDescription}", () =>
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
    /// Releases all resources used by the engine, stopping any active polling.
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

    private async Task LoadAsync()
    {
        var values = await _fetchConfiguration(default).ConfigureAwait(false);

        // Build the data before committing any state so a duplicate-key failure
        // cannot leave _loadedValues out of sync with the data that was loaded.
        var data = BuildData(values);

        _commitData(data, false);
        _loadedValues = values;

        if (_pollingInterval() is { } interval)
        {
            await StopPollingAsync().ConfigureAwait(false);

            _cancellationToken = new CancellationTokenSource();
            _pollingTask = PollForChangesAsync(interval, _cancellationToken.Token);
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
        _logger?.Information("Starting {ResourceNoun} polling with interval {PollingInterval}", _resourceNoun, interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            try
            {
                _logger?.Debug("Polling for {ResourceNoun} changes", _resourceNoun);
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - break without logging
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error during {ResourceNoun} polling, will retry in {PollingInterval}", _resourceNoun, interval);
            }
        }

        _logger?.Information("{ResourceNoun} polling stopped", _resourceNoun);
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (_logger != null)
        {
            await _logger.TimeAsync($"Reloading {_resourceDescription}",
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

        var newValues = await _fetchConfiguration(cancellationToken).ConfigureAwait(false);

        if (oldValues.SetEquals(newValues))
        {
            _logger?.Debug("No {ResourceNoun} changes detected", _resourceNoun);
            return;
        }

        // Build the data before committing any state so a duplicate-key failure
        // leaves _loadedValues unchanged and the next poll retries the reload.
        var data = BuildData(newValues);

        _loadedValues = newValues;
        _commitData(data, true);

        var addedCount = newValues.Except(oldValues).Count();
        var removedCount = oldValues.Except(newValues).Count();
        _logger?.Information("{ResourceNoun} changes detected and reloaded. {AddedCount} added, {RemovedCount} removed",
            _resourceNoun, addedCount, removedCount);
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
                    _duplicateKeyOptionsHint);
            }

            data[key] = value;
        }

        return data;
    }
}
