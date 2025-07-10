using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
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
    /// Gets the configuration options for the secrets manager provider.
    /// </summary>
    public SecretsManagerConfigurationProviderOptions Options { get; }

    /// <summary>
    /// Gets the AWS credentials used for authentication.
    /// </summary>
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
        var client = CreateClient();
        
        // No automatic logger resolution - use explicit logger overloads if logging is needed
        return new SecretsManagerConfigurationProvider(client, Options, logger: null);
    }

    private IAmazonSecretsManager CreateClient()
    {
        if (Options.CreateClient != null)
        {
            return Options.CreateClient();
        }

        var clientConfig = new AmazonSecretsManagerConfig
        {
            RegionEndpoint = Region
        };

        Options.ConfigureSecretsManagerConfig(clientConfig);

        return Credentials switch
        {
            null => new AmazonSecretsManagerClient(clientConfig),
            _ => new AmazonSecretsManagerClient(Credentials, clientConfig)
        };
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SecretsManagerConfigurationSourceWithLogger : IConfigurationSource
{
    private readonly AWSCredentials? _credentials;
    private readonly SecretsManagerConfigurationProviderOptions _options;
    private readonly ILogger _logger;
    
    /// <summary>
    /// Gets or sets the AWS region endpoint.
    /// </summary>
    public RegionEndpoint? Region { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsManagerConfigurationSourceWithLogger"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger are null.</exception>
    public SecretsManagerConfigurationSourceWithLogger(AWSCredentials? credentials, SecretsManagerConfigurationProviderOptions options, ILogger logger)
    {
        _credentials = credentials;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = CreateClient();
        return new SecretsManagerConfigurationProvider(client, _options, _logger);
    }

    private IAmazonSecretsManager CreateClient()
    {
        if (_options.CreateClient != null)
        {
            return _options.CreateClient();
        }

        var clientConfig = new AmazonSecretsManagerConfig
        {
            RegionEndpoint = Region
        };

        _options.ConfigureSecretsManagerConfig(clientConfig);

        return _credentials switch
        {
            null => new AmazonSecretsManagerClient(clientConfig),
            _ => new AmazonSecretsManagerClient(_credentials, clientConfig)
        };
    }
}