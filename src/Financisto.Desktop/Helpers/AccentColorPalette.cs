using Avalonia.Controls;
using Avalonia.Media;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// The 20 colors Android's AccountActivity offers for an account accent color, in the same order,
/// laid out by the ColorPicker as 4 rows of 5.
/// </summary>
public sealed class AccentColorPalette : IColorPalette
{
    private static readonly Color[] Colors =
    [
        Color.Parse("#000000"), Color.Parse("#ffffff"), Color.Parse("#ff0000"), Color.Parse("#800000"), Color.Parse("#ff00ff"),
        Color.Parse("#ffc0cb"), Color.Parse("#00ffff"), Color.Parse("#add8e6"), Color.Parse("#0000ff"), Color.Parse("#00008b"),
        Color.Parse("#c0c0c0"), Color.Parse("#808080"), Color.Parse("#ffa500"), Color.Parse("#a52a2a"), Color.Parse("#ffff00"),
        Color.Parse("#800080"), Color.Parse("#00ff00"), Color.Parse("#7fffd4"), Color.Parse("#008000"), Color.Parse("#808000"),
    ];

    public int ColorCount => 5;

    public int ShadeCount => Colors.Length / ColorCount;

    public Color GetColor(int colorIndex, int shadeIndex) => Colors[shadeIndex * ColorCount + colorIndex];
}
