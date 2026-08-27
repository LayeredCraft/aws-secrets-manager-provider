using System;
using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Xunit;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationSourceTests
{
    public SecretsManagerConfigurationSourceTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1", EnvironmentVariableTarget.Process);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider(IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource();

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_with_credentials(AWSCredentials credentials,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource(credentials);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_can_create_a_IConfigurationProvider_with_options(
        SecretsManagerConfigurationProviderOptions options, IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_invokes_config_client_method(IConfigurationBuilder configurationBuilder)
    {
        var configureClientCalled = false;
        var options = new SecretsManagerConfigurationProviderOptions
        {
            ConfigureSecretsManagerConfig = config => configureClientCalled = config is not null
        };

        var sut = new SecretsManagerConfigurationSource(options: options);

        sut.Build(configurationBuilder);

        configureClientCalled.Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Build_uses_given_client_factory_method(IConfigurationBuilder configurationBuilder,
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

        var sut = new SecretsManagerConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        clientFactoryCalled.Should().BeTrue();
    }
}
