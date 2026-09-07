using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSSecretsManager.Provider.Internal;
using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider.Internal;

/// <summary>
/// Configuration provider that loads parameters from AWS SSM Parameter Store.
/// </summary>
public class SsmConfigurationProvider : ConfigurationProvider, IDisposable
{
    /// <summary>
    /// Gets the configuration options for the SSM provider.
    /// </summary>
    public SsmConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Gets the AWS SSM client used to retrieve parameters.
    /// </summary>
    public IAmazonSimpleSystemsManagement Client { get; }

    private readonly ILogger? _logger;
    private HashSet<(string, string?)> _loadedValues = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsmConfigurationProvider"/> class.
    /// </summary>
    /// <param name="client">The AWS SSM client.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when client or options are null.</exception>
    public SsmConfigurationProvider(IAmazonSimpleSystemsManagement client, SsmConfigurationProviderOptions options, ILogger? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Loads the configuration data from AWS SSM Parameter Store.
    /// </summary>
    public override void Load()
    {
        // Note: Using GetAwaiter().GetResult() is required here because the ConfigurationProvider.Load()
        // method must be synchronous, but AWS SDK operations are async-only. This follows the same
        // pattern used by other configuration providers that integrate with async-only services.
        // The ConfigureAwait(false) helps prevent deadlocks in synchronization contexts.
        if (_logger != null)
        {
            _logger.Time("Loading parameters from AWS SSM Parameter Store", () =>
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
    /// Forces a reload of the configuration data from AWS SSM Parameter Store.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ForceReloadAsync(CancellationToken cancellationToken)
    {
        return ReloadAsync(cancellationToken);
    }

    private async Task LoadAsync()
    {
        _loadedValues = await FetchConfigurationAsync(default).ConfigureAwait(false);

        SetData(_loadedValues, triggerReload: false);

        if (Options.PollingInterval.HasValue)
        {
            await StopPollingAsync().ConfigureAwait(false);

            _cancellationToken = new CancellationTokenSource();
            _pollingTask = PollForChangesAsync(Options.PollingInterval.Value, _cancellationToken.Token);
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
        _logger?.Information("Starting SSM parameter polling with interval {PollingInterval}", interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            try
            {
                _logger?.Debug("Polling for parameter changes");
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - break without logging
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error during SSM parameter polling, will retry in {PollingInterval}", interval);
            }
        }

        _logger?.Information("SSM parameter polling stopped");
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (_logger != null)
        {
            await _logger.TimeAsync("Reloading parameters from AWS SSM Parameter Store",
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

        if (!oldValues.SetEquals(newValues))
        {
            _loadedValues = newValues;
            SetData(_loadedValues, triggerReload: true);

            var addedCount = newValues.Except(oldValues).Count();
            var removedCount = oldValues.Except(newValues).Count();
            _logger?.Information("Parameter changes detected and reloaded. {AddedCount} added, {RemovedCount} removed",
                addedCount, removedCount);
        }
        else
        {
            _logger?.Debug("No parameter changes detected");
        }
    }

    private async Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = new HashSet<(string, string?)>();
        var response = default(GetParametersByPathResponse);

        do
        {
            var request = new GetParametersByPathRequest
            {
                Path = Options.Path,
                Recursive = Options.Recursive,
                WithDecryption = Options.WithDecryption,
                NextToken = response?.NextToken
            };

            response = await Client.GetParametersByPathAsync(request, cancellationToken).ConfigureAwait(false);

            foreach (var parameter in response.Parameters)
            {
                if (!Options.ParameterFilter(parameter))
                    continue;

                var value = parameter.Value;

                if (value is null)
                    continue;

                var configurationKey = Options.KeyGenerator(parameter.Name, Options.Path);

                if (JsonFlattener.TryParseJson(value, out var jElement))
                {
                    foreach (var (key, item) in JsonFlattener.ExtractValues(jElement!, configurationKey))
                    {
                        AddConfigurationValue(configuration, key, item);
                    }
                }
                else
                {
                    AddConfigurationValue(configuration, configurationKey, value);
                }
            }
        } while (response.NextToken != null);

        return configuration;
    }

    private void AddConfigurationValue(HashSet<(string, string?)> configuration, string key, string? value)
    {
        if (!configuration.Add((key, value)))
        {
            _logger?.Warning("Duplicate configuration key '{ConfigurationKey}' was generated more than once; the first value is kept", key);
        }
    }

    private void SetData(IEnumerable<(string, string?)> values, bool triggerReload)
    {
        var data = new Dictionary<string, string?>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (data.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Configuration key '{key}' was generated more than once (keys are case-insensitive). " +
                    "Adjust the KeyGenerator or ParameterFilter options so each parameter maps to a unique key.");
            }

            data[key] = value;
        }

        Data = data;

        if (triggerReload)
        {
            OnReload();
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="SsmConfigurationProvider"/>.
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
}
