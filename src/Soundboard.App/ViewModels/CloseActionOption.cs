using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>A <see cref="CloseAction"/> with its label for the "Khi bấm X" picker.</summary>
public sealed record CloseActionOption(CloseAction Action, string Label)
{
    public static IReadOnlyList<CloseActionOption> All { get; } =
    [
        new(CloseAction.Ask, "Hỏi mỗi lần"),
        new(CloseAction.MinimizeToTray, "Thu nhỏ xuống khay"),
        new(CloseAction.Exit, "Thoát app"),
    ];

    /// <summary>The option for <paramref name="action"/>; "Hỏi mỗi lần" for a value this version does not know.</summary>
    public static CloseActionOption For(CloseAction action) => All.FirstOrDefault(o => o.Action == action) ?? All[0];
}
