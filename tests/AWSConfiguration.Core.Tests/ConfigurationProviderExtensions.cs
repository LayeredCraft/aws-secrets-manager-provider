using Microsoft.Extensions.Configuration;

namespace AWSConfiguration.Core.Tests;

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
