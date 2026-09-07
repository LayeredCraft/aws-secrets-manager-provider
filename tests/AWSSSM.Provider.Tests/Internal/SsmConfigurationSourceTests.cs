using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using AWSSSM.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Xunit;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSSM.Provider.Tests.Internal;

public class SsmConfigurationSourceTests
{
    public SsmConfigurationSourceTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1", EnvironmentVariableTarget.Process);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider(IConfigurationBuilder configurationBuilder)
    {
        var sut = new SsmConfigurationSource();

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SsmConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_with_credentials(AWSCredentials credentials,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SsmConfigurationSource(credentials);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SsmConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_with_options(
        SsmConfigurationProviderOptions options, IConfigurationBuilder configurationBuilder)
    {
        var sut = new SsmConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SsmConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_invokes_config_client_method(IConfigurationBuilder configurationBuilder)
    {
        var configureClientCalled = false;
        var options = new SsmConfigurationProviderOptions
        {
            ConfigureSsmConfig = config => configureClientCalled = config is not null
        };

        var sut = new SsmConfigurationSource(options: options);

        sut.Build(configurationBuilder);

        configureClientCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_uses_given_client_factory_method(IConfigurationBuilder configurationBuilder,
        SsmConfigurationProviderOptions options)
    {
        var clientFactoryCalled = false;
        options.CreateClient = () =>
        {
            clientFactoryCalled = true;
            return new AmazonSimpleSystemsManagementClient(
                new AnonymousAWSCredentials(),
                new AmazonSimpleSystemsManagementConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 });
        };

        var sut = new SsmConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        clientFactoryCalled.Should().BeTrue();
    }
}

public class SsmConfigurationSourceWithLoggerTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void Build_creates_provider_with_logger(Microsoft.Extensions.Logging.ILogger<SsmConfigurationProvider> logger,
        SsmConfigurationProviderOptions options, IConfigurationBuilder configurationBuilder)
    {
        var sut = new SsmConfigurationSourceWithLogger(null, options, logger)
        {
            Region = Amazon.RegionEndpoint.USEast1
        };

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SsmConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Constructor_throws_on_null_options(Microsoft.Extensions.Logging.ILogger<SsmConfigurationProvider> logger)
    {
        var ctor = () => new SsmConfigurationSourceWithLogger(null, null!, logger);

        ctor.Should().Throw<ArgumentNullException>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Constructor_throws_on_null_logger(SsmConfigurationProviderOptions options)
    {
        var ctor = () => new SsmConfigurationSourceWithLogger(null, options, null!);

        ctor.Should().Throw<ArgumentNullException>();
    }
}
