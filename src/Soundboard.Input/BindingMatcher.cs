using System.Collections.Frozen;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.Input;

/// <summary>
/// Decides, for every raw key/button event, which bindings fire and whether the event is hidden
/// from the game. Pure logic so it can be tested without hooks. <see cref="Process"/> runs on the
/// hook thread and must stay fast (§7.3.9): dictionary lookups only, no I/O, no locks.
/// </summary>
internal sealed class BindingMatcher
{
    private static readonly Binding[] NoBindings = [];

    private static readonly (Modifiers Flag, int[] Keys)[] ModifierKeys =
    [
        (Modifiers.Ctrl, [VirtualKeys.LeftControl, VirtualKeys.RightControl, VirtualKeys.Control]),
        (Modifiers.Alt, [VirtualKeys.LeftAlt, VirtualKeys.RightAlt, VirtualKeys.Alt]),
        (Modifiers.Shift, [VirtualKeys.LeftShift, VirtualKeys.RightShift, VirtualKeys.Shift]),
        (Modifiers.Win, [VirtualKeys.LeftWin, VirtualKeys.RightWin]),
    ];

    private readonly Func<int, bool> _isKeyPhysicallyDown;

    // Hook thread only.
    private readonly Dictionary<(InputDevice, int), Binding[]> _active = [];
    private readonly HashSet<(InputDevice, int)> _swallowed = [];
    private readonly HashSet<int> _heldModifierKeys = [];

    // Swapped from other threads.
    private FrozenDictionary<(InputDevice, int), Binding[]> _bindings = FrozenDictionary<(InputDevice, int), Binding[]>.Empty;
    private TaskCompletionSource<Trigger?>? _capture;

    /// <param name="isKeyPhysicallyDown">
    /// Asks Windows whether a key is down (state before the current event). Used to recover when a
    /// key-up was never delivered, e.g. after Win+L or a UAC prompt, which would otherwise leave a
    /// modifier "stuck" and break every binding.
    /// </param>
    public BindingMatcher(Func<int, bool> isKeyPhysicallyDown)
    {
        ArgumentNullException.ThrowIfNull(isKeyPhysicallyDown);
        _isKeyPhysicallyDown = isKeyPhysicallyDown;
    }

    /// <summary>True when the mouse hook is needed; it is only installed then, to keep mouse moves untouched otherwise.</summary>
    public bool NeedsMouse { get; private set; }

    public bool IsCapturing => Volatile.Read(ref _capture) is not null;

    public void SetBindings(IReadOnlyList<Binding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var map = bindings
            .GroupBy(b => (b.Trigger.Device, b.Trigger.Code))
            .ToFrozenDictionary(g => g.Key, g => g.ToArray());
        NeedsMouse = bindings.Any(b => b.Trigger.Device == InputDevice.Mouse);
        Volatile.Write(ref _bindings, map);
    }

    public void BeginCapture(TaskCompletionSource<Trigger?> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var previous = Interlocked.Exchange(ref _capture, capture);
        previous?.TrySetResult(null);
    }

    public void CancelCapture(TaskCompletionSource<Trigger?> capture)
    {
        if (Interlocked.CompareExchange(ref _capture, null, capture) == capture)
        {
            capture.TrySetResult(null);
        }
    }

