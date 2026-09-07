using System;
using Amazon;
using Amazon.Runtime;
using AWSSSM.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSSM.Provider;

public static class SsmExtensions
{
    public static IConfigurationBuilder AddSsmParameters(this IConfigurationBuilder configurationBuilder,
        AWSCredentials? credentials = null,
        RegionEndpoint? region = null,
        Action<SsmConfigurationProviderOptions>? configurator = null)
    {
        var options = new SsmConfigurationProviderOptions();

        configurator?.Invoke(options);

        var source = new SsmConfigurationSource(credentials, options);

        if (region is not null)
        {
            source.Region = region;
        }

        configurationBuilder.Add(source);

        return configurationBuilder;
    }

    /// <summary>
    /// Adds AWS SSM Parameter Store as a configuration source with explicit logger support.
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder</param>
    /// <param name="logger">Logger instance for diagnostic information</param>
    /// <param name="credentials">AWS credentials</param>
    /// <param name="region">AWS region</param>
    /// <param name="configurator">Options configurator</param>
    /// <returns>The configuration builder</returns>
    public static IConfigurationBuilder AddSsmParameters(this IConfigurationBuilder configurationBuilder,
        ILogger<SsmConfigurationProvider> logger,
        AWSCredentials? credentials = null,
        RegionEndpoint? region = null,
        Action<SsmConfigurationProviderOptions>? configurator = null)
    {
        var options = new SsmConfigurationProviderOptions();

        configurator?.Invoke(options);

        var source = new SsmConfigurationSourceWithLogger(credentials, options, logger);

        if (region is not null)
        {
            source.Region = region;
        }

        configurationBuilder.Add(source);

        return configurationBuilder;
    }

    /// <summary>
    /// Adds AWS SSM Parameter Store as a configuration source with logger factory support.
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder</param>
    /// <param name="loggerFactory">Logger factory for creating logger instances</param>
    /// <param name="credentials">AWS credentials</param>
    /// <param name="region">AWS region</param>
    /// <param name="configurator">Options configurator</param>
    /// <returns>The configuration builder</returns>
    public static IConfigurationBuilder AddSsmParameters(this IConfigurationBuilder configurationBuilder,
        ILoggerFactory loggerFactory,
        AWSCredentials? credentials = null,
        RegionEndpoint? region = null,
        Action<SsmConfigurationProviderOptions>? configurator = null)
    {
        var logger = loggerFactory.CreateLogger<SsmConfigurationProvider>();
        return configurationBuilder.AddSsmParameters(logger, credentials, region, configurator);
    }
}
