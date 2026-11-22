using Soundboard.Core.Models;

namespace Soundboard.Input;

/// <summary>A binding that fired, queued from the hook thread to the dispatch thread.</summary>
internal readonly record struct MatchedTrigger(Binding Binding, TriggerPhase Phase);
