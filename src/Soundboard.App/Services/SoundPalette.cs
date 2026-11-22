namespace Soundboard.App.Services;

/// <summary>Tile colours offered to the user; new sounds cycle through them.</summary>
public static class SoundPalette
{
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#7C4DFF", "#448AFF", "#00BCD4", "#26A69A", "#66BB6A", "#C0CA33",
        "#FFB300", "#FF7043", "#EF5350", "#EC407A", "#AB47BC", "#8D6E63",
    ];

    public static string ForIndex(int index) => Colors[((index % Colors.Count) + Colors.Count) % Colors.Count];
}
