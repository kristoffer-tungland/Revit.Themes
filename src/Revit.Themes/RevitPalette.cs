using System.Windows.Media;

namespace Revit.Themes;

/// <summary>
/// Revit's theme colors by their Revit resource name, and the semantic colors the library publishes as resources.
/// Read from the running Revit when possible; otherwise a snapshot of Revit 2026's light or dark theme.
/// </summary>
public sealed class RevitPalette
{
    /// <summary>Resource key published for each semantic color, and the Revit color it takes its value from.</summary>
    public static IReadOnlyList<(string Key, string RevitName)> SemanticKeys { get; } =
    [
        ("Revit.BackgroundColor", "HigStatusBarBkg"),
        ("Revit.ControlBackgroundColor", "PushButtonFaceColor"),
        ("Revit.InputBackgroundColor", "AGridInteriorColor"),
        ("Revit.HeaderBackgroundColor", "AGridHeaderColor"),
        ("Revit.HoverColor", "PushButtonHoverColor"),
        ("Revit.PressedColor", "PushButtonPressedColor"),
        ("Revit.SelectionColor", "ScheduleHeaderSelectedColor"),
        ("Revit.BorderColor", "ComboBorderColor"),
        ("Revit.SubtleBorderColor", "AGridBorderColor"),
        ("Revit.FocusBorderColor", "PushButtonBorderFocusColor"),
        ("Revit.ForegroundColor", "PushButtonTextColor"),
        ("Revit.SecondaryForegroundColor", "AGridDisabledTextColor"),
        ("Revit.DisabledForegroundColor", "PushButtonDisabledTextColor"),
        ("Revit.HighlightColor", "OKButtonFaceColor"),
        ("Revit.AccentHoverColor", "OKButtonHoverColor"),
        ("Revit.AccentPressedColor", "OKButtonPressedColor"),
        ("Revit.AccentDisabledColor", "OKButtonDisabledColor"),
        ("Revit.HyperlinkColor", "HigHyperlinkText"),
    ];

    // Snapshot of UIFramework themes/themecolorlight.xaml and themecolordark.xaml in Revit 2026.
    private static readonly IReadOnlyDictionary<string, Color> LightSnapshot = ColorTable(
        ("HigStatusBarBkg", 0xFFF5F5F5), ("PushButtonFaceColor", 0xFFEEEEEE), ("AGridInteriorColor", 0xFFFFFFFF),
        ("AGridHeaderColor", 0xFFE6E6E6), ("PushButtonHoverColor", 0xFFDEDEDE), ("PushButtonPressedColor", 0xFFD2D2D2),
        ("ScheduleHeaderSelectedColor", 0xFFD4D4D4), ("ComboBorderColor", 0xFFCCCCCC), ("AGridBorderColor", 0xFFECECEC),
        ("PushButtonBorderFocusColor", 0xFF0696D7), ("PushButtonTextColor", 0xFF3C3C3C), ("AGridDisabledTextColor", 0xFF808080),
        ("PushButtonDisabledTextColor", 0xFFA7A7A7), ("OKButtonFaceColor", 0xFF0696D7), ("OKButtonHoverColor", 0xFF1893CA),
        ("OKButtonPressedColor", 0xFF2490C1), ("OKButtonDisabledColor", 0xFF83CBEB), ("HigHyperlinkText", 0xFF006EAF));

    private static readonly IReadOnlyDictionary<string, Color> DarkSnapshot = ColorTable(
        ("HigStatusBarBkg", 0xFF3B4453), ("PushButtonFaceColor", 0xFF2E3440), ("AGridInteriorColor", 0xFF3B4453),
        ("AGridHeaderColor", 0xFF222933), ("PushButtonHoverColor", 0xFF434C5A), ("PushButtonPressedColor", 0xFF515C6B),
        ("ScheduleHeaderSelectedColor", 0xFF444E5C), ("ComboBorderColor", 0xFF687689), ("AGridBorderColor", 0xFF485263),
        ("PushButtonBorderFocusColor", 0xFF38ABDF), ("PushButtonTextColor", 0xFFF5F5F5), ("AGridDisabledTextColor", 0xFF989CA4),
        ("PushButtonDisabledTextColor", 0xFF7E8188), ("OKButtonFaceColor", 0xFF0696D7), ("OKButtonHoverColor", 0xFF21A0DB),
        ("OKButtonPressedColor", 0xFF33A5DC), ("OKButtonDisabledColor", 0xFF26739C), ("HigHyperlinkText", 0xFF6DD2FF));

