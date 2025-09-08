# Sample6 - Custom Client Factory

This sample demonstrates how to provide a custom AWS Secrets Manager client factory for advanced client configuration.

## What it demonstrates

- Custom `IAmazonSecretsManager` client creation
- Advanced client configuration options
- Explicit region and client settings control

## Key features

```csharp
builder.AddSecretsManager(configurator: options =>
{
    options.CreateClient = CreateClient;
});

static IAmazonSecretsManager CreateClient()
{
    return new AmazonSecretsManagerClient(RegionEndpoint.EUWest1);
}
```

This configuration:
- Uses a custom factory method to create the AWS client
- Allows full control over client initialization
- Enables advanced AWS SDK client configuration

## Advanced Client Configuration Examples

```csharp
// Custom region and retry policy
static IAmazonSecretsManager CreateClient()
{
    var config = new AmazonSecretsManagerConfig
    {
        RegionEndpoint = RegionEndpoint.EUWest1,
        MaxErrorRetry = 5,
        Timeout = TimeSpan.FromSeconds(30)
    };
    return new AmazonSecretsManagerClient(config);
}

// Using specific credentials
static IAmazonSecretsManager CreateClient()
{
    var credentials = new BasicAWSCredentials("accessKey", "secretKey");
    return new AmazonSecretsManagerClient(credentials, RegionEndpoint.USEast1);
}

// With proxy configuration
static IAmazonSecretsManager CreateClient()
{
    var config = new AmazonSecretsManagerConfig
    {
        ProxyHost = "proxy.example.com",
        ProxyPort = 8080,
        RegionEndpoint = RegionEndpoint.USWest2
    };
    return new AmazonSecretsManagerClient(config);
}
```

## Use cases

- Applications requiring specific client configurations (timeouts, retries, proxies)
- Custom authentication scenarios
- Advanced networking requirements
- Custom AWS SDK client settings
- Integration with existing AWS client management patterns

## Running the sample

1. Ensure AWS credentials are configured
2. Ensure access to AWS Secrets Manager in EU West 1
3. Run: `dotnet run`

## Requirements

- AWS credentials configured  
- Access to AWS Secrets Manager in the region specified in the client factory
- Any additional requirements based on your custom client configuration