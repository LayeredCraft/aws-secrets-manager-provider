using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Testcontainers.Floci;
using Xunit;

namespace AWSSSM.Provider.Tests.Integration;

public sealed class FlociFixture : IAsyncLifetime
{
    private FlociContainer? _container;

    public IAmazonSimpleSystemsManagement CreateClient()
    {
        if (_container is null)
        {
            throw new InvalidOperationException("Floci container was not started.");
        }

        return new AmazonSimpleSystemsManagementClient(
            new BasicAWSCredentials(FlociBuilder.AccessKey, FlociBuilder.SecretKey),
            new AmazonSimpleSystemsManagementConfig
            {
                ServiceURL = _container.GetConnectionString(),
                AuthenticationRegion = FlociBuilder.Region
            });
    }

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailable())
        {
            return;
        }

        _container = new FlociBuilder("floci/floci:2.0.1").Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public static bool DockerAvailable()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (Uri.TryCreate(dockerHost, UriKind.Absolute, out var uri))
        {
            // unix sockets and named pipes can be probed locally; remote daemons
            // cannot - assume reachable and let the container start fail loudly.
            return uri.Scheme switch
            {
                "unix" => File.Exists(uri.LocalPath),
                "npipe" => System.OperatingSystem.IsWindows() && File.Exists(@"\\.\pipe\docker_engine"),
                _ => true
            };
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            socket.Connect(new UnixDomainSocketEndPoint("/var/run/docker.sock"));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

[CollectionDefinition("Floci")]
public sealed class FlociCollection : ICollectionFixture<FlociFixture>;
