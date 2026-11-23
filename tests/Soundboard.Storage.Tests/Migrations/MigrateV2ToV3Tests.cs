using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.Core.Models;
using Soundboard.Storage.Migrations;

namespace Soundboard.Storage.Tests.Migrations;

public sealed class MigrateV2ToV3Tests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Apply_AddsYouTubeImportFlagOff()
    {
        var root = V2Fixture();

        new MigrateV2ToV3().Apply(root);

        root["features"]!["youTubeImport"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void Apply_KeepsEveryV2Value()
    {
        var root = V2Fixture();
        var before = root.DeepClone().AsObject();

        new MigrateV2ToV3().Apply(root);

        foreach (var (name, value) in before)
        {
            if (value is JsonObject obj)
            {
                foreach (var (inner, innerValue) in obj)
                {
                    JsonNode.DeepEquals(root[name]![inner], innerValue).Should().BeTrue($"{name}.{inner} must survive the migration");
                }
            }
            else
            {
                JsonNode.DeepEquals(root[name], value).Should().BeTrue($"{name} must survive the migration");
            }
        }
    }

    [Fact]
    public void Apply_DoesNotOverwriteAFlagAlreadyPresent()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 2, "features": { "youTubeImport": true } }""")!.AsObject();

        new MigrateV2ToV3().Apply(root);

        root["features"]!["youTubeImport"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Apply_MissingFeatures_AddsThemWithTheFlagOff()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 2 }""")!.AsObject();

        new MigrateV2ToV3().Apply(root);

        root["features"]!["youTubeImport"]!.GetValue<bool>().Should().BeFalse();
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "features": null }""")]
    [InlineData("""{ "schemaVersion": 2, "features": [true] }""")]
    public void Apply_DamagedFeatures_ThrowsJsonExceptionSoTheFileIsTreatedAsUnreadable(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();

        var act = () => new MigrateV2ToV3().Apply(root);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task Store_LoadsV2Fixture_AtTheCurrentVersion_KeepingEveryP6Setting()
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
        s.Features.Should().Be(new FeatureFlags { Ducking = true, SoundEffects = true, Playlist = true });
        s.Audio.DuckingDb.Should().Be(-9f);
        s.Audio.VoicePitchSemitones.Should().Be(3f);
        s.Playlist.Should().ContainSingle();
        s.WebRemote.Port.Should().Be(9000);
        s.WebRemote.Token.Should().Be("fixture-token-not-a-secret");
        s.Sounds.Should().ContainSingle().Which.Edit.Effects.Should().ContainSingle().Which.EffectId.Should().Be("reverb");
        s.Bindings.Should().HaveCount(2);

        var saved = JsonNode.Parse(File.ReadAllText(paths.SettingsFile))!;
        saved["schemaVersion"]!.GetValue<int>().Should().Be(AppSettings.CurrentSchemaVersion);
        saved["features"]!["youTubeImport"]!.GetValue<bool>().Should().BeFalse();
        File.ReadAllText(paths.SettingsBackupFile).Should().Contain("\"schemaVersion\": 2", "the v2 file is kept as the backup");
    }

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "settings.v2.json");

    private static JsonObject V2Fixture() => JsonNode.Parse(File.ReadAllText(FixturePath))!.AsObject();
}
