# Platform Support

## Target framework matrix

| Target | Notes |
|---|---|
| `netstandard2.0` | Enables consumption from .NET Framework 4.6.2+ and .NET Core 2.0+, alongside anything else that targets netstandard2.0. |
| `net8.0` | Long-term-supported .NET release. |
| `net9.0` | Current .NET release. |
| `net10.0` | Current .NET release. |
| `net11.0` | Forward-looking target, tracked as it becomes generally available. |

`Microsoft.Extensions.Configuration` is version-pinned per target framework in this package (via central package management) so each build picks up the matching stable (or, for `net11.0`, current preview) line of that package rather than an incompatible cross-major version — this is an internal packaging detail and requires no action from consumers.

## Local development against LocalStack

The provider has no LocalStack-specific code path, but its extensibility hooks are sufficient to point it at a local AWS Secrets Manager emulator such as [LocalStack](https://www.localstack.cloud/) for local development or integration testing:

```csharp
builder.AddSecretsManager(
    credentials: new BasicAWSCredentials("test", "test"), // LocalStack accepts any non-empty values
    configurator: options =>
    {
        options.ConfigureSecretsManagerConfig = config =>
        {
            config.ServiceURL = "http://localhost:4566";
            config.UseHttp = true;
        };
    });
```

This works because `ConfigureSecretsManagerConfig` runs against the same `AmazonSecretsManagerConfig` the real client is built from — as long as `CreateClient` isn't set (which bypasses this entirely; see [Advanced Usage](advanced.md)), overriding `ServiceURL` redirects every Secrets Manager call to your local endpoint. Everything else (JSON flattening, filtering, batch fetch, polling) behaves identically against LocalStack as it does against real AWS.

This is a supported pattern via existing extensibility, not a dedicated first-class LocalStack integration — there's no LocalStack-specific package, sample, or test in this repository.
