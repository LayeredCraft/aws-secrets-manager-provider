# AWSSSM.Provider

[![NuGet version](https://img.shields.io/nuget/vpre/AWSSSM.Provider.svg)](https://www.nuget.org/packages/AWSSSM.Provider)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AWSSSM.Provider.svg)](https://www.nuget.org/packages/AWSSSM.Provider/)

An AWS SSM Parameter Store-backed configuration provider for [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/), from the [LayeredCraft/aws-secrets-manager-provider](https://github.com/LayeredCraft/aws-secrets-manager-provider) project.

It loads parameters under a hierarchy path from [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) and maps them to configuration keys, with optional background polling for changes.

> Looking for AWS Secrets Manager instead? Use the sibling package [`AWSSecretsManager.Provider`](https://www.nuget.org/packages/AWSSecretsManager.Provider).

## ✨ Features

- ✅ Targeted to .NET 8, 9, 10, and 11 (plus `netstandard2.0`)
- ✅ Official Native AOT support on `net8.0` and later
- ✅ Hierarchical parameter loading via `GetParametersByPath` (recursive by default)
- ✅ Automatic JSON flattening of `String` and `SecureString` parameter values
- ✅ Optional background polling for parameter changes
- ✅ Comprehensive structured logging
- ✅ Parameter filtering and custom key generation
- ✅ Familiar API: `AddSsmParameters` mirrors `AddSecretsManager` overloads (plain / `ILogger<TProvider>` / `ILoggerFactory`)

## 🔧 Quick Start

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

A parameter `/MyApp/Db/Host` maps to the configuration key `Db:Host`. Your application must have AWS credentials available through the default AWS SDK mechanisms.

## 🔒 Important

`Recursive` defaults to `true` and `Path` is a hierarchy prefix. Setting `Path = "/"` loads every parameter in the account/region that the caller's IAM policy permits — keep the path narrow and the IAM permissions scoped. See the [SSM Parameter Store docs](https://layeredcraft.github.io/aws-secrets-manager-provider/ssm-parameter-store/) for key-mapping rules, filtering, polling, and LocalStack/Floci examples.

## 📖 Documentation

👉 **[layeredcraft.github.io/aws-secrets-manager-provider](https://layeredcraft.github.io/aws-secrets-manager-provider/)**

## 📄 License

This project is licensed under the [MIT License](https://github.com/LayeredCraft/aws-secrets-manager-provider/blob/main/LICENSE).
