using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Revit.Themes.Tests;

public class RevitThemeTests
{
    [Theory]
    [InlineData("Dark", RevitTheme.Dark)]
    [InlineData("light", RevitTheme.Light)]
    [InlineData(true, RevitTheme.Dark)]
    [InlineData(false, RevitTheme.Light)]
    [InlineData(null, RevitTheme.Light)]
    [InlineData("Something else", RevitTheme.Light)]
    public void ParsesRevitThemeValues(object? raw, RevitTheme expected) =>
        Assert.Equal(expected, RevitThemeService.ParseTheme(raw));

    [Fact]
    public void SnapshotsMatchRevitColors()
    {
        var dark = RevitPalette.Default(RevitTheme.Dark);
        var light = RevitPalette.Default(RevitTheme.Light);
        Assert.Equal(Color.FromRgb(0x3B, 0x44, 0x53), dark.Background);
        Assert.Equal(Color.FromRgb(0xF5, 0xF5, 0xF5), dark.Foreground);
        Assert.Equal(Color.FromRgb(0xF5, 0xF5, 0xF5), light.Background);
        Assert.Equal(Color.FromRgb(0x3C, 0x3C, 0x3C), light.Foreground);
        Assert.Equal(Color.FromRgb(0x06, 0x96, 0xD7), dark.Accent);
        Assert.Equal(dark.Accent, light.Accent);
        Assert.False(dark.IsFromRevit);
    }

    [Fact]
    public void RevitColorsWinAndMissingNamesFallBackToTheSnapshot()
    {
        var palette = RevitPalette.FromRevitColors(RevitTheme.Dark, new Dictionary<string, Color> { ["OKButtonFaceColor"] = Colors.Orange });
        Assert.True(palette.IsFromRevit);
        Assert.Equal(Colors.Orange, palette.Accent);
        Assert.Equal(RevitPalette.Default(RevitTheme.Dark).Background, palette.Background);
        Assert.Throws<KeyNotFoundException>(() => palette["NoSuchColor"]);
    }

    [Fact]
    public void OutsideRevitTheCurrentPaletteIsTheSnapshot() => Sta.Run(() =>
    {
        var palette = RevitThemeService.GetCurrentPalette(() => "Dark");
        Assert.Equal(RevitTheme.Dark, palette.Theme);
        Assert.False(palette.IsFromRevit);
        Assert.Equal(RevitPalette.Default(RevitTheme.Dark).Background, palette.Background);
    });

    [Fact]
    public void PublishesEverySemanticColorWithAFrozenBrush() => Sta.Run(() =>
    {
        var resources = new RevitThemeResourceDictionary(RevitPalette.Default(RevitTheme.Dark), includeControlStyles: false);
        foreach (var (key, revitName) in RevitPalette.SemanticKeys)
        {
            var color = Assert.IsType<Color>(resources[key]);
            Assert.Equal(resources.Palette[revitName], color);
            var brush = Assert.IsType<SolidColorBrush>(resources[RevitThemeResourceDictionary.BrushKey(key)]);
            Assert.True(brush.IsFrozen);
            Assert.Equal(color, brush.Color);
        }
        Assert.Equal("Revit.BackgroundBrush", RevitThemeResourceDictionary.BrushKey("Revit.BackgroundColor"));
    });

    [Fact]
    public void ControlStylesAreOptional() => Sta.Run(() =>
    {
        EnsurePackScheme();
        var colorsOnly = new RevitThemeResourceDictionary(RevitPalette.Default(RevitTheme.Light), includeControlStyles: false);
        Assert.Null(colorsOnly[typeof(System.Windows.Controls.Button)]);
        Assert.Empty(colorsOnly.MergedDictionaries);

        colorsOnly.IncludeControlStyles = true;
        Assert.IsType<Style>(colorsOnly[typeof(System.Windows.Controls.Button)]);
    });

    [Fact]
    public void SetThemeSwitchesColors() => Sta.Run(() =>
    {
        var resources = new RevitThemeResourceDictionary(RevitPalette.Default(RevitTheme.Light), includeControlStyles: false);
        resources.SetTheme(RevitTheme.Dark);
        Assert.Equal(RevitTheme.Dark, resources.CurrentTheme);
        Assert.Equal(RevitPalette.Default(RevitTheme.Dark).Background, (Color)resources["Revit.BackgroundColor"]);
    });

    [Fact]
    public void ApplyToAddsOneDictionaryAndRefreshesIt() => Sta.Run(() =>
    {
        EnsurePackScheme();
        var target = new ResourceDictionary();
        var first = RevitThemeResourceDictionary.ApplyTo(target, () => "Light");
        var second = RevitThemeResourceDictionary.ApplyTo(target, () => "Dark");
        Assert.Same(first, second);
        Assert.Single(target.MergedDictionaries);
        Assert.Equal(RevitTheme.Dark, second.CurrentTheme);
    });

    [Fact]
    public void AutoRefreshAppliesOnTheThreadThatOwnsTheDictionary()
    {
        // Revit raises ThemeChanged on its own thread; a window on another UI thread must get the colors on its thread.
        RevitThemeResourceDictionary? resources = null;
        Dispatcher? owner = null;
        var ready = new ManualResetEventSlim();
        var theme = "Light";
        var thread = new Thread(() =>
        {
            resources = new RevitThemeResourceDictionary(RevitPalette.Default(RevitTheme.Light), includeControlStyles: false);
            resources.EnableAutoRefresh(() => theme);
            owner = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        try
        {
            theme = "Dark";
            RevitThemeService.RaiseThemeChanged();
            var applied = owner!.Invoke(() => resources!.CurrentTheme, DispatcherPriority.Background);
            Assert.Equal(RevitTheme.Dark, applied);
        }
        finally
        {
            owner!.Invoke(() => resources!.DisableAutoRefresh());
            owner.InvokeShutdown();
            thread.Join();
        }
    }

    private static void EnsurePackScheme()
    {
        if (!UriParser.IsKnownScheme("pack"))
        {
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        }

        if (Application.Current is null)
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }
}

internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
