using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Revit.Themes;

public static class RevitThemeService
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static EventHandler? _themeChanged;
    private static bool _isSubscribedToRevit;
    private static bool _hasKnownTheme;
    private static RevitTheme _knownTheme;

    /// <summary>Raised on Revit's thread after Revit switches between its light and dark UI theme.</summary>
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

    /// <summary>
    /// Listens to <c>UIApplication.ThemeChanged</c> on the given application (Revit 2024 and later).
    /// Prefer this over the automatic lookup when a <c>UIApplication</c> is at hand. Call it on Revit's thread.
    /// </summary>
    public static bool Attach(object uiApplication)
    {
        if (_isSubscribedToRevit)
        {
            return true;
        }

        _isSubscribedToRevit = TrySubscribeToThemeChanged(uiApplication);
        return _isSubscribedToRevit;
    }

    public static RevitTheme GetCurrentTheme(Func<object?>? currentThemeProvider = null)
    {
        var rawTheme = currentThemeProvider?.Invoke() ?? TryGetRevitCurrentTheme();
        return ParseTheme(rawTheme);
    }

    /// <summary>
    /// The current theme with Revit's own colors when it runs in Revit, otherwise the built-in snapshot.
    /// Reading Revit's colors needs an STA thread, such as Revit's main thread.
    /// </summary>
    public static RevitPalette GetCurrentPalette(Func<object?>? currentThemeProvider = null)
    {
        var theme = GetCurrentTheme(currentThemeProvider);
        return TryReadRevitColors(theme) is { } colors ? RevitPalette.FromRevitColors(theme, colors) : RevitPalette.Default(theme);
    }

    /// <summary>The semantic colors by resource key, for example <c>Revit.BackgroundColor</c>.</summary>
    public static IReadOnlyDictionary<string, Color> GetCurrentColors(Func<object?>? currentThemeProvider = null) =>
        GetCurrentPalette(currentThemeProvider).SemanticColors;

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

    /// <summary>Raises <see cref="ThemeChanged"/> as Revit would; for tests.</summary>
    internal static void RaiseThemeChanged() => _themeChanged?.Invoke(null, EventArgs.Empty);

    // Revit keeps its theme colors in UIFramework's themes/themecolorlight.xaml and themecolordark.xaml.
    private static IReadOnlyDictionary<string, Color>? TryReadRevitColors(RevitTheme theme)
    {
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "UIFramework"))
        {
            return null;
        }

        var name = theme == RevitTheme.Dark ? "themecolordark" : "themecolorlight";
        try
        {
            if (Application.LoadComponent(new Uri($"/UIFramework;component/themes/{name}.xaml", UriKind.Relative)) is not ResourceDictionary dictionary)
            {
                return null;
            }

            var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                switch (entry.Value)
                {
                    case Color color when entry.Key is string key:
                        colors[key] = color;
                        break;
                    case SolidColorBrush brush when entry.Key is string key:
                        colors[key] = brush.Color;
                        break;
                }
            }

            return colors.Count > 0 ? colors : null;
        }
        catch (IOException)
        {
            // This Revit version has no such theme dictionary.
            return null;
        }
        catch (XamlParseException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // Not an STA thread.
            return null;
        }
    }

    private static void EnsureRevitSubscription()
    {
        if (_isSubscribedToRevit)
        {
            return;
        }

        _isSubscribedToRevit =
            TryGetCurrentUiApplication() is { } uiApplication && TrySubscribeToThemeChanged(uiApplication) ||
            TrySubscribeToLegacyThemeChanged();
    }

    private static bool TrySubscribeToThemeChanged(object uiApplication)
    {
        var themeChangedEvent = uiApplication.GetType().GetEvent("ThemeChanged", PublicInstance);
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
        var propertyChangedEvent = currentTheme?.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged), PublicInstance);
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

        Expression sender = parameters.Length > 0
            ? Expression.Convert(parameters[0], typeof(object))
            : Expression.Constant(null, typeof(object));
        Expression args = parameters.Length > 1
            ? Expression.Convert(parameters[1], typeof(object))
            : Expression.Constant(null, typeof(object));

        var body = Expression.Call(
            Expression.Constant(handler),
            handler.GetType().GetMethod(nameof(Action<object?, object?>.Invoke))!,
            sender,
            args);

        return Expression.Lambda(eventHandlerType, body, parameters).Compile();
    }

    // Without a UIApplication from the caller, rebuild one from Revit's internal application object.
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

            var constructor = uiApplicationType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: [applicationType],
                modifiers: null);
            return constructor?.Invoke([application]);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (MemberAccessException)
        {
            return null;
        }
    }

    private static object? TryGetCurrentRevitApplication()
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
        var applicationConstructor = applicationType?.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance,
            binder: null,
            types: [proxyType],
            modifiers: null);
        return applicationConstructor?.Invoke([proxy]);
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

        // Before Revit 2024 the UI has no dark theme.
        return null;
    }

    private static object? TryGetPropertyValue(object? source, string propertyName)
    {
        var property = source?.GetType().GetProperty(propertyName, PublicInstance | BindingFlags.IgnoreCase);
        if (property is null || property.GetIndexParameters().Length != 0)
        {
            return null;
        }

        return property.GetValue(source);
    }

    private static Type? ResolveType(string assemblyQualifiedName)
    {
        var resolvedType = Type.GetType(assemblyQualifiedName, throwOnError: false);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        var separatorIndex = assemblyQualifiedName.IndexOf(',');
        var typeName = separatorIndex >= 0 ? assemblyQualifiedName.Substring(0, separatorIndex) : assemblyQualifiedName;
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
