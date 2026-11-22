namespace Soundboard.Core.Models;

/// <summary>
/// One entry of a sound's effect chain. Parameters are strings so new effects can be added
/// without changing the settings schema.
/// </summary>
public sealed record EffectConfig(string EffectId, IReadOnlyDictionary<string, string> Params);
