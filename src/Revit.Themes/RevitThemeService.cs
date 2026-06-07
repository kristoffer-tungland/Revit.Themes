using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
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
    private static bool _hasKnownTheme;
    private static RevitTheme _knownTheme;

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

        _isSubscribedToRevit =
            TrySubscribeToUiApplicationThemeChanged() ||
            TrySubscribeToLegacyThemeChanged();
    }

    private static bool TrySubscribeToUiApplicationThemeChanged()
    {
        var uiApplication = TryGetCurrentUiApplication();
        if (uiApplication is null)
        {
            return false;
        }

        var themeChangedEvent = uiApplication.GetType().GetEvent("ThemeChanged", BindingFlags.Public | BindingFlags.Instance);
        if (themeChangedEvent?.EventHandlerType is null)
        {
            return false;
        }

        var handler = CreateEventHandler(themeChangedEvent.EventHandlerType, OnThemeSourceChanged);
        themeChangedEvent.AddEventHandler(uiApplication, handler);
        CaptureCurrentTheme();
        return true;
    }

    private static bool TrySubscribeToLegacyThemeChanged()
    {
        var applicationThemeType = ResolveType("UIFramework.ApplicationTheme, UIFramework");
        var currentTheme = applicationThemeType?.GetProperty("CurrentTheme", PublicStatic)?.GetValue(null);
        var propertyChangedEvent = currentTheme?.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged), BindingFlags.Public | BindingFlags.Instance);
        if (propertyChangedEvent is null)
        {
            return false;
        }

        propertyChangedEvent.AddEventHandler(currentTheme, new PropertyChangedEventHandler(OnLegacyThemePropertyChanged));
        CaptureCurrentTheme();
        return true;
    }

    private static void OnLegacyThemePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnThemeSourceChanged(sender, e);
    }

    private static void OnThemeSourceChanged(object? sender, object? args)
    {
        if (!IsRelevantThemeChange(args))
        {
            return;
        }

        var currentTheme = GetCurrentTheme();
        if (_hasKnownTheme && currentTheme == _knownTheme)
        {
            return;
        }

        _knownTheme = currentTheme;
        _hasKnownTheme = true;
        _themeChanged?.Invoke(sender, EventArgs.Empty);
    }

    private static bool IsRelevantThemeChange(object? args)
    {
        var themeChangedType = TryGetPropertyValue(args, "ThemeChangedType")?.ToString();
        return themeChangedType is null || themeChangedType.Equals("UITheme", StringComparison.OrdinalIgnoreCase);
    }

    private static void CaptureCurrentTheme()
    {
        _knownTheme = GetCurrentTheme();
        _hasKnownTheme = true;
    }

    private static Delegate CreateEventHandler(Type eventHandlerType, Action<object?, object?> handler)
    {
        var invokeMethod = eventHandlerType.GetMethod("Invoke")
            ?? throw new NotSupportedException($"Unable to create event handler for '{eventHandlerType.FullName}'.");
        var parameters = invokeMethod.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();

        var sender = parameters.Length > 0
            ? Expression.Convert(parameters[0], typeof(object))
            : Expression.Constant(null, typeof(object));
        var args = parameters.Length > 1
            ? Expression.Convert(parameters[1], typeof(object))
            : Expression.Constant(null, typeof(object));

        var body = Expression.Call(
            Expression.Constant(handler),
            handler.GetType().GetMethod(nameof(Action<object?, object?>.Invoke))!,
            sender,
            args);

        return Expression.Lambda(eventHandlerType, body, parameters).Compile();
    }

    private static object? TryGetCurrentUiApplication()
    {
        try
        {
            var application = TryGetCurrentRevitApplication();
            if (application is null)
            {
                return null;
            }

            var applicationType = ResolveType("Autodesk.Revit.ApplicationServices.Application, RevitAPI");
            var uiApplicationType = ResolveType("Autodesk.Revit.UI.UIApplication, RevitAPIUI");
            if (applicationType is null || uiApplicationType is null)
            {
                return null;
            }

            var constructor = uiApplicationType?.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: [applicationType],
                modifiers: null);
            return constructor?.Invoke([application]);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryGetCurrentRevitApplication()
    {
        try
        {
            var dbApiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "RevitDBAPI");
            if (dbApiAssembly is null)
            {
                return null;
            }

            var getApplicationMethod = dbApiAssembly.ManifestModule
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "RevitApplication.getApplication_");
            var internalApplication = getApplicationMethod?.Invoke(null, null);
            if (internalApplication is null)
            {
                return null;
            }

            var proxyType = dbApiAssembly.GetType("Autodesk.Revit.Proxy.ApplicationServices.ApplicationProxy", throwOnError: false);
            if (proxyType is null)
            {
                return null;
            }

            var proxyConstructor = proxyType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance,
                binder: null,
                types: [internalApplication.GetType()],
                modifiers: null);
            var proxy = proxyConstructor?.Invoke([internalApplication]);
            if (proxy is null)
            {
                return null;
            }

            var applicationType = ResolveType("Autodesk.Revit.ApplicationServices.Application, RevitAPI");
            if (applicationType is null)
            {
                return null;
            }

            var applicationConstructor = applicationType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance,
                binder: null,
                types: [proxyType],
                modifiers: null);
            return applicationConstructor?.Invoke([proxy]);
        }
        catch
        {
            return null;
        }
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

    private static object? TryGetPropertyValue(object? source, string propertyName)
    {
        if (source is null)
        {
            return null;
        }

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
