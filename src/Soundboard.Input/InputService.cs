using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Models;
using Soundboard.Input.Hooks;

namespace Soundboard.Input;

/// <summary>
/// Hook thread → <see cref="BindingMatcher"/> → channel → dispatch thread → <see cref="IActionDispatcher"/>.
/// The hook thread only decides and enqueues; actions run on the dispatch thread (§7.3.9, §4.2).
/// </summary>
public sealed class InputService : IInputService
{
    private const int KeyDownBit = 0x8000;

    private readonly IActionDispatcher _dispatcher;
    private readonly ILogger<InputService> _logger;
    private readonly BindingMatcher _matcher;
    private readonly Channel<InputItem> _queue = Channel.CreateUnbounded<InputItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
    });

    // Hook thread only.
    private readonly List<MatchedTrigger> _fired = [];

    private readonly CancellationTokenSource _stopping = new();
    private LowLevelHooks? _hooks;
    private Task? _dispatchLoop;
    private int _captures;

    public InputService(IActionDispatcher dispatcher, ILogger<InputService> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _dispatcher = dispatcher;
        _logger = logger;
        _matcher = new BindingMatcher(vk => (NativeMethods.GetAsyncKeyState(vk) & KeyDownBit) != 0);
    }

    public void Start()
    {
        if (_hooks is not null)
        {
            return;
        }

        _dispatchLoop = Task.Run(DispatchLoopAsync);
        var hooks = new LowLevelHooks(OnHookEvent);
        hooks.Start();
        _hooks = hooks;
        UpdateMouseHook();
        _logger.LogInformation("Input hooks installed");
    }

    public void SetBindings(IReadOnlyList<Binding> bindings)
    {
        _matcher.SetBindings(bindings);
        UpdateMouseHook();
    }

    public async Task<Trigger?> CaptureNextTriggerAsync(CancellationToken cancellationToken = default)
    {
        // Continuations must not run on the hook thread, which completes this source.
        var capture = new TaskCompletionSource<Trigger?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Increment(ref _captures);
        _matcher.BeginCapture(capture);
        UpdateMouseHook();
        try
        {
            using (cancellationToken.Register(() => _matcher.CancelCapture(capture)))
            {
                return await capture.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _captures);
            UpdateMouseHook();
        }
    }

    public void Dispose()
    {
        _hooks?.Dispose();
        _hooks = null;
        _queue.Writer.TryComplete();
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private void UpdateMouseHook() => _hooks?.SetMouseHookWanted(_matcher.NeedsMouse || Volatile.Read(ref _captures) > 0);

    // Hook thread. Never throws: an exception escaping an [UnmanagedCallersOnly] callback kills the process.
    private bool OnHookEvent(RawInputEvent input)
    {
        try
        {
            _fired.Clear();
            var suppress = _matcher.Process(input, _fired);
            foreach (var trigger in _fired)
            {
                _queue.Writer.TryWrite(new InputItem(trigger, null));
            }

            return suppress;
        }
        catch (Exception ex)
        {
            // Logged on the dispatch thread: file I/O here could exceed the hook timeout.
            _queue.Writer.TryWrite(new InputItem(default, ex));
            return false;
        }
    }

    private async Task DispatchLoopAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                if (item.Error is not null)
                {
                    _logger.LogError(item.Error, "Input hook handler failed");
                    continue;
                }

                Dispatch(item.Trigger);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private void Dispatch(MatchedTrigger trigger)
    {
        var binding = trigger.Binding;
        try
        {
            var result = _dispatcher.Dispatch(binding.ActionId, binding.TargetId, trigger.Phase, binding.Mode);
            if (result == DispatchResult.UnknownAction)
            {
                _logger.LogWarning("Binding {BindingId} refers to unknown action {ActionId}", binding.Id, binding.ActionId);
            }
        }
        catch (Exception ex)
        {
            // One failing action must not stop every later hotkey.
            _logger.LogError(ex, "Action {ActionId} failed for binding {BindingId}", binding.ActionId, binding.Id);
        }
    }

    private readonly record struct InputItem(MatchedTrigger Trigger, Exception? Error);
}
