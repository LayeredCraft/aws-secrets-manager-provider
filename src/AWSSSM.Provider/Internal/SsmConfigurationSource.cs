using System;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using AWSConfiguration.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider.Internal;

/// <summary>
/// Configuration source for AWS SSM Parameter Store without logger support.
/// </summary>
public class SsmConfigurationSource : AwsConfigurationSourceBase<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementConfig>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SsmConfigurationSource"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    /// <param name="options">The configuration options.</param>
    public SsmConfigurationSource(AWSCredentials? credentials = null, SsmConfigurationProviderOptions? options = null)
        : base(credentials)
    {
        Options = options ?? new SsmConfigurationProviderOptions();
    }

    /// <summary>
    /// Gets the configuration options for the SSM provider.
    /// </summary>
    public SsmConfigurationProviderOptions Options { get; }

    /// <inheritdoc />
    protected override void ConfigureClient(AmazonSimpleSystemsManagementConfig clientConfig)
    {
        Options.ConfigureSsmConfig(clientConfig);
    }

    /// <inheritdoc />
    protected override Func<IAmazonSimpleSystemsManagement>? CustomClientFactory => Options.CreateClient;

    /// <inheritdoc />
    protected override IAmazonSimpleSystemsManagement CreateDefaultClient(AmazonSimpleSystemsManagementConfig clientConfig)
    {
        return new AmazonSimpleSystemsManagementClient(clientConfig);
    }

    /// <inheritdoc />
    protected override IAmazonSimpleSystemsManagement CreateClient(AWSCredentials credentials, AmazonSimpleSystemsManagementConfig clientConfig)
    {
        return new AmazonSimpleSystemsManagementClient(credentials, clientConfig);
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    protected override IConfigurationProvider BuildProvider(IAmazonSimpleSystemsManagement client)
    {
        // No automatic logger resolution - use explicit logger overloads if logging is needed
        return new SsmConfigurationProvider(client, Options, logger: null);
    }
}

/// <summary>
/// Configuration source that supports explicit logger injection
/// </summary>
public class SsmConfigurationSourceWithLogger : AwsConfigurationSourceBase<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementConfig>
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
        : base(credentials)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override void ConfigureClient(AmazonSimpleSystemsManagementConfig clientConfig)
    {
        _options.ConfigureSsmConfig(clientConfig);
    }

    /// <inheritdoc />
    protected override Func<IAmazonSimpleSystemsManagement>? CustomClientFactory => _options.CreateClient;

    /// <inheritdoc />
    protected override IAmazonSimpleSystemsManagement CreateDefaultClient(AmazonSimpleSystemsManagementConfig clientConfig)
    {
        return new AmazonSimpleSystemsManagementClient(clientConfig);
    }

    /// <inheritdoc />
    protected override IAmazonSimpleSystemsManagement CreateClient(AWSCredentials credentials, AmazonSimpleSystemsManagementConfig clientConfig)
    {
        return new AmazonSimpleSystemsManagementClient(credentials, clientConfig);
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    protected override IConfigurationProvider BuildProvider(IAmazonSimpleSystemsManagement client)
    {
        return new SsmConfigurationProvider(client, _options, _logger);
    }
}