    private readonly IReadOnlyDictionary<string, Color> _colors;

    private RevitPalette(RevitTheme theme, IReadOnlyDictionary<string, Color> colors, bool isFromRevit)
    {
        Theme = theme;
        _colors = colors;
        IsFromRevit = isFromRevit;
    }

    public RevitTheme Theme { get; }

    /// <summary>True when the colors were read from the running Revit, false for the built-in snapshot.</summary>
    public bool IsFromRevit { get; }

    /// <summary>Every theme color by its Revit name, for example <c>OKButtonFaceColor</c>.</summary>
    public IReadOnlyDictionary<string, Color> RevitColors => _colors;

    /// <summary>A Revit color by name; the snapshot fills in names this Revit version does not define.</summary>
    public Color this[string revitName] =>
        _colors.TryGetValue(revitName, out var color) ? color
        : Snapshot(Theme).TryGetValue(revitName, out var fallback) ? fallback
        : throw new KeyNotFoundException($"Revit has no theme color named '{revitName}'.");

    /// <summary>Panels such as the Properties palette.</summary>
    public Color Background => this["HigStatusBarBkg"];
    public Color ControlBackground => this["PushButtonFaceColor"];
    public Color InputBackground => this["AGridInteriorColor"];
    public Color HeaderBackground => this["AGridHeaderColor"];
    public Color Hover => this["PushButtonHoverColor"];
    public Color Pressed => this["PushButtonPressedColor"];
    public Color Selection => this["ScheduleHeaderSelectedColor"];
    public Color Border => this["ComboBorderColor"];
    public Color SubtleBorder => this["AGridBorderColor"];
    public Color FocusBorder => this["PushButtonBorderFocusColor"];
    public Color Foreground => this["PushButtonTextColor"];
    public Color SecondaryForeground => this["AGridDisabledTextColor"];
    public Color DisabledForeground => this["PushButtonDisabledTextColor"];
    /// <summary>Revit's accent: the face of OK buttons.</summary>
    public Color Accent => this["OKButtonFaceColor"];
    public Color AccentHover => this["OKButtonHoverColor"];
    public Color AccentPressed => this["OKButtonPressedColor"];
    public Color AccentDisabled => this["OKButtonDisabledColor"];
    public Color Hyperlink => this["HigHyperlinkText"];

    /// <summary>The semantic colors by resource key, for example <c>Revit.BackgroundColor</c>.</summary>
    public IReadOnlyDictionary<string, Color> SemanticColors =>
        SemanticKeys.ToDictionary(entry => entry.Key, entry => this[entry.RevitName], StringComparer.Ordinal);

    /// <summary>The built-in snapshot of Revit's colors for a theme.</summary>
    public static RevitPalette Default(RevitTheme theme) => new(theme, Snapshot(theme), isFromRevit: false);

    /// <summary>Colors read from Revit; names it lacks fall back to the snapshot.</summary>
    public static RevitPalette FromRevitColors(RevitTheme theme, IReadOnlyDictionary<string, Color> colors) =>
        new(theme, new Dictionary<string, Color>(colors.ToDictionary(entry => entry.Key, entry => entry.Value), StringComparer.Ordinal), isFromRevit: true);

    private static IReadOnlyDictionary<string, Color> Snapshot(RevitTheme theme) => theme == RevitTheme.Dark ? DarkSnapshot : LightSnapshot;

    private static IReadOnlyDictionary<string, Color> ColorTable(params (string Name, uint Argb)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => Color.FromArgb(
            (byte)(entry.Argb >> 24), (byte)(entry.Argb >> 16), (byte)(entry.Argb >> 8), (byte)entry.Argb), StringComparer.Ordinal);
}
