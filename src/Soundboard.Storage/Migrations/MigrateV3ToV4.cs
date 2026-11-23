using System.Text.Json;
using System.Text.Json.Nodes;
using Soundboard.Core.Models;

namespace Soundboard.Storage.Migrations;

/// <summary>
/// v3 → v4: adds <c>window.closeAction</c> = "Ask". Every v3 value is kept untouched; the key is written out
/// explicitly so a v4 file shows the user the setting exists.
/// </summary>
internal sealed class MigrateV3ToV4 : ISettingsMigration
{
    private const string WindowKey = "window";
    private const string CloseActionKey = "closeAction";

    public int FromVersion => 3;

    public void Apply(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        JsonObject window;
        if (!root.TryGetPropertyValue(WindowKey, out var node))
        {
            window = [];
            root[WindowKey] = window;
        }
        else
        {
            // Present but null or not an object means the file is damaged: a migration only adds what is
            // missing, it must never "repair" damage and hide it from the store's recovery path.
            window = node as JsonObject ?? throw new JsonException($"'{WindowKey}' must be a JSON object.");
        }

        if (!window.ContainsKey(CloseActionKey))
        {
            window[CloseActionKey] = nameof(CloseAction.Ask);
        }
    }
}
