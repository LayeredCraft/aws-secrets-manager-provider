using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSSSM.Provider.Tests.Types;
using AWSSSM.Provider.Internal;
using Compono;
using Microsoft.Extensions.Configuration;

namespace AWSSSM.Provider.Tests;

public sealed class ComponoTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseGeneratedTestDoubles();

        builder.Register<Amazon.RegionEndpoint>(_ => Amazon.RegionEndpoint.USEast1);
        builder.Register<AWSCredentials>(_ => new AnonymousAWSCredentials());
        builder.Register<ConfigurationProvider>(_ => new TestConfigurationProvider());
        builder.Register<SsmConfigurationProviderOptions>(_ => new SsmConfigurationProviderOptions());
        builder.Register<FakeSsmClient>(_ => new FakeSsmClient());
        builder.Register<Parameter>(context => new Parameter
        {
            Name = context.Resolve<string>(),
            Value = context.Resolve<string>(),
            Type = ParameterType.String
        });
        builder.Register<RootObject>(context => new RootObject
        {
            Property = context.Resolve<string>(),
            Mid = context.Resolve<MidLevel>()
        });
        builder.Register<MidLevel>(context => new MidLevel
        {
            Property = context.Resolve<string>(),
            Leaf = context.Resolve<Leaf>()
        });
        builder.Register<Leaf>(context => new Leaf { Property = context.Resolve<string>() });
        builder.Register<RootObjectWithArray>(context => new RootObjectWithArray
        {
            Properties = new[] { context.Resolve<string>(), context.Resolve<string>(), context.Resolve<string>() },
            Mids = new[] { context.Resolve<MidLevel>(), context.Resolve<MidLevel>(), context.Resolve<MidLevel>() }
        });
        builder.Register<FakeBackedSsmConfigurationProvider>(context =>
            new FakeBackedSsmConfigurationProvider(
                context.Resolve<FakeSsmClient>(),
                context.Resolve<SsmConfigurationProviderOptions>()));
    }
}

public class FakeBackedSsmConfigurationProvider : SsmConfigurationProvider
{
    public FakeBackedSsmConfigurationProvider(FakeSsmClient client, SsmConfigurationProviderOptions options)
        : base(client, options, null)
    {
    }
}

public class FakeSsmClient : AmazonSimpleSystemsManagementClient
{
    private readonly List<GetParametersByPathRequest> _requests = new();
    private readonly Queue<GetParametersByPathResponse> _responses = new();
    private GetParametersByPathResponse _response = new() { Parameters = new List<Parameter>() };

    public FakeSsmClient()
        : base(new AnonymousAWSCredentials(),
            new AmazonSimpleSystemsManagementConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 })
    {
    }

    public IReadOnlyList<GetParametersByPathRequest> Requests => _requests;

    public void SetupResponse(GetParametersByPathResponse response)
    {
        response.Parameters ??= new List<Parameter>();
        _responses.Clear();
        _response = response;
    }

    public void SetupResponses(params GetParametersByPathResponse[] responses)
    {
        _responses.Clear();

        foreach (var response in responses)
        {
            response.Parameters ??= new List<Parameter>();
            _responses.Enqueue(response);
        }

        _response = responses.Length > 0 ? responses[^1] : new() { Parameters = new List<Parameter>() };
    }

    public override Task<GetParametersByPathResponse> GetParametersByPathAsync(GetParametersByPathRequest request,
        CancellationToken cancellationToken = default)
    {
        _requests.Add(request);

        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : _response);
    }
}

public class TestConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string? value)
    {
        Data[key] = value;
    }
}
