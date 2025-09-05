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
- ✅ **NEW**: OpenTelemetry observability with metrics, traces, and AWS region tracking
- ✅ Published as new NuGet packages: [`AWSSecretsManager.Provider`](https://www.nuget.org/packages/AWSSecretsManager.Provider) + [`AWSSecretsManager.Provider.Observability`](https://www.nuget.org/packages/AWSSecretsManager.Provider.Observability)

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

### 📊 OpenTelemetry Observability 

The provider includes comprehensive OpenTelemetry support for monitoring secret loading performance, AWS API interactions, and operational metrics:

```csharp
// Add OpenTelemetry observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAWSSecretsManager()  // 👈 AWS Secrets Manager tracing
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AWSSecretsManagerTelemetry.MeterName)  // 👈 AWS Secrets Manager metrics
        .AddConsoleExporter());

// Configure AWS Secrets Manager with logging
builder.Configuration.AddSecretsManager(loggerFactory);
```

**📈 Available Metrics:**
- `aws_secrets.loaded` - Number of secrets loaded
- `aws_secrets.load.duration` - Time to load secrets (histogram)
- `aws_secrets.reload.duration` - Time to reload secrets (histogram)
- `aws_secrets.api_calls` - AWS API call counts by operation
- `aws_secrets.polling_cycles` - Background polling success counts
- `aws_secrets.configuration_errors` - Configuration error counts by type
- `aws_secrets.batch_size` - Batch operation sizes (histogram)
- `aws_secrets.json_parse.duration` - JSON parsing times (histogram)

**🔍 Available Traces:**
- `aws_secrets.load` - Main secret loading operation
- `aws_secrets.reload` - Secret reload operation
- `aws_secrets.fetch_configuration` - Individual secret fetching
- `aws_secrets.fetch_configuration_batch` - Batch secret fetching
- `aws_secrets.aws_api_call` - Individual AWS API calls
- `aws_secrets.json_parse` - JSON parsing operations

**🏷️ Key Attributes:**
- `aws.region` - AWS region information
- `aws_secrets.operation.type` - Type of operation (load, reload, fetch, etc.)
- `aws_secrets.api_call.operation` - AWS API operation (ListSecrets, GetSecretValue, etc.)
- `aws_secrets.use_batch_fetch` - Whether batch fetching is enabled
- `aws_secrets.json_parse.success` - JSON parsing success status
- `aws_secrets.secret.count` - Number of secrets processed

**📋 Complete Example:**

See the [SampleWebOtel](/samples/SampleWebOtel/) sample for a complete ASP.NET Core application with OpenTelemetry integration.

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

## 📚 Samples

| Sample | Description | Key Features | When to Use |
|--------|-------------|--------------|-------------|
| [Sample1](/samples/Sample1/) | Basic usage with default settings | Default credentials, region, and options | Starting point for simple scenarios |
| [Sample2](/samples/Sample2/) | Custom AWS region configuration | Explicit region (EU-West-1) | Multi-region deployments |
| [Sample3](/samples/Sample3/) | Named AWS profile credentials | Credential profile store, custom profile | Using named AWS profiles |
| [Sample4](/samples/Sample4/) | Secret filtering by ARN | Explicit allow list of secret ARNs | Restricting to specific secrets |
| [Sample5](/samples/Sample5/) | Custom key generation | Uppercase key transformation | Custom configuration key naming |
| [Sample6](/samples/Sample6/) | Custom client factory | Client creation with custom configuration | Advanced AWS client customization |
| [Sample7](/samples/Sample7/) | Comprehensive logging | Full logging integration, manual reload | Debugging and operational visibility |
| [SampleWeb](/samples/SampleWeb/) | ASP.NET Core integration | Web application, logger factory, polling | Modern web applications |
| [SampleWebOtel](/samples/SampleWebOtel/) | **NEW**: OpenTelemetry integration | Traces, metrics, console exporters | Production monitoring and observability |

Each sample includes a detailed `Program.cs` with comments explaining the configuration options and use cases.

---

## 📦 Packages

| Package | Description | Version |
|---------|-------------|---------|
| [AWSSecretsManager.Provider](https://www.nuget.org/packages/AWSSecretsManager.Provider) | Core AWS Secrets Manager configuration provider | [![NuGet](https://img.shields.io/nuget/vpre/AWSSecretsManager.Provider.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider) |
| [AWSSecretsManager.Provider.Observability](https://www.nuget.org/packages/AWSSecretsManager.Provider.Observability) | OpenTelemetry observability extensions | [![NuGet](https://img.shields.io/nuget/vpre/AWSSecretsManager.Provider.Observability.svg)](https://www.nuget.org/packages/AWSSecretsManager.Provider.Observability) |

### Installation

```bash
# Core provider
dotnet add package AWSSecretsManager.Provider

# Optional: OpenTelemetry observability  
dotnet add package AWSSecretsManager.Provider.Observability
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