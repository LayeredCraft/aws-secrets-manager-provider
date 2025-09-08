# AWS Secrets Manager Configuration Provider - Samples

This directory contains comprehensive samples demonstrating different usage patterns and features of the AWS Secrets Manager Configuration Provider.

## Quick Start

Each sample is a standalone .NET project that can be run independently:

```bash
cd samples/Sample1
dotnet run
```

## Sample Overview

| Sample | Description | Key Features |
|--------|-------------|--------------|
| **[Sample1](./Sample1/)** | Basic Usage | Default credentials, default region, all secrets |
| **[Sample2](./Sample2/)** | Region Configuration | Custom AWS region specification |
| **[Sample3](./Sample3/)** | Credential Profiles | Using named AWS credential profiles |
| **[Sample4](./Sample4/)** | Secret Filtering | Loading only specific secrets by ARN |
| **[Sample5](./Sample5/)** | Custom Key Generation | Transforming configuration key names |
| **[Sample6](./Sample6/)** | Custom Client Factory | Advanced AWS client configuration |
| **[Sample7](./Sample7/)** | Advanced Logging | Comprehensive logging and polling |
| **[SampleWeb](./SampleWeb/)** | ASP.NET Core | Web application integration |

## Prerequisites

All samples require:
- .NET 9.0 or compatible version
- AWS credentials configured (one of):
  - AWS CLI (`aws configure`)
  - Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
  - IAM roles (for EC2/ECS/Lambda)
  - AWS credential profiles
- Access to AWS Secrets Manager in your configured region

## Common Setup

1. **Configure AWS credentials**:
   ```bash
   aws configure
   ```

2. **Create test secrets in AWS Secrets Manager** (optional, for testing):
   ```bash
   aws secretsmanager create-secret --name "TestSecret1" --secret-string '{"username":"admin","password":"secret123"}'
   aws secretsmanager create-secret --name "TestSecret2" --secret-string "MySecretValue"
   ```

3. **Run any sample**:
   ```bash
   cd samples/Sample1
   dotnet run
   ```

## Sample Progression

For learning purposes, we recommend going through the samples in order:

1. **Sample1** - Start here for basic usage
2. **Sample2** - Learn region configuration
3. **Sample3** - Understand credential management
4. **Sample4** - Security with secret filtering
5. **Sample5** - Customization with key generation
6. **Sample6** - Advanced client configuration
7. **Sample7** - Production logging and monitoring
8. **SampleWeb** - Web application integration

## Framework Support

All samples target .NET 9.0 but the AWS Secrets Manager Configuration Provider supports:
- .NET Standard 2.0
- .NET 8.0+
- .NET Framework 4.6.2+ (via .NET Standard 2.0)

## Build All Samples

To build all samples at once from the repository root:

```bash
dotnet build samples/
```

## Additional Resources

- **Main Documentation**: See the main [README.md](../README.md) for complete API documentation
- **Source Code**: [src/AWSSecretsManager.Provider/](../src/AWSSecretsManager.Provider/)
- **Tests**: [tests/AWSSecretsManager.Provider.Tests/](../tests/AWSSecretsManager.Provider.Tests/)

## Need Help?

- Check the individual sample README files for detailed explanations
- Review the main project documentation
- Look at the test files for additional usage examples
- AWS Secrets Manager documentation: https://docs.aws.amazon.com/secretsmanager/