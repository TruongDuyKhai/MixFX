namespace Soundboard.Core.Actions;

/// <summary>
/// Outcome of a dispatch. Returned rather than logged because Core has no logging dependency;
/// the caller decides how to report it.
/// </summary>
public enum DispatchResult
{
    Executed,

    /// <summary>No action is registered under that id, typically a binding saved by a newer or older version.</summary>
    UnknownAction,
}
