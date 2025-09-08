using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LayeredCraft.StructuredLogging;

namespace AWSSecretsManager.Provider.Internal;

/// <summary>
/// Configuration provider that loads secrets from AWS Secrets Manager.
/// </summary>
public class SecretsManagerConfigurationProvider : ConfigurationProvider, IDisposable
{
    /// <summary>
    /// Gets the configuration options for the secrets manager provider.
    /// </summary>
    public SecretsManagerConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Gets the AWS Secrets Manager client used to retrieve secrets.
    /// </summary>
    public IAmazonSecretsManager Client { get; }

    private readonly ILogger? _logger;
    private HashSet<(string, string)> _loadedValues = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationProvider"/> class.
    /// </summary>
    /// <param name="client">The AWS Secrets Manager client.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when client or options are null.</exception>
    public SecretsManagerConfigurationProvider(IAmazonSecretsManager client, SecretsManagerConfigurationProviderOptions options, ILogger? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Loads the configuration data from AWS Secrets Manager.
    /// </summary>
    public override void Load()
    {
        // Note: Using GetAwaiter().GetResult() is required here because the ConfigurationProvider.Load()
        // method must be synchronous, but AWS SDK operations are async-only. This follows the same
        // pattern used by other configuration providers that integrate with async-only services.
        // The ConfigureAwait(false) helps prevent deadlocks in synchronization contexts.
        if (_logger != null)
        {
            _logger.Time("Loading secrets from AWS Secrets Manager", () =>
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
    /// Forces a reload of the configuration data from AWS Secrets Manager.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ForceReloadAsync(CancellationToken cancellationToken)
    {
        return ReloadAsync(cancellationToken);
    }

    private async Task LoadAsync()
    {
        _loadedValues = Options.UseBatchFetch switch
        {
            true => await FetchConfigurationBatchAsync(default).ConfigureAwait(false),
            _ => await FetchConfigurationAsync(default).ConfigureAwait(false)
        };

        SetData(_loadedValues, triggerReload: false);


        if (Options.PollingInterval.HasValue)
        {
            _cancellationToken = new CancellationTokenSource();
            _pollingTask = PollForChangesAsync(Options.PollingInterval.Value, _cancellationToken.Token);
        }
    }

    private async Task PollForChangesAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        _logger?.Information("Starting secret polling with interval {PollingInterval}", interval);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            try
            {
                _logger?.Debug("Polling for secret changes");
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - break without logging
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error during secret polling, will retry in {PollingInterval}", interval);
            }
        }
        
        _logger?.Information("Secret polling stopped");
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (_logger != null)
        {
            await _logger.TimeAsync("Reloading secrets from AWS Secrets Manager", async () =>
            {
                var oldValues = _loadedValues;

                var newValues = Options.UseBatchFetch switch
                {
                    true => await FetchConfigurationBatchAsync(cancellationToken).ConfigureAwait(false),
                    _ => await FetchConfigurationAsync(cancellationToken).ConfigureAwait(false)
                };

                if (!oldValues.SetEquals(newValues))
                {
                    _loadedValues = newValues;
                    SetData(_loadedValues, triggerReload: true);
                    
                    var addedCount = newValues.Except(oldValues).Count();
                    var removedCount = oldValues.Except(newValues).Count();
                    _logger.Information("Secret changes detected and reloaded. {AddedCount} added, {RemovedCount} removed",
                        addedCount, removedCount);
                }
                else
                {
                    _logger.Debug("No secret changes detected");
                }
            });
        }
        else
        {
            var oldValues = _loadedValues;

            var newValues = Options.UseBatchFetch switch
            {
                true => await FetchConfigurationBatchAsync(cancellationToken).ConfigureAwait(false),
                _ => await FetchConfigurationAsync(cancellationToken).ConfigureAwait(false)
            };

            if (!oldValues.SetEquals(newValues))
            {
                _loadedValues = newValues;
                SetData(_loadedValues, triggerReload: true);
            }
        }
    }

    private static bool TryParseJson(string data, out JsonElement? jsonElement)
    {
        jsonElement = null;

        data = data.TrimStart();
        var firstChar = data.FirstOrDefault();

        if (firstChar != '[' && firstChar != '{')
        {
            return false;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(data);
            //  https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-use-dom-utf8jsonreader-utf8jsonwriter?pivots=dotnet-6-0#jsondocument-is-idisposable
            //  Its recommended to return the clone of the root element as the json document will be disposed
            jsonElement = jsonDocument.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<(string key, string value)> ExtractValues(JsonElement? jsonElement, string prefix)
    {
        if (jsonElement == null)
        {
            yield break;
        }
        var element = jsonElement.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var currentIndex = 0;
                foreach (var el in element.EnumerateArray())
                {
                    var secretKey = $"{prefix}{ConfigurationPath.KeyDelimiter}{currentIndex}";
                    foreach (var (key, value) in ExtractValues(el, secretKey))
                    {
                        yield return (key, value);
                    }
                    currentIndex++;
                }
                break;
            }
            case JsonValueKind.Number:
            {
                var value = element.GetRawText();
                yield return (prefix, value);
                break;
            }
            case JsonValueKind.String:
            {
                var value = element.GetString() ?? "";
                yield return (prefix, value);
                break;
            }
            case JsonValueKind.True:
            {
                var value = element.GetBoolean();
                yield return (prefix, value.ToString());
                break;
            }
            case JsonValueKind.False:
            {
                var value = element.GetBoolean();
                yield return (prefix, value.ToString());
                break;
            }
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    var secretKey = $"{prefix}{ConfigurationPath.KeyDelimiter}{property.Name}";
                    foreach (var (key, value) in ExtractValues(property.Value, secretKey))
                    {
                        yield return (key, value);
                    }
                }
                break;
            }
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            default:
            {
                throw new FormatException("unsupported json token");
            }
        }
    }

