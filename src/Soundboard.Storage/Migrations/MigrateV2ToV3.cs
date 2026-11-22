using System.Text.Json;
using System.Text.Json.Nodes;

namespace Soundboard.Storage.Migrations;

/// <summary>
/// v2 → v3: adds the <c>youTubeImport</c> feature flag, off. Every v2 value is kept untouched; the key is
/// written out explicitly so a v3 file shows the user every switch that exists.
/// </summary>
internal sealed class MigrateV2ToV3 : ISettingsMigration
{
    private const string FeaturesKey = "features";
    private const string YouTubeImportKey = "youTubeImport";

    public int FromVersion => 2;

    public void Apply(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        JsonObject features;
        if (!root.TryGetPropertyValue(FeaturesKey, out var node))
        {
            features = [];
            root[FeaturesKey] = features;
        }
        else
        {
            // Present but null or not an object means the file is damaged: a migration only adds what is
            // missing, it must never "repair" damage and hide it from the store's recovery path.
            features = node as JsonObject ?? throw new JsonException($"'{FeaturesKey}' must be a JSON object.");
        }

        if (!features.ContainsKey(YouTubeImportKey))
        {
            features[YouTubeImportKey] = false;
        }
    }
}
