using Soundboard.Core.Models;

namespace Soundboard.Core.Bindings;

/// <summary>Warnings shown while assigning a key (PROJECT_PROMPT.md §5.5). Advisory only: the user may still save.</summary>
public static class BindingRules
{
    // Keys Valorant and most shooters use by default. Warned about even with modifiers: holding
    // Ctrl (crouch) while pressing W would still fire Ctrl+W mid-fight.
    private static readonly HashSet<int> CommonGameKeys =
    [
        'W', 'A', 'S', 'D', 'E', 'Q', 'C', 'X', 'F', 'G', 'V', 'B',
        '1', '2', '3', '4',
        VirtualKeys.Space,
        VirtualKeys.Shift, VirtualKeys.LeftShift, VirtualKeys.RightShift,
        VirtualKeys.Control, VirtualKeys.LeftControl, VirtualKeys.RightControl,
    ];

    public static bool IsCommonGameKey(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return trigger.Device == InputDevice.Mouse
            ? trigger.Code is MouseButtonCodes.Left or MouseButtonCodes.Right
            : CommonGameKeys.Contains(trigger.Code);
    }

    /// <summary>Other bindings that fire on exactly the same trigger.</summary>
    public static IReadOnlyList<Binding> FindConflicts(Trigger trigger, IEnumerable<Binding> bindings, Guid? ignoreBindingId = null)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(bindings);

        return bindings.Where(b => b.Id != ignoreBindingId && b.Trigger == trigger).ToList();
    }

    /// <summary>User-facing warnings for assigning <paramref name="trigger"/>; empty when it looks safe.</summary>
    public static IReadOnlyList<string> Warnings(Trigger trigger, IEnumerable<Binding> bindings, Guid? ignoreBindingId = null)
    {
        var warnings = new List<string>();
        var name = TriggerFormatter.Format(trigger);

        if (IsCommonGameKey(trigger))
        {
            warnings.Add($"{name} là phím game hay dùng; bấm trong trận sẽ phát sound ngoài ý muốn.");
        }

        if (FindConflicts(trigger, bindings, ignoreBindingId).Count > 0)
        {
            warnings.Add($"{name} đã được gán cho việc khác; cả hai sẽ cùng chạy.");
        }

        return warnings;
    }
}
