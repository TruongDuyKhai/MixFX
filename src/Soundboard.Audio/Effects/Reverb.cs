using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Effects;

/// <summary>
/// Small Schroeder/Freeverb-style room: four damped comb filters and two all-passes per channel
/// (the right channel's delays are slightly longer for stereo width). All buffers are allocated up
/// front, so <see cref="Process"/> is allocation-free.
/// </summary>
internal sealed class Reverb : IEffect
{
    // Freeverb tunings at 44.1 kHz, rescaled to the engine rate.
    private static readonly int[] CombTunings = [1116, 1188, 1277, 1356];
    private static readonly int[] AllPassTunings = [556, 441];
    private const int StereoSpread = 23;
    private const double TuningRate = 44_100;

    private const float InputGain = 0.03f;
    private const float Damping = 0.2f;
    private const float AllPassFeedback = 0.5f;
    private const float RoomScale = 0.28f;
    private const float RoomOffset = 0.7f;
    private const float WetScale = 3f;

    private readonly Comb[] _leftCombs;
    private readonly Comb[] _rightCombs;
    private readonly AllPass[] _leftAllPasses;
    private readonly AllPass[] _rightAllPasses;
    private readonly float _feedback;
    private readonly float _wet;

    /// <param name="room">0 (small) … 1 (large).</param>
    /// <param name="wet">0 (dry) … 1 (very reverberant).</param>
    public Reverb(float room, float wet)
    {
        _feedback = (Math.Clamp(room, 0f, 1f) * RoomScale) + RoomOffset;
        _wet = Math.Clamp(wet, 0f, 1f);
        _leftCombs = CombTunings.Select(t => new Comb(Scale(t))).ToArray();
        _rightCombs = CombTunings.Select(t => new Comb(Scale(t + StereoSpread))).ToArray();
        _leftAllPasses = AllPassTunings.Select(t => new AllPass(Scale(t))).ToArray();
        _rightAllPasses = AllPassTunings.Select(t => new AllPass(Scale(t + StereoSpread))).ToArray();
    }

    public string Id => EffectCatalog.Reverb;

    public void Process(Span<float> interleavedStereo)
    {
        if (_wet <= 0f)
        {
            return;
        }

        for (var i = 0; i + 1 < interleavedStereo.Length; i += 2)
        {
            var dryLeft = interleavedStereo[i];
            var dryRight = interleavedStereo[i + 1];
            var input = (dryLeft + dryRight) * InputGain;

            var left = 0f;
            var right = 0f;
            for (var c = 0; c < _leftCombs.Length; c++)
            {
                left += _leftCombs[c].Process(input, _feedback);
                right += _rightCombs[c].Process(input, _feedback);
            }

            for (var a = 0; a < _leftAllPasses.Length; a++)
            {
                left = _leftAllPasses[a].Process(left);
                right = _rightAllPasses[a].Process(right);
            }

            interleavedStereo[i] = dryLeft + (left * _wet * WetScale);
            interleavedStereo[i + 1] = dryRight + (right * _wet * WetScale);
        }
    }

    public void Reset()
    {
        foreach (var comb in _leftCombs.Concat(_rightCombs))
        {
            comb.Reset();
        }

        foreach (var allPass in _leftAllPasses.Concat(_rightAllPasses))
        {
            allPass.Reset();
        }
    }

    private static int Scale(int tuning) => (int)Math.Round(tuning * EngineFormat.SampleRate / TuningRate);

    private sealed class Comb(int length)
    {
        private readonly float[] _buffer = new float[length];
        private int _index;
        private float _filter;

        public float Process(float input, float feedback)
        {
            var output = _buffer[_index];
            _filter = (output * (1 - Damping)) + (_filter * Damping);
            _buffer[_index] = input + (_filter * feedback);
            _index = (_index + 1) % _buffer.Length;
            return output;
        }

        public void Reset()
        {
            Array.Clear(_buffer);
            _filter = 0;
            _index = 0;
        }
    }

    private sealed class AllPass(int length)
    {
        private readonly float[] _buffer = new float[length];
        private int _index;

        public float Process(float input)
        {
            var buffered = _buffer[_index];
            _buffer[_index] = input + (buffered * AllPassFeedback);
            _index = (_index + 1) % _buffer.Length;
            return buffered - input;
        }

        public void Reset()
        {
            Array.Clear(_buffer);
            _index = 0;
        }
    }
}
