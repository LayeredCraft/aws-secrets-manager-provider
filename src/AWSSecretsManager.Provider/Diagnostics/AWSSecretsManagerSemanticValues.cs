namespace AWSSecretsManager.Provider.Diagnostics;

public static class AWSSecretsManagerSemanticValues
{
    public const string True = "true";
    public const string False = "false";
    
    public const string OperationTypeLoad = "load";
    public const string OperationTypeReload = "reload";
    public const string OperationTypeFetch = "fetch";
    public const string OperationTypeBatchFetch = "batch_fetch";
    
    public const string ErrorTypeResourceNotFound = "resource_not_found";
    public const string ErrorTypeDecryptionFailure = "decryption_failure";
    public const string ErrorTypeInternalService = "internal_service";
    public const string ErrorTypeInvalidParameter = "invalid_parameter";
    public const string ErrorTypeInvalidRequest = "invalid_request";
    public const string ErrorTypeJsonParse = "json_parse";
    public const string ErrorTypePolling = "polling";
    
    public const string ApiCallOperationListSecrets = "ListSecrets";
    public const string ApiCallOperationGetSecretValue = "GetSecretValue";
    public const string ApiCallOperationBatchGetSecretValue = "BatchGetSecretValue";
    
    public const string ApiCallResultSuccess = "success";
    public const string ApiCallResultError = "error";
}