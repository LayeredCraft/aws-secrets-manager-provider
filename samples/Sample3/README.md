# Sample3 - Credential Profile Usage

This sample demonstrates how to use specific AWS credential profiles with the Secrets Manager provider.

## What it demonstrates

- Loading credentials from a specific AWS profile
- Using the region from the credential profile
- Credential profile store chain integration

## Key features

```csharp
var chain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();

if (chain.TryGetProfile("MyProfile", out var profile))
{
    var credentials = profile.GetAWSCredentials(profile.CredentialProfileStore);
    builder.AddSecretsManager(credentials, profile.Region);
}
```

This configuration:
- Loads a specific named profile ("MyProfile") from AWS credentials
- Uses both credentials and region from the profile
- Provides explicit control over which AWS account/role to use

## Use cases

- Applications that need to use specific AWS profiles
- Multi-account scenarios where different profiles access different accounts
- Development environments where multiple AWS accounts are configured
- Service accounts with specific IAM roles

## Running the sample

1. Configure an AWS profile named "MyProfile" using `aws configure --profile MyProfile`
2. Ensure the profile has access to AWS Secrets Manager
3. Run: `dotnet run`

## Requirements

- AWS CLI configured with a profile named "MyProfile"
- The profile must have valid credentials and region
- Access to AWS Secrets Manager in the profile's region