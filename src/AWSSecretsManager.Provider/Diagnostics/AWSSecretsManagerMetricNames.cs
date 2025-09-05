namespace AWSSecretsManager.Provider.Diagnostics;

public static class AWSSecretsManagerMetricNames
{
    public const string SecretsLoaded = "aws_secrets.loaded";
    public const string ApiCalls = "aws_secrets.api_calls";
    public const string PollingCycles = "aws_secrets.polling_cycles";
    public const string ConfigurationErrors = "aws_secrets.configuration_errors";
    
    public const string LoadDuration = "aws_secrets.load.duration";
    public const string ReloadDuration = "aws_secrets.reload.duration";
    public const string BatchSize = "aws_secrets.batch.size";
    public const string JsonParseDuration = "aws_secrets.json_parse.duration";
}