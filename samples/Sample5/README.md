# Sample5 - Custom Key Generation

This sample demonstrates how to customize the configuration keys generated from secret names and values.

## What it demonstrates

- Custom key transformation using the `KeyGenerator` option
- Converting secret keys to uppercase for consistency
- Flexible key naming strategies

## Key features

```csharp
builder.AddSecretsManager(configurator: options =>
{
    options.KeyGenerator = (entry, key) => key.ToUpper();
});
```

This configuration:
- Transforms all configuration keys to uppercase
- Provides consistent key casing regardless of secret naming
- Allows custom logic for key generation

## Key Generation Function

The `KeyGenerator` function receives:
- `entry`: The raw secret entry from AWS Secrets Manager
- `key`: The default generated configuration key

It returns the transformed key that will be used in the configuration.

## Use cases

- Applications requiring consistent key casing (uppercase, lowercase, camelCase)
- Legacy applications with specific key naming conventions
- Multi-environment scenarios where key formatting needs to be standardized
- Integration with existing configuration systems that have naming requirements

## Custom Key Examples

```csharp
// Convert to uppercase
options.KeyGenerator = (entry, key) => key.ToUpper();

// Add prefix
options.KeyGenerator = (entry, key) => $"SECRET_{key}";

// Convert to camelCase
options.KeyGenerator = (entry, key) => 
    char.ToLowerInvariant(key[0]) + key.Substring(1);

// Replace characters
options.KeyGenerator = (entry, key) => key.Replace("-", "_");
```

## Running the sample

1. Ensure AWS credentials are configured
2. Run: `dotnet run`
3. Check that configuration keys are transformed to uppercase

## Requirements

- AWS credentials configured
- Access to AWS Secrets Manager
- Secrets in your AWS account to see the key transformation effect