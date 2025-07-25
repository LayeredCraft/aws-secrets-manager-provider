using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using AutoFixture;
using AutoFixture.Xunit3;
using AWSSecretsManager.Provider.Internal;
using AWSSecretsManager.Provider.Tests.Types;
using NSubstitute;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using NSubstitute.ExceptionExtensions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationProviderTests
{
    [Theory, CustomAutoData]
    public void Simple_values_in_string_can_be_handled([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Frozen] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Complex_JSON_objects_in_string_can_be_handled([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObject test, [Frozen] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut, IFixture fixture)
    {
        var getSecretValueResponse = fixture.Build<GetSecretValueResponse>()
            .With(p => p.SecretString, JsonSerializer.Serialize(test))
            .Without(p => p.SecretBinary)
            .Create();

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObject.Property)).Should().Be(test.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Property))
            .Should().Be(test.Mid.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Leaf), nameof(Leaf.Property))
            .Should().Be(test.Mid.Leaf.Property);
    }

    [Theory, CustomAutoData]
    public void Complex_JSON_objects_with_arrays_can_be_handled([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObjectWithArray test,
        [Frozen] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut, IFixture fixture)
    {
        var getSecretValueResponse = fixture.Build<GetSecretValueResponse>()
            .With(p => p.SecretString, JsonSerializer.Serialize(test))
            .Without(p => p.SecretBinary)
            .Create();

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObjectWithArray.Properties), "0")
            .Should().Be(test.Properties[0]);
        sut.Get(testEntry.Name, nameof(RootObjectWithArray.Mids), "0", nameof(MidLevel.Property))
            .Should().Be(test.Mids[0].Property);
    }

    [Theory, CustomAutoData]
    public void Array_Of_Complex_JSON_objects_with_arrays_can_be_handled([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObjectWithArray[] test,
        [Frozen] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut, IFixture fixture)
    {
        var getSecretValueResponse = fixture.Build<GetSecretValueResponse>()
            .With(p => p.SecretString, JsonSerializer.Serialize(test))
            .Without(p => p.SecretBinary)
            .Create();

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name, "0", nameof(RootObjectWithArray.Properties), "0")
            .Should().Be(test[0].Properties[0]);
        sut.Get(testEntry.Name, "0", nameof(RootObjectWithArray.Mids), "0", nameof(MidLevel.Property))
            .Should().Be(test[0].Mids[0].Property);
        sut.Get(testEntry.Name, "1", nameof(RootObjectWithArray.Properties), "0")
            .Should().Be(test[1].Properties[0]);
        sut.Get(testEntry.Name, "1", nameof(RootObjectWithArray.Mids), "0", nameof(MidLevel.Property))
            .Should().Be(test[1].Mids[0].Property);
    }

    [Theory, CustomAutoData]
    public void Values_in_binary_are_ignored([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Frozen] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut, IFixture fixture)
    {
        var getSecretValueResponse = fixture.Build<GetSecretValueResponse>()
            .With(p => p.SecretBinary)
            .Without(p => p.SecretString)
            .Create();

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.HasKey(testEntry.Name).Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public void Secrets_can_be_filtered_out_via_options([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        options.SecretFilter = _ => false;

        sut.Load();

        secretsManager.DidNotReceive()
            .GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>());

        sut.Get(testEntry.Name).Should().BeNull();
    }

    [Theory, CustomAutoData]
    public void Secrets_can_be_listed_explicitly_and_not_searched([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Frozen] IAmazonSecretsManager secretsManager, [Frozen] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretKey = "KEY";
        var firstSecretArn = listSecretsResponse.SecretList.Select(x => x.ARN).First();
        secretsManager.GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(x => x.SecretId.Equals(firstSecretArn)),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(getSecretValueResponse));

        options.SecretFilter = _ => true;
        options.AcceptedSecretArns = new List<string> { firstSecretArn };
        options.KeyGenerator = (_, _) => secretKey;

        sut.Load();

        secretsManager.DidNotReceive()
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(x => !x.SecretId.Equals(firstSecretArn)),
                Arg.Any<CancellationToken>());
        secretsManager.DidNotReceive().ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>());

        sut.Get(testEntry.Name).Should().BeNull();
        sut.Get(secretKey).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Secrets_listed_explicitly_and_saved_to_configuration_with_their_names_as_keys(
        GetSecretValueResponse getSecretValueResponse, [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.GetSecretValueAsync(
            Arg.Is<GetSecretValueRequest>(x => x.SecretId.Equals(getSecretValueResponse.ARN)),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(getSecretValueResponse));

        options.AcceptedSecretArns = new List<string> { getSecretValueResponse.ARN };

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        secretsManager.DidNotReceive()
            .GetSecretValueAsync(
                Arg.Is<GetSecretValueRequest>(x => !x.SecretId.Equals(getSecretValueResponse.ARN)),
                Arg.Any<CancellationToken>());

        sut.Get(getSecretValueResponse.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Secrets_can_be_filtered_out_via_options_on_fetching([Frozen] SecretListEntry testEntry,
        GetSecretValueResponse getSecretValueResponse,
        [Frozen] IAmazonSecretsManager secretsManager, [Frozen] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        options.ListSecretsFilters = new List<Filter>
            { new Filter { Key = FilterNameStringType.Name, Values = new List<string> { testEntry.Name } } };

        var listSecretsResponse = new ListSecretsResponse
        {
            SecretList = new List<SecretListEntry> { testEntry }
        };

        secretsManager.ListSecretsAsync(
            Arg.Is<ListSecretsRequest>(request => request.Filters == options.ListSecretsFilters),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        secretsManager.Received(1)
            .ListSecretsAsync(Arg.Is<ListSecretsRequest>(request => request.Filters == options.ListSecretsFilters),
                Arg.Any<CancellationToken>());

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Keys_can_be_customized_via_options([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse, string newKey,
        [Frozen] IAmazonSecretsManager secretsManager, [Frozen] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        options.KeyGenerator = (_, _) => newKey;

        sut.Load();

        sut.Get(testEntry.Name).Should().BeNull();
        sut.Get(newKey).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Keys_should_be_case_insensitive([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Frozen] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name.ToLower()).Should().Be(getSecretValueResponse.SecretString);
        sut.Get(testEntry.Name.ToUpper()).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void Get_secret_value_request_can_be_customized_via_options(ListSecretsResponse listSecretsResponse,
        GetSecretValueResponse getSecretValueResponse,
        string secretVersionStage, [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        options.ConfigureSecretValueRequest = (request, _) => request.VersionStage = secretVersionStage;

        sut.Load();

        secretsManager.Received(1)
            .GetSecretValueAsync(Arg.Is<GetSecretValueRequest>(x => x.VersionStage == secretVersionStage),
                Arg.Any<CancellationToken>());
    }

    [Theory, CustomAutoData]
    public void Should_throw_on_missing_secret_value(ListSecretsResponse listSecretsResponse,
        [Frozen] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ResourceNotFoundException("Oops"));

        var loadAction = () => sut.Load();
        loadAction.Should().Throw<MissingSecretValueException>();
    }

    [Theory, CustomAutoData]
    public void Should_skip_on_missing_secret_value_if_configured(ListSecretsResponse listSecretsResponse,
        [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ResourceNotFoundException("Oops"));

        options.IgnoreMissingValues = true;

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();
    }

    [Theory, CustomAutoData]
    public void Should_throw_on_batch_missing_secret_values(ListSecretsResponse listSecretsResponse,
        [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut,
        IFixture fixture)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        var batchGetSecretValueResponse = fixture.Build<BatchGetSecretValueResponse>()
            .With(p => p.SecretValues)
            .With(p => p.Errors,
                new List<APIErrorType> { new APIErrorType { ErrorCode = nameof(ResourceNotFoundException) } })
            .Without(p => p.NextToken)
            .Create();

        secretsManager.BatchGetSecretValueAsync(Arg.Any<BatchGetSecretValueRequest>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(batchGetSecretValueResponse));

        options.UseBatchFetch = true;

        var loadAction = () => sut.Load();
        loadAction.Should().Throw<AggregateException>();
    }

    [Theory, CustomAutoData]
    public void Should_skip_on_missing_batch_secret_values_if_configured(ListSecretsResponse listSecretsResponse,
        [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut,
        IFixture fixture)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        var batchGetSecretValueResponse = fixture.Build<BatchGetSecretValueResponse>()
            .With(p => p.SecretValues)
            .With(p => p.Errors,
                new List<APIErrorType> { new APIErrorType { ErrorCode = nameof(ResourceNotFoundException) } })
            .Without(p => p.NextToken)
            .Create();

        secretsManager.BatchGetSecretValueAsync(Arg.Any<BatchGetSecretValueRequest>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(batchGetSecretValueResponse));

        options.UseBatchFetch = true;
        options.IgnoreMissingValues = true;

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();
    }

    [Theory, CustomAutoData]
    public void Should_poll_and_reload_when_secrets_changed([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueInitialResponse,
        GetSecretValueResponse getSecretValueUpdatedResponse, [Frozen] IAmazonSecretsManager secretsManager,
        [Frozen] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut,
        Action<object> changeCallback, object changeCallbackState)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueInitialResponse), Task.FromResult(getSecretValueUpdatedResponse));

        options.PollingInterval = TimeSpan.FromMilliseconds(100);

        sut.GetReloadToken().RegisterChangeCallback(changeCallback, changeCallbackState);

        sut.Load();
        sut.Get(testEntry.Name).Should().Be(getSecretValueInitialResponse.SecretString);

        Thread.Sleep(200);

        changeCallback.Received(1)(changeCallbackState);
        sut.Get(testEntry.Name).Should().Be(getSecretValueUpdatedResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public async Task Should_reload_when_forceReload_called([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueInitialResponse,
        GetSecretValueResponse getSecretValueUpdatedResponse, [Frozen] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut,
        Action<object> changeCallback, object changeCallbackState)
    {
        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueInitialResponse), Task.FromResult(getSecretValueUpdatedResponse));

        sut.GetReloadToken().RegisterChangeCallback(changeCallback, changeCallbackState);

        sut.Load();
        sut.Get(testEntry.Name).Should().Be(getSecretValueInitialResponse.SecretString);

        await sut.ForceReloadAsync(CancellationToken.None);

        changeCallback.Received(1)(changeCallbackState);
        sut.Get(testEntry.Name).Should().Be(getSecretValueUpdatedResponse.SecretString);
    }

    [Theory]
    [CustomInlineAutoData("{THIS IS NOT AN OBJECT}")]
    [CustomInlineAutoData("[THIS IS NOT AN ARRAY]")]
    public void Incorrect_json_should_be_processed_as_string(string content, [Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Frozen] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        getSecretValueResponse.SecretString = content;

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, CustomAutoData]
    public void JSON_with_leading_spaces_should_be_processed_as_JSON([Frozen] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObject test, [Frozen] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut, IFixture fixture)
    {
        var secretString = " " + JsonSerializer.Serialize(test);

        var getSecretValueResponse = fixture.Build<GetSecretValueResponse>()
            .With(p => p.SecretString, secretString)
            .Without(p => p.SecretBinary)
            .Create();

        secretsManager.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(listSecretsResponse));

        secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(getSecretValueResponse));

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObject.Property)).Should().Be(test.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Property))
            .Should().Be(test.Mid.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Leaf), nameof(Leaf.Property))
            .Should().Be(test.Mid.Leaf.Property);
    }
}