using System.Windows;

namespace Revit.Themes;

public sealed class RevitThemeResourceDictionary : ResourceDictionary
{
    private static readonly Uri BaseResourcesUri = new("/Revit.Themes;component/Themes/Base.xaml", UriKind.Relative);

    private EventHandler? _autoRefreshHandler;

    public RevitThemeResourceDictionary()
    {
        Refresh();
    }

    public RevitTheme CurrentTheme { get; private set; }

    public void Refresh(Func<object?>? currentThemeProvider = null)
    {
        SetTheme(RevitThemeService.GetCurrentTheme(currentThemeProvider));
    }

    public void SetTheme(RevitTheme theme)
    {
        CurrentTheme = theme;

        MergedDictionaries.Clear();
        MergedDictionaries.Add(new ResourceDictionary { Source = BaseResourcesUri });
        MergedDictionaries.Add(new ResourceDictionary { Source = GetThemeResourcesUri(theme) });
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
