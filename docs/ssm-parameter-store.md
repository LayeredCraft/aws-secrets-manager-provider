# SSM Parameter Store (AWSSSM.Provider)

**AWSSSM.Provider** is the sibling package of AWSSecretsManager.Provider — a [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) provider backed by [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html). Same pipeline, same `IConfigurationBuilder` ergonomics, same JSON flattening rules — pointed at Parameter Store instead of Secrets Manager.

## Installation

```bash
dotnet add package AWSSSM.Provider
```

Supported: `netstandard2.0` (→ .NET Framework 4.6.2+), `net8.0`, `net9.0`, `net10.0`, `net11.0`.

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

| Option                                                                        | Default     | Purpose                                                                                                                                                                                                                                                                                                                                                |
| ----------------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Path` (`string`)                                                             | `"/"`       | Parameter hierarchy to fetch via `GetParametersByPath`. A trailing slash is normalized away (`"/MyApp/"` behaves exactly like `"/MyApp"`).                                                                                                                                                                                                             |
| `Recursive` (`bool`)                                                          | `true`      | Fetch all levels below `Path`, not just the immediate level. See the [IAM warning](#iam-permissions) about recursive access.                                                                                                                                                                                                                           |
| `WithDecryption` (`bool`)                                                     | `true`      | Decrypt `SecureString` parameters. Requires `kms:Decrypt` on the encrypting key.                                                                                                                                                                                                                                                                       |
| `ParameterFilter` (`Func<Parameter, bool>`)                                   | `_ => true` | Client-side predicate over each returned parameter. For server-side filtering use `ConfigureGetParametersByPathRequest` with `ParameterFilters`.                                                                                                                                                                                                       |
| `KeyGenerator` (`Func<string, string, string>`)                               | identity    | Transforms each **final** configuration key. It runs after the path prefix is stripped and after any JSON property/index suffixes are appended — same point in the pipeline as the Secrets Manager provider's `KeyGenerator`. Receives `(finalKey, path)`.                                                                                             |
| `ConfigureGetParametersByPathRequest` (`Action<GetParametersByPathRequest>?`) | `null`      | Customize each `GetParametersByPathRequest` before it is sent — e.g. `MaxResults` or server-side `ParameterFilters` (by `Type`, `KeyId`, `Label`). Invoked once per page; the provider re-applies `Path`, `Recursive`, `WithDecryption`, and `NextToken` afterwards, so the request always matches the configured options and pagination is preserved. |
| `ConfigureSsmConfig` (`Action<AmazonSimpleSystemsManagementConfig>`)          | no-op       | Customize the client config (timeouts, local endpoint `ServiceURL`). Ignored if `CreateClient` is set.                                                                                                                                                                                                                                                 |
| `CreateClient` (`Func<IAmazonSimpleSystemsManagement>?`)                      | `null`      | Full override of client construction.                                                                                                                                                                                                                                                                                                                  |
| `PollingInterval` (`TimeSpan?`)                                               | `null`      | `null` (default) = load once. Set to enable background reload of changed parameters.                                                                                                                                                                                                                                                                   |

## Key mapping rules

- Default mapping strips the configured `Path` prefix and converts remaining `/` to `:` — matching `appsettings.json` nesting.
- **JSON object/array parameter values** are recursively flattened using the same rules as the Secrets Manager provider (see [Configuration & Secret Mapping](configuration.md)): `:`-delimited keys, numeric array indices, conservative detection (`{`/`[` first character), `null` values preserved.
- **Plain string values** become one configuration key under the mapped parameter name.
- `KeyGenerator` runs **after** mapping and flattening, on each final key — a custom generator can rewrite JSON-property and array-index suffixes too, exactly like `AWSSecretsManager.Provider`.
- Keys are case-insensitive. Duplicate keys with identical values (byte-for-byte) log a warning and skip the extra entry; conflicting values throw `InvalidOperationException` (see [Migration & Compatibility](migration.md)).

## Logging

As with the Secrets Manager provider, there is **no automatic DI-based logger resolution** — pass an `ILogger<SsmConfigurationProvider>` or `ILoggerFactory` explicitly to the logging overloads:

```csharp
using var loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
builder.Configuration.AddSsmParameters(loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromSeconds(30));
```

## IAM permissions

!!! warning "Recursive access can expose denied child parameters"
The [`GetParametersByPath` API documentation](https://docs.aws.amazon.com/systems-manager/latest/APIReference/API_GetParametersByPath.html) notes that retrieving parameters **recursively** can return child parameters _even when access to a child path is explicitly denied by IAM_. If a caller can call `GetParametersByPath` on a parent path, recursively denied children may still come back. Keep `Path` as narrow as possible and scope `ssm:GetParametersByPath` tightly to the specific hierarchy level — don't rely on an IAM `Deny` on a child path to hide parameters from a recursive parent-path query.

The calling identity needs:

- `ssm:GetParametersByPath` on the parameter path hierarchy
- `kms:Decrypt` on the KMS key used by any `SecureString` parameter (when `WithDecryption = true`)

## Local testing with Floci

The provider can be pointed at [Floci](https://github.com/floci-io/floci) (a free, local AWS emulator that fully emulates SSM Parameter Store, including SecureString decryption) for local development:

```csharp
using Amazon.Runtime;
using AWSSSM.Provider;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSsmParameters(
    credentials: new BasicAWSCredentials("test", "test"),
    configurator: options =>
    {
        options.ConfigureSsmConfig = config =>
        {
            config.ServiceURL = "http://localhost:4566";
            config.AuthenticationRegion = "us-east-1";
        };
    });
```

Floci accepts any non-empty credentials, but the snippet above passes them explicitly (`BasicAWSCredentials("test", "test")`) so the snippet doesn't depend on your default AWS credential chain resolving something. The repository's own integration test suite runs against Floci via Testcontainers — see `tests/AWSSSM.Provider.Tests/Integration/`.

## Differences from the Secrets Manager provider

- One paginated API call (`GetParametersByPath`) does both listing and fetching — there is no separate discovery step, no `UseBatchFetch`, and no `IgnoreMissingValues` (an absent path yields an empty result, not an error).
- Scoping is path-based (`Path` + `ParameterFilter`) rather than ARN-based.
- Parameters have no JSON-only limitation beyond the shared flattening rules — String, StringList, and SecureString types are all supported.
