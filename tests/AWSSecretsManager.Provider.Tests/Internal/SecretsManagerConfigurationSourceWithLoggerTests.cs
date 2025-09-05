using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationSourceWithLoggerTests
{
    public SecretsManagerConfigurationSourceWithLoggerTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1", EnvironmentVariableTarget.Process);
    }

    [Theory, CustomAutoData]
    public void Constructor_throws_when_options_is_null(AWSCredentials credentials, ILogger<SecretsManagerConfigurationProvider> logger)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(credentials, null!, logger);
        
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Theory, CustomAutoData]
    public void Constructor_throws_when_logger_is_null(AWSCredentials credentials, SecretsManagerConfigurationProviderOptions options)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(credentials, options, null!);
        
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Theory, CustomAutoData]
    public void Constructor_accepts_null_credentials(SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger)
    {
        var action = () => new SecretsManagerConfigurationSourceWithLogger(null, options, logger);
        
        action.Should().NotThrow();
    }

    [Theory, CustomAutoData]
    public void Build_can_create_a_IConfigurationProvider_with_logger(AWSCredentials credentials, 
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, CustomAutoData]
    public void Build_can_create_a_IConfigurationProvider_without_credentials(
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSourceWithLogger(null, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, CustomAutoData]
    public void Build_invokes_config_client_method(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder,
        Action<AmazonSecretsManagerConfig> secretsManagerConfiguration)
    {
        var options = new SecretsManagerConfigurationProviderOptions
        {
            ConfigureSecretsManagerConfig = secretsManagerConfiguration
        };

        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        sut.Build(configurationBuilder);

        secretsManagerConfiguration.Received(1)(Arg.Is<AmazonSecretsManagerConfig>(c => c != null));
    }

    [Theory, CustomAutoData]
    public void Build_uses_given_client_factory_method(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder,
        SecretsManagerConfigurationProviderOptions options, Func<IAmazonSecretsManager> clientFactory)
    {
        options.CreateClient = clientFactory;

        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        clientFactory.Received(1)();
    }

    [Theory, CustomAutoData]
    public void Region_property_can_be_set_and_read(AWSCredentials credentials,
        SecretsManagerConfigurationProviderOptions options, ILogger<SecretsManagerConfigurationProvider> logger,
        Amazon.RegionEndpoint region)
    {
        var sut = new SecretsManagerConfigurationSourceWithLogger(credentials, options, logger);

        sut.Region = region;

        sut.Region.Should().Be(region);
    }

    [Theory, CustomAutoData]
    public void Build_uses_region_when_creating_client(AWSCredentials credentials,
        ILogger<SecretsManagerConfigurationProvider> logger, IConfigurationBuilder configurationBuilder,
        Amazon.RegionEndpoint region)
    {
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