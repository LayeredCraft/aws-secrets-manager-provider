using System;
using Amazon;
using Amazon.Runtime;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests;

public class SecretsManagerExtensionsTests
{
    private readonly IConfigurationBuilder configurationBuilder;

    public SecretsManagerExtensionsTests()
    {
        configurationBuilder = Substitute.For<IConfigurationBuilder>();
    }

    [Fact]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_no_parameters()
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSource>());
    }

    [Theory, CustomAutoData]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_region(RegionEndpoint region)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, region: region);

        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSource>(s => s.Region == region));
    }

    [Theory, CustomAutoData]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_optionConfigurator(Action<SecretsManagerConfigurationProviderOptions> optionConfigurator)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, configurator: optionConfigurator);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSource>());

        optionConfigurator.Received(1)(Arg.Any<SecretsManagerConfigurationProviderOptions>());
    }

    [Theory, CustomAutoData]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_credentials(AWSCredentials credentials)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, credentials);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSource>());
    }

    [Theory, CustomAutoData]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_credentials_and_region(AWSCredentials credentials, RegionEndpoint region)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, credentials, region);

        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSource>(s => s.Region == region));
    }

    // Tests for Logger-based overload
    [Theory, CustomAutoData]
    public void AddSecretsManager_with_logger_creates_SecretsManagerConfigurationSourceWithLogger(ILogger<SecretsManagerConfigurationProvider> logger)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_logger_and_credentials(ILogger<SecretsManagerConfigurationProvider> logger, AWSCredentials credentials)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, credentials);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_logger_and_region(ILogger<SecretsManagerConfigurationProvider> logger, RegionEndpoint region)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, region: region);

        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSourceWithLogger>(s => s.Region == region));
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_logger_and_configurator(ILogger<SecretsManagerConfigurationProvider> logger, Action<SecretsManagerConfigurationProviderOptions> configurator)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, configurator: configurator);

        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
        configurator.Received(1)(Arg.Any<SecretsManagerConfigurationProviderOptions>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_logger_and_all_parameters(ILogger<SecretsManagerConfigurationProvider> logger, AWSCredentials credentials, RegionEndpoint region, Action<SecretsManagerConfigurationProviderOptions> configurator)
    {
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, credentials, region, configurator);

        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSourceWithLogger>(s => s.Region == region));
        configurator.Received(1)(Arg.Any<SecretsManagerConfigurationProviderOptions>());
    }

    // Tests for LoggerFactory-based overload
    [Theory, CustomAutoData]
    public void AddSecretsManager_with_loggerFactory_creates_logger_and_calls_logger_overload(ILoggerFactory loggerFactory)
    {
        var logger = Substitute.For<ILogger<SecretsManagerConfigurationProvider>>();
        loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>().Returns(logger);
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory);

        loggerFactory.Received(1).CreateLogger<SecretsManagerConfigurationProvider>();
        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_loggerFactory_and_credentials(ILoggerFactory loggerFactory, AWSCredentials credentials)
    {
        var logger = Substitute.For<ILogger<SecretsManagerConfigurationProvider>>();
        loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>().Returns(logger);
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, credentials);

        loggerFactory.Received(1).CreateLogger<SecretsManagerConfigurationProvider>();
        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_loggerFactory_and_region(ILoggerFactory loggerFactory, RegionEndpoint region)
    {
        var logger = Substitute.For<ILogger<SecretsManagerConfigurationProvider>>();
        loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>().Returns(logger);
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, region: region);

        loggerFactory.Received(1).CreateLogger<SecretsManagerConfigurationProvider>();
        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSourceWithLogger>(s => s.Region == region));
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_loggerFactory_and_configurator(ILoggerFactory loggerFactory, Action<SecretsManagerConfigurationProviderOptions> configurator)
    {
        var logger = Substitute.For<ILogger<SecretsManagerConfigurationProvider>>();
        loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>().Returns(logger);
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, configurator: configurator);

        loggerFactory.Received(1).CreateLogger<SecretsManagerConfigurationProvider>();
        configurationBuilder.Received(1).Add(Arg.Any<SecretsManagerConfigurationSourceWithLogger>());
        configurator.Received(1)(Arg.Any<SecretsManagerConfigurationProviderOptions>());
    }

    [Theory, CustomAutoData]
    public void AddSecretsManager_with_loggerFactory_and_all_parameters(ILoggerFactory loggerFactory, AWSCredentials credentials, RegionEndpoint region, Action<SecretsManagerConfigurationProviderOptions> configurator)
    {
        var logger = Substitute.For<ILogger<SecretsManagerConfigurationProvider>>();
        loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>().Returns(logger);
        configurationBuilder.Add(Arg.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, credentials, region, configurator);

        loggerFactory.Received(1).CreateLogger<SecretsManagerConfigurationProvider>();
        configurationBuilder.Received(1).Add(Arg.Is<SecretsManagerConfigurationSourceWithLogger>(s => s.Region == region));
        configurator.Received(1)(Arg.Any<SecretsManagerConfigurationProviderOptions>());
    }
}