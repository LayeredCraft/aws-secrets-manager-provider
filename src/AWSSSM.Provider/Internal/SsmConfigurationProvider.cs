using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider.Internal;

/// <summary>
/// Configuration provider that loads parameters from AWS SSM Parameter Store.
/// </summary>
public class SsmConfigurationProvider : PollingConfigurationProvider
{
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
        : base(logger)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    protected override string ResourceDescription => "parameters from AWS SSM Parameter Store";

    /// <inheritdoc />
    protected override string ResourceNoun => "SSM parameter";

    /// <inheritdoc />
    protected override string DuplicateKeyOptionsHint =>
        "Adjust the KeyGenerator or ParameterFilter options so each parameter maps to a unique key.";

    /// <inheritdoc />
    protected override TimeSpan? PollingInterval => Options.PollingInterval;

    /// <summary>
    /// Fetches the current parameter values by walking the configured hierarchy path.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of configuration key/value pairs.</returns>
    protected override async Task<HashSet<(string, string?)>> FetchConfigurationAsync(CancellationToken cancellationToken)
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
}
