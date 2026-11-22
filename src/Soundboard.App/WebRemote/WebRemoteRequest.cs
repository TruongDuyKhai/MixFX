namespace Soundboard.App.WebRemote;

/// <summary>The parts of an HTTP request the web remote looks at; independent of the HTTP server so it can be tested without a network.</summary>
public sealed record WebRemoteRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    string Host,
    string? HeaderToken);
