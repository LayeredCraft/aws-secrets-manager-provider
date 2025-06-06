using System;
using System.Collections.Generic;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace AWSSecretsManager.Provider.Internal;

public class SecretsManagerConfigurationProviderOptions
{
    /// <summary>
    /// A list of identifiers for the secrets that are to be retrieved.
    /// The secret ARN (full or partial) and secret name are supported.
    /// <remarks>
    /// See <see cref="GetSecretValueRequest.SecretId"/> for more info on supported values.
    /// </remarks>
    /// <example>
    /// <code>
    /// AcceptedSecretArns = new List&lt;string&gt;
    /// {
    ///     "MySecretFullARN-abcxyz",
    ///     "MySecretPartialARN",
    ///     "MySecretUniqueName"
    /// };
    /// </code>
    /// </example>
    /// </summary>
    public List<string> AcceptedSecretArns { get; set; } = new();

    /// <summary>
    /// A function that determines whether or not a given secret should be retrieved.
    /// </summary>
    /// <example>
    /// <code>SecretFilter = secret => secret.Name.Contains("IncludeMe");</code>
    /// </example>
    public Func<SecretListEntry, bool> SecretFilter { get; set; } = _ => true;

    /// <summary>
    /// A list of filters that get passed to the client to filter the listed secrets before returning them. 
    /// </summary>
    /// <example>
    /// <code>
    /// ListSecretsFilters = new List&lt;Filter&gt;
    /// {
    ///     new Filter
    ///     {
    ///         Key = FilterNameStringType.Name,
    ///         Values = new List&lt;string&gt; { "IncludeMe" }
    ///     }
    /// };
    /// </code>
    /// </example>
    public List<Filter> ListSecretsFilters { get; set; } = new();

    /// <summary>
    /// Defines a function that can be used to generate secret keys.
    /// </summary>
    /// <example>
    /// <code>
    /// KeyGenerator = (secret, key) => key.ToUpper();
    /// </code>
    /// </example>
    public Func<SecretListEntry, string, string> KeyGenerator { get; set; } = (secret, key) => key;

    /// <summary>
    /// Defines a function that can be used to customize the <see cref="GetSecretValueRequest"/> before it is sent.
    /// This Option is only used if <see cref="UseBatchFetch"/> is set to false.
    /// </summary>
    /// <example>
    /// <code>
    /// ConfigureSecretValueRequest = (request, context) => request.VersionStage = "AWSCURRENT";
    /// </code>
    /// </example>
    public Action<GetSecretValueRequest, SecretValueContext> ConfigureSecretValueRequest { get; set; } = (_, _) => { };

    /// <summary>
    /// Defines a function that can be used to customize the <see cref="BatchGetSecretValueRequest"/> before it is sent.
    /// This Option is only used if <see cref="UseBatchFetch"/> is set to true.
    /// </summary>
    /// <example>
    /// <code>
    /// ConfigureSecretValueRequest = (request, context) => request.VersionStage = "AWSCURRENT";
    /// </code>
    /// </example>
    public Action<BatchGetSecretValueRequest, List<SecretValueContext>> ConfigureBatchSecretValueRequest { get; set; } = (_, _) => { };

    /// <summary>
    /// A function that can be used to configure the <see cref="AmazonSecretsManagerClient"/>
    /// that's injected into the client.
    /// </summary>
    /// <example>
    /// <code>
    /// ConfigureSecretsManagerConfig = config => config.Timeout = TimeSpan.FromSeconds(5);
    /// </code>
    /// </example>
    public Action<AmazonSecretsManagerConfig> ConfigureSecretsManagerConfig { get; set; } = _ => { };

    /// <summary>
    /// A function that can be used to provide a custom method to create a client.
    /// </summary>
    /// <example>
    /// <code>
    /// CreateClient = () => new MyCustomSecretsManagerClient();
    /// </code>
    /// </example>
    public Func<IAmazonSecretsManager>? CreateClient { get; set; }

    /// <summary>
    /// The time that should be waited before refreshing the secrets.
    /// If null, secrets will not be refreshed.
    /// </summary>
    /// <example>
    /// <code>
    /// PollingInterval = TimeSpan.FromMinutes(15);
    /// </code>
    /// </example>
    public TimeSpan? PollingInterval { get; set; }

    /// <summary>
    /// If True, Requests will use BatchGetSecretValue to retrieve up to 20 secrets at a time.
    /// If set to true, <see cref="ConfigureSecretValueRequest"/> will no longer work,
    /// you must instead use <see cref="ConfigureBatchSecretValueRequest"/>
    /// <para/>
    /// Note: You must make sure secretsmanager:BatchGetSecretValue is allowed for the resource!
    /// </summary>
    public bool UseBatchFetch { get; set; }

    /// <summary>
    /// If true, the provider will ignore missing values and not throw an exception.
    /// </summary>
    /// <example>
    /// <code>
    /// IgnoreMissingValues = true;
    /// </code>
    /// </example>
    public bool IgnoreMissingValues { get; set; }

}