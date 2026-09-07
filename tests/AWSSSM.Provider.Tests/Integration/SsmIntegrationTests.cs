using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSSSM.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Xunit;
using AwesomeAssertions;

namespace AWSSSM.Provider.Tests.Integration;

[Collection("Floci")]
public class SsmIntegrationTests
{
    private readonly FlociFixture _fixture;

    public SsmIntegrationTests(FlociFixture fixture)
    {
        if (!FlociFixture.DockerAvailable())
        {
            Assert.Skip("Docker is not available - Floci integration tests skipped.");
        }

        _fixture = fixture;
    }

    private string NewPath() => $"/it-{Guid.NewGuid():N}";

    [Fact]
    public async Task Parameters_are_mapped_to_configuration_keys()
    {
        var path = NewPath();
        using var client = _fixture.CreateClient();

        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/Db/Host", Value = "localhost", Type = ParameterType.String, Overwrite = true
        });
        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/Db/Port", Value = "5432", Type = ParameterType.String, Overwrite = true
        });
        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/Db/Config",
            Value = """{"ConnectionString":"server=db","Replicas":["r1","r2"]}""",
            Type = ParameterType.String,
            Overwrite = true
        });

        var config = BuildConfiguration(path, client);

        config[$"Db:Host"].Should().Be("localhost");
        config[$"Db:Port"].Should().Be("5432");
        config[$"Db:Config:ConnectionString"].Should().Be("server=db");
        config[$"Db:Config:Replicas:0"].Should().Be("r1");
        config[$"Db:Config:Replicas:1"].Should().Be("r2");
    }

    [Fact]
    public async Task SecureString_parameters_are_decrypted()
    {
        var path = NewPath();
        using var client = _fixture.CreateClient();

        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/ApiKey", Value = "super-secret", Type = ParameterType.SecureString, Overwrite = true
        });

        var config = BuildConfiguration(path, client);

        config["ApiKey"].Should().Be("super-secret");
    }

    [Fact]
    public async Task Pagination_fetches_all_parameters()
    {
        var path = NewPath();
        using var client = _fixture.CreateClient();

        for (var i = 0; i < 15; i++)
        {
            await client.PutParameterAsync(new PutParameterRequest
            {
                Name = $"{path}/Param{i:D2}", Value = $"value{i}", Type = ParameterType.String, Overwrite = true
            });
        }

        var config = BuildConfiguration(path, client);

        for (var i = 0; i < 15; i++)
        {
            config[$"Param{i:D2}"].Should().Be($"value{i}");
        }
    }

    [Fact]
    public async Task Polling_reloads_changed_parameters()
    {
        var path = NewPath();
        using var client = _fixture.CreateClient();

        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/Setting", Value = "initial", Type = ParameterType.String, Overwrite = true
        });

        var options = new SsmConfigurationProviderOptions { Path = path, PollingInterval = TimeSpan.FromMilliseconds(200) };
        options.CreateClient = () => client;

        using var provider = (SsmConfigurationProvider)new SsmConfigurationSource(options: options)
            .Build(new ConfigurationBuilder());
        provider.Load();

        provider.Get("Setting").Should().Be("initial");

        await client.PutParameterAsync(new PutParameterRequest
        {
            Name = $"{path}/Setting", Value = "updated", Type = ParameterType.String, Overwrite = true
        });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && provider.Get("Setting") != "updated")
        {
            await Task.Delay(100);
        }

        provider.Get("Setting").Should().Be("updated");
    }

    private IConfiguration BuildConfiguration(string path, IAmazonSimpleSystemsManagement client)
    {
        return new ConfigurationBuilder()
            .AddSsmParameters(configurator: options =>
            {
                options.Path = path;
                options.CreateClient = () => client;
            })
            .Build();
    }
}
