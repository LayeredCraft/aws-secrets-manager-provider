using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider.Internal;

/// <summary>
/// Configuration provider that loads parameters from AWS SSM Parameter Store.
/// </summary>
public class SsmConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly PollingEngine _engine;

    /// <summary>
    /// Gets the configuration options for the SSM provider.
    /// </summary>
    public SsmConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Gets the AWS SSM client used to retrieve parameters.
    /// </summary>
    public IAmazonSimpleSystemsManagement Client { get; }

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

        _engine = new PollingEngine(
            logger,
            resourceDescription: "parameters from AWS SSM Parameter Store",
            resourceNoun: "SSM parameter",
            duplicateKeyOptionsHint: "Adjust the KeyGenerator or ParameterFilter options so each parameter maps to a unique key.",
            fetchConfiguration: FetchConfigurationAsync,
            pollingInterval: () => Options.PollingInterval,
            commitData: (data, publishChange) =>
            {
                Data = data;
                if (publishChange)
                {
                    OnReload();
                }
            });
    }

    /// <summary>
    /// Loads the configuration data from AWS SSM Parameter Store.
    /// </summary>
    public override void Load()
    {
        _engine.Load();
    }

    /// <summary>
    /// Forces a reload of the configuration data from AWS SSM Parameter Store.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ForceReloadAsync(CancellationToken cancellationToken)
    {
        return _engine.ForceReloadAsync(cancellationToken);
    }

    /// <summary>
    /// Releases all resources used by the provider, stopping any active polling.
    /// </summary>
    public void Dispose()
    {
        _engine.Dispose();
    }

    private void AddConfigurationValue(HashSet<(string, string?)> configuration, string key, string? value)
    {
        _engine.AddConfigurationValue(configuration, key, value);
    }

    /// <summary>
    /// Fetches the current parameter values by walking the configured hierarchy path.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of configuration key/value pairs.</returns>
    private async Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken)
    {
        // Normalize once so the AWS request and the key mapping agree even when
        // the caller configures a trailing slash (e.g. "/MyApp/").
        var normalizedPath = (Options.Path ?? "/").TrimEnd('/');
        if (normalizedPath.Length == 0)
        {
            normalizedPath = "/";
        }

        var configuration = new HashSet<(string, string?)>();
        var response = default(GetParametersByPathResponse);

        do
        {
            var request = new GetParametersByPathRequest
            {
                Path = normalizedPath,
                Recursive = Options.Recursive,
                WithDecryption = Options.WithDecryption
            };

            Options.ConfigureGetParametersByPathRequest?.Invoke(request);

            // Re-apply the provider-owned fields after the hook so the request always
            // stays in sync with the configured options and key mapping; NextToken is
            // assigned last so pagination cannot be clobbered by the hook either.
            request.Path = normalizedPath;
            request.Recursive = Options.Recursive;
            request.WithDecryption = Options.WithDecryption;
            request.NextToken = response?.NextToken;

            response = await Client.GetParametersByPathAsync(request, cancellationToken).ConfigureAwait(false);

            foreach (var parameter in response.Parameters)
            {
                if (!Options.ParameterFilter(parameter))
                    continue;

                var value = parameter.Value;

                if (value is null)
                    continue;

                var baseKey = SsmConfigurationProviderOptions.DefaultKeyGenerator(parameter.Name, normalizedPath);

                if (JsonFlattener.TryParseJson(value, out var jElement))
                {
                    foreach (var (key, item) in JsonFlattener.ExtractValues(jElement!, baseKey))
                    {
                        AddConfigurationValue(configuration, Options.KeyGenerator(key, normalizedPath), item);
                    }
                }
                else
                {
                    AddConfigurationValue(configuration, Options.KeyGenerator(baseKey, normalizedPath), value);
                }
            }
        } while (response.NextToken != null);

        return configuration;
    }
}
