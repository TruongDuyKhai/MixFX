namespace Soundboard.Audio.Tests;

/// <summary>Measures what an audio callback allocates once warmed up (PROJECT_PROMPT.md §7.3.8).</summary>
internal static class Allocations
{
    private const int Rounds = 5;
    private const int CallsPerRound = 100;

    /// <summary>
    /// Bytes allocated on this thread by <see cref="CallsPerRound"/> calls of <paramref name="call"/>, in the
    /// quietest of up to <see cref="Rounds"/> rounds (it stops at the first round with none).
    /// </summary>
    /// <remarks>
    /// A real allocation in a callback happens on every call, so it shows up in every round and the result
    /// stays above zero. GitHub's CI runners occasionally charge a one-off kilobyte or two to the test thread
    /// in the middle of a pure arithmetic loop (never reproduced locally, even under load); a single round
    /// absorbs that, and the next round measures zero.
    /// </remarks>
    public static long InSteadyState(Action call)
    {
        ArgumentNullException.ThrowIfNull(call);

        // Warm-up: compiles every method on the path and touches every buffer once.
        call();

        var least = long.MaxValue;
        for (var round = 0; round < Rounds && least > 0; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < CallsPerRound; i++)
            {
                call();
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return least;
    }
}
