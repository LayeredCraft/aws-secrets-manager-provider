# SampleWebOtel - OpenTelemetry Integration Demo

This sample demonstrates how to use the AWS Secrets Manager Provider with comprehensive OpenTelemetry observability support.

## Features Demonstrated

- **OpenTelemetry Tracing**: Distributed traces for secret loading operations
- **OpenTelemetry Metrics**: Performance metrics for monitoring 
- **AWS Secrets Manager Provider**: Core configuration provider functionality
- **Console Exporters**: View telemetry data directly in the console
- **ASP.NET Core Integration**: Full web application setup

## What You'll See

### Traces
- `aws_secrets.load` - Main secret loading operation
- `aws_secrets.fetch_configuration_batch` - Batch secret fetching 
- `aws_secrets.aws_api_call` - Individual AWS API calls
- `aws_secrets.json_parse` - JSON parsing operations

### Metrics  
- `aws_secrets.loaded` - Number of secrets loaded
- `aws_secrets.load.duration` - Secret loading duration
- `aws_secrets.api_calls` - AWS API call counts
- `aws_secrets.batch_size` - Batch operation sizes

### Attributes
- `aws.region` - AWS region information
- `aws_secrets.operation.type` - Type of operation
- `aws_secrets.api_call.operation` - Specific AWS API operation
- `aws_secrets.json_parse.success` - JSON parsing success status

## Running the Sample

```bash
cd samples/SampleWebOtel
dotnet run
```

## Endpoints

- `GET /` - Welcome message
- `GET /config` - Show loaded configuration keys  
- `GET /health` - Health check with telemetry status
- `GET /telemetry-info` - Detailed OpenTelemetry integration information

## Telemetry Output

The sample uses console exporters, so you'll see OpenTelemetry data directly in your terminal:

```
Activity.TraceId:            abc123...
Activity.SpanId:             def456...
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: AWSSecretsManager.Provider
Activity.DisplayName:        aws_secrets.load
Activity.Kind:               Internal
Activity.StartTime:          2024-01-01T12:00:00.0000000Z
Activity.Duration:           00:00:01.2345678
Activity.Tags:
    aws_secrets.operation.type: load
    aws_secrets.use_batch_fetch: true
    aws.region: us-west-2
```

## Prerequisites

- AWS credentials configured (via AWS CLI, environment variables, or IAM roles)
- Access to AWS Secrets Manager in your configured region
- .NET 9.0 SDK

## Customization

Modify the OpenTelemetry configuration in `Program.cs` to:
- Add additional exporters (OTLP, Jaeger, etc.)
- Configure sampling rates
- Add custom attributes
- Enable additional instrumentation libraries