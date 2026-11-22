namespace Soundboard.App.WebRemote;

public sealed record WebRemoteResponse(int StatusCode, string ContentType, string Body)
{
    public const string Json = "application/json; charset=utf-8";
    public const string Html = "text/html; charset=utf-8";

    public static WebRemoteResponse Error(int statusCode, string message) =>
        new(statusCode, Json, System.Text.Json.JsonSerializer.Serialize(new { error = message }));
}
