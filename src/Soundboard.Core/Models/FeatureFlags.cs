namespace Soundboard.Core.Models;

/// <summary>
/// Switches for features that are not stable yet. Every flag must default to off
/// (PROJECT_PROMPT.md §7.2.7). Empty until the first experimental feature lands.
/// </summary>
public sealed record FeatureFlags;
