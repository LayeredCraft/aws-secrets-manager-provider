using Amazon.Runtime;
using Amazon.SecretsManager;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationSourceTests
{
    public SecretsManagerConfigurationSourceTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1", EnvironmentVariableTarget.Process);
    }

    [Theory, CustomAutoData]
    public void Build_can_create_a_IConfigurationProvider(IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource();

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, CustomAutoData]
    public void Build_can_create_a_IConfigurationProvider_with_credentials(AWSCredentials credentials,
        IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource(credentials);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, CustomAutoData]
    public void Build_can_create_a_IConfigurationProvider_with_options(
        SecretsManagerConfigurationProviderOptions options, IConfigurationBuilder configurationBuilder)
    {
        var sut = new SecretsManagerConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SecretsManagerConfigurationProvider>();
    }

    [Theory, CustomAutoData]
    public void Build_invokes_config_client_method(IConfigurationBuilder configurationBuilder,
        Action<AmazonSecretsManagerConfig> secretsManagerConfiguration)
    {
        var options = new SecretsManagerConfigurationProviderOptions
        {
            ConfigureSecretsManagerConfig = secretsManagerConfiguration
        };

        var sut = new SecretsManagerConfigurationSource(options: options);

        sut.Build(configurationBuilder);

        secretsManagerConfiguration.Received(1)(Arg.Is<AmazonSecretsManagerConfig>(c => c != null));
    }

    [Theory, CustomAutoData]
    public void Build_uses_given_client_factory_method(IConfigurationBuilder configurationBuilder,
        SecretsManagerConfigurationProviderOptions options, Func<IAmazonSecretsManager> clientFactory)
    {
        options.CreateClient = clientFactory;

        var sut = new SecretsManagerConfigurationSource(options: options);

        var provider = sut.Build(configurationBuilder);

        provider.Should().NotBeNull();
        clientFactory.Received(1)();
    }
}