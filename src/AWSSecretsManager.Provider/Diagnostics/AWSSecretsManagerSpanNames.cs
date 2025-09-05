namespace AWSSecretsManager.Provider.Diagnostics;

public static class AWSSecretsManagerSpanNames
{
    public const string Load = "aws_secrets.load";
    public const string Reload = "aws_secrets.reload";
    public const string FetchConfiguration = "aws_secrets.fetch_configuration";
    public const string FetchConfigurationBatch = "aws_secrets.fetch_configuration_batch";
    public const string AwsApiCall = "aws_secrets.aws_api_call";
    public const string JsonParse = "aws_secrets.json_parse";
}