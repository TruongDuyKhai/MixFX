using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Decoding;

/// <summary>Identifies one decoded, trimmed region of a library file.</summary>
internal readonly record struct SoundCacheKey(string FilePath, int TrimStartMs, int? TrimEndMs)
{
    public static SoundCacheKey For(string filePath, EditParams edit) => new(filePath, edit.TrimStartMs, edit.TrimEndMs);
}

/// <summary>Short sounds decoded into RAM (§5.1). Thread-safe: filled by preloading and by the engine thread.</summary>
internal sealed class SoundCache
{
    private readonly ConcurrentDictionary<SoundCacheKey, CachedSound> _entries = new();

    public int Count => _entries.Count;

    public bool TryGet(SoundCacheKey key, [NotNullWhen(true)] out CachedSound? sound) => _entries.TryGetValue(key, out sound);

    public void Set(SoundCacheKey key, CachedSound sound) => _entries[key] = sound;

    /// <summary>Drops entries for sounds that were deleted or re-trimmed. Voices still playing keep their own reference.</summary>
    public void RetainOnly(IReadOnlySet<SoundCacheKey> keys)
    {
        foreach (var key in _entries.Keys)
        {
            if (!keys.Contains(key))
            {
                _entries.TryRemove(key, out _);
            }
        }
    }
}
