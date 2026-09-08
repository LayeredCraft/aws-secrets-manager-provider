using System;
using Amazon;
using Amazon.Runtime;

namespace AWSConfiguration.Core.Internal;

/// <summary>
/// Internal helper that owns the AWS client construction pattern (custom factory, region,
/// credentials) shared by AWS provider configuration sources.
/// </summary>
internal static class AwsClientFactory
{
    /// <summary>
    /// Creates an AWS service client using the shared construction pattern:
    /// a custom client factory when configured, otherwise a client built from the
    /// configured region and credentials.
    /// </summary>
    /// <typeparam name="TClient">The AWS service client interface.</typeparam>
    /// <typeparam name="TConfig">The AWS service client configuration type.</typeparam>
    /// <param name="customClientFactory">A function that provides a custom client, or null to use the default construction.</param>
    /// <param name="credentials">The AWS credentials to use for authentication, or null for default credential resolution.</param>
    /// <param name="region">The AWS region endpoint.</param>
    /// <param name="configureClient">Configures the service client configuration before the client is created.</param>
    /// <param name="createDefaultClient">Creates the client for anonymous/default credential resolution.</param>
    /// <param name="createClientWithCredentials">Creates the client with explicit credentials.</param>
    /// <returns>The configured AWS service client.</returns>
    public static TClient Create<TClient, TConfig>(
        Func<TClient>? customClientFactory,
        AWSCredentials? credentials,
        RegionEndpoint? region,
        Action<TConfig> configureClient,
        Func<TConfig, TClient> createDefaultClient,
        Func<AWSCredentials, TConfig, TClient> createClientWithCredentials)
        where TConfig : ClientConfig, new()
        where TClient : IAmazonService
    {
        if (customClientFactory is not null)
        {
            return customClientFactory();
        }

        var clientConfig = new TConfig
        {
            RegionEndpoint = region
        };

        configureClient(clientConfig);

        return credentials switch
        {
            null => createDefaultClient(clientConfig),
            _ => createClientWithCredentials(credentials, clientConfig)
        };
    }
}
