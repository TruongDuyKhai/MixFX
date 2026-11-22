namespace Soundboard.Input.Hooks;

/// <summary>
/// Drops the second delivery of the same input event. While the hooks are being re-armed, the new
/// and the old hook are briefly both installed; an event arriving then passes through our callback
/// twice (new hook → CallNextHookEx → old hook) and would otherwise fire its binding twice.
/// Real events never repeat exactly: auto-repeat and fresh presses carry a new timestamp.
/// </summary>
internal struct DuplicateEventFilter
{
    private int _lastMessage;
    private uint _lastCode;
    private uint _lastTime;
    private bool _hasLast;

    /// <summary>Returns true when this exact event was just seen; otherwise remembers it.</summary>
    public bool IsDuplicate(int message, uint code, uint time)
    {
        if (_hasLast && message == _lastMessage && code == _lastCode && time == _lastTime)
        {
            return true;
        }

        _lastMessage = message;
        _lastCode = code;
        _lastTime = time;
        _hasLast = true;
        return false;
    }
}
