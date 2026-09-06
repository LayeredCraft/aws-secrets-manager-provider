# Authentication & Security

## Credential resolution

If you don't pass `credentials` to `AddSecretsManager`, the underlying `AmazonSecretsManagerClient` resolves credentials using the standard [AWS SDK for .NET default credential chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html): environment variables, the shared AWS credentials/config file, ECS/EC2 instance metadata, or an assumed role — in that general order. This library does not add, override, or reimplement any part of that chain; it's exactly what you'd get constructing `AmazonSecretsManagerClient` directly.

To use a specific credential source explicitly, pass it in:

```csharp
using Amazon.Runtime.CredentialManagement;
using AWSSecretsManager.Provider;

var chain = new CredentialProfileStoreChain();
if (chain.TryGetProfile("MyProfile", out var profile))
{
    var credentials = profile.GetAWSCredentials(profile.CredentialProfileStore);
    builder.AddSecretsManager(credentials, profile.Region);
}
```

This is the pattern used by the `Sample3` sample — resolving a named profile explicitly rather than relying on ambient environment defaults, useful when a process needs to use a specific profile that differs from the machine's default.

## Region resolution

By default (no `ConfigureSecretsManagerConfig`/`CreateClient` set): the `region` parameter passed to `AddSecretsManager`, if not `null`, is assigned to the client config; otherwise the AWS SDK client itself falls back to its usual resolution (environment variable, shared config file, or instance metadata).

This is **not** the final word if you also use the other extensibility hooks, though:

- `ConfigureSecretsManagerConfig` runs *after* `region` is assigned to the client config, so its callback can overwrite `RegionEndpoint` — if you set both `region` and a `ConfigureSecretsManagerConfig` callback that also touches region, the callback wins.
- `CreateClient` bypasses `region` (and `ConfigureSecretsManagerConfig`) entirely — you own region resolution completely in that case.

There is no separate region-resolution logic in this library beyond passing values through to `AmazonSecretsManagerConfig.RegionEndpoint`, but which value ends up in effect depends on which of these hooks you combine.

## Required IAM permissions

Minimum policy for the default (non-batch, non-allowlisted) configuration:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:ListSecrets",
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "*"
    }
  ]
}
```

Add `secretsmanager:BatchGetSecretValue` if you set `UseBatchFetch = true`.

**Prefer scoping `Resource` down** to the specific secret ARNs your application actually needs, and prefer `AcceptedSecretArns` over broad `ListSecrets` access where the exact secret set is known ahead of time — this both reduces the IAM surface area and skips the `ListSecrets` API call entirely (see [Configuration & Secret Mapping](configuration.md)).

## Don't expose configuration contents through your application

Configuration built from this provider contains **secret values**. It's easy to accidentally leak them — e.g. by exposing a diagnostics endpoint that lists configuration keys and values instead of just key names. If you build anything like this, expose key *names* only, never values:

```csharp
app.MapGet("/config", (IConfiguration config) =>
{
    // Key names only — never return values from an endpoint like this.
    var keys = config.AsEnumerable().Select(kvp => kvp.Key).ToArray();
    return new { ConfigurationKeys = keys, Count = keys.Length };
});
```

This mirrors the caution already present in the `SampleWeb` sample.

## Logging doesn't include secret values

The provider's structured logging (see [Getting Started](getting-started.md)) logs operation timing, counts, and key names where relevant — it never logs secret values themselves.
