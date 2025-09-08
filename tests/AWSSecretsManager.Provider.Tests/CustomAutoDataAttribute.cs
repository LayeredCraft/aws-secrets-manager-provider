using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.SecretsManager.Model;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Kernel;
using AutoFixture.Xunit3;
using AWSSecretsManager.Provider.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWSSecretsManager.Provider.Tests;

[AttributeUsage(AttributeTargets.Method)]
public class CustomAutoDataAttribute : AutoDataAttribute
{
    public CustomAutoDataAttribute() : base(FixtureHelpers.CreateFixture)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CustomInlineAutoDataAttribute : InlineAutoDataAttribute
{
    public CustomInlineAutoDataAttribute(params object[] args) : base(FixtureHelpers.CreateFixture, args)
    {
    }
}

public class ConfigurationProviderSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(ConfigurationProvider))
        {
            return new TestConfigurationProvider();
        }
        
        return new NoSpecimen();
    }
}

public class TestConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string value)
    {
        Data[key] = value;
    }
}

public static class FixtureHelpers
{
    public static IFixture CreateFixture()
    {
        IFixture fixture = new Fixture();

        // Add custom specimen builder for ConfigurationProvider before AutoNSubstitute
        fixture.Customizations.Add(new ConfigurationProviderSpecimenBuilder());

        fixture.Customize(new AutoNSubstituteCustomization
        {
            GenerateDelegates = true
        });

        fixture.Customize<SecretsManagerConfigurationProviderOptions>(o => o.OmitAutoProperties());

        fixture.Customize<MemoryStream>(c =>
        {
            return c.FromFactory((string str) =>
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                return new MemoryStream(bytes);
            }).OmitAutoProperties();
        });

        fixture.Customize<ListSecretsResponse>(o => o
            .With(p => p.SecretList, (SecretListEntry entry) => new List<SecretListEntry> { entry })
            .Without(p => p.NextToken));

        fixture.Customize<GetSecretValueResponse>(o => o
            .With(p => p.SecretString)
            .Without(p => p.SecretBinary));

        // Configure SecretsManagerConfigurationProvider to use null logger by default in tests
        fixture.Customize<SecretsManagerConfigurationProvider>(c => 
            c.FromFactory((Amazon.SecretsManager.IAmazonSecretsManager client, SecretsManagerConfigurationProviderOptions options) =>
                new SecretsManagerConfigurationProvider(client, options, null)));

        return fixture;
    }
}