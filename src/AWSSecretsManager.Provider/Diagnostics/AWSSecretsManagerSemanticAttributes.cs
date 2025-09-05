namespace AWSSecretsManager.Provider.Diagnostics;

public static class AWSSecretsManagerSemanticAttributes
{
    public const string OperationType = "aws_secrets.operation.type";
    public const string BatchSize = "aws_secrets.batch.size";
    public const string ErrorType = "aws_secrets.error.type";
    public const string AwsRegion = "aws.region";
    public const string SecretCount = "aws_secrets.secret.count";
    public const string PollingInterval = "aws_secrets.polling.interval";
    public const string UseBatchFetch = "aws_secrets.use_batch_fetch";
    public const string IgnoreMissingValues = "aws_secrets.ignore_missing_values";
    public const string ApiCallOperation = "aws_secrets.api_call.operation";
    public const string ApiCallResult = "aws_secrets.api_call.result";
    public const string JsonParseSuccess = "aws_secrets.json_parse.success";
    public const string SecretsChanged = "aws_secrets.secrets.changed";
    public const string SecretsAdded = "aws_secrets.secrets.added";
    public const string SecretsRemoved = "aws_secrets.secrets.removed";
    public const string ApiCallCount = "aws_secrets.api_calls.count";
    public const string JsonParseCount = "aws_secrets.json_parses.count";
    public const string JsonParseSuccessCount = "aws_secrets.json_parses.success_count";
}