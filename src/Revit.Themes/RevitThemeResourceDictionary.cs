using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Revit.Themes;

/// <summary>
/// Publishes Revit's theme colors as <c>Revit.*Color</c> and <c>Revit.*Brush</c> resources, optionally with implicit Revit-like control styles.
/// </summary>
public sealed class RevitThemeResourceDictionary : ResourceDictionary
{
    private static readonly Uri ControlStylesUri = new("/Revit.Themes;component/Themes/Controls.xaml", UriKind.Relative);

    private EventHandler? _autoRefreshHandler;
    private bool _includeControlStyles = true;

    /// <summary>Reads the current Revit theme and colors. Call it on Revit's thread or another STA thread.</summary>
    public RevitThemeResourceDictionary()
    {
        Refresh();
    }

    /// <summary>Uses colors already read, for example on Revit's thread before opening a window on its own thread.</summary>
    public RevitThemeResourceDictionary(RevitPalette palette, bool includeControlStyles = true)
    {
        _includeControlStyles = includeControlStyles;
        Apply(palette);
    }

    public RevitTheme CurrentTheme { get; private set; }

    public RevitPalette Palette { get; private set; } = RevitPalette.Default(RevitTheme.Light);

    /// <summary>
    /// Implicit styles for Window, Button, TextBox, ComboBox, CheckBox and ListBox.
    /// Turn it off when another control library, such as WPF-UI, styles the controls and only the colors are wanted.
    /// </summary>
    public bool IncludeControlStyles
    {
        get => _includeControlStyles;
        set
        {
            if (_includeControlStyles == value)
            {
                return;
            }

            _includeControlStyles = value;
            Apply(Palette);
        }
    }

    public void Refresh(Func<object?>? currentThemeProvider = null)
    {
        Apply(RevitThemeService.GetCurrentPalette(currentThemeProvider));
    }

    /// <summary>Uses the built-in snapshot of Revit's colors for the theme.</summary>
    public void SetTheme(RevitTheme theme)
    {
        Apply(RevitPalette.Default(theme));
    }

    public void Apply(RevitPalette palette)
    {
        Palette = palette;
        CurrentTheme = palette.Theme;

        // Frozen brushes can be shared by windows that run on their own threads.
        foreach (var entry in palette.SemanticColors)
        {
            this[entry.Key] = entry.Value;
            var brush = new SolidColorBrush(entry.Value);
            brush.Freeze();
            this[BrushKey(entry.Key)] = brush;
        }

        MergedDictionaries.Clear();
        if (_includeControlStyles)
        {
            MergedDictionaries.Add(new ResourceDictionary { Source = ControlStylesUri });
        }
    }

    /// <summary>
    /// Follows Revit's theme changes. Revit raises them on its own thread; the colors are read there and applied
    /// on the thread that called this method, so windows that run on their own UI thread stay safe.
    /// </summary>
    public void EnableAutoRefresh(Func<object?>? currentThemeProvider = null)
    {
        DisableAutoRefresh();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _autoRefreshHandler = (_, _) =>
        {
            var palette = RevitThemeService.GetCurrentPalette(currentThemeProvider);
            if (dispatcher.CheckAccess())
            {
                Apply(palette);
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() => Apply(palette)));
            }
        };
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
            resources = new RevitThemeResourceDictionary(RevitThemeService.GetCurrentPalette(currentThemeProvider));
            target.MergedDictionaries.Add(resources);
        }
        else
        {
            resources.Refresh(currentThemeProvider);
        }

        return resources;
    }

    /// <summary><c>Revit.BackgroundColor</c> becomes <c>Revit.BackgroundBrush</c>.</summary>
    public static string BrushKey(string colorKey) =>
        colorKey.EndsWith("Color", StringComparison.Ordinal) ? colorKey.Substring(0, colorKey.Length - "Color".Length) + "Brush" : colorKey + "Brush";
}
