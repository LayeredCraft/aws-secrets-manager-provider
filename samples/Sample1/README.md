# Sample1 - Basic Usage

This sample demonstrates the most basic usage of the AWS Secrets Manager Configuration Provider.

## What it demonstrates

- Default AWS credentials from the AWS SDK credential chain
- Default AWS region from the user's profile/environment
- Default configuration options (all secrets from the account)

## Key features

```csharp
builder.AddSecretsManager();
```

This simple call:
- Uses AWS SDK's default credential resolution (environment variables, IAM roles, profiles, etc.)
- Uses the default AWS region from your configuration
- Loads all accessible secrets from AWS Secrets Manager into configuration

## Running the sample

1. Ensure AWS credentials are configured (AWS CLI, environment variables, or IAM role)
2. Run: `dotnet run`

## Requirements

- AWS credentials configured
- AWS region configured
- Access to AWS Secrets Manager