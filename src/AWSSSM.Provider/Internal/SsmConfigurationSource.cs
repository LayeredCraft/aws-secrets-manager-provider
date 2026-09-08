using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider.Internal;

/// <summary>
/// Configuration source for AWS SSM Parameter Store without logger support.
/// </summary>
public class SsmConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SsmConfigurationSource"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    public SsmConfigurationSource(AWSCredentials? credentials = null, SsmConfigurationProviderOptions? options = null)
    {
        Credentials = credentials;
        Options = options ?? new SsmConfigurationProviderOptions();
    }

    /// <summary>
    /// Gets the AWS credentials used for authentication.
    /// </summary>
    public AWSCredentials? Credentials { get; }

    /// <summary>
    /// Gets or sets the AWS region endpoint.
    /// </summary>
    public RegionEndpoint? Region { get; set; }

    /// <summary>
    /// Gets the configuration options for the SSM provider.
    /// </summary>
    public SsmConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = AwsClientFactory.Create<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementConfig>(
            customClientFactory: Options.CreateClient,
            credentials: Credentials,
            region: Region,
            configureClient: Options.ConfigureSsmConfig,
            createDefaultClient: clientConfig => new AmazonSimpleSystemsManagementClient(clientConfig),
            createClientWithCredentials: (credentials, clientConfig) => new AmazonSimpleSystemsManagementClient(credentials, clientConfig));

        // No automatic logger resolution - use explicit logger overloads if logging is needed
        return new SsmConfigurationProvider(client, Options, logger: null);
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SsmConfigurationSourceWithLogger : IConfigurationSource
{
    private readonly SsmConfigurationProviderOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsmConfigurationSourceWithLogger"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger are null.</exception>
    public SsmConfigurationSourceWithLogger(AWSCredentials? credentials, SsmConfigurationProviderOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Credentials = credentials;
    }

    /// <summary>
    /// Gets the AWS credentials used for authentication.
    /// </summary>
    public AWSCredentials? Credentials { get; }

    /// <summary>
    /// Gets or sets the AWS region endpoint.
    /// </summary>
    public RegionEndpoint? Region { get; set; }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = AwsClientFactory.Create<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementConfig>(
            customClientFactory: _options.CreateClient,
            credentials: Credentials,
            region: Region,
            configureClient: _options.ConfigureSsmConfig,
            createDefaultClient: clientConfig => new AmazonSimpleSystemsManagementClient(clientConfig),
            createClientWithCredentials: (credentials, clientConfig) => new AmazonSimpleSystemsManagementClient(credentials, clientConfig));

        return new SsmConfigurationProvider(client, _options, _logger);
    }
}
