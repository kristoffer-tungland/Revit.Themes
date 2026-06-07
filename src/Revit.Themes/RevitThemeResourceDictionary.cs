using System.Windows;
using System.Windows.Media;

namespace Revit.Themes;

public sealed class RevitThemeResourceDictionary : ResourceDictionary
{
    private static readonly Uri BaseResourcesUri = new("/Revit.Themes;component/Themes/Base.xaml", UriKind.Relative);
    private static readonly string[] ThemeColorKeys =
    [
        "Revit.BackgroundColor",
        "Revit.ControlBackgroundColor",
        "Revit.BorderColor",
        "Revit.ForegroundColor",
        "Revit.HighlightColor",
    ];

    private EventHandler? _autoRefreshHandler;

    public RevitThemeResourceDictionary()
    {
        Refresh();
    }

    public RevitTheme CurrentTheme { get; private set; }

    public void Refresh(Func<object?>? currentThemeProvider = null)
    {
        var theme = RevitThemeService.GetCurrentTheme(currentThemeProvider);
        var colors = RevitThemeService.GetCurrentColors(currentThemeProvider);
        SetTheme(theme, colors);
    }

    public void SetTheme(RevitTheme theme)
    {
        SetTheme(theme, new Dictionary<string, Color>(0));
    }

    private void SetTheme(RevitTheme theme, IReadOnlyDictionary<string, Color> colors)
    {
        CurrentTheme = theme;

        MergedDictionaries.Clear();
        MergedDictionaries.Add(new ResourceDictionary { Source = BaseResourcesUri });
        MergedDictionaries.Add(new ResourceDictionary { Source = GetThemeResourcesUri(theme) });
        ApplyThemeColors(colors);
    }

    private void ApplyThemeColors(IReadOnlyDictionary<string, Color> colors)
    {
        foreach (var key in ThemeColorKeys)
        {
            Remove(key);
        }

        foreach (var colorEntry in colors)
        {
            this[colorEntry.Key] = colorEntry.Value;
        }
    }

    public void EnableAutoRefresh(Func<object?>? currentThemeProvider = null)
    {
        DisableAutoRefresh();
        _autoRefreshHandler = (_, _) => Refresh(currentThemeProvider);
        RevitThemeService.ThemeChanged += _autoRefreshHandler;
    }

    public void DisableAutoRefresh()
    {
        if (_autoRefreshHandler is not null)
        {
            RevitThemeService.ThemeChanged -= _autoRefreshHandler;
            _autoRefreshHandler = null;
        }
    }

    public static RevitThemeResourceDictionary ApplyTo(ResourceDictionary target, Func<object?>? currentThemeProvider = null)
    {
        var resources = target.MergedDictionaries.OfType<RevitThemeResourceDictionary>().FirstOrDefault();
        if (resources is null)
        {
            resources = new RevitThemeResourceDictionary();
            target.MergedDictionaries.Add(resources);
        }

        resources.Refresh(currentThemeProvider);
        return resources;
    }

    private static Uri GetThemeResourcesUri(RevitTheme theme)
    {
        return theme switch
        {
            RevitTheme.Dark => new Uri("/Revit.Themes;component/Themes/Dark.xaml", UriKind.Relative),
            _ => new Uri("/Revit.Themes;component/Themes/Light.xaml", UriKind.Relative),
        };
    }
}
