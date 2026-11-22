using System.ComponentModel;
using System.Runtime.InteropServices;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;
using static Soundboard.Input.Hooks.NativeMethods;

namespace Soundboard.Input.Hooks;

/// <summary>
/// Owns the WH_KEYBOARD_LL / WH_MOUSE_LL hooks and the message loop they need. Windows calls the
/// hooks on this thread; if a callback takes longer than LowLevelHooksTimeout the hook is silently
/// removed, so callbacks only call <c>handler</c> (which must be fast) and return (§7.3.9).
/// One instance per process: the static callbacks reach it through <see cref="s_active"/>.
/// </summary>
internal sealed unsafe class LowLevelHooks : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private static LowLevelHooks? s_active;

    private readonly Func<RawInputEvent, bool> _handler;
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private uint _threadId;
    private Exception? _startError;
    private int _mouseWanted;

    // Hook thread only.
    private nint _keyboardHook;
    private nint _mouseHook;

    /// <param name="handler">Called on the hook thread for each key/button transition; returns true to hide the event from other apps.</param>
    public LowLevelHooks(Func<RawInputEvent, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref s_active, this, null) is not null)
        {
            throw new InvalidOperationException("Low-level hooks are already installed in this process.");
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Soundboard input hooks",

            // Every key press in every app waits on this thread, so it must win the CPU even while a game is busy.
            Priority = ThreadPriority.Highest,
        };
        _thread.Start();
        _started.Wait();

        if (_startError is not null)
        {
            Volatile.Write(ref s_active, null);
            throw _startError;
        }
    }

    /// <summary>Installs the mouse hook only while it is needed, so mouse movement in games is otherwise untouched.</summary>
    public void SetMouseHookWanted(bool wanted)
    {
        if (Interlocked.Exchange(ref _mouseWanted, wanted ? 1 : 0) != (wanted ? 1 : 0) && _threadId != 0)
        {
            PostThreadMessageW(_threadId, WmAppUpdateMouseHook, 0, 0);
        }
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            return;
        }

        PostThreadMessageW(_threadId, WmQuit, 0, 0);
        _thread.Join(StopTimeout);
        _thread = null;
        Interlocked.CompareExchange(ref s_active, null, this);
        _started.Dispose();
    }

    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        var hooks = Volatile.Read(ref s_active);
        if (code >= 0 && hooks is not null)
        {
            var message = (int)wParam;
            var isDown = message is WmKeyDown or WmSysKeyDown;
            if (isDown || message is WmKeyUp or WmSysKeyUp)
            {
                var data = (KbdLlHookStruct*)lParam;
                if (hooks._handler(new RawInputEvent(InputDevice.Keyboard, (int)data->VkCode, isDown)))
                {
                    return 1;
                }
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint MouseProc(int code, nint wParam, nint lParam)
    {
        var message = (int)wParam;

        // Fast path: movement is by far the most frequent event and never matters to us.
        if (message != WmMouseMove && code >= 0 && Volatile.Read(ref s_active) is { } hooks)
        {
            var (button, isDown) = message switch
            {
                WmLButtonDown => (MouseButtonCodes.Left, true),
                WmLButtonUp => (MouseButtonCodes.Left, false),
                WmRButtonDown => (MouseButtonCodes.Right, true),
                WmRButtonUp => (MouseButtonCodes.Right, false),
                WmMButtonDown => (MouseButtonCodes.Middle, true),
                WmMButtonUp => (MouseButtonCodes.Middle, false),
                WmXButtonDown or WmXButtonUp => (XButton(((MsLlHookStruct*)lParam)->MouseData), message == WmXButtonDown),
                _ => (0, false),
            };

            if (button != 0 && hooks._handler(new RawInputEvent(InputDevice.Mouse, button, isDown)))
            {
                return 1;
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static int XButton(uint mouseData) => (int)(mouseData >> 16) switch
    {
        XButton1 => MouseButtonCodes.X1,
        XButton2 => MouseButtonCodes.X2,
        _ => 0,
    };

    private void Run()
    {
        _threadId = GetCurrentThreadId();

        // Creates this thread's message queue so PostThreadMessage works as soon as Start returns.
        PeekMessageW(out _, 0, 0, 0, PmNoRemove);

        var module = GetModuleHandleW(null);
        _keyboardHook = SetWindowsHookExW(WhKeyboardLl, (nint)(delegate* unmanaged<int, nint, nint, nint>)&KeyboardProc, module, 0);
        if (_keyboardHook == 0)
        {
            _startError = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not install the keyboard hook.");
            _started.Set();
            return;
        }

        UpdateMouseHook(module);
        _started.Set();

        while (GetMessageW(out var message, 0, 0, 0) > 0)
        {
            if (message.Message == WmAppUpdateMouseHook)
            {
                UpdateMouseHook(module);
            }
        }

        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
        }

        UnhookWindowsHookEx(_keyboardHook);
    }

    private void UpdateMouseHook(nint module)
    {
        var wanted = Volatile.Read(ref _mouseWanted) != 0;
        if (wanted && _mouseHook == 0)
        {
            _mouseHook = SetWindowsHookExW(WhMouseLl, (nint)(delegate* unmanaged<int, nint, nint, nint>)&MouseProc, module, 0);
        }
        else if (!wanted && _mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }
    }
}
