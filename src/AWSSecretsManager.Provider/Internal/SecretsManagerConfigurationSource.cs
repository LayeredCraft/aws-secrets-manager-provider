using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSecretsManager.Provider.Internal;

/// <summary>
/// Configuration source for AWS Secrets Manager without logger support.
/// </summary>
public class SecretsManagerConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationSource"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    public SecretsManagerConfigurationSource(AWSCredentials? credentials = null, SecretsManagerConfigurationProviderOptions? options = null)
    {
        Credentials = credentials;
        Options = options ?? new SecretsManagerConfigurationProviderOptions();
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
    /// Gets the configuration options for the secrets manager provider.
    /// </summary>
    public SecretsManagerConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = AwsClientFactory.Create<IAmazonSecretsManager, AmazonSecretsManagerConfig>(
            customClientFactory: Options.CreateClient,
            credentials: Credentials,
            region: Region,
            configureClient: Options.ConfigureSecretsManagerConfig,
            createDefaultClient: clientConfig => new AmazonSecretsManagerClient(clientConfig),
            createClientWithCredentials: (credentials, clientConfig) => new AmazonSecretsManagerClient(credentials, clientConfig));

        // No automatic logger resolution - use explicit logger overloads if logging is needed
        return new SecretsManagerConfigurationProvider(client, Options, logger: null);
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SecretsManagerConfigurationSourceWithLogger : IConfigurationSource
{
    private readonly SecretsManagerConfigurationProviderOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationSourceWithLogger"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger are null.</exception>
    public SecretsManagerConfigurationSourceWithLogger(AWSCredentials? credentials, SecretsManagerConfigurationProviderOptions options, ILogger logger)
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
        var client = AwsClientFactory.Create<IAmazonSecretsManager, AmazonSecretsManagerConfig>(
            customClientFactory: _options.CreateClient,
            credentials: Credentials,
            region: Region,
            configureClient: _options.ConfigureSecretsManagerConfig,
            createDefaultClient: clientConfig => new AmazonSecretsManagerClient(clientConfig),
            createClientWithCredentials: (credentials, clientConfig) => new AmazonSecretsManagerClient(credentials, clientConfig));

        return new SecretsManagerConfigurationProvider(client, _options, _logger);
    }
}
