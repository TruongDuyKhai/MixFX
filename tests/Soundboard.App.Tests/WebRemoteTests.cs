using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.WebRemote;
using Soundboard.Core.Actions;
using Soundboard.Core.Models;

namespace Soundboard.App.Tests;

public sealed class WebRemoteTests
{
    private const string Token = "secret-token";

    private static readonly SoundItem Horn = new(Guid.NewGuid(), "<b>Horn</b>", "horn.wav", "#FF7043", 1f, PlaybackKind.Preloaded, EditParams.None);

    private readonly RecordingDispatcher _dispatcher = new(ActionIds.PlaySound, ActionIds.StopAll, ActionIds.PttHold);
    private readonly WebRemoteRequestHandler _handler;

    public WebRemoteTests()
    {
        _handler = new WebRemoteRequestHandler(_dispatcher, new FixedSettings(new AppSettings { Sounds = [Horn] }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    [InlineData("secret-toke")]
    public void MissingOrWrongToken_Is401(string? token)
    {
        Handle("POST", "/api/trigger", Query(("action", "StopAll"), ("token", token))).StatusCode.Should().Be(401);
        _dispatcher.Calls.Should().BeEmpty();
    }

    [Fact]
    public void TokenInHeader_IsAccepted()
    {
        var response = _handler.Handle(new WebRemoteRequest("POST", "/api/trigger", Query(("action", "StopAll")), "localhost:8765", Token), Token);

        response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("evil.example.com:8765")]
    [InlineData("192.168.1.5:8765")]
    public void ForeignHost_Is403_EvenWithTheToken(string host)
    {
        var response = _handler.Handle(new WebRemoteRequest("POST", "/api/trigger", Query(("action", "StopAll"), ("token", Token)), host, null), Token);

        response.StatusCode.Should().Be(403);
        _dispatcher.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Page_ListsSoundsWithEscapedNames()
    {
        var response = Handle("GET", "/", Query(("token", Token)));

        response.StatusCode.Should().Be(200);
        response.ContentType.Should().StartWith("text/html");
        response.Body.Should().Contain("&lt;b&gt;Horn&lt;/b&gt;").And.NotContain("<b>Horn</b>");
        response.Body.Should().Contain(Horn.Id.ToString());
    }

    [Fact]
    public void SoundsApi_ReturnsJson()
    {
        var response = Handle("GET", "/api/sounds", Query(("token", Token)));

        var sounds = JsonDocument.Parse(response.Body).RootElement;
        sounds.GetArrayLength().Should().Be(1);
        sounds[0].GetProperty("name").GetString().Should().Be("<b>Horn</b>");
    }

    [Fact]
    public void TriggerPress_DispatchesDownThenUp()
    {
        Handle("POST", "/api/trigger", Query(("token", Token), ("action", "PlaySound"), ("target", Horn.Id.ToString()))).StatusCode.Should().Be(200);

        _dispatcher.Calls.Should().Equal(
            (ActionIds.PlaySound, (Guid?)Horn.Id, TriggerPhase.Down, TriggerMode.Press),
            (ActionIds.PlaySound, (Guid?)Horn.Id, TriggerPhase.Up, TriggerMode.Press));
    }

    [Fact]
    public void TriggerWithPhaseAndMode_DispatchesExactlyThat()
    {
        Handle("POST", "/api/trigger", Query(("token", Token), ("action", "PttHold"), ("phase", "down"), ("mode", "hold")));

        _dispatcher.Calls.Should().Equal((ActionIds.PttHold, (Guid?)null, TriggerPhase.Down, TriggerMode.Hold));
    }

    [Theory]
    [InlineData("target", "not-a-guid", 400)]
    [InlineData("phase", "sideways", 400)]
    [InlineData("mode", "loud", 400)]
    [InlineData("mode", "42", 400)]
    public void BadParameters_Are400(string name, string value, int expected)
    {
        Handle("POST", "/api/trigger", Query(("token", Token), ("action", "PlaySound"), (name, value))).StatusCode.Should().Be(expected);
    }

    [Fact]
    public void UnknownActionOrEndpoint_Is404()
    {
        Handle("POST", "/api/trigger", Query(("token", Token), ("action", "FormatHardDrive"))).StatusCode.Should().Be(404);
        Handle("GET", "/api/trigger", Query(("token", Token), ("action", "StopAll"))).StatusCode.Should().Be(404);
        Handle("GET", "/admin", Query(("token", Token))).StatusCode.Should().Be(404);
    }

    [Fact]
    public void Token_IsUrlSafeAndUnique()
    {
        var a = WebRemoteToken.Generate();
        var b = WebRemoteToken.Generate();

        a.Should().NotBe(b).And.HaveLength(32).And.MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public async Task Server_ServesOverRealHttpAndStopsWhenDisabled()
    {
        var port = FreePort();
        var enabled = new AppSettings
        {
            Sounds = [Horn],
            Features = new FeatureFlags { WebRemote = true },
            WebRemote = new WebRemoteSettings { Port = port, Token = Token },
        };
        using var server = new WebRemoteServer(_handler, NullLogger<WebRemoteServer>.Instance);
        using var http = new HttpClient();
        var ct = TestContext.Current.CancellationToken;

        server.Apply(enabled);
        var ok = await http.PostAsync($"http://localhost:{port}/api/trigger?token={Token}&action=StopAll", null, ct);
        var denied = await http.PostAsync($"http://localhost:{port}/api/trigger?token=nope&action=StopAll", null, ct);
        server.Apply(enabled with { Features = new FeatureFlags() });
        var afterStop = () => http.GetAsync($"http://localhost:{port}/?token={Token}", ct);

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _dispatcher.Calls.Should().HaveCount(2, "StopAll pressed once: down and up");
        server.IsRunning.Should().BeFalse();
        await afterStop.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void Server_WithoutToken_DoesNotStart()
    {
        using var server = new WebRemoteServer(_handler, NullLogger<WebRemoteServer>.Instance);

        server.Apply(new AppSettings { Features = new FeatureFlags { WebRemote = true } });

        server.IsRunning.Should().BeFalse();
    }

    private WebRemoteResponse Handle(string method, string path, IReadOnlyDictionary<string, string> query) =>
        _handler.Handle(new WebRemoteRequest(method, path, query, "localhost:8765", null), Token);

    private static Dictionary<string, string> Query(params (string Name, string? Value)[] pairs) =>
        pairs.Where(p => p.Value is not null).ToDictionary(p => p.Name, p => p.Value!, StringComparer.OrdinalIgnoreCase);

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
