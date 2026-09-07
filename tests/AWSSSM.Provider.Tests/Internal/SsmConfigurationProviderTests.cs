using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AWSSecretsManager.Provider.Tests.Types;
using AWSSSM.Provider.Internal;
using AWSSSM.Provider.Tests;
using Xunit;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSSM.Provider.Tests.Internal;

public class SsmConfigurationProviderTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void Simple_values_can_be_handled([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, FakeBackedSsmConfigurationProvider sut)
    {
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name).Should().Be(testParameter.Value);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Path_prefix_is_stripped_and_slashes_become_colons(
        [Shared] FakeSsmClient ssm, [Shared] SsmConfigurationProviderOptions options,
        FakeBackedSsmConfigurationProvider sut)
    {
        var response = new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/MyApp/Db/Host", Value = "localhost", Type = ParameterType.String },
                new Parameter { Name = "/MyApp/Db/Port", Value = "5432", Type = ParameterType.String }
            }
        };

        options.Path = "/MyApp";
        ssm.SetupResponse(response);

        sut.Load();

        sut.Get("Db", "Host").Should().Be("localhost");
        sut.Get("Db", "Port").Should().Be("5432");
        sut.HasKey("MyApp", "Db", "Host").Should().BeFalse();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Default_root_path_maps_full_parameter_name(
        [Shared] FakeSsmClient ssm, FakeBackedSsmConfigurationProvider sut)
    {
        var response = new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/MyApp/Db/Host", Value = "localhost", Type = ParameterType.String }
            }
        };

        ssm.SetupResponse(response);

        sut.Load();

        sut.Get("MyApp", "Db", "Host").Should().Be("localhost");
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Request_is_built_from_options([Shared] FakeSsmClient ssm,
        [Shared] SsmConfigurationProviderOptions options, FakeBackedSsmConfigurationProvider sut)
    {
        options.Path = "/MyApp";
        options.Recursive = false;
        options.WithDecryption = false;
        ssm.SetupResponse(new GetParametersByPathResponse());

        sut.Load();

        ssm.Requests.Should().ContainSingle().Which.Should().Match<GetParametersByPathRequest>(r =>
            r.Path == "/MyApp" && r.Recursive == false && r.WithDecryption == false);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Complex_JSON_objects_can_be_handled([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, RootObject test, FakeBackedSsmConfigurationProvider sut)
    {
        testParameter.Value = JsonSerializer.Serialize(test);
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name, nameof(RootObject.Property)).Should().Be(test.Property);
        sut.Get(testParameter.Name, nameof(RootObject.Mid), nameof(MidLevel.Property))
            .Should().Be(test.Mid.Property);
        sut.Get(testParameter.Name, nameof(RootObject.Mid), nameof(MidLevel.Leaf), nameof(Leaf.Property))
            .Should().Be(test.Mid.Leaf.Property);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Complex_JSON_objects_with_arrays_can_be_handled([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, RootObjectWithArray test, FakeBackedSsmConfigurationProvider sut)
    {
        testParameter.Value = JsonSerializer.Serialize(test);
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name, nameof(RootObjectWithArray.Properties), "0")
            .Should().Be(test.Properties[0]);
        sut.Get(testParameter.Name, nameof(RootObjectWithArray.Mids), "0", nameof(MidLevel.Property))
            .Should().Be(test.Mids[0].Property);
    }

    [Theory]
    [InlineData("{THIS IS NOT AN OBJECT}")]
    [InlineData("[THIS IS NOT AN ARRAY]")]
    public void Incorrect_json_should_be_processed_as_string(string content)
    {
        var ssm = new FakeSsmClient();
        var sut = new SsmConfigurationProvider(ssm, new SsmConfigurationProviderOptions(), null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { new Parameter { Name = "test-param", Value = content } }
        });

        sut.Load();

        sut.Get("test-param").Should().Be(content);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void JSON_with_leading_spaces_should_be_processed_as_JSON([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, RootObject test, FakeBackedSsmConfigurationProvider sut)
    {
        testParameter.Value = " " + JsonSerializer.Serialize(test);
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name, nameof(RootObject.Property)).Should().Be(test.Property);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void JSON_with_null_property_value_should_not_throw([Shared] FakeSsmClient ssm,
        FakeBackedSsmConfigurationProvider sut)
    {
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "test-param", Value = """{"Key": null}""" }
            }
        });

        var loadAction = () => sut.Load();
        loadAction.Should().NotThrow();

        sut.HasKey("test-param", "Key").Should().BeTrue();
        sut.Get("test-param", "Key").Should().BeNull();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Parameters_can_be_filtered_out_via_options([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, [Shared] SsmConfigurationProviderOptions options,
        FakeBackedSsmConfigurationProvider sut)
    {
        options.ParameterFilter = _ => false;
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name).Should().BeNull();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Keys_can_be_customized_via_options([Shared] Parameter testParameter,
        string newKey, [Shared] FakeSsmClient ssm, [Shared] SsmConfigurationProviderOptions options,
        FakeBackedSsmConfigurationProvider sut)
    {
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        options.KeyGenerator = (_, _) => newKey;

        sut.Load();

        sut.Get(testParameter.Name).Should().BeNull();
        sut.Get(newKey).Should().Be(testParameter.Value);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Keys_should_be_case_insensitive([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, FakeBackedSsmConfigurationProvider sut)
    {
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { testParameter }
        });

        sut.Load();

        sut.Get(testParameter.Name.ToLower()).Should().Be(testParameter.Value);
        sut.Get(testParameter.Name.ToUpper()).Should().Be(testParameter.Value);
    }

    [Theory, Compose<ComponoTestProfile>]
    public async Task Should_poll_and_reload_when_parameters_changed([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, [Shared] SsmConfigurationProviderOptions options,
        FakeBackedSsmConfigurationProvider sut, object changeCallbackState)
    {
        var callbackCallCount = 0;
        object? callbackState = null;
        void ChangeCallback(object? state)
        {
            callbackCallCount++;
            callbackState = state;
        }

        options.PollingInterval = TimeSpan.FromMilliseconds(100);

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, changeCallbackState);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { new Parameter { Name = testParameter.Name, Value = "initial" } }
        });
        sut.Load();
        sut.Get(testParameter.Name).Should().Be("initial");
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { new Parameter { Name = testParameter.Name, Value = "updated" } }
        });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && callbackCallCount == 0)
        {
            await Task.Delay(50);
        }

        callbackCallCount.Should().Be(1);
        callbackState.Should().BeSameAs(changeCallbackState);
        sut.Get(testParameter.Name).Should().Be("updated");

        sut.Dispose();
    }

    [Theory, Compose<ComponoTestProfile>]
    public async Task Should_reload_when_forceReload_called([Shared] Parameter testParameter,
        [Shared] FakeSsmClient ssm, FakeBackedSsmConfigurationProvider sut, object changeCallbackState)
    {
        var callbackCallCount = 0;
        object? callbackState = null;
        void ChangeCallback(object? state)
        {
            callbackCallCount++;
            callbackState = state;
        }

        sut.GetReloadToken().RegisterChangeCallback(ChangeCallback, changeCallbackState);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { new Parameter { Name = testParameter.Name, Value = "initial" } }
        });
        sut.Load();
        sut.Get(testParameter.Name).Should().Be("initial");
        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter> { new Parameter { Name = testParameter.Name, Value = "updated" } }
        });

        await sut.ForceReloadAsync(CancellationToken.None);

        callbackCallCount.Should().Be(1);
        callbackState.Should().BeSameAs(changeCallbackState);
        sut.Get(testParameter.Name).Should().Be("updated");
    }

    [Fact]
    public void Key_generator_requires_separator_after_path_prefix()
    {
        var ssm = new FakeSsmClient();
        var options = new SsmConfigurationProviderOptions { Path = "/MyApp" };
        var sut = new SsmConfigurationProvider(ssm, options, null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/MyAppDev/Setting", Value = "value", Type = ParameterType.String }
            }
        });

        sut.Load();

        sut.HasKey("Dev", "Setting").Should().BeFalse();
        sut.Get("MyAppDev", "Setting").Should().Be("value");
    }

    [Fact]
    public void Case_insensitive_duplicate_keys_throw_descriptive_exception()
    {
        var ssm = new FakeSsmClient();
        var options = new SsmConfigurationProviderOptions { Path = "/MyApp" };
        var sut = new SsmConfigurationProvider(ssm, options, null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/MyApp/Db/Host", Value = "localhost", Type = ParameterType.String },
                new Parameter { Name = "/myapp/db/host", Value = "remote", Type = ParameterType.String }
            }
        });

        var loadAction = () => sut.Load();

        loadAction.Should().Throw<InvalidOperationException>().WithMessage("*Db:Host*");
    }

    [Fact]
    public void Same_key_from_flattened_json_and_child_parameter_throws_descriptive_exception()
    {
        var ssm = new FakeSsmClient();
        var sut = new SsmConfigurationProvider(ssm, new SsmConfigurationProviderOptions(), null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/MyApp/Db", Value = """{"Host": "json-host"}""", Type = ParameterType.String },
                new Parameter { Name = "/MyApp/Db/Host", Value = "param-host", Type = ParameterType.String }
            }
        });

        var loadAction = () => sut.Load();

        loadAction.Should().Throw<InvalidOperationException>().WithMessage("*Db:Host*");
    }

    [Fact]
    public void Key_generator_throws_for_parameter_that_maps_to_empty_key()
    {
        var ssm = new FakeSsmClient();
        var options = new SsmConfigurationProviderOptions { Path = "/" };
        var sut = new SsmConfigurationProvider(ssm, options, null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "/", Value = "value", Type = ParameterType.String }
            }
        });

        var loadAction = () => sut.Load();

        loadAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty configuration key*");
    }

    [Fact]
    public async Task Loading_twice_does_not_leave_multiple_pollers_running()
    {
        var ssm = new FakeSsmClient();
        var options = new SsmConfigurationProviderOptions { PollingInterval = TimeSpan.FromMilliseconds(50) };
        var sut = new SsmConfigurationProvider(ssm, options, null);

        ssm.SetupResponse(new GetParametersByPathResponse
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "test-param", Value = "value", Type = ParameterType.String }
            }
        });

        sut.Load();
        sut.Load();
        sut.Dispose();

        var requestCountAfterDispose = ssm.Requests.Count;

        await Task.Delay(200);

        ssm.Requests.Count.Should().Be(requestCountAfterDispose);
    }
}
