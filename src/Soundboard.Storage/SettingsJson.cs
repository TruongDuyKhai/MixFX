using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soundboard.Storage;

/// <summary>JSON format of settings.json, shared by the store and the migrations.</summary>
internal static class SettingsJson
{
    public const string SchemaVersionProperty = "schemaVersion";

    // Lenient parsing because users may hand-edit the file in Notepad.
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,

            // Reject null or missing values for non-nullable members here, so a damaged file is
            // detected at load time (and recovered from backup) instead of crashing later.
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,

            // Enum names instead of numbers keep the file readable and stable if enums are reordered.
            Converters = { new JsonStringEnumConverter() },
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
