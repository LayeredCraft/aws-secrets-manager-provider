using Amazon;
using Amazon.Runtime;
using AWSSSM.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Compono;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSSM.Provider.Tests;

public class SsmExtensionsTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void SsmConfigurationSource_can_be_added_via_convenience_method_with_no_parameters([Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSource)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SsmConfigurationSource_can_be_added_via_convenience_method_with_region([Shared] IConfigurationBuilder configurationBuilder)
    {
        var region = RegionEndpoint.USEast1;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, region: region);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s =>
            s is SsmConfigurationSource source && source.Region == region)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SsmConfigurationSource_can_be_added_via_convenience_method_with_optionConfigurator([Shared] IConfigurationBuilder configurationBuilder)
    {
        var configuratorCalled = false;
        void OptionConfigurator(SsmConfigurationProviderOptions _) => configuratorCalled = true;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, configurator: OptionConfigurator);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSource)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void SsmConfigurationSource_can_be_added_via_convenience_method_with_credentials(AWSCredentials credentials,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, credentials);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSource)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSsmParameters_with_logger_creates_SsmConfigurationSourceWithLogger(
        ILogger<SsmConfigurationProvider> logger, [Shared] IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, logger);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSourceWithLogger)).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSsmParameters_with_logger_and_configurator(ILogger<SsmConfigurationProvider> logger,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        var configuratorCalled = false;
        void Configurator(SsmConfigurationProviderOptions _) => configuratorCalled = true;
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, logger, configurator: Configurator);

        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSourceWithLogger)).Once();
        configuratorCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void AddSsmParameters_with_loggerFactory_creates_logger_and_calls_logger_overload(
        [Shared] ILoggerFactory loggerFactory, ILogger<SsmConfigurationProvider> logger,
        [Shared] IConfigurationBuilder configurationBuilder)
    {
        loggerFactory.Configure().CreateLogger(Match.Any<string>()).Returns(logger);
        configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder);

        SsmExtensions.AddSsmParameters(configurationBuilder, loggerFactory);

        loggerFactory.Verify().CreateLogger(Match.Any<string>()).Once();
        configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(s => s is SsmConfigurationSourceWithLogger)).Once();
    }
}
