using System.Text.Json;
using System.Text.Json.Nodes;
using Soundboard.Core.Models;

namespace Soundboard.Storage.Migrations;

/// <summary>
/// v1 → v2 (P6): adds the feature flags (all off), ducking depth, voice changer pitch, an empty
/// playlist and web remote settings. Every v1 value is kept untouched; only missing keys are added,
/// written out explicitly so a v2 file always shows the user what can be configured.
/// </summary>
internal sealed class MigrateV1ToV2 : ISettingsMigration
{
    public int FromVersion => 1;

    public void Apply(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var audio = ObjectOrCreate(root, "audio");
        AddIfMissing(audio, "duckingDb", JsonValue.Create(AudioSettings.DefaultDuckingDb));
        AddIfMissing(audio, "voicePitchSemitones", JsonValue.Create(0f));

        var features = ObjectOrCreate(root, "features");
        foreach (var flag in new[] { "ducking", "soundEffects", "voiceChanger", "playlist", "webRemote" })
        {
            AddIfMissing(features, flag, JsonValue.Create(false));
        }

        AddIfMissing(root, "playlist", new JsonArray());

        var webRemote = ObjectOrCreate(root, "webRemote");
        AddIfMissing(webRemote, "port", JsonValue.Create(WebRemoteSettings.DefaultPort));
        AddIfMissing(webRemote, "token", null);
    }

    private static void AddIfMissing(JsonObject target, string name, JsonNode? value)
    {
        if (!target.ContainsKey(name))
        {
            target[name] = value;
        }
    }

    private static JsonObject ObjectOrCreate(JsonObject root, string name)
    {
        if (!root.TryGetPropertyValue(name, out var node))
        {
            var created = new JsonObject();
            root[name] = created;
            return created;
        }

        // Present but null or not an object means the file is damaged: a migration only adds what is
        // missing, it must never "repair" damage and hide it from the store's recovery path.
        return node as JsonObject ?? throw new JsonException($"'{name}' must be a JSON object.");
    }
}
