# AWSSecretsManager.Provider

**AWSSecretsManager.Provider** is a [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) provider that loads configuration values from [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/). It plugs into the standard `IConfigurationBuilder` pipeline the same way `AddJsonFile` or `AddEnvironmentVariables` do, so secrets show up alongside your other configuration sources with no special API to learn.

A sibling package, **[AWSSSM.Provider](ssm-parameter-store.md)**, provides the same pipeline backed by AWS SSM Parameter Store.

It is a modern, community-maintained fork of [Kralizek/AWSSecretsManagerConfigurationExtensions](https://github.com/Kralizek/AWSSecretsManagerConfigurationExtensions) (originally by Renato Golia), retargeted to current .NET, converted to `System.Text.Json` only, and extended with structured logging, polling/reload, and batch-fetch support.

## What it does

- Fetches secrets from AWS Secrets Manager and exposes them as `IConfiguration` key/value pairs.
- Flattens JSON-object/array secret values into hierarchical, `:`-delimited configuration keys — the same shape `appsettings.json` produces.
- Supports filtering (by ARN allowlist, custom predicate, or AWS-side `ListSecrets` filters), custom key naming, custom AWS client construction, and optional background polling for live reload.
- Works from `netstandard2.0` up through the latest .NET, in console apps, worker services, and ASP.NET Core.

## What it doesn't do

- It does not create, rotate, or manage secrets — it only reads them.
- It does not decode binary secret values (`SecretBinary`) — only string secrets (`SecretString`) are surfaced (see [Configuration & Secret Mapping](configuration.md)).
- It does not resolve AWS credentials or region on its own beyond what the AWS SDK for .NET already does — see [Authentication & Security](authentication-and-security.md).

## Where to go next

| Page | Read this for... |
|---|---|
| [Getting Started](getting-started.md) | Installing the package and wiring up your first `AddSecretsManager()` call. |
| [Configuration & Secret Mapping](configuration.md) | Every option on `SecretsManagerConfigurationProviderOptions`, and exactly how a secret value becomes one or more configuration keys. |
| [Advanced Usage](advanced.md) | Batch fetching, polling/reload, `ForceReloadAsync`, and custom AWS client construction. |
| [Authentication & Security](authentication-and-security.md) | Credential resolution, region resolution, required IAM permissions, and secret-exposure guidance. |
| [Platform Support](platform-support.md) | Target framework compatibility and using the provider against LocalStack for local development. |
| [Troubleshooting & FAQ](troubleshooting.md) | Diagnosing common failures and answers to recurring questions. |
| [API Reference](api-reference.md) | Full signatures for every public type and member. |
| [AI Coding Agent Skill](agent-skill.md) | An installable Agent Skill that teaches AI coding assistants to use this package correctly. |

## Installation

```bash
dotnet add package AWSSecretsManager.Provider
```

See [Getting Started](getting-started.md) for a complete first example.
