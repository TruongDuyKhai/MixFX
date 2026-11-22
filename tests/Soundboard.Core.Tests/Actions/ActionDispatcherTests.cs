using Soundboard.Core.Actions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Actions;

public sealed class ActionDispatcherTests
{
    [Fact]
    public void Dispatch_KnownAction_ExecutesItWithTargetAndPhase()
    {
        var play = new RecordingAction("PlaySound");
        var stop = new RecordingAction("StopAll");
        var dispatcher = new ActionDispatcher([play, stop]);
        var soundId = Guid.NewGuid();

        var result = dispatcher.Dispatch("PlaySound", soundId, TriggerPhase.Up);

        result.Should().Be(DispatchResult.Executed);
        play.Calls.Should().ContainSingle()
            .Which.Should().Be(new ActionContext { TargetId = soundId, Phase = TriggerPhase.Up });
        stop.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Dispatch_NullTarget_IsPassedThrough()
    {
        var stop = new RecordingAction("StopAll");
        var dispatcher = new ActionDispatcher([stop]);

        dispatcher.Dispatch("StopAll", null, TriggerPhase.Down);

        stop.Calls.Should().ContainSingle().Which.TargetId.Should().BeNull();
    }

    [Fact]
    public void Dispatch_UnknownAction_ReturnsUnknownAndRunsNothing()
    {
        var play = new RecordingAction("PlaySound");
        var dispatcher = new ActionDispatcher([play]);

        var result = dispatcher.Dispatch("DoesNotExist", null, TriggerPhase.Down);

        result.Should().Be(DispatchResult.UnknownAction);
        play.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Dispatch_IdDiffersOnlyByCase_IsUnknown()
    {
        var dispatcher = new ActionDispatcher([new RecordingAction("PlaySound")]);

        dispatcher.Dispatch("playsound", null, TriggerPhase.Down).Should().Be(DispatchResult.UnknownAction);
    }

    [Fact]
    public void Dispatch_NoActionsRegistered_ReturnsUnknown()
    {
        var dispatcher = new ActionDispatcher([]);

        dispatcher.Dispatch("PlaySound", null, TriggerPhase.Down).Should().Be(DispatchResult.UnknownAction);
    }

    [Fact]
    public void Dispatch_ActionThrows_ExceptionReachesCaller()
    {
        var dispatcher = new ActionDispatcher([new ThrowingAction("Boom")]);

        var act = () => dispatcher.Dispatch("Boom", null, TriggerPhase.Down);

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void Dispatch_NullId_Throws()
    {
        var dispatcher = new ActionDispatcher([]);

        var act = () => dispatcher.Dispatch(null!, null, TriggerPhase.Down);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_DuplicateIds_Throws()
    {
        var act = () => new ActionDispatcher([new RecordingAction("PlaySound"), new RecordingAction("PlaySound")]);

        act.Should().Throw<ArgumentException>().WithMessage("*PlaySound*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyId_Throws(string id)
    {
        var act = () => new ActionDispatcher([new RecordingAction(id)]);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class RecordingAction(string id) : IAction
    {
        public string Id { get; } = id;

        public List<ActionContext> Calls { get; } = [];

        public void Execute(ActionContext context) => Calls.Add(context);
    }

    private sealed class ThrowingAction(string id) : IAction
    {
        public string Id { get; } = id;

        public void Execute(ActionContext context) => throw new InvalidOperationException("boom");
    }
}
