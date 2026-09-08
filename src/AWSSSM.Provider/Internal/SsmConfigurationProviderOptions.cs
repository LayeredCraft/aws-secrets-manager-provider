using System;
using System.Collections.Generic;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace AWSSSM.Provider.Internal;

public class SsmConfigurationProviderOptions
{
    /// <summary>
    /// The hierarchy path of parameters to retrieve. Defaults to the root path.
    /// A trailing slash is normalized away before the request is sent and before
    /// keys are mapped, so <c>"/MyApp/"</c> behaves exactly like <c>"/MyApp"</c>.
    /// </summary>
    /// <example>
    /// <code>Path = "/MyApp";</code>
    /// </example>
    public string Path { get; set; } = "/";

    /// <summary>
    /// If true, all parameters below <see cref="Path"/> are retrieved recursively.
    /// </summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// If true, SecureString parameters are decrypted before being returned.
    /// Requires kms:Decrypt permission for the key that encrypted the parameter.
    /// </summary>
    public bool WithDecryption { get; set; } = true;

    /// <summary>
    /// A function that determines whether or not a given parameter should be included.
    /// </summary>
    /// <example>
    /// <code>ParameterFilter = parameter => parameter.Name.Contains("prod");</code>
    /// </example>
    public Func<Parameter, bool> ParameterFilter { get; set; } = _ => true;

    /// <summary>
    /// Defines a function used to transform the final configuration key for each
    /// loaded value. It runs after the parameter name is mapped (path prefix
    /// stripped, '/' converted to ':') and after any JSON property/index suffixes
    /// are appended, mirroring the <c>AWSSecretsManager.Provider</c> behavior where
    /// the generator receives each final flattened key.
    /// </summary>
    /// <example>
    /// <code>KeyGenerator = (key, path) => key.ToUpperInvariant();</code>
    /// </example>
    public Func<string, string, string> KeyGenerator { get; set; } = static (key, _) => key;

    /// <summary>
    /// A function that can be used to configure the <see cref="AmazonSimpleSystemsManagementConfig"/>
    /// that's used to create the client.
    /// </summary>
    /// <example>
    /// <code>ConfigureSsmConfig = config => config.Timeout = TimeSpan.FromSeconds(5);</code>
    /// </example>
    public Action<AmazonSimpleSystemsManagementConfig> ConfigureSsmConfig { get; set; } = _ => { };

    /// <summary>
    /// A function that can be used to provide a custom method to create a client.
    /// </summary>
    /// <example>
    /// <code>CreateClient = () => new MyCustomSsmClient();</code>
    /// </example>
    public Func<IAmazonSimpleSystemsManagement>? CreateClient { get; set; }

    /// <summary>
    /// A function that can be used to customize each <see cref="GetParametersByPathRequest"/>
    /// before it is sent. This is invoked once per page; the provider sets
    /// <see cref="GetParametersByPathRequest.NextToken"/> after this hook returns, so
    /// pagination is preserved. Use it for request-level knobs such as
    /// <see cref="GetParametersByPathRequest.MaxResults"/> and
    /// <see cref="GetParametersByPathRequest.ParameterFilters"/> (e.g. by <c>Type</c>,
    /// <c>KeyId</c>, or <c>Label</c>), which are applied server-side.
    /// </summary>
    /// <example>
    /// <code>ConfigureGetParametersByPathRequest = request => request.MaxResults = 10;</code>
    /// </example>
    public Action<GetParametersByPathRequest>? ConfigureGetParametersByPathRequest { get; set; }

    /// <summary>
    /// The time that should be waited before refreshing the parameters.
    /// If null, parameters will not be refreshed.
    /// </summary>
    /// <example>
    /// <code>PollingInterval = TimeSpan.FromMinutes(15);</code>
    /// </example>
    public TimeSpan? PollingInterval { get; set; }

    internal static string DefaultKeyGenerator(string parameterName, string path)
    {
        var normalizedPath = path.TrimEnd('/');
        var prefix = normalizedPath.Length == 0 ? "/" : normalizedPath + "/";

        var key = parameterName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? parameterName.Substring(prefix.Length)
            : parameterName;

        key = key.TrimStart('/').Replace('/', ':');

        if (key.Length == 0)
        {
            key = parameterName.TrimStart('/').Replace('/', ':');
        }

        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                $"Parameter '{parameterName}' maps to an empty configuration key. " +
                "Adjust the KeyGenerator option so each parameter maps to a unique, non-empty key.");
        }

        return key;
    }
}
