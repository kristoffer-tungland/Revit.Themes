using System.Globalization;
using System.Reflection;
using System.Windows.Media;

namespace Revit.Themes;

public static class RevitThemeService
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private static readonly string[] ColorContainerPropertyNames =
    [
        "Colors",
        "Palette",
        "ColorPalette",
        "ColorTheme",
        "ThemeColors",
        "ThemePalette",
        "Brushes",
    ];

    private static readonly KeyValuePair<string, string[]>[] ResourceColorPropertyNames =
    [
        new("Revit.BackgroundColor",
        [
            "BackgroundColor",
            "ApplicationBackgroundColor",
            "WindowBackgroundColor",
            "MainWindowBackgroundColor",
            "WorkspaceBackgroundColor",
        ]),
        new("Revit.ControlBackgroundColor",
        [
            "ControlBackgroundColor",
            "PanelBackgroundColor",
            "RibbonBackgroundColor",
            "ContentBackgroundColor",
            "InputBackgroundColor",
        ]),
        new("Revit.BorderColor",
        [
            "BorderColor",
            "PanelBorderColor",
            "SeparatorColor",
            "OutlineColor",
            "StrokeColor",
        ]),
        new("Revit.ForegroundColor",
        [
            "ForegroundColor",
            "TextColor",
            "WindowTextColor",
            "PanelTextColor",
            "ControlTextColor",
        ]),
        new("Revit.HighlightColor",
        [
            "HighlightColor",
            "AccentColor",
            "SelectionColor",
            "SystemAccentColor",
            "FocusColor",
            "LinkColor",
        ]),
    ];

    private static EventHandler? _themeChanged;
    private static bool _isSubscribedToRevit;

    public static event EventHandler? ThemeChanged
    {
        add
        {
            _themeChanged += value;
            EnsureRevitSubscription();
        }
        remove
        {
            _themeChanged -= value;
        }
    }

    private static void EnsureRevitSubscription()
    {
        if (_isSubscribedToRevit)
        {
            return;
        }

        _isSubscribedToRevit = TrySubscribeToRevitThemeChanged();
    }

    private static bool TrySubscribeToRevitThemeChanged()
    {
        var uiThemeManager = ResolveType("Autodesk.Revit.UI.UIThemeManager, RevitAPIUI");
        if (uiThemeManager is null)
        {
            return false;
        }

        var themeChangedEvent = uiThemeManager.GetEvent("ThemeChanged", BindingFlags.Public | BindingFlags.Static);
        if (themeChangedEvent is null)
        {
            return false;
        }

        themeChangedEvent.AddEventHandler(null, new EventHandler(OnRevitThemeChanged));
        return true;
    }

    private static void OnRevitThemeChanged(object? sender, EventArgs e)
    {
        _themeChanged?.Invoke(sender, e);
    }

    public static RevitTheme GetCurrentTheme(Func<object?>? currentThemeProvider = null)
    {
        var rawTheme = currentThemeProvider?.Invoke() ?? TryGetRevitCurrentTheme();
        return ParseTheme(rawTheme);
    }

    public static IReadOnlyDictionary<string, Color> GetCurrentColors(Func<object?>? currentThemeProvider = null)
    {
        var rawTheme = currentThemeProvider?.Invoke() ?? TryGetRevitCurrentTheme();
        var applicationTheme = TryGetRevitApplicationTheme();
        var colorSources = GetColorSources(rawTheme, applicationTheme).ToArray();
        if (colorSources.Length == 0)
        {
            return new Dictionary<string, Color>(0, StringComparer.Ordinal);
        }

        var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var resourceColorProperty in ResourceColorPropertyNames)
        {
            if (TryGetFirstColor(colorSources, resourceColorProperty.Value, out var color))
            {
                colors[resourceColorProperty.Key] = color;
            }
        }

        return colors;
    }

    internal static RevitTheme ParseTheme(object? rawTheme)
    {
        if (rawTheme is null)
        {
            return RevitTheme.Light;
        }

        if (rawTheme is bool isDark)
        {
            return isDark ? RevitTheme.Dark : RevitTheme.Light;
        }

        var name = rawTheme.ToString();
        if (name is null)
        {
            return RevitTheme.Light;
        }

        if (name.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            return RevitTheme.Dark;
        }

        if (name.Equals("Light", StringComparison.OrdinalIgnoreCase))
        {
            return RevitTheme.Light;
        }

        return Enum.TryParse<RevitTheme>(name, ignoreCase: true, out var parsedTheme)
            ? parsedTheme
            : RevitTheme.Light;
    }

    private static object? TryGetRevitCurrentTheme()
    {
        var uiThemeManager = ResolveType("Autodesk.Revit.UI.UIThemeManager, RevitAPIUI");
        if (uiThemeManager is not null)
        {
            var currentTheme = uiThemeManager.GetProperty("CurrentTheme", PublicStatic);
            if (currentTheme is not null)
            {
                return currentTheme.GetValue(null);
            }

            var isDarkTheme = uiThemeManager.GetProperty("IsDarkTheme", PublicStatic);
            if (isDarkTheme is not null)
            {
                return isDarkTheme.GetValue(null);
            }
        }

        return TryGetRevitApplicationTheme();
    }

    private static object? TryGetRevitApplicationTheme()
    {
        var componentManager = ResolveType("Autodesk.Windows.ComponentManager, AdWindows");
        var applicationTheme = componentManager?.GetProperty("ApplicationTheme", PublicStatic);
        return applicationTheme?.GetValue(null);
    }

    private static IEnumerable<object> GetColorSources(object? rawTheme, object? applicationTheme)
    {
        return ExpandColorSources(rawTheme).Concat(ExpandColorSources(applicationTheme));
    }

    private static IEnumerable<object> ExpandColorSources(object? source)
    {
        if (source is null)
        {
            yield break;
        }

        yield return source;

        foreach (var propertyName in ColorContainerPropertyNames)
        {
            var container = TryGetPropertyValue(source, propertyName);
            if (container is not null)
            {
                yield return container;
            }
        }
    }

    private static bool TryGetFirstColor(IEnumerable<object> sources, IEnumerable<string> propertyNames, out Color color)
    {
        foreach (var propertyName in propertyNames)
        {
            foreach (var source in sources)
            {
                if (TryGetColor(source, propertyName, out color))
                {
                    return true;
                }
            }
        }

        color = default;
        return false;
    }

    private static bool TryGetColor(object source, string propertyName, out Color color)
    {
        var value = TryGetPropertyValue(source, propertyName);
        return TryConvertToColor(value, out color);
    }

    private static object? TryGetPropertyValue(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName, PublicInstance | BindingFlags.IgnoreCase);
        if (property is null || property.GetIndexParameters().Length != 0)
        {
            return null;
        }

        try
        {
            return property.GetValue(source);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryConvertToColor(object? value, out Color color)
    {
        switch (value)
        {
            case null:
                color = default;
                return false;
            case Color mediaColor:
                color = mediaColor;
                return true;
            case SolidColorBrush brush:
                color = brush.Color;
                return true;
            case string colorText when TryParseColor(colorText, out color):
                return true;
            case int argb:
                color = FromArgb(unchecked((uint)argb));
                return true;
            case uint unsignedArgb:
                color = FromArgb(unsignedArgb);
                return true;
        }

        if (TryConvertFromArgbProperties(value, out color))
        {
            return true;
        }

        var text = value.ToString();
        return text is not null && TryParseColor(text, out color);
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color parsedColor)
            {
                color = parsedColor;
                return true;
            }
        }
        catch
        {
        }

        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexColor))
        {
            color = FromArgb(hexColor);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryConvertFromArgbProperties(object value, out Color color)
    {
        if (TryGetByteProperty(value, "R", out var red) &&
            TryGetByteProperty(value, "G", out var green) &&
            TryGetByteProperty(value, "B", out var blue))
        {
            var alpha = TryGetByteProperty(value, "A", out var alphaValue) ? alphaValue : byte.MaxValue;
            color = Color.FromArgb(alpha, red, green, blue);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryGetByteProperty(object value, string propertyName, out byte component)
    {
        var propertyValue = TryGetPropertyValue(value, propertyName);
        switch (propertyValue)
        {
            case byte byteValue:
                component = byteValue;
                return true;
            case int intValue when intValue >= byte.MinValue && intValue <= byte.MaxValue:
                component = (byte)intValue;
                return true;
            default:
                component = default;
                return false;
        }
    }

    private static Color FromArgb(uint argb)
    {
        return Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
    }

    private static Type? ResolveType(string assemblyQualifiedName)
    {
        var resolvedType = Type.GetType(assemblyQualifiedName, throwOnError: false);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        var typeName = assemblyQualifiedName;
        var separatorIndex = assemblyQualifiedName.IndexOf(',');
        if (separatorIndex >= 0)
        {
            typeName = assemblyQualifiedName.Substring(0, separatorIndex);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            resolvedType = assembly.GetType(typeName, throwOnError: false);
            if (resolvedType is not null)
            {
                return resolvedType;
            }
        }

        return null;
    }
}
