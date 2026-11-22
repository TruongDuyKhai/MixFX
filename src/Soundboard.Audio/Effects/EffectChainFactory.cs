using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Effects;

/// <summary>Builds a fresh effect chain (one per voice: effects keep state) from <see cref="EditParams.Effects"/>.</summary>
internal static class EffectChainFactory
{
    private const float DefaultRoom = 0.5f;
    private const float DefaultWet = 0.3f;

    /// <summary>
    /// Unknown ids (from a newer version or hand edits) and stages that would change nothing are
    /// skipped, so an empty array means "no processing at all".
    /// </summary>
    public static IEffect[] Create(IReadOnlyList<EffectConfig> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        var chain = new List<IEffect>(configs.Count);
        foreach (var config in configs)
        {
            switch (config.EffectId)
            {
                case EffectCatalog.Pitch:
                    var semitones = config.GetFloat(EffectCatalog.PitchSemitones, 0f);
                    if (semitones != 0f)
                    {
                        chain.Add(new PitchShifter(semitones));
                    }

                    break;

                case EffectCatalog.Reverb:
                    var wet = config.GetFloat(EffectCatalog.ReverbWet, DefaultWet);
                    if (wet > 0f)
                    {
                        chain.Add(new Reverb(config.GetFloat(EffectCatalog.ReverbRoom, DefaultRoom), wet));
                    }

                    break;
            }
        }

        return [.. chain];
    }
}
