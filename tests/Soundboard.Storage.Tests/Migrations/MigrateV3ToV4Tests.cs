using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.Core.Models;
using Soundboard.Storage.Migrations;

namespace Soundboard.Storage.Tests.Migrations;

public sealed class MigrateV3ToV4Tests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Apply_AddsCloseActionAsk()
    {
        var root = V3Fixture();

        new MigrateV3ToV4().Apply(root);

        root["window"]!["closeAction"]!.GetValue<string>().Should().Be("Ask");
    }

    [Fact]
    public void Apply_KeepsEveryV3Value()
    {
        var root = V3Fixture();
        var before = root.DeepClone().AsObject();

        new MigrateV3ToV4().Apply(root);

        foreach (var (name, value) in before)
        {
            JsonNode.DeepEquals(root[name], value).Should().BeTrue($"{name} must survive the migration");
        }
    }

    [Fact]
    public void Apply_DoesNotOverwriteAnAnswerAlreadyPresent()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 3, "window": { "closeAction": "Exit" } }""")!.AsObject();

        new MigrateV3ToV4().Apply(root);

        root["window"]!["closeAction"]!.GetValue<string>().Should().Be("Exit");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 3, "window": null }""")]
    [InlineData("""{ "schemaVersion": 3, "window": "tray" }""")]
    public void Apply_DamagedWindow_ThrowsJsonExceptionSoTheFileIsTreatedAsUnreadable(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();

        var act = () => new MigrateV3ToV4().Apply(root);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task Store_LoadsV3FixtureAsV4_AskingOnClose()
    {
        var paths = new StoragePaths(_dir.Path);
        Directory.CreateDirectory(_dir.Path);
        File.Copy(FixturePath, paths.SettingsFile);
        using var store = new JsonSettingsStore(paths, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(loaded.Settings, TestContext.Current.CancellationToken);

        loaded.Status.Should().Be(SettingsLoadStatus.Loaded);
        var s = loaded.Settings;
        s.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        s.Window.CloseAction.Should().Be(CloseAction.Ask);
        s.Features.Should().Be(new FeatureFlags { Ducking = true, Playlist = true, YouTubeImport = true });
        s.Sounds.Should().ContainSingle().Which.Name.Should().Be("Airhorn");
        s.WebRemote.Port.Should().Be(9000);

        var saved = JsonNode.Parse(File.ReadAllText(paths.SettingsFile))!;
        saved["window"]!["closeAction"]!.GetValue<string>().Should().Be("Ask");
        File.ReadAllText(paths.SettingsBackupFile).Should().Contain("\"schemaVersion\": 3", "the v3 file is kept as the backup");
    }

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "settings.v3.json");

    private static JsonObject V3Fixture() => JsonNode.Parse(File.ReadAllText(FixturePath))!.AsObject();
}
