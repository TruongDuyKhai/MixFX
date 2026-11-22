using System.Globalization;

namespace Soundboard.Core.Models;

/// <summary>Effect ids and parameter names stored in <see cref="EffectConfig"/>. Persisted: never rename.</summary>
public static class EffectCatalog
{
    public const string Pitch = "pitch";
    public const string PitchSemitones = "semitones";

    public const string Reverb = "reverb";
    public const string ReverbRoom = "room";
    public const string ReverbWet = "wet";

    public const float MaxPitchSemitones = 12f;

    public static EffectConfig PitchConfig(float semitones) =>
        new(Pitch, new Dictionary<string, string> { [PitchSemitones] = Format(semitones) });

    public static EffectConfig ReverbConfig(float room, float wet) =>
        new(Reverb, new Dictionary<string, string> { [ReverbRoom] = Format(room), [ReverbWet] = Format(wet) });

    /// <summary>
    /// Reads a numeric parameter. Values are stored invariant-culture; a missing or malformed value
    /// (hand-edited settings) falls back instead of breaking playback.
    /// </summary>
    public static float GetFloat(this EffectConfig effect, string name, float fallback)
    {
        ArgumentNullException.ThrowIfNull(effect);

        return effect.Params.TryGetValue(name, out var text)
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && float.IsFinite(value)
                ? value
                : fallback;
    }

    /// <summary>The first effect with <paramref name="effectId"/>, or null.</summary>
    public static EffectConfig? Find(this IReadOnlyList<EffectConfig> effects, string effectId)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return effects.FirstOrDefault(e => e.EffectId == effectId);
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
