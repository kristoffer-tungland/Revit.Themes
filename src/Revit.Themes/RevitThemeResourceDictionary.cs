using System.Windows;

namespace Revit.Themes;

public sealed class RevitThemeResourceDictionary : ResourceDictionary
{
    private static readonly Uri BaseResourcesUri = new("/Revit.Themes;component/Themes/Base.xaml", UriKind.Relative);

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
