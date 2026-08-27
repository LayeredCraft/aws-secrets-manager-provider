using System;
using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationSourceWithLoggerTests
{
    public SecretsManagerConfigurationSourceWithLoggerTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1", EnvironmentVariableTarget.Process);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Constructor_throws_when_options_is_null(AWSCredentials credentials, ILogger<SecretsManagerConfigurationProvider> logger)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(credentials, null!, logger);
        
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Constructor_throws_when_logger_is_null(AWSCredentials credentials, SecretsManagerConfigurationProviderOptions options)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(credentials, options, null!);
        
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Constructor_accepts_null_credentials(SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(null, options, logger);
        
        action.Should().NotThrow();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_with_logger(AWSCredentials credentials, 
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_without_credentials(
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSourceWithLogger(null, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_invokes_config_client_method(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder)
    {
        var configureClientCalled = false;
        var options = new SecretsManagerConfigurationProviderOptions
        {
            ConfigureSecretsManagerConfig = config => configureClientCalled = config is not null
        };

        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        sut.Build(configurationBuilder);

        configureClientCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_uses_given_client_factory_method(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder,
        SecretsManagerConfigurationProviderOptions options)
    {
        var clientFactoryCalled = false;
        options.CreateClient = () =>
        {
            clientFactoryCalled = true;
            return new AmazonSecretsManagerClient(
                new AnonymousAWSCredentials(),
                new AmazonSecretsManagerConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 });
        };

        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        clientFactoryCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Region_property_can_be_set_and_read(AWSCredentials credentials,
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger)
    {
        var region = Amazon.RegionEndpoint.USEast1;
        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        sut.Region = region;

        sut.Region.Should().Be(region);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_uses_region_when_creating_client(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder)
    {
        var region = Amazon.RegionEndpoint.USEast1;
        var configureClientCalled = false;
        var capturedConfig = default(AmazonSecretsManagerConfig);
        
        var options = new SecretsManagerConfigurationProviderOptions
        {
            ConfigureSecretsManagerConfig = config =>
            {
                configureClientCalled = true;
                capturedConfig = config;
            }
        };

        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger)
        {
            Region = region
        };

        sut.Build(configurationBuilder);

        configureClientCalled.Should().BeTrue();
        capturedConfig?.RegionEndpoint.Should().Be(region);
    }
}
