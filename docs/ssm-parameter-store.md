# SSM Parameter Store (AWSSSM.Provider)

**AWSSSM.Provider** is the sibling package of AWSSecretsManager.Provider — a [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) provider backed by [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html). Same pipeline, same `IConfigurationBuilder` ergonomics, same JSON flattening rules — pointed at Parameter Store instead of Secrets Manager.

## Installation

```bash
dotnet add package AWSSSM.Provider
```

Supported: `netstandard2.0` (→ .NET Framework 4.6.2+), `net8.0`, `net9.0`.

## Getting started

```csharp
using AWSSSM.Provider;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSsmParameters(configurator: options =>
{
    options.Path = "/MyApp";
});
```

A parameter hierarchy of:

```
/MyApp/Db/Host      = localhost
/MyApp/Db/Port      = 5432
/MyApp/Db/Config    = {"ConnectionString":"server=db"}
```

becomes:

```
config["Db:Host"]                       → localhost
config["Db:Port"]                       → 5432
config["Db:Config:ConnectionString"]    → server=db
```

With the default `Path = "/"` the leading hierarchy level is kept: `/MyApp/Db/Host` maps to `MyApp:Db:Host`.

!!! warning "Default path fetches the entire account"
    The defaults (`Path = "/"`, `Recursive = true`) fetch **every parameter in the account**, which increases startup latency, pulls in unrelated parameters, and requires broad `ssm:GetParametersByPath` permissions. Always scope `Path` to your application's hierarchy (e.g. `"/MyApp"`) in real environments.

## Options (`SsmConfigurationProviderOptions`)

| Option | Default | Purpose |
|---|---|---|
| `Path` (`string`) | `"/"` | Parameter hierarchy to fetch via `GetParametersByPath`. |
| `Recursive` (`bool`) | `true` | Fetch all levels below `Path`, not just the immediate level. |
| `WithDecryption` (`bool`) | `true` | Decrypt `SecureString` parameters. Requires `kms:Decrypt` on the encrypting key. |
| `ParameterFilter` (`Func<Parameter, bool>`) | `_ => true` | Client-side predicate over each returned parameter. |
| `KeyGenerator` (`Func<string, string, string>`) | strip path, `/` → `:` | Rewrites each parameter name into a configuration key. Receives `(parameterName, path)`. |
| `ConfigureSsmConfig` (`Action<AmazonSimpleSystemsManagementConfig>`) | no-op | Customize the client config (timeouts, local endpoint `ServiceURL`). Ignored if `CreateClient` is set. |
| `CreateClient` (`Func<IAmazonSimpleSystemsManagement>?`) | `null` | Full override of client construction. |
| `PollingInterval` (`TimeSpan?`) | `null` | `null` (default) = load once. Set to enable background reload of changed parameters. |

## Key mapping rules

- Default mapping strips the configured `Path` prefix and converts remaining `/` to `:` — matching `appsettings.json` nesting.
- **JSON object/array parameter values** are recursively flattened using the same rules as the Secrets Manager provider (see [Configuration & Secret Mapping](configuration.md)): `:`-delimited keys, numeric array indices, conservative detection (`{`/`[` first character), `null` values preserved.
- **Plain string values** become one configuration key under the mapped parameter name.
- Keys are case-insensitive.

## Logging

As with the Secrets Manager provider, there is **no automatic DI-based logger resolution** — pass an `ILogger<SsmConfigurationProvider>` or `ILoggerFactory` explicitly to the logging overloads:

```csharp
using var loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
builder.Configuration.AddSsmParameters(loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromSeconds(30));
```

## IAM permissions

The calling identity needs:

- `ssm:GetParametersByPath` on the parameter path hierarchy
- `kms:Decrypt` on the KMS key used by any `SecureString` parameter (when `WithDecryption = true`)

## Local testing with Floci

The provider can be pointed at [Floci](https://github.com/floci-io/floci) (a free, local AWS emulator that fully emulates SSM Parameter Store, including SecureString decryption) for local development:

```csharp
options.ConfigureSsmConfig = config =>
{
    config.ServiceURL = "http://localhost:4566";
    config.AuthenticationRegion = "us-east-1";
};
```

Any non-empty credentials work against Floci. The repository's own integration test suite runs against Floci via Testcontainers — see `tests/AWSSSM.Provider.Tests/Integration/`.

## Differences from the Secrets Manager provider

- One paginated API call (`GetParametersByPath`) does both listing and fetching — there is no separate discovery step, no `UseBatchFetch`, and no `IgnoreMissingValues` (an absent path yields an empty result, not an error).
- Scoping is path-based (`Path` + `ParameterFilter`) rather than ARN-based.
- Parameters have no JSON-only limitation beyond the shared flattening rules — String, StringList, and SecureString types are all supported.
