using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Soundboard.App.Tests.YouTube;

/// <summary>
/// Stands in for github.com: serves a "latest" tag and release assets for yt-dlp and Deno, the way the real
/// site does after redirects. Each test changes what one URL returns to simulate a failure.
/// </summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    public const string YtDlpTag = "2026.10.26";
    public const string DenoTag = "v2.9.8";
    public const string DenoAsset = "deno-x86_64-pc-windows-msvc.zip";

    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

    public FakeGitHub()
    {
        ServeRelease(YtDlpTag, Encoding.ASCII.GetBytes("yt-dlp " + YtDlpTag), DenoTag, DenoZip("deno " + DenoTag));
    }

    public List<string> Requests { get; } = [];

    /// <summary>Makes <paramref name="ytDlpTag"/> / <paramref name="denoTag"/> the latest releases with these files and correct checksums.</summary>
    public void ServeRelease(string ytDlpTag, byte[] ytDlpExe, string denoTag, byte[] denoZip)
    {
        Latest("yt-dlp/yt-dlp", ytDlpTag);
        Latest("denoland/deno", denoTag);

        var ytBase = $"https://github.com/yt-dlp/yt-dlp/releases/download/{ytDlpTag}/";
        Bytes(ytBase + "yt-dlp.exe", ytDlpExe);
        Text(ytBase + "SHA2-256SUMS", $"{Sha256("other")}  yt-dlp\n{Sha256(ytDlpExe)}  yt-dlp.exe\n");

        var denoBase = $"https://github.com/denoland/deno/releases/download/{denoTag}/";
        Bytes(denoBase + DenoAsset, denoZip);
        Text(denoBase + DenoAsset + ".sha256sum",
            $"\r\nAlgorithm : SHA256\r\nHash      : {Sha256(denoZip).ToUpperInvariant()}\r\nPath      : C:\\a\\deno\\{DenoAsset}\r\n\r\n");
    }

    public void Route(string url, Func<HttpResponseMessage> respond) => _routes[url] = respond;

    public void Bytes(string url, byte[] body) => Route(url, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });

    public void Text(string url, string body) => Route(url, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    /// <summary>"/releases/latest" answers as if redirected to "/releases/tag/&lt;tag&gt;".</summary>
    public void Latest(string repository, string tag) =>
        Route($"https://github.com/{repository}/releases/latest", () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Head, $"https://github.com/{repository}/releases/tag/{tag}"),
        });

    public static byte[] DenoZip(string exeContent, params (string Name, string Content)[] extraEntries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in extraEntries.Prepend(("deno.exe", exeContent)))
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    public static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string Sha256(string text) => Sha256(Encoding.ASCII.GetBytes(text));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add($"{request.Method} {url}");
        if (!_routes.TryGetValue(url, out var respond))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        var response = respond();
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}
