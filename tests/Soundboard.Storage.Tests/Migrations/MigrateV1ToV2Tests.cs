using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.Core.Models;
using Soundboard.Storage.Migrations;

namespace Soundboard.Storage.Tests.Migrations;

public sealed class MigrateV1ToV2Tests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Apply_AddsV2KeysWithFeaturesOff()
    {
        var root = V1Fixture();

        new MigrateV1ToV2().Apply(root);

        root["features"]!.AsObject().Select(p => p.Value!.GetValue<bool>()).Should().HaveCount(5).And.OnlyContain(on => !on);
        root["audio"]!["duckingDb"]!.GetValue<float>().Should().Be(AudioSettings.DefaultDuckingDb);
        root["audio"]!["voicePitchSemitones"]!.GetValue<float>().Should().Be(0f);
        root["playlist"]!.AsArray().Should().BeEmpty();
        root["webRemote"]!["port"]!.GetValue<int>().Should().Be(WebRemoteSettings.DefaultPort);
        root["webRemote"]!["token"].Should().BeNull();
    }

    [Fact]
    public void Apply_KeepsEveryV1Value()
    {
        var root = V1Fixture();
        var before = root.DeepClone().AsObject();

        new MigrateV1ToV2().Apply(root);

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
    public void Apply_DoesNotOverwriteKeysAlreadyPresent()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 1, "audio": { "duckingDb": -6 }, "playlist": ["00000000-0000-0000-0000-000000000001"] }""")!.AsObject();

        new MigrateV1ToV2().Apply(root);

        root["audio"]!["duckingDb"]!.GetValue<float>().Should().Be(-6f);
        root["playlist"]!.AsArray().Should().HaveCount(1);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "features": "oops" }""")]
    [InlineData("""{ "schemaVersion": 1, "audio": null }""")]
    public void Apply_DamagedShape_ThrowsJsonExceptionSoTheFileIsTreatedAsUnreadable(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();

        var act = () => new MigrateV1ToV2().Apply(root);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task Store_LoadsV1Fixture_AndSavesItAtTheCurrentVersion()
    {
        var paths = new StoragePaths(_dir.Path);
        Directory.CreateDirectory(_dir.Path);
        File.Copy(FixturePath, paths.SettingsFile);
        using var store = new JsonSettingsStore(paths, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(loaded.Settings, TestContext.Current.CancellationToken);

        loaded.Status.Should().Be(SettingsLoadStatus.Loaded);
        // The whole chain runs: v1 → v2 here, then every later migration.
        loaded.Settings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        loaded.Settings.Features.Should().Be(new FeatureFlags());
        loaded.Settings.Sounds.Should().ContainSingle().Which.Name.Should().Be("Airhorn");
        JsonNode.Parse(File.ReadAllText(paths.SettingsFile))!["schemaVersion"]!.GetValue<int>().Should().Be(AppSettings.CurrentSchemaVersion);
        File.ReadAllText(paths.SettingsBackupFile).Should().Contain("\"schemaVersion\": 1", "the v1 file is kept as the backup");
    }

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "settings.v1.json");

    private static JsonObject V1Fixture() => JsonNode.Parse(File.ReadAllText(FixturePath))!.AsObject();
}
