# Sample4 - Secret Filtering by ARN

This sample demonstrates how to filter secrets by their ARNs, loading only specific secrets instead of all accessible secrets.

## What it demonstrates

- Filtering secrets using ARN-based allowlists
- Security best practice of loading only required secrets
- Performance optimization by reducing the number of secrets loaded

## Key features

```csharp
var acceptedARNs = new[]
{
    "MySecretFullARN-abcxyz",
    "MySecretPartialARN", 
    "MySecretUniqueName"
};

builder.AddSecretsManager(configurator: options =>
{
    options.AcceptedSecretArns.AddRange(acceptedARNs);
});
```

This configuration:
- Only loads secrets that match the specified ARNs
- Supports full ARNs, partial ARNs, and secret names
- Improves security by explicitly defining which secrets to load
- Reduces memory usage and startup time

## ARN Matching Rules

- **Full ARN**: `arn:aws:secretsmanager:region:account:secret:name-suffix`
- **Partial ARN**: Can match by name prefix or partial ARN
- **Secret Name**: Just the secret name without ARN prefix

## Use cases

- Production environments where only specific secrets should be accessible
- Multi-tenant applications that need to isolate secrets
- Performance-critical applications that want to minimize secret loading
- Compliance scenarios requiring explicit secret access control

## Running the sample

1. Ensure AWS credentials are configured
2. Replace the example ARNs with actual secret ARNs from your account
3. Run: `dotnet run`

## Requirements

- AWS credentials configured
- Valid secret ARNs that exist in your AWS account
- Access to the specified secrets in AWS Secrets Manager