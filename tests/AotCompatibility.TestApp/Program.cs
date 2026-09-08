using System;
using AWSSecretsManager.Provider;
using AWSSSM.Provider;
using Microsoft.Extensions.Configuration;

var configurationBuilder = new ConfigurationBuilder();
configurationBuilder
    .AddSecretsManager()
    .AddSsmParameters();

if (configurationBuilder.Sources.Count != 2)
{
    Console.WriteLine($"AOT compatibility failure: expected 2 configuration sources but found {configurationBuilder.Sources.Count}.");
    return 1;
}

return 0;