    private void SetData(IEnumerable<(string, string)> values, bool triggerReload)
    {
        Data = values.ToDictionary<(string, string), string, string?>(x => x.Item1, x => x.Item2, StringComparer.InvariantCultureIgnoreCase);
        if (triggerReload)
        {
            OnReload();
        }
    }

    private async Task<IReadOnlyList<SecretListEntry>> FetchAllSecretsAsync(CancellationToken cancellationToken)
    {
        var response = default(ListSecretsResponse);

        if (Options.AcceptedSecretArns.Count > 0)
        {
            return Options.AcceptedSecretArns.Select(x => new SecretListEntry { ARN = x, Name = x }).ToList();
        }

        var result = new List<SecretListEntry>();

        do
        {
            var nextToken = response?.NextToken;

            var request = new ListSecretsRequest { NextToken = nextToken, Filters = Options.ListSecretsFilters };

            response = await Client.ListSecretsAsync(request, cancellationToken).ConfigureAwait(false);

            result.AddRange(response.SecretList);
        } while (response.NextToken != null);
        return result;
    }

    private async Task<HashSet<(string, string)>> FetchConfigurationAsync(CancellationToken cancellationToken)
    {
        var secrets = await FetchAllSecretsAsync(cancellationToken).ConfigureAwait(false);
        var configuration = new HashSet<(string, string)>();
        foreach (var secret in secrets)
        {
            try
            {
                if (!Options.SecretFilter(secret)) continue;

                var request = new GetSecretValueRequest { SecretId = secret.ARN };
                Options.ConfigureSecretValueRequest?.Invoke(request, new SecretValueContext(secret));
                GetSecretValueResponse? secretValue;

                try
                {
                    secretValue = await Client.GetSecretValueAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (ResourceNotFoundException) when (Options.IgnoreMissingValues)
                {
                    continue;
                }

                var secretEntry = Options.AcceptedSecretArns.Count > 0
                    ? new SecretListEntry
                    {
                        ARN = secret.ARN,
                        Name = secretValue.Name,
                        CreatedDate = secretValue.CreatedDate
                    }
                    : secret;

                var secretName = secretEntry.Name;
                var secretString = secretValue.SecretString;

                if (secretString is null)
                    continue;

                if (TryParseJson(secretString, out var jElement))
                {
                    // [MaybeNullWhen(false)] attribute is available in .net standard since version 2.1
                    var values = ExtractValues(jElement!, secretName);

                    foreach (var (key, value) in values)
                    {
                        var configurationKey = Options.KeyGenerator(secretEntry, key);
                        configuration.Add((configurationKey, value));
                    }
                }
                else
                {
                    var configurationKey = Options.KeyGenerator(secretEntry, secretName);
                    configuration.Add((configurationKey, secretString));
                }
            }
            catch (ResourceNotFoundException e)
            {
                throw new MissingSecretValueException($"Error retrieving secret value (Secret: {secret.Name} Arn: {secret.ARN})", secret.Name, secret.ARN, e);
            }
        }
        return configuration;
    }

    private static List<List<SecretListEntry>> ChunkList(IReadOnlyList<SecretListEntry> source,
        Func<SecretListEntry, bool> optionsSecretFilter, int chunkSize)
    {
        // This is for sake of cleanliness vs getting 'fancy' with things.
        // We can always optimize later.
        return source
            .Where(optionsSecretFilter)
            .Select(static (item, index) => (item, index))
            .GroupBy(x => x.index / chunkSize)
            .Select(static group => group.Select(static x => x.item).ToList())
            .ToList();
    }

    private async Task<HashSet<(string, string)>> FetchConfigurationBatchAsync(CancellationToken cancellationToken)
    {
        var secrets = await FetchAllSecretsAsync(cancellationToken).ConfigureAwait(false);
        var configuration = new HashSet<(string, string)>();
        var chunked = ChunkList(secrets, Options.SecretFilter, 20);
        foreach (var secretSet in chunked)
        {
            var request = new BatchGetSecretValueRequest() { SecretIdList = secretSet.Select(a => a.ARN).ToList() };
            Options.ConfigureBatchSecretValueRequest(request,
                secretSet.Select(a => new SecretValueContext(a)).ToList());
            //Paranoia safety code here... probably not be needed with our chunking strategy.
            var resultSet = new List<BatchGetSecretValueResponse>();

            try
            {
                var secretValueSet = default(BatchGetSecretValueResponse);
                do
                {
                    request.NextToken = secretValueSet?.NextToken;
                    secretValueSet = await Client.BatchGetSecretValueAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    if (secretValueSet.Errors?.Any() == true)
                    {
                        var set = HandleBatchErrors(secretValueSet);

                        if (!Options.IgnoreMissingValues || set.Any(e => e is not MissingSecretValueException))
                        {
                            throw new AggregateException(set);
                        }
                    }
                    resultSet.Add(secretValueSet);
                } while (!string.IsNullOrWhiteSpace(secretValueSet.NextToken));

                foreach (var (secretValue, secret) in
                         resultSet.SelectMany(a => a.SecretValues.Select(b => b))
                             .Join(secretSet, a => a.ARN, b => b.ARN, (a, b) => (a, b)))
                {

                    var secretEntry = Options.AcceptedSecretArns.Count > 0
                        ? new SecretListEntry
                        {
                            ARN = secret.ARN,
                            Name = secretValue.Name,
                            CreatedDate = secretValue.CreatedDate
                        }
                        : secret;

                    var secretName = secretEntry.Name;
                    var secretString = secretValue.SecretString;

                    if (secretString is null)
                        continue;

                    if (TryParseJson(secretString, out var jElement))
                    {
                        // [MaybeNullWhen(false)] attribute is available in .net standard since version 2.1
                        var values = ExtractValues(jElement!, secretName);

                        foreach (var (key, value) in values)
                        {
                            var configurationKey = Options.KeyGenerator(secretEntry, key);
                            configuration.Add((configurationKey, value));
                        }
                    }
                    else
                    {
                        var configurationKey = Options.KeyGenerator(secretEntry, secretName);
                        configuration.Add((configurationKey, secretString));
                    }

                }
            }
            catch (ResourceNotFoundException e)
            {
                throw new MissingSecretValueException(
                    $"Error retrieving secret value (Secrets: {secretSet.Select(a => a.Name).Aggregate((a, b) => a + "," + b)} " +
                    $"Arns: {secretSet.Select(a => a.ARN).Aggregate((a, b) => a + "," + b)})",
                    secretSet.Select(a => a.Name).Aggregate((a, b) => a + "," + b),
                    secretSet.Select(a => a.ARN).Aggregate((a, b) => a + "," + b), e);
            }

        }

        return configuration;
    }

    private static List<Exception> HandleBatchErrors(BatchGetSecretValueResponse secretValueSet)
    {
        var set = secretValueSet.Errors.Select<APIErrorType, Exception>(errorResponse =>
        {
            return errorResponse.ErrorCode switch
            {
                "DecryptionFailure" => new DecryptionFailureException(errorResponse.Message, ErrorType.Unknown,
                    errorResponse.ErrorCode, secretValueSet.ResponseMetadata.RequestId,
                    secretValueSet.HttpStatusCode),
                "InternalServiceError" => new InternalServiceErrorException(errorResponse.Message,
                    ErrorType.Unknown, errorResponse.ErrorCode, secretValueSet.ResponseMetadata.RequestId,
                    secretValueSet.HttpStatusCode),
                "InvalidParameterException" => new InvalidParameterException(errorResponse.Message,
                    ErrorType.Unknown, errorResponse.ErrorCode, secretValueSet.ResponseMetadata.RequestId,
                    secretValueSet.HttpStatusCode),
                "InvalidRequestException" => new InvalidRequestException(errorResponse.Message, ErrorType.Unknown,
                    errorResponse.ErrorCode, secretValueSet.ResponseMetadata.RequestId,
                    secretValueSet.HttpStatusCode),
                "ResourceNotFoundException" => new MissingSecretValueException(errorResponse.Message,
                    errorResponse.SecretId, errorResponse.SecretId,
                    new ResourceNotFoundException(errorResponse.Message, ErrorType.Unknown, errorResponse.ErrorCode,
                        secretValueSet.ResponseMetadata.RequestId, secretValueSet.HttpStatusCode)),
                _ => new AmazonServiceException(errorResponse.Message, ErrorType.Unknown, errorResponse.ErrorCode,
                    secretValueSet.ResponseMetadata.RequestId, secretValueSet.HttpStatusCode)
            };
        }).ToList();
        return set;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="SecretsManagerConfigurationProvider"/>.
    /// </summary>
    public void Dispose()
    {
        _cancellationToken?.Cancel();
        _cancellationToken = null;

        try
        {
            _pollingTask?.GetAwaiter().GetResult();
        }
        catch (TaskCanceledException)
        {
        }
        _pollingTask = null;
    }
}