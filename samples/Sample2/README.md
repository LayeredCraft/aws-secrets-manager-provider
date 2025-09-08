# Sample2 - Region Configuration

This sample demonstrates how to specify a custom AWS region for the Secrets Manager provider.

## What it demonstrates

- Using default AWS credentials
- Explicitly setting the AWS region to EU West 1
- Default configuration options

## Key features

```csharp
builder.AddSecretsManager(region: RegionEndpoint.EUWest1);
```

This configuration:
- Uses AWS SDK's default credential resolution
- Forces the provider to use the EU West 1 region regardless of your default region
- Loads all accessible secrets from the specified region

## Use cases

- Multi-region applications where secrets are stored in a specific region
- Compliance requirements where data must stay in certain regions
- Cost optimization by using regions closer to your application

## Running the sample

1. Ensure AWS credentials are configured
2. Ensure you have access to AWS Secrets Manager in EU West 1
3. Run: `dotnet run`

## Requirements

- AWS credentials configured
- Access to AWS Secrets Manager in EU West 1 region