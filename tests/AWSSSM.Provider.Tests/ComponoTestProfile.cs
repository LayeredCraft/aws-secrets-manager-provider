using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSSecretsManager.Provider.Tests.Types;
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
    private GetParametersByPathResponse _response = new() { Parameters = new List<Parameter>() };
    private Exception? _exception;

    public FakeSsmClient()
        : base(new AnonymousAWSCredentials(),
            new AmazonSimpleSystemsManagementConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 })
    {
    }

    public IReadOnlyList<GetParametersByPathRequest> Requests => _requests;

    public void SetupResponse(GetParametersByPathResponse response)
    {
        response.Parameters ??= new List<Parameter>();
        _response = response;
        _exception = null;
    }

    public void ThrowOnRequest(Exception exception)
    {
        _exception = exception;
    }

    public override Task<GetParametersByPathResponse> GetParametersByPathAsync(GetParametersByPathRequest request,
        CancellationToken cancellationToken = default)
    {
        _requests.Add(request);

        if (_exception is not null)
        {
            throw _exception;
        }

        return Task.FromResult(_response);
    }
}

public class TestConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string? value)
    {
        Data[key] = value;
    }
}
