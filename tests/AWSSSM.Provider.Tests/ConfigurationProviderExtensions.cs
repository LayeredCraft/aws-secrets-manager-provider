using Microsoft.Extensions.Configuration;
using Xunit;
using Compono.XunitV3;
using AwesomeAssertions;

namespace AWSSSM.Provider.Tests;

public static class ConfigurationProviderExtensions
{
    public static string Get(this IConfigurationProvider provider, params string[] pathSegments)
    {
        var key = ConfigurationPath.Combine(pathSegments);

        if (provider.TryGet(key, out var value))
        {
            return value;
        }

        return null;
    }

    public static bool HasKey(this IConfigurationProvider provider, params string[] pathSegments)
    {
        var key = ConfigurationPath.Combine(pathSegments);

        return provider.TryGet(key, out var _);
    }
}

public class ConfigurationProviderExtensionsTests
{
    [Theory, Compose<ComponoTestProfile>]
    public void Added_keys_are_found(ConfigurationProvider provider, string key, string value)
    {
        provider.Set(key, value);

        ConfigurationProviderExtensions.HasKey(provider, key).Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Added_nested_keys_are_found(ConfigurationProvider provider, string firstKey, string secondKey, string value)
    {
        provider.Set($"{firstKey}{ConfigurationPath.KeyDelimiter}{secondKey}", value);

        ConfigurationProviderExtensions.HasKey(provider, firstKey, secondKey).Should().BeTrue();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Non_added_keys_are_not_found(ConfigurationProvider provider, string key)
    {
        ConfigurationProviderExtensions.HasKey(provider, key).Should().BeFalse();
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Values_can_be_retrieved(ConfigurationProvider provider, string key, string value)
    {
        provider.Set(key, value);

        ConfigurationProviderExtensions.Get(provider, key).Should().Be(value);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Values_of_nested_keys_can_be_retrieved(ConfigurationProvider provider, string firstKey, string secondKey, string value)
    {
        provider.Set($"{firstKey}{ConfigurationPath.KeyDelimiter}{secondKey}", value);

        ConfigurationProviderExtensions.Get(provider, firstKey, secondKey).Should().Be(value);
    }

    [Theory, Compose<ComponoTestProfile>]
    public void Non_added_keys_return_null(ConfigurationProvider provider, string key)
    {
        ConfigurationProviderExtensions.Get(provider, key).Should().BeNull();
    }
}