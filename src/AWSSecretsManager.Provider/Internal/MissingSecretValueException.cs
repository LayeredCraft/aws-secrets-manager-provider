using System;

namespace AWSSecretsManager.Provider.Internal;

/// <summary>
/// Exception thrown when a required secret value is missing from AWS Secrets Manager.
/// </summary>
public class MissingSecretValueException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingSecretValueException"/> class.
    /// </summary>
    /// <param name="errorMessage">The error message that explains the reason for the exception.</param>
    /// <param name="secretName">The name of the secret that could not be retrieved.</param>
    /// <param name="secretArn">The ARN of the secret that could not be retrieved.</param>
    /// <param name="exception">The exception that is the cause of the current exception.</param>
    public MissingSecretValueException(string errorMessage, string secretName, string secretArn, Exception exception) : base(errorMessage, exception)
    {
        SecretName = secretName;
        SecretArn = secretArn;
    }

    /// <summary>
    /// Gets the ARN of the secret that could not be retrieved.
    /// </summary>
    public string SecretArn { get; }

    /// <summary>
    /// Gets the name of the secret that could not be retrieved.
    /// </summary>
    public string SecretName { get; }
}