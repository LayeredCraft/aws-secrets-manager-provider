# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET library providing an AWS Secrets Manager configuration provider for Microsoft.Extensions.Configuration. It's a modern fork of the original Kralizek/AWSSecretsManagerConfigurationExtensions, targeting .NET 8+ and using System.Text.Json exclusively.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests (uses Microsoft.Testing.Platform)
cd tests/AWSSecretsManager.Provider.Tests && dotnet run --framework net8.0
cd tests/AWSSecretsManager.Provider.Tests && dotnet run --framework net9.0

# Build and test in one command
dotnet build && cd tests/AWSSecretsManager.Provider.Tests && dotnet run --framework net8.0

# Run tests for a specific project
cd tests/AWSSecretsManager.Provider.Tests && dotnet run --framework net8.0

# Run a specific test method
dotnet test --filter "MethodName=TestMethodName"

# Pack the NuGet package
dotnet pack src/AWSSecretsManager.Provider/
```

## Project Structure

### Core Architecture
- **Main library**: `src/AWSSecretsManager.Provider/` - Contains the configuration provider implementation
- **Tests**: `tests/AWSSecretsManager.Provider.Tests/` - XUnit v3 tests with NSubstitute for mocking
- **Samples**: `samples/` - Multiple sample projects demonstrating different usage patterns

### Key Components

#### Public API
- `SecretsManagerExtensions` - Extension methods for `IConfigurationBuilder` with three overloads:
  - Basic: `AddSecretsManager()` without logging
  - With logger: `AddSecretsManager(ILogger<SecretsManagerConfigurationProvider>)`
  - With logger factory: `AddSecretsManager(ILoggerFactory)`

#### Internal Implementation
- `SecretsManagerConfigurationProvider` - Main provider that loads secrets from AWS
- `SecretsManagerConfigurationSource` - Configuration source implementation
- `SecretsManagerConfigurationSourceWithLogger` - Logger-enabled configuration source
- `SecretsManagerConfigurationProviderOptions` - Comprehensive configuration options

### Key Features
- **Batch fetching**: Supports `BatchGetSecretValue` for retrieving up to 20 secrets at once
- **Polling**: Optional background polling for secret updates
- **Structured logging**: Uses LayeredCraft.StructuredLogging with performance extensions for automatic timing
- **Logging**: Comprehensive logging with different levels (Info, Debug, Warning, Error)
- **Filtering**: Multiple ways to filter secrets (by ARN, name patterns, custom filters)
- **Customization**: Key generation, client configuration, request customization
- **Error handling**: Option to ignore missing values vs. throwing exceptions

## Testing Framework

Uses **XUnit v3** with:
- **NSubstitute** for mocking AWS services
- **AutoFixture** for test data generation
- **AwesomeAssertions** for enhanced assertions

## Target Frameworks

- .NET Standard 2.0 (for library)
- .NET 8.0 and .NET 9.0 (for library and tests)

## Package Dependencies

### Main Library
- `AWSSDK.SecretsManager` - AWS SDK for Secrets Manager
- `Microsoft.Extensions.Configuration` - Configuration abstractions
- `Microsoft.Extensions.Logging.Abstractions` - Logging support
- `System.Text.Json` - JSON serialization
- `LayeredCraft.StructuredLogging` - Structured logging with performance extensions

### Test Dependencies
- `xunit.v3` - Test framework
- `NSubstitute` - Mocking framework
- `AutoFixture` - Test data generation
- `AwesomeAssertions` - Enhanced assertions

## Development Notes

### Recent Migration
The project recently migrated from NUnit to XUnit v3, so test patterns follow XUnit conventions with `[Fact]` and `[Theory]` attributes.

### AWS Integration
All AWS interactions are abstracted through `IAmazonSecretsManager` interface, making the code highly testable with mocks.

### Logging Strategy
The provider uses LayeredCraft.StructuredLogging with performance extensions for automatic timing:
- **Information**: Key operations and structured data
- **Debug**: Detailed processing information and polling status
- **Warning**: Recoverable errors (polling failures, missing secrets when ignored)
- **Error**: Critical failures with full context
- **Performance Extensions**: Automatic timing for Load() and ReloadAsync() operations

### Configuration Flexibility
The `SecretsManagerConfigurationProviderOptions` class provides extensive customization options for filtering, client configuration, and request handling.