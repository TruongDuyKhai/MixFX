using Soundboard.Core.Models;

namespace Soundboard.App.Services;

/// <summary>The answer to "what should the close button do?".</summary>
/// <param name="Action"><see cref="CloseAction.MinimizeToTray"/> or <see cref="CloseAction.Exit"/>.</param>
/// <param name="Remember">"Không hỏi lại" was ticked: use this answer from now on.</param>
public sealed record CloseChoice(CloseAction Action, bool Remember);