    /// <summary>Hook thread: appends fired bindings to <paramref name="fired"/> and returns true to hide the event from other apps.</summary>
    public bool Process(RawInputEvent input, List<MatchedTrigger> fired)
    {
        var key = (input.Device, input.Code);
        var modifier = input.Device == InputDevice.Keyboard ? VirtualKeys.ModifierOf(input.Code) : Modifiers.None;

        if (!input.IsDown)
        {
            if (modifier != Modifiers.None)
            {
                _heldModifierKeys.Remove(input.Code);
            }

            if (_swallowed.Remove(key))
            {
                return true;
            }

            if (!_active.Remove(key, out var released))
            {
                return false;
            }

            foreach (var binding in released)
            {
                fired.Add(new MatchedTrigger(binding, TriggerPhase.Up));
            }

            return Suppresses(released);
        }

        if (IsAutoRepeat(input, key, out var repeating))
        {
            return repeating;
        }

        var capture = Volatile.Read(ref _capture);
        if (capture is not null && TryCapture(input, key, modifier, capture))
        {
            return true;
        }

        DropStaleModifiers();
        var held = HeldModifiers() & ~modifier;
        if (modifier != Modifiers.None)
        {
            _heldModifierKeys.Add(input.Code);
        }

        if (capture is not null)
        {
            // While the user assigns a key, nothing else fires.
            return false;
        }

        var matched = Match(key, held);
        _active[key] = matched;
        foreach (var binding in matched)
        {
            fired.Add(new MatchedTrigger(binding, TriggerPhase.Down));
        }

        return Suppresses(matched);
    }

    private static bool Suppresses(Binding[] bindings)
    {
        foreach (var binding in bindings)
        {
            if (!binding.PassThrough)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Windows repeats key-down while a key is held; only the first one may fire.</summary>
    private bool IsAutoRepeat(RawInputEvent input, (InputDevice, int) key, out bool suppress)
    {
        suppress = false;
        if (_swallowed.Contains(key))
        {
            suppress = true;
            return true;
        }

        if (!_active.TryGetValue(key, out var held))
        {
            return false;
        }

        // Mouse buttons never repeat, and a keyboard key that Windows reports as up was released
        // while we missed the event: both mean this is a fresh press.
        if (input.Device == InputDevice.Mouse || !_isKeyPhysicallyDown(input.Code))
        {
            _active.Remove(key);
            return false;
        }

        suppress = Suppresses(held);
        return true;
    }

    private bool TryCapture(RawInputEvent input, (InputDevice, int) key, Modifiers modifier, TaskCompletionSource<Trigger?> capture)
    {
        // Modifiers alone just build the combination; left/right clicks are the user operating the UI.
        if (modifier != Modifiers.None
            || (input.Device == InputDevice.Mouse && input.Code is MouseButtonCodes.Left or MouseButtonCodes.Right))
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref _capture, null, capture) != capture)
        {
            return false;
        }

        _swallowed.Add(key);
        DropStaleModifiers();
        capture.TrySetResult(input.Device == InputDevice.Keyboard && input.Code == VirtualKeys.Escape
            ? null
            : new Trigger(input.Device, input.Code, HeldModifiers()));
        return true;
    }

    private Binding[] Match((InputDevice, int) key, Modifiers held)
    {
        if (!Volatile.Read(ref _bindings).TryGetValue(key, out var candidates))
        {
            return NoBindings;
        }

        var count = 0;
        foreach (var binding in candidates)
        {
            if (binding.Trigger.Modifiers == held)
            {
                count++;
            }
        }

        if (count == candidates.Length)
        {
            return candidates;
        }

        if (count == 0)
        {
            return NoBindings;
        }

        var matched = new Binding[count];
        var i = 0;
        foreach (var binding in candidates)
        {
            if (binding.Trigger.Modifiers == held)
            {
                matched[i++] = binding;
            }
        }

        return matched;
    }

    private Modifiers HeldModifiers()
    {
        var held = Modifiers.None;
        foreach (var vk in _heldModifierKeys)
        {
            held |= VirtualKeys.ModifierOf(vk);
        }

        return held;
    }

    private void DropStaleModifiers()
    {
        if (_heldModifierKeys.Count == 0)
        {
            return;
        }

        foreach (var (_, keys) in ModifierKeys)
        {
            foreach (var vk in keys)
            {
                if (_heldModifierKeys.Contains(vk) && !_isKeyPhysicallyDown(vk))
                {
                    _heldModifierKeys.Remove(vk);
                }
            }
        }
    }
}
