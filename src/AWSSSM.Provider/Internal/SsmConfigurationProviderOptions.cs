using System;
using System.Collections.Generic;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace AWSSSM.Provider.Internal;

public class SsmConfigurationProviderOptions
{
    /// <summary>
    /// The hierarchy path of parameters to retrieve. Defaults to the root path.
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
    /// Defines a function that can be used to generate configuration keys from a parameter name.
    /// Receives the parameter name and the configured <see cref="Path"/>.
    /// The default strips the path prefix and converts '/' to ':'.
    /// </summary>
    /// <example>
    /// <code>KeyGenerator = (name, path) => name.Replace("/", "__");</code>
    /// </example>
    public Func<string, string, string> KeyGenerator { get; set; } = DefaultKeyGenerator;

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

        return key.Length > 0 ? key : parameterName.TrimStart('/').Replace('/', ':');
    }
}
