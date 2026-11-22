using System.Buffers.Text;
using System.Security.Cryptography;

namespace Soundboard.App.WebRemote;

public static class WebRemoteToken
{
    private const int TokenBytes = 24;

    /// <summary>192 random bits, URL-safe so it can sit in a query string.</summary>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
}
