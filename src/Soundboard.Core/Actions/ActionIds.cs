namespace Soundboard.Core.Actions;

/// <summary>
/// Ids of the built-in actions (PROJECT_PROMPT.md §5.5). They are persisted in users' bindings,
/// so the string values must never change.
/// </summary>
public static class ActionIds
{
    public const string PlaySound = "PlaySound";
    public const string StopAll = "StopAll";
    public const string ToggleMic = "ToggleMic";
    public const string PttHold = "PttHold";
    public const string VolumeUp = "VolumeUp";
    public const string VolumeDown = "VolumeDown";
}
