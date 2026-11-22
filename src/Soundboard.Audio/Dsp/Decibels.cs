namespace Soundboard.Audio.Dsp;

internal static class Decibels
{
    public static float ToGain(float decibels) => MathF.Pow(10f, decibels / 20f);

    public static float FromGain(float gain) => 20f * MathF.Log10(gain);
}
