using System.Reflection;

namespace Revit.Themes;

public static class RevitThemeService
{
    public static RevitTheme GetCurrentTheme(Func<object?>? currentThemeProvider = null)
    {
        var rawTheme = currentThemeProvider?.Invoke() ?? TryGetRevitCurrentTheme();
        return ParseTheme(rawTheme);
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
            var currentTheme = uiThemeManager.GetProperty("CurrentTheme", BindingFlags.Public | BindingFlags.Static);
            if (currentTheme is not null)
            {
                return currentTheme.GetValue(null);
            }

            var isDarkTheme = uiThemeManager.GetProperty("IsDarkTheme", BindingFlags.Public | BindingFlags.Static);
            if (isDarkTheme is not null)
            {
                return isDarkTheme.GetValue(null);
            }
        }

        var componentManager = ResolveType("Autodesk.Windows.ComponentManager, AdWindows");
        var applicationTheme = componentManager?.GetProperty("ApplicationTheme", BindingFlags.Public | BindingFlags.Static);
        return applicationTheme?.GetValue(null);
    }

    private static Type? ResolveType(string assemblyQualifiedName)
    {
        var resolvedType = Type.GetType(assemblyQualifiedName, throwOnError: false);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            resolvedType = assembly.GetType(assemblyQualifiedName.Split(',')[0], throwOnError: false);
            if (resolvedType is not null)
            {
                return resolvedType;
            }
        }

        return null;
    }
}
