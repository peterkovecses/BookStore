namespace BookStore.UI.Common.Constants;

public static class UIErrorCodes
{
    public const string ApiError = "UI.Api.Error";
    public const string EmptyResponse = "UI.Api.EmptyResponse";
    public const string Exception = "UI.Exception";
    public const string Validation = "UI.Validation.Error";
}

public static class UIErrors
{
    public static Error ApiError(string message) => Error.Failure(UIErrorCodes.ApiError, message);
    public static Error EmptyResponse() => Error.Failure(UIErrorCodes.EmptyResponse, "Response body was empty.");
    public static Error Exception(string message) => Error.Failure(UIErrorCodes.Exception, message);
    public static Error Validation(Dictionary<string, object> metadata) => Error.Validation(UIErrorCodes.Validation, "Validation failed.", metadata);
}
