using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Models;

namespace Soundboard.App.WebRemote;

/// <summary>
/// Routes web remote requests (<see cref="FeatureFlags.WebRemote"/>). Like every other trigger
/// source it only talks to <see cref="IActionDispatcher"/> (PROJECT_PROMPT.md §4.2).
/// <list type="bullet">
/// <item><c>GET /?token=…</c> — a page of sound buttons.</item>
/// <item><c>GET /api/sounds?token=…</c> — the sounds as JSON.</item>
/// <item><c>POST /api/trigger?token=…&amp;action=PlaySound&amp;target=&lt;id&gt;[&amp;phase=press|down|up][&amp;mode=press|toggle|hold|restart]</c></item>
/// </list>
/// </summary>
public sealed class WebRemoteRequestHandler(IActionDispatcher dispatcher, ISettingsProvider settings)
{
    public const string TokenHeader = "X-Soundboard-Token";

    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "[::1]"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WebRemoteResponse Handle(WebRemoteRequest request, string token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(token);

        // A web page on another site can make the browser resolve its own host name to 127.0.0.1
        // (DNS rebinding); such requests carry that foreign name in Host and are refused.
        var host = request.Host.Split(':')[0];
        if (!LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase) && !request.Host.StartsWith("[::1]", StringComparison.Ordinal))
        {
            return WebRemoteResponse.Error(403, "Only localhost is accepted.");
        }

        var supplied = request.HeaderToken ?? request.Query.GetValueOrDefault("token");
        if (!TokenMatches(supplied, token))
        {
            return WebRemoteResponse.Error(401, "Missing or wrong token.");
        }

        return (request.Method.ToUpperInvariant(), request.Path.TrimEnd('/')) switch
        {
            ("GET", "") => new WebRemoteResponse(200, WebRemoteResponse.Html, WebRemotePage.Render(settings.Current, token)),
            ("GET", "/api/sounds") => Sounds(),
            ("POST", "/api/trigger") => Trigger(request.Query),
            _ => WebRemoteResponse.Error(404, "Unknown endpoint."),
        };
    }

    private static bool TokenMatches(string? supplied, string expected) =>
        supplied is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));

    private WebRemoteResponse Sounds()
    {
        var sounds = settings.Current.Sounds.Select(s => new { id = s.Id, name = s.Name, color = s.Color });
        return new WebRemoteResponse(200, WebRemoteResponse.Json, JsonSerializer.Serialize(sounds, JsonOptions));
    }

    private WebRemoteResponse Trigger(IReadOnlyDictionary<string, string> query)
    {
        var action = query.GetValueOrDefault("action");
        if (string.IsNullOrEmpty(action))
        {
            return WebRemoteResponse.Error(400, "'action' is required.");
        }

        Guid? target = null;
        if (query.TryGetValue("target", out var targetText))
        {
            if (!Guid.TryParse(targetText, out var parsed))
            {
                return WebRemoteResponse.Error(400, "'target' must be a sound id.");
            }

            target = parsed;
        }

        if (!Enum.TryParse<TriggerMode>(query.GetValueOrDefault("mode", nameof(TriggerMode.Press)), ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
        {
            return WebRemoteResponse.Error(400, "'mode' must be press, toggle, hold or restart.");
        }

        // "press" is a full key stroke (down then up), the natural meaning of a button on a Stream Deck.
        TriggerPhase[]? phases = query.GetValueOrDefault("phase", "press").ToLowerInvariant() switch
        {
            "press" => [TriggerPhase.Down, TriggerPhase.Up],
            "down" => [TriggerPhase.Down],
            "up" => [TriggerPhase.Up],
            _ => null,
        };
        if (phases is null)
        {
            return WebRemoteResponse.Error(400, "'phase' must be press, down or up.");
        }

        foreach (var phase in phases)
        {
            if (dispatcher.Dispatch(action, target, phase, mode) == DispatchResult.UnknownAction)
            {
                return WebRemoteResponse.Error(404, $"Unknown action '{action}'.");
            }
        }

        return new WebRemoteResponse(200, WebRemoteResponse.Json, """{"result":"ok"}""");
    }
}

/// <summary>The tiny self-contained page served at <c>/</c>.</summary>
internal static class WebRemotePage
{
    public static string Render(AppSettings settings, string token)
    {
        var buttons = new StringBuilder();
        foreach (var sound in settings.Sounds)
        {
            buttons.Append(CultureInvariant($"<button style=\"border-top:6px solid {Color(sound.Color)}\" onclick=\"go('PlaySound','{sound.Id}')\">{WebUtility.HtmlEncode(sound.Name)}</button>"));
        }

        var tokenJs = JsonSerializer.Serialize(token);
        return $$"""
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Soundboard</title>
            <style>
              body{margin:0;padding:16px;background:#1b1b1f;color:#e8e8ec;font:15px system-ui,sans-serif}
              h1{font-size:18px;margin:0 0 12px}
              #grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));gap:10px}
              button{min-height:72px;padding:10px;border:0;border-radius:10px;background:#2c2c34;color:inherit;font:inherit;font-weight:600;cursor:pointer}
              button:active{background:#383842}
              #stop{margin-top:14px;width:100%;min-height:48px;background:#4a2a2a}
            </style></head><body>
            <h1>Soundboard</h1>
            <div id="grid">{{buttons}}</div>
            <button id="stop" onclick="go('StopAll')">■ Dừng tất cả</button>
            <script>
              const token = {{tokenJs}};
              function go(action, target) {
                const q = new URLSearchParams({ token, action });
                if (target) q.set('target', target);
                fetch('/api/trigger?' + q, { method: 'POST' });
              }
            </script>
            </body></html>
            """;
    }

    private static string CultureInvariant(FormattableString text) => FormattableString.Invariant(text);

    // Colours come from settings.json; only a plain #RRGGBB value may reach the style attribute.
    private static string Color(string color) =>
        color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit) ? color : "#888888";
}
