---
name: aws-ssm-provider
description: Helps correctly integrate and configure the AWSSSM.Provider NuGet package (a Microsoft.Extensions.Configuration provider backed by AWS SSM Parameter Store) in .NET applications. Use when a user is adding AddSsmParameters, configuring SsmConfigurationProviderOptions, wiring SSM Parameter Store into IConfigurationBuilder/ASP.NET Core/generic-host configuration, or diagnosing parameter-to-configuration-key mapping, path prefixes, SecureString decryption, polling/reload, or credential/region issues for this specific package. Do not use for generic AWS SSM or Systems Manager questions unrelated to .NET configuration, for raw AWSSDK.SimpleSystemsManagement usage without this package, or for AWS Secrets Manager (that is the sibling AWSSecretsManager.Provider package).
license: MIT
metadata:
  author: LayeredCraft
  version: 1.0.0
---

# AWSSSM.Provider

`AWSSSM.Provider` is a `Microsoft.Extensions.Configuration` provider that loads AWS SSM Parameter Store parameters as configuration key/value pairs. It is the sibling of `AWSSecretsManager.Provider` (same repo, same JSON flattening rules, different AWS service and API shape). Do not confuse the two packages: Secrets Manager uses `AddSecretsManager`/`SecretsManagerConfigurationProviderOptions`; this one uses `AddSsmParameters`/`SsmConfigurationProviderOptions` and `IAmazonSimpleSystemsManagement` (note: NOT `IAmazonSSM` — AWS SDK for .NET v4 names the interface `IAmazonSimpleSystemsManagement`).

## Installation

```bash
dotnet add package AWSSSM.Provider
```

Supported: `netstandard2.0` (→ .NET Framework 4.6.2+), `net8.0`, `net9.0`.

## Core usage patterns

There are exactly three `AddSsmParameters` extension methods on `IConfigurationBuilder`, all sharing the same optional `credentials`/`region`/`configurator` parameters:

```csharp
using AWSSSM.Provider;

// No logging
builder.AddSsmParameters();
builder.AddSsmParameters(credentials, region);
builder.AddSsmParameters(configurator: options => { ... });

// Explicit ILogger<SsmConfigurationProvider>
builder.AddSsmParameters(logger, credentials, region, configurator);

// ILoggerFactory (creates the logger internally, then delegates to the ILogger overload)
builder.AddSsmParameters(loggerFactory, credentials, region, configurator);
```

**Critical fact, easy to get wrong:** there is **no automatic DI-based logger resolution**. Configuration setup runs before the DI container exists. Pass an `ILogger`/`ILoggerFactory` explicitly if log output is wanted.

Typical pattern:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSsmParameters(configurator: options =>
{
    options.Path = "/MyApp";
});
```

## Options quick-reference (`SsmConfigurationProviderOptions`)

| Option | Purpose |
|---|---|
| `Path` (`string`, default `"/"`) | Hierarchy to fetch via `GetParametersByPath`. This is the primary scoping mechanism — prefer a narrow path over fetching from root. |
| `Recursive` (`bool`, default `true`) | `false` fetches only the immediate level of `Path`. |
| `WithDecryption` (`bool`, default `true`) | Decrypts `SecureString` parameters; needs `kms:Decrypt` on the encrypting key. |
| `ParameterFilter` (`Func<Parameter, bool>`) | Client-side predicate applied to each returned parameter. |
| `KeyGenerator` (`Func<string, string, string>`) | Receives `(parameterName, path)`; returns the configuration key. Default strips the path prefix and converts `/` → `:`. |
| `ConfigureSsmConfig` | Customizes `AmazonSimpleSystemsManagementConfig` (timeouts, local emulator `ServiceURL`). Ignored if `CreateClient` is set. |
| `CreateClient` (`Func<IAmazonSimpleSystemsManagement>?`) | Full client construction override; bypasses region/credentials/`ConfigureSsmConfig`. |
| `PollingInterval` (`TimeSpan?`) | `null` (default) = load once. Set to enable background reload. |

That is the complete set — the package deliberately has no `UseBatchFetch`, `IgnoreMissingValues`, or ARN-allowlist options. `GetParametersByPath` is a single paginated call that both lists and fetches, and an absent path yields an empty configuration rather than an error.

## Key mapping rules (highest-value section — read fully)

- Default mapping: strip the configured `Path` prefix from the parameter name, trim leading `/`, convert remaining `/` to `:`. With `Path = "/MyApp"`, `/MyApp/Db/Host` → `Db:Host`. With default `Path = "/"`, `/MyApp/Db/Host` → `MyApp:Db:Host`.
- **JSON object/array parameter values** are recursively flattened the same way as the Secrets Manager sibling package: `:`-delimited keys, numeric array indices, `System.Text.Json` only (never Newtonsoft). Detection is conservative — only attempted when the value's first non-whitespace character is `{` or `[`; failed parse falls back to plain-string storage.
- **Plain string, StringList, and SecureString values** become one configuration key under the mapped parameter name (StringList is stored as its raw comma-joined string, not exploded).
- **Keys are case-insensitive.**
- A parameter name exactly equal to a non-root `Path` falls back to the full parameter name as key (never an empty key).

## Credentials & region

Default: standard AWS SDK for .NET credential chain and region-resolution fallback — this package adds no logic on top. For explicit control pass `AWSCredentials`/`RegionEndpoint` to `AddSsmParameters`. For local emulators use `ConfigureSsmConfig` to set `ServiceURL` (e.g. Floci or LocalStack at `http://localhost:4566`) with any non-empty credentials. Never invent env-var names or resolution order this package doesn't implement.

## IAM permissions

- `ssm:GetParametersByPath` on the parameter hierarchy (the only SSM read permission this package exercises)
- `kms:Decrypt` on the encrypting key when `WithDecryption = true` and `SecureString` parameters are in scope

## Polling

`PollingInterval` set → background loop refetches; reload (`OnReload`) fires only when the fetched key set differs from the previous one; most poll failures are logged as warnings and do not stop the loop, `OperationCanceledException` breaks it silently. `ForceReloadAsync(CancellationToken)` triggers the same fetch-diff-reload on demand without starting an additional loop. `Dispose()` cancels and awaits the most recently started polling loop.

## Common mistakes to avoid

- Don't recommend `AddSecretsManager`, `SecretsManagerConfigurationProviderOptions`, or `IAmazonSecretsManager` for this package — wrong sibling.
- Don't claim an `IAmazonSSM` interface exists — the AWS SDK v4 interface is `IAmazonSimpleSystemsManagement`.
- Don't claim logging works without an explicit `ILogger`/`ILoggerFactory` overload.
- Don't invent options (`UseBatchFetch`, `IgnoreMissingValues`, `AcceptedSecretArns`) that don't exist on `SsmConfigurationProviderOptions`.
- Don't claim StringList parameters are split into per-element configuration keys — they are stored as the raw string.
- Don't claim a missing path or absent parameter throws — the result is simply an empty/missing key.

## Where to point the user for more depth

Full documentation: [SSM Parameter Store page](https://layeredcraft.github.io/aws-secrets-manager-provider/ssm-parameter-store/) — plus the shared [Configuration & Secret Mapping](https://layeredcraft.github.io/aws-secrets-manager-provider/configuration/) rules for JSON flattening details.
