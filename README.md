# AWSSecretsManager.Provider
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-1-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->
[![NuGet version](https://img.shields.io/nuget/vpre/AWSSecretsManager.Provider.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AWSSecretsManager.Provider.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider/)
[![Build Status](https://github.com/LayeredCraft/aws-secrets-manager-provider/actions/workflows/build.yaml/badge.svg)](https://github.com/LayeredCraft/aws-secrets-manager-provider/actions)

This is a modern, community-maintained fork of [Kralizek/AWSSecretsManagerConfigurationExtensions](https://github.com/Kralizek/AWSSecretsManagerConfigurationExtensions), originally developed by Renato Golia.

It provides a configuration provider for [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) that loads secrets from [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/).

---

## 🚀 What's New in This Fork

- ✅ Targeted to .NET 8 and .NET 9
- ✅ Converted to use `System.Text.Json` only
- ✅ Refactored structure for better modern SDK usage
- ✅ **NEW**: Comprehensive logging support with `ILogger` integration
- ✅ Published as a new NuGet package: [`AWSSecretsManager.Provider`](https://www.nuget.org/packages/AWSSecretsManager.Provider)

---

## 🔧 Usage

### ASP.NET Core Example

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddSecretsManager(); // 👈 AWS Secrets Manager integration
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}
```

### Console App Example

```csharp
static void Main(string[] args)
{
    var builder = new ConfigurationBuilder();
    builder.AddSecretsManager();

    var config = builder.Build();
    Console.WriteLine("Secret: " + config["MySecret"]);
}
```

Your application must have AWS credentials available through the default AWS SDK mechanisms. Learn more here:  
👉 [AWS SDK Credential Config](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html)

### 📋 Logging Support

The provider includes comprehensive logging support for better observability:

```csharp
// Using ILoggerFactory (recommended)
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
builder.Configuration.AddSecretsManager(
    loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromMinutes(5));

// Using explicit ILogger
var logger = loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>();
builder.Configuration.AddSecretsManager(
    logger,
    configurator: options => options.PollingInterval = TimeSpan.FromMinutes(5));
```

**Log Levels:**
- **Information**: Key operations (loading, reloading, polling status)
- **Debug**: Batch processing details and secret counts  
- **Trace**: Individual secret processing and change detection
- **Warning**: Polling errors and missing secrets (when ignored)
- **Error**: Failed operations with full context

**Example Log Output:**
```
[Information] Loading secrets from AWS Secrets Manager
[Debug] Fetching 15 secrets in 1 batches
[Information] Successfully loaded 47 configuration keys in 1,234ms
[Information] Starting secret polling with interval 00:05:00
```

---

## 🔒 Configuration Options

This provider supports several customization options, including:

- **Credentials**: Pass your own credentials if needed.
- **Region**: Customize the AWS region.
- **Filtering**: Control which secrets are loaded via filters or explicit allow lists.
- **Key generation**: Customize how configuration keys are named.
- **Version stage**: Set version stages for secrets.
- **Logging**: Full logging support with `ILogger` integration for observability.
- **LocalStack support**: Override `ServiceUrl` for local testing.

Refer to [samples](/samples/) for examples of each option.

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
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!