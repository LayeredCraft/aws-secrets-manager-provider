using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using AWSSecretsManager.Provider.Internal;
using AWSSecretsManager.Provider.Tests.Types;
using System.Text.Json;
using Xunit;
using Compono;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSecretsManager.Provider.Tests.Internal;

public class SecretsManagerConfigurationProviderTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void Simple_values_in_string_can_be_handled([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Shared] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Complex_JSON_objects_in_string_can_be_handled([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObject test, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = JsonSerializer.Serialize(test) };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObject.Property)).Should().Be(test.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Property))
            .Should().Be(test.Mid.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Leaf), nameof(Leaf.Property))
            .Should().Be(test.Mid.Leaf.Property);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Complex_JSON_objects_with_arrays_can_be_handled([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObjectWithArray test,
        [Shared] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = JsonSerializer.Serialize(test) };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObjectWithArray.Properties), "0")
            .Should().Be(test.Properties[0]);
        sut.Get(testEntry.Name, nameof(RootObjectWithArray.Mids), "0", nameof(MidLevel.Property))
            .Should().Be(test.Mids[0].Property);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Array_Of_Complex_JSON_objects_with_arrays_can_be_handled([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObjectWithArray[] test,
        [Shared] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = JsonSerializer.Serialize(test) };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

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

    [Theory, Compose<ComponoTestProfile>]
    public void Values_in_binary_are_ignored([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretBinary = new System.IO.MemoryStream() };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.HasKey(testEntry.Name).Should().BeFalse();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Secrets_can_be_filtered_out_via_options([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        options.SecretFilter = _ => false;

        sut.Load();

        secretsManager.Verify().GetSecretValueAsync(Match.Any<GetSecretValueRequest>(), Match.Any<CancellationToken>()).Never();

        sut.Get(testEntry.Name).Should().BeNull();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Secrets_can_be_listed_explicitly_and_not_searched([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Shared] IAmazonSecretsManager secretsManager, [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretKey = "KEY";
        var firstSecretArn = listSecretsResponse.SecretList.Select(x => x.ARN).First();
        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        options.SecretFilter = _ => true;
        options.AcceptedSecretArns = new List<string> { firstSecretArn };
        options.KeyGenerator = (_, _) => secretKey;

        sut.Load();

        secretsManager.Verify().GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(x => x.SecretId == firstSecretArn),
            Match.Any<CancellationToken>()).Once();
        secretsManager.Verify().GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(x => x.SecretId != firstSecretArn),
            Match.Any<CancellationToken>()).Never();
        secretsManager.Verify().ListSecretsAsync(Match.Any<ListSecretsRequest>(), Match.Any<CancellationToken>()).Never();

        sut.Get(testEntry.Name).Should().BeNull();
        sut.Get(secretKey).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Secrets_listed_explicitly_and_saved_to_configuration_with_their_names_as_keys(
        GetSecretValueResponse getSecretValueResponse, [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        options.AcceptedSecretArns = new List<string> { getSecretValueResponse.ARN };

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        secretsManager.Verify().GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(x => x.SecretId == getSecretValueResponse.ARN),
            Match.Any<CancellationToken>()).Once();
        secretsManager.Verify().GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(x => x.SecretId != getSecretValueResponse.ARN),
            Match.Any<CancellationToken>()).Never();

        sut.Get(getSecretValueResponse.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Secrets_can_be_filtered_out_via_options_on_fetching([Shared] SecretListEntry testEntry,
        GetSecretValueResponse getSecretValueResponse,
        [Shared] IAmazonSecretsManager secretsManager, [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        options.ListSecretsFilters = new List<Filter>
            { new Filter { Key = FilterNameStringType.Name, Values = new List<string> { testEntry.Name } } };

        var listSecretsResponse = new ListSecretsResponse
        {
            SecretList = new List<SecretListEntry> { testEntry }
        };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        secretsManager.Verify().ListSecretsAsync(
            Match.Is<ListSecretsRequest>(request => ReferenceEquals(request.Filters, options.ListSecretsFilters)),
            Match.Any<CancellationToken>()).Once();

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Keys_can_be_customized_via_options([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse, string newKey,
        [Shared] IAmazonSecretsManager secretsManager, [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        options.KeyGenerator = (_, _) => newKey;

        sut.Load();

        sut.Get(testEntry.Name).Should().BeNull();
        sut.Get(newKey).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Keys_should_be_case_insensitive([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueResponse,
        [Shared] IAmazonSecretsManager secretsManager, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name.ToLower()).Should().Be(getSecretValueResponse.SecretString);
        sut.Get(testEntry.Name.ToUpper()).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Get_secret_value_request_can_be_customized_via_options(ListSecretsResponse listSecretsResponse,
        GetSecretValueResponse getSecretValueResponse,
        string secretVersionStage, [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        options.ConfigureSecretValueRequest = (request, _) => request.VersionStage = secretVersionStage;

        sut.Load();

        secretsManager.Verify().GetSecretValueAsync(
            Match.Is<GetSecretValueRequest>(x => x.VersionStage == secretVersionStage),
            Match.Any<CancellationToken>()).Once();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Should_throw_on_missing_secret_value(ListSecretsResponse listSecretsResponse,
        [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.ThrowOnGetSecretValue(new ResourceNotFoundException("Oops"));

        var loadAction = () => sut.Load();
        loadAction.Should().Throw<MissingSecretValueException>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Should_skip_on_missing_secret_value_if_configured(ListSecretsResponse listSecretsResponse,
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.ThrowOnGetSecretValue(new ResourceNotFoundException("Oops"));

        options.IgnoreMissingValues = true;

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Should_throw_on_batch_missing_secret_values(ListSecretsResponse listSecretsResponse,
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        var batchGetSecretValueResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>(),
            Errors = new List<APIErrorType> { new APIErrorType { ErrorCode = nameof(ResourceNotFoundException), Message = "Oops", SecretId = "missing-secret" } },
            ResponseMetadata = new Amazon.Runtime.ResponseMetadata { RequestId = "request-id" }
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchGetSecretValueResponse);

        options.UseBatchFetch = true;

        var loadAction = () => sut.Load();
        loadAction.Should().Throw<AggregateException>();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Should_skip_on_missing_batch_secret_values_if_configured(ListSecretsResponse listSecretsResponse,
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut)
    {
        secretsManager.SetListSecretsResponse(listSecretsResponse);

        var batchGetSecretValueResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>(),
            Errors = new List<APIErrorType> { new APIErrorType { ErrorCode = nameof(ResourceNotFoundException), Message = "Oops", SecretId = "missing-secret" } },
            ResponseMetadata = new Amazon.Runtime.ResponseMetadata { RequestId = "request-id" }
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchGetSecretValueResponse);

        options.UseBatchFetch = true;
        options.IgnoreMissingValues = true;

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Should_poll_and_reload_when_secrets_changed([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueInitialResponse,
        GetSecretValueResponse getSecretValueUpdatedResponse, [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options, SecretsManagerConfigurationProvider sut,
        object changeCallbackState)
    {
        var callbackCallCount = 0;
        object? callbackState = null;
        void ChangeCallback(object? state)
        {
            callbackCallCount++;
            callbackState = state;
        }

        secretsManager.SetListSecretsResponse(listSecretsResponse);
        secretsManager.QueueGetSecretValueResponses(getSecretValueInitialResponse);

        options.PollingInterval = TimeSpan.FromMilliseconds(100);

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, changeCallbackState);

        sut.Load();
        sut.Get(testEntry.Name).Should().Be(getSecretValueInitialResponse.SecretString);
        secretsManager.QueueGetSecretValueResponses(getSecretValueUpdatedResponse);

        Thread.Sleep(200);

        callbackCallCount.Should().Be(1);
        callbackState.Should().BeSameAs(changeCallbackState);
        sut.Get(testEntry.Name).Should().Be(getSecretValueUpdatedResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public async Task Should_reload_when_forceReload_called([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, GetSecretValueResponse getSecretValueInitialResponse,
        GetSecretValueResponse getSecretValueUpdatedResponse, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut,
        object changeCallbackState)
    {
        var callbackCallCount = 0;
        object? callbackState = null;
        void ChangeCallback(object? state)
        {
            callbackCallCount++;
            callbackState = state;
        }

        secretsManager.SetListSecretsResponse(listSecretsResponse);
        secretsManager.QueueGetSecretValueResponses(getSecretValueInitialResponse);

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, changeCallbackState);

        sut.Load();
        sut.Get(testEntry.Name).Should().Be(getSecretValueInitialResponse.SecretString);
        secretsManager.QueueGetSecretValueResponses(getSecretValueUpdatedResponse);

        await sut.ForceReloadAsync(CancellationToken.None);

        callbackCallCount.Should().Be(1);
        callbackState.Should().BeSameAs(changeCallbackState);
        sut.Get(testEntry.Name).Should().Be(getSecretValueUpdatedResponse.SecretString);
    }

    [Theory]
    [InlineData("{THIS IS NOT AN OBJECT}")]
    [InlineData("[THIS IS NOT AN ARRAY]")]
    public void Incorrect_json_should_be_processed_as_string(string content)
    {
        var testEntry = new SecretListEntry { Name = "test-secret" };
        var listSecretsResponse = new ListSecretsResponse { SecretList = new List<SecretListEntry> { testEntry } };
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = content };
        var secretsManager = Composer.Create(builder => builder.UseGeneratedTestDoubles()).Create<IAmazonSecretsManager>();
        var sut = new SecretsManagerConfigurationProvider(secretsManager, new SecretsManagerConfigurationProviderOptions(), null);

        secretsManager.SetListSecretsResponse(listSecretsResponse);
        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name).Should().Be(getSecretValueResponse.SecretString);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void JSON_with_leading_spaces_should_be_processed_as_JSON([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, RootObject test, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        var secretString = " " + JsonSerializer.Serialize(test);

        var getSecretValueResponse = new GetSecretValueResponse { SecretString = secretString };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        sut.Load();

        sut.Get(testEntry.Name, nameof(RootObject.Property)).Should().Be(test.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Property))
            .Should().Be(test.Mid.Property);
        sut.Get(testEntry.Name, nameof(RootObject.Mid), nameof(MidLevel.Leaf), nameof(Leaf.Property))
            .Should().Be(test.Mid.Leaf.Property);
    }

    // Partial ARN matching tests
    // When AcceptedSecretArns contains short names or partial ARNs, AWS Secrets Manager returns the
    // full ARN (with a random "-AbCdEf" suffix) in the BatchGetSecretValue response. The previous
    // exact-equality join on ARN silently dropped every result in those cases.

    [Theory, Compose<ComponoTestProfile>]
    public void Batch_fetch_with_accepted_secret_names_should_match_returned_secrets(
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretName = "MyTestSecret";
        var fullArn = $"arn:aws:secretsmanager:us-east-1:123456789012:secret:{secretName}-AbCdEf";
        const string secretValue = "test-value";

        var batchResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>
            {
                new SecretValueEntry { ARN = fullArn, Name = secretName, SecretString = secretValue }
            },
            Errors = new List<APIErrorType>()
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchResponse);

        options.UseBatchFetch = true;
        options.AcceptedSecretArns = new List<string> { secretName };

        sut.Load();

        secretsManager.Verify().ListSecretsAsync(Match.Any<ListSecretsRequest>(), Match.Any<CancellationToken>()).Never();
        sut.Get(secretName).Should().Be(secretValue);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Batch_fetch_with_accepted_partial_arns_should_match_returned_secrets(
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretName = "MyTestSecret";
        const string partialArn = $"{secretName}-AbCdEf";
        var fullArn = $"arn:aws:secretsmanager:us-east-1:123456789012:secret:{partialArn}";
        const string secretValue = "test-value";

        var batchResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>
            {
                new SecretValueEntry { ARN = fullArn, Name = secretName, SecretString = secretValue }
            },
            Errors = new List<APIErrorType>()
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchResponse);

        options.UseBatchFetch = true;
        options.AcceptedSecretArns = new List<string> { partialArn };

        sut.Load();

        secretsManager.Verify().ListSecretsAsync(Match.Any<ListSecretsRequest>(), Match.Any<CancellationToken>()).Never();
        sut.Get(secretName).Should().Be(secretValue);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Batch_fetch_with_accepted_full_arns_should_match_returned_secrets(
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretName = "MyTestSecret";
        var fullArn = $"arn:aws:secretsmanager:us-east-1:123456789012:secret:{secretName}-AbCdEf";
        const string secretValue = "test-value";

        var batchResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>
            {
                new SecretValueEntry { ARN = fullArn, Name = secretName, SecretString = secretValue }
            },
            Errors = new List<APIErrorType>()
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchResponse);

        options.UseBatchFetch = true;
        options.AcceptedSecretArns = new List<string> { fullArn };

        sut.Load();

        secretsManager.Verify().ListSecretsAsync(Match.Any<ListSecretsRequest>(), Match.Any<CancellationToken>()).Never();
        sut.Get(secretName).Should().Be(secretValue);
    }

    // JSON null value handling tests
    // Previously, JsonValueKind.Null was grouped with JsonValueKind.Undefined and threw FormatException.
    // The fix correctly maps null JSON values to null configuration entries.

    [Theory, Compose<ComponoTestProfile>]
    public void JSON_with_null_property_value_should_not_throw([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = """{"Key": null}""" };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        sut.HasKey(testEntry.Name, "Key").Should().BeTrue();
        sut.Get(testEntry.Name, "Key").Should().BeNull();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void JSON_with_nested_null_property_value_should_not_throw([Shared] SecretListEntry testEntry,
        ListSecretsResponse listSecretsResponse, [Shared] IAmazonSecretsManager secretsManager,
        SecretsManagerConfigurationProvider sut)
    {
        var getSecretValueResponse = new GetSecretValueResponse { SecretString = """{"Parent": {"Child": null}}""" };

        secretsManager.SetListSecretsResponse(listSecretsResponse);

        secretsManager.QueueGetSecretValueResponses(getSecretValueResponse);

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        sut.HasKey(testEntry.Name, "Parent", "Child").Should().BeTrue();
        sut.Get(testEntry.Name, "Parent", "Child").Should().BeNull();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Batch_fetch_JSON_with_null_property_value_should_not_throw(
        [Shared] IAmazonSecretsManager secretsManager,
        [Shared] SecretsManagerConfigurationProviderOptions options,
        SecretsManagerConfigurationProvider sut)
    {
        const string secretName = "MySecret";
        const string fullArn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:MySecret-AbCdEf";

        var batchResponse = new BatchGetSecretValueResponse
        {
            SecretValues = new List<SecretValueEntry>
            {
                new SecretValueEntry { ARN = fullArn, Name = secretName, SecretString = """{"Key": null}""" }
            },
            Errors = new List<APIErrorType>()
        };

        secretsManager.QueueBatchGetSecretValueResponses(batchResponse);

        options.UseBatchFetch = true;
        options.AcceptedSecretArns = new List<string> { fullArn };

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        sut.HasKey(secretName, "Key").Should().BeTrue();
        sut.Get(secretName, "Key").Should().BeNull();
    }
}