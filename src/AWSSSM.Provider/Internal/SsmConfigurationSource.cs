using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
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
    /// Gets the configuration options for the SSM provider.
    /// </summary>
    public SsmConfigurationProviderOptions Options { get; }

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
        return new SsmConfigurationProvider(client, Options, logger: null);
    }

    private IAmazonSimpleSystemsManagement CreateClient()
    {
        if (Options.CreateClient != null)
        {
            return Options.CreateClient();
        }

        var clientConfig = new AmazonSimpleSystemsManagementConfig
        {
            RegionEndpoint = Region
        };

        Options.ConfigureSsmConfig(clientConfig);

        return Credentials switch
        {
            null => new AmazonSimpleSystemsManagementClient(clientConfig),
            _ => new AmazonSimpleSystemsManagementClient(Credentials, clientConfig)
        };
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SsmConfigurationSourceWithLogger : IConfigurationSource
{
    private readonly AWSCredentials? _credentials;
    private readonly SsmConfigurationProviderOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets or sets the AWS region endpoint.
    /// </summary>
    public RegionEndpoint? Region { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SsmConfigurationSourceWithLogger"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger are null.</exception>
    public SsmConfigurationSourceWithLogger(AWSCredentials? credentials, SsmConfigurationProviderOptions options, ILogger logger)
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
        return new SsmConfigurationProvider(client, _options, _logger);
    }

    private IAmazonSimpleSystemsManagement CreateClient()
    {
        if (_options.CreateClient != null)
        {
            return _options.CreateClient();
        }

        var clientConfig = new AmazonSimpleSystemsManagementConfig
        {
            RegionEndpoint = Region
        };

        _options.ConfigureSsmConfig(clientConfig);

        return _credentials switch
        {
            null => new AmazonSimpleSystemsManagementClient(clientConfig),
            _ => new AmazonSimpleSystemsManagementClient(_credentials, clientConfig)
        };
    }
}
