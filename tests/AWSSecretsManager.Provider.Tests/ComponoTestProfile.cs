using System.Text;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using AWSSecretsManager.Provider.Internal;
using AWSSecretsManager.Provider.Tests.Types;
using Compono;
using Microsoft.Extensions.Configuration;

namespace AWSSecretsManager.Provider.Tests;

public sealed class ComponoTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseGeneratedTestDoubles();

        builder.Register<Amazon.RegionEndpoint>(_ => Amazon.RegionEndpoint.USEast1);
        builder.Register<AWSCredentials>(_ => new AnonymousAWSCredentials());
        builder.Register<ConfigurationProvider>(_ => new TestConfigurationProvider());
        builder.Register<SecretsManagerConfigurationProviderOptions>(_ => new SecretsManagerConfigurationProviderOptions());
        builder.Register<MemoryStream>(context => new MemoryStream(Encoding.UTF8.GetBytes(context.Resolve<string>())));
        builder.Register<SecretListEntry>(context =>
        {
            var name = context.Resolve<string>();
            return new SecretListEntry
            {
                Name = name,
                ARN = $"arn:aws:secretsmanager:us-east-1:123456789012:secret:{name}-AbCdEf"
            };
        });
        builder.Register<ListSecretsResponse>(context => new ListSecretsResponse
        {
            SecretList = new List<SecretListEntry> { context.Resolve<SecretListEntry>() }
        });
        builder.Register<GetSecretValueResponse>(context => new GetSecretValueResponse
        {
            ARN = context.Resolve<string>(),
            Name = context.Resolve<string>(),
            SecretString = context.Resolve<string>()
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
        builder.Register<SecretsManagerConfigurationProvider>(context =>
            new SecretsManagerConfigurationProvider(
                context.Resolve<IAmazonSecretsManager>(),
                context.Resolve<SecretsManagerConfigurationProviderOptions>(),
                null));
    }
}

public static class SecretsManagerTestDoubleExtensions
{
    public static void SetListSecretsResponse(this IAmazonSecretsManager secretsManager, ListSecretsResponse response) =>
        secretsManager.Configure()
            .ListSecretsAsync(Match.Any<ListSecretsRequest>(), Match.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

    public static void QueueGetSecretValueResponses(this IAmazonSecretsManager secretsManager,
        GetSecretValueResponse response) =>
        secretsManager.Configure()
            .GetSecretValueAsync(Match.Any<GetSecretValueRequest>(), Match.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

    public static void QueueBatchGetSecretValueResponses(this IAmazonSecretsManager secretsManager,
        BatchGetSecretValueResponse response) =>
        secretsManager.Configure()
            .BatchGetSecretValueAsync(Match.Any<BatchGetSecretValueRequest>(), Match.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

    public static void ThrowOnGetSecretValue(this IAmazonSecretsManager secretsManager, ResourceNotFoundException exception) =>
        secretsManager.Configure()
            .GetSecretValueAsync(Match.Any<GetSecretValueRequest>(), Match.Any<CancellationToken>())
            .Throws(exception);
}

public class TestConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string? value)
    {
        Data[key] = value;
    }
}
