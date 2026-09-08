# AWSSecretsManager.Provider

<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->

[![All Contributors](https://img.shields.io/badge/all_contributors-2-orange.svg?style=flat-square)](#contributors-)

<!-- ALL-CONTRIBUTORS-BADGE:END -->

[![NuGet version](https://img.shields.io/nuget/vpre/AWSSecretsManager.Provider.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AWSSecretsManager.Provider.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider/)
[![Build Status](https://github.com/LayeredCraft/aws-secrets-manager-provider/actions/workflows/pr-build.yaml/badge.svg)](https://github.com/LayeredCraft/aws-secrets-manager-provider/actions/workflows/pr-build.yaml)

This is a modern, community-maintained fork of [Kralizek/AWSSecretsManagerConfigurationExtensions](https://github.com/Kralizek/AWSSecretsManagerConfigurationExtensions), originally developed by Renato Golia.

It provides a configuration provider for [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) that loads secrets from [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/).

---

## 🚀 What's New in This Fork

- ✅ Targeted to .NET 8, 9, 10, and 11 (plus `netstandard2.0`)
- ✅ Converted to use `System.Text.Json` only
- ✅ Refactored structure for better modern SDK usage
- ✅ Comprehensive logging, batch fetch, and polling/reload support
- ✅ Published as a new NuGet package: [`AWSSecretsManager.Provider`](https://www.nuget.org/packages/AWSSecretsManager.Provider)

---

## 🔧 Quick Start

```bash
dotnet add package AWSSecretsManager.Provider
```

```csharp
using AWSSecretsManager.Provider;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder();
builder.AddSecretsManager();

var config = builder.Build();
Console.WriteLine("Secret: " + config["MySecret"]);
```

Your application must have AWS credentials available through the default AWS SDK mechanisms. See [Authentication & Security](https://layeredcraft.github.io/aws-secrets-manager-provider/authentication-and-security/) for details.

## 🌿 Sibling Package: AWSSSM.Provider

The same configuration-pipeline pattern, backed by [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) instead of Secrets Manager:

```bash
dotnet add package AWSSSM.Provider
```

```csharp
using AWSSSM.Provider;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder();
builder.AddSsmParameters(configurator: options => options.Path = "/MyApp");

var config = builder.Build();
Console.WriteLine("Db Host: " + config["Db:Host"]);
```

See the [SSM Parameter Store docs](https://layeredcraft.github.io/aws-secrets-manager-provider/ssm-parameter-store/) for options and key-mapping rules.

## 📖 Documentation

The full documentation site covers installation, every configuration option, secret-to-key mapping rules, batch fetch and polling, authentication/IAM, LocalStack, troubleshooting, and the complete API reference:

👉 **[layeredcraft.github.io/aws-secrets-manager-provider](https://layeredcraft.github.io/aws-secrets-manager-provider/)**

## 🤖 AI Coding Agent Skill

This repo ships a downloadable [Agent Skill](https://agentskills.io/) that teaches AI coding agents to use this package correctly:

```bash
npx skills add LayeredCraft/aws-secrets-manager-provider
```

See [docs/agent-skill.md](https://layeredcraft.github.io/aws-secrets-manager-provider/agent-skill/) or the skill source at [`skills/aws-secrets-manager-provider/`](./skills/aws-secrets-manager-provider/).

Using `AWSSSM.Provider` too? There's a dedicated SSM skill at [`skills/aws-ssm-provider/`](./skills/aws-ssm-provider/) covering `AddSsmParameters`, `SsmConfigurationProviderOptions`, path/key-mapping rules, and SSM-specific IAM guidance.

## 📚 Samples

The repository includes comprehensive samples demonstrating different usage patterns:

| Sample                                | Description           | Key Features                                           |
| ------------------------------------- | --------------------- | ------------------------------------------------------ |
| **[Sample1](./samples/Sample1/)**     | Basic Usage           | Default credentials, default region, all secrets       |
| **[Sample2](./samples/Sample2/)**     | Region Configuration  | Custom AWS region specification                        |
| **[Sample3](./samples/Sample3/)**     | Credential Profiles   | Using named AWS credential profiles                    |
| **[Sample4](./samples/Sample4/)**     | Secret Filtering      | Loading specific secrets by ARN allowlist              |
| **[Sample5](./samples/Sample5/)**     | Custom Key Generation | Transforming configuration key names (e.g., uppercase) |
| **[Sample6](./samples/Sample6/)**     | Custom Client Factory | Advanced AWS client configuration                      |
| **[Sample7](./samples/Sample7/)**     | Advanced Logging      | Comprehensive logging, polling, and monitoring         |
| **[SampleWeb](./samples/SampleWeb/)** | ASP.NET Core          | Web application integration with endpoints             |

Each sample includes a detailed README with usage examples, prerequisites, and explanations. See the complete [samples overview](./samples/) for setup instructions and learning progression.

---

## 📦 Installation

```bash
dotnet add package AWSSecretsManager.Provider
```

---

## ✅ Building Locally

This repo is built with the standard .NET SDK:

```bash
dotnet build
dotnet test
```

To preview the documentation site locally:

```bash
uv sync
uv run mkdocs serve
```

---

## 🙌 Acknowledgments

This project is based on the excellent work by [Renato Golia](https://github.com/Kralizek) and inspired by the broader .NET and AWS developer community.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/ncipollina"><img src="https://avatars.githubusercontent.com/u/1405469?v=4?s=100" width="100px;" alt="Nick Cipollina"/><br /><sub><b>Nick Cipollina</b></sub></a><br /><a href="https://github.com/LayeredCraft/aws-secrets-manager-provider/commits?author=ncipollina" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/ransagy"><img src="https://avatars.githubusercontent.com/u/6785058?v=4?s=100" width="100px;" alt="Ran Sagy"/><br /><sub><b>Ran Sagy</b></sub></a><br /><a href="https://github.com/LayeredCraft/aws-secrets-manager-provider/commits?author=ransagy" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!
