using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Logging;

namespace AWSSecretsManager.Provider.Internal;

/// <summary>
/// Configuration provider that loads secrets from AWS Secrets Manager.
/// </summary>
public class SecretsManagerConfigurationProvider : PollingConfigurationProvider
{
    /// <summary>
    /// Gets the configuration options for the secrets manager provider.
    /// </summary>
    public SecretsManagerConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Gets the AWS Secrets Manager client used to retrieve secrets.
    /// </summary>
    public IAmazonSecretsManager Client { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationProvider"/> class.
    /// </summary>
    /// <param name="client">The AWS Secrets Manager client.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when client or options are null.</exception>
    public SecretsManagerConfigurationProvider(IAmazonSecretsManager client, SecretsManagerConfigurationProviderOptions options, ILogger? logger = null)
        : base(logger)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    protected override string ResourceDescription => "secrets from AWS Secrets Manager";

    /// <inheritdoc />
    protected override string ResourceNoun => "secret";

    /// <inheritdoc />
    protected override string DuplicateKeyOptionsHint =>
        "Adjust the KeyGenerator or SecretFilter options so each secret maps to a unique key.";

    /// <inheritdoc />
    protected override TimeSpan? PollingInterval => Options.PollingInterval;

    /// <summary>
    /// Fetches the current configuration values, using batch fetching when enabled.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of configuration key/value pairs.</returns>
    protected override async Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken)
    {
        return Options.UseBatchFetch switch
        {
            true => await FetchConfigurationBatchAsync(cancellationToken).ConfigureAwait(false),
            _ => await FetchSingleSecretsAsync(cancellationToken).ConfigureAwait(false)
        };
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

    private async Task<HashSet<(string, string?)>> FetchSingleSecretsAsync(CancellationToken cancellationToken)
    {
        var secrets = await FetchAllSecretsAsync(cancellationToken).ConfigureAwait(false);
        var configuration = new HashSet<(string, string?)>();
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

                if (JsonFlattener.TryParseJson(secretString, out var jElement))
                {
                    // [MaybeNullWhen(false)] attribute is available in .net standard since version 2.1
                    var values = JsonFlattener.ExtractValues(jElement!, secretName);

                    foreach (var (key, value) in values)
                    {
                        var configurationKey = Options.KeyGenerator(secretEntry, key);
                        AddConfigurationValue(configuration, configurationKey, value);
                    }
                }
                else
                {
                    var configurationKey = Options.KeyGenerator(secretEntry, secretName);
                    AddConfigurationValue(configuration, configurationKey, secretString);
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

    private async Task<HashSet<(string, string?)>> FetchConfigurationBatchAsync(CancellationToken cancellationToken)
    {
        var secrets = await FetchAllSecretsAsync(cancellationToken).ConfigureAwait(false);
        var configuration = new HashSet<(string, string?)>();
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

                foreach (var secretValue in resultSet.SelectMany(a => a.SecretValues))
                {
                    // Match the returned secret back to our input list using flexible ARN/name comparison.
                    // AWS returns full ARNs (with a random suffix like "-AbCdEf") even when the caller
                    // supplied a short name or partial ARN, so exact equality on ARN alone would silently
                    // drop every result when AcceptedSecretArns contains anything other than full ARNs.
                    var secret = secretSet.FirstOrDefault(s =>
                        s.ARN.Equals(secretValue.ARN, StringComparison.OrdinalIgnoreCase) ||
                        secretValue.ARN.EndsWith(s.ARN, StringComparison.OrdinalIgnoreCase) ||
                        s.ARN.Equals(secretValue.Name, StringComparison.OrdinalIgnoreCase));

                    if (secret == null) continue;

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

                    if (JsonFlattener.TryParseJson(secretString, out var jElement))
                    {
                        // [MaybeNullWhen(false)] attribute is available in .net standard since version 2.1
                        var values = JsonFlattener.ExtractValues(jElement!, secretName);

                        foreach (var (key, value) in values)
                        {
                            var configurationKey = Options.KeyGenerator(secretEntry, key);
                            AddConfigurationValue(configuration, configurationKey, value);
                        }
                    }
                    else
                    {
                        var configurationKey = Options.KeyGenerator(secretEntry, secretName);
                        AddConfigurationValue(configuration, configurationKey, secretString);
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
}
