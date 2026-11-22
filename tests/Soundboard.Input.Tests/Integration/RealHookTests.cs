using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.Input.Tests.Integration;

/// <summary>
/// Installs the real global hooks and synthesises F23/F24, keys almost no application uses.
/// Explicit because it touches system-wide input: <c>dotnet test tests/Soundboard.Input.Tests -- xUnit.Explicit=only</c>.
/// Synthetic input is used only here, in a test, never by the app (PROJECT_PROMPT.md §2).
/// </summary>
public sealed partial class RealHookTests
{
    private const int F23 = VirtualKeys.F1 + 22;
    private const int F24 = VirtualKeys.F1 + 23;

    [Fact(Explicit = true)]
    public async Task GlobalHook_FiresBindingDownAndUp()
    {
        var dispatcher = new RecordingDispatcher();
        using var input = new InputService(dispatcher, NullLogger<InputService>.Instance);
        input.Start();
        var target = Guid.NewGuid();

        // PassThrough = false so the foreground application never receives the synthetic key.
        input.SetBindings([new Binding(Guid.NewGuid(), new Trigger(InputDevice.Keyboard, F24, Modifiers.None), "Test", target, TriggerMode.Hold, PassThrough: false)]);

        SendKey(F24, down: true);
        SendKey(F24, down: false);

        await WaitUntilAsync(() => dispatcher.Calls.Count >= 2);
        dispatcher.Calls.Should().Equal(("Test", target, TriggerPhase.Down, TriggerMode.Hold), ("Test", target, TriggerPhase.Up, TriggerMode.Hold));
    }

    [Fact(Explicit = true)]
    public async Task GlobalHook_CapturesNextKey()
    {
        using var input = new InputService(new RecordingDispatcher(), NullLogger<InputService>.Instance);
        input.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var capture = input.CaptureNextTriggerAsync(timeout.Token);
        SendKey(F23, down: true);
        SendKey(F23, down: false);

        (await capture).Should().Be(new Trigger(InputDevice.Keyboard, F23, Modifiers.None));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private static void SendKey(int vk, bool down)
    {
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput { VirtualKey = (ushort)vk, Flags = down ? 0u : KeyEventKeyUp },
        };
        SendInput(1, [input], Marshal.SizeOf<NativeInput>()).Should().Be(1u);
    }

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] NativeInput[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInput
    {
        [FieldOffset(0)]
        public uint Type;

        // The union starts after the 4-byte type, padded to pointer alignment; sized for its largest member (MOUSEINPUT).
        [FieldOffset(8)]
        public KeyboardInput Keyboard;

        [FieldOffset(8)]
        private readonly MousePadding _padding;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct MousePadding;

    private sealed class RecordingDispatcher : IActionDispatcher
    {
        public ConcurrentQueue<(string, Guid?, TriggerPhase, TriggerMode)> Queue { get; } = new();

        public List<(string, Guid?, TriggerPhase, TriggerMode)> Calls => [.. Queue];

        public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase) => Dispatch(actionId, targetId, phase, TriggerMode.Press);

        public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase, TriggerMode mode)
        {
            Queue.Enqueue((actionId, targetId, phase, mode));
            return DispatchResult.Executed;
        }
    }
}
