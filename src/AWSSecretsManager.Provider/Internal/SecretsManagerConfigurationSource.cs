using System;
using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSecretsManager.Provider.Internal;

/// <summary>
/// Configuration source for AWS Secrets Manager without logger support.
/// </summary>
public class SecretsManagerConfigurationSource : AwsConfigurationSourceBase<IAmazonSecretsManager, AmazonSecretsManagerConfig>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationSource"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    public SecretsManagerConfigurationSource(AWSCredentials? credentials = null, SecretsManagerConfigurationProviderOptions? options = null)
        : base(credentials)
    {
        Options = options ?? new SecretsManagerConfigurationProviderOptions();
    }

    /// <summary>
    /// Gets the configuration options for the secrets manager provider.
    /// </summary>
    public SecretsManagerConfigurationProviderOptions Options { get; }

    /// <inheritdoc />
    protected override void ConfigureClient(AmazonSecretsManagerConfig clientConfig)
    {
        Options.ConfigureSecretsManagerConfig(clientConfig);
    }

    /// <inheritdoc />
    protected override Func<IAmazonSecretsManager>? CustomClientFactory => Options.CreateClient;

    /// <inheritdoc />
    protected override IAmazonSecretsManager CreateDefaultClient(AmazonSecretsManagerConfig clientConfig)
    {
        return new AmazonSecretsManagerClient(clientConfig);
    }

    /// <inheritdoc />
    protected override IAmazonSecretsManager CreateClient(AWSCredentials credentials, AmazonSecretsManagerConfig clientConfig)
    {
        return new AmazonSecretsManagerClient(credentials, clientConfig);
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    protected override IConfigurationProvider BuildProvider(IAmazonSecretsManager client)
    {
        // No automatic logger resolution - use explicit logger overloads if logging is needed
        return new SecretsManagerConfigurationProvider(client, Options, logger: null);
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SecretsManagerConfigurationSourceWithLogger : AwsConfigurationSourceBase<IAmazonSecretsManager, AmazonSecretsManagerConfig>
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
        : base(credentials)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override void ConfigureClient(AmazonSecretsManagerConfig clientConfig)
    {
        _options.ConfigureSecretsManagerConfig(clientConfig);
    }

    /// <inheritdoc />
    protected override Func<IAmazonSecretsManager>? CustomClientFactory => _options.CreateClient;

    /// <inheritdoc />
    protected override IAmazonSecretsManager CreateDefaultClient(AmazonSecretsManagerConfig clientConfig)
    {
        return new AmazonSecretsManagerClient(clientConfig);
    }

    /// <inheritdoc />
    protected override IAmazonSecretsManager CreateClient(AWSCredentials credentials, AmazonSecretsManagerConfig clientConfig)
    {
        return new AmazonSecretsManagerClient(credentials, clientConfig);
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    protected override IConfigurationProvider BuildProvider(IAmazonSecretsManager client)
    {
        return new SecretsManagerConfigurationProvider(client, _options, _logger);
    }
}
