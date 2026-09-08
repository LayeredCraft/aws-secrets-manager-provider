using AWSSecretsManager.Provider;
using AWSSSM.Provider;
using Microsoft.Extensions.Configuration;

var configurationBuilder = new ConfigurationBuilder();
configurationBuilder
    .AddSecretsManager()
    .AddSsmParameters();

if (configurationBuilder.Sources.Count != 2)
{
    return 1;
}

return 0;
