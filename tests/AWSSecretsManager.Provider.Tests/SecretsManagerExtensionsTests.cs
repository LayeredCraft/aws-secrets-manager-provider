using Amazon;
using Amazon.Runtime;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Compono;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests;

public class SecretsManagerExtensionsTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_no_parameters([Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSource)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_region([Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, region: region);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSource source && source.Region == region)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_optionConfigurator([Shared] IConfigurationBuilder configurationBuilder)
    {
        var configuratorCalled = false;
        void OptionConfigurator(SecretsManagerConfigurationProviderOptions _) => configuratorCalled = true;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, configurator: OptionConfigurator);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSource)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_credentials(AWSCredentials credentials,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, credentials);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSource)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SecretsManagerConfigurationSource_can_be_added_via_convenience_method_with_credentials_and_region([Shared] IConfigurationBuilder configurationBuilder)
    {
        var credentials = new AnonymousAWSCredentials();
        var region = RegionEndpoint.USEast1;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, credentials, region);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSource source && source.Region == region)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_logger_creates_SecretsManagerConfigurationSourceWithLogger(
        ILogger<SecretsManagerConfigurationProvider> logger, [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_logger_and_credentials(ILogger<SecretsManagerConfigurationProvider> logger,
        AWSCredentials credentials, [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, credentials);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_logger_and_region(ILogger<SecretsManagerConfigurationProvider> logger,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, region: region);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSourceWithLogger source && source.Region == region)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_logger_and_configurator(ILogger<SecretsManagerConfigurationProvider> logger,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        var configuratorCalled = false;
        void Configurator(SecretsManagerConfigurationProviderOptions _) => configuratorCalled = true;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, configurator: Configurator);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_logger_and_all_parameters(ILogger<SecretsManagerConfigurationProvider> logger,
        AWSCredentials credentials, [Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        var configuratorCalled = false;
        void Configurator(SecretsManagerConfigurationProviderOptions _) => configuratorCalled = true;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, logger, credentials, region, Configurator);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSourceWithLogger source && source.Region == region)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_loggerFactory_creates_logger_and_calls_logger_overload(
        [Shared] ILoggerFactory loggerFactory, ILogger<SecretsManagerConfigurationProvider> logger,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_loggerFactory_and_credentials([Shared] ILoggerFactory loggerFactory,
        ILogger<SecretsManagerConfigurationProvider> logger, AWSCredentials credentials,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, credentials);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_loggerFactory_and_region([Shared] ILoggerFactory loggerFactory,
        ILogger<SecretsManagerConfigurationProvider> logger, [Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, region: region);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSourceWithLogger source && source.Region == region)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_loggerFactory_and_configurator([Shared] ILoggerFactory loggerFactory,
        ILogger<SecretsManagerConfigurationProvider> logger, [Shared] IConfigurationBuilder configurationBuilder)
    {
        var configuratorCalled = false;
        void Configurator(SecretsManagerConfigurationProviderOptions _) => configuratorCalled = true;
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, configurator: Configurator);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SecretsManagerConfigurationSourceWithLogger)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSecretsManager_with_loggerFactory_and_all_parameters([Shared] ILoggerFactory loggerFactory,
        ILogger<SecretsManagerConfigurationProvider> logger, AWSCredentials credentials,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        var configuratorCalled = false;
        void Configurator(SecretsManagerConfigurationProviderOptions _) => configuratorCalled = true;
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SecretsManagerExtensions.AddSecretsManager(configurationBuilder, loggerFactory, credentials, region, Configurator);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SecretsManagerConfigurationSourceWithLogger source && source.Region == region)).Once();
        configuratorCalled.Should().BeTrue();
    }
}
