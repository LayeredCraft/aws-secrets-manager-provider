using System;
using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;

namespace AWSConfiguration.Core.Internal;

/// <summary>
/// Base class for configuration sources backed by an AWS SDK service client.
/// Owns the client construction pattern (custom factory, region, credentials) shared by
/// AWS provider sources so concrete sources only describe how to build their provider.
/// </summary>
/// <typeparam name="TClient">The AWS service client interface.</typeparam>
/// <typeparam name="TConfig">The AWS service client configuration type.</typeparam>
public abstract class AwsConfigurationSourceBase<TClient, TConfig> : IConfigurationSource
    where TConfig : ClientConfig, new()
    where TClient : IAmazonService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AwsConfigurationSourceBase{TClient, TConfig}"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use for authentication.</param>
    protected AwsConfigurationSourceBase(AWSCredentials? credentials = null)
    {
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
    /// Configures the service client configuration before the client is created.
    /// </summary>
    /// <param name="clientConfig">The client configuration to customize.</param>
    protected abstract void ConfigureClient(TConfig clientConfig);

    /// <summary>
    /// Gets a function that provides a custom client, or null to use the default construction.
    /// </summary>
    protected abstract Func<TClient>? CustomClientFactory { get; }

    /// <summary>
    /// Creates the default service client for anonymous/default credential resolution.
    /// </summary>
    /// <param name="clientConfig">The configured client configuration.</param>
    /// <returns>The AWS service client.</returns>
    protected abstract TClient CreateDefaultClient(TConfig clientConfig);

    /// <summary>
    /// Creates the service client with explicit credentials.
    /// </summary>
    /// <param name="credentials">The AWS credentials.</param>
    /// <param name="clientConfig">The configured client configuration.</param>
    /// <returns>The AWS service client.</returns>
    protected abstract TClient CreateClient(AWSCredentials credentials, TConfig clientConfig);

    /// <summary>
    /// Creates the configuration provider for the built client.
    /// </summary>
    /// <param name="client">The AWS service client.</param>
    /// <returns>The configuration provider instance.</returns>
    protected abstract IConfigurationProvider BuildProvider(TClient client);

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = CreateClient();
        return BuildProvider(client);
    }

    private TClient CreateClient()
    {
        var customFactory = CustomClientFactory;
        if (customFactory is not null)
        {
            return customFactory();
        }

        var clientConfig = new TConfig
        {
            RegionEndpoint = Region
        };

        ConfigureClient(clientConfig);

        return Credentials switch
        {
            null => CreateDefaultClient(clientConfig),
            _ => CreateClient(Credentials, clientConfig)
        };
    }
}
