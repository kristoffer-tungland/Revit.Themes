# Revit.Themes

A small WPF library that gives add-in windows Revit's own colors. It follows Revit's light or dark UI theme and publishes the colors as resources. Implicit Revit-like control styles are optional.

## Supported Revit versions

- Revit 2019-2027. Revit 2024 and later have a dark UI theme; earlier versions are always light.

Theme detection uses reflection, so one build runs across versions where the theme APIs differ.

## Where the colors come from

Revit keeps its theme colors in `UIFramework` (`themes/themecolorlight.xaml` and `themes/themecolordark.xaml`). When the library runs inside Revit, it reads them from there, so the colors match the running version. Outside Revit, or when a version lacks them, it uses a built-in snapshot of Revit 2026's colors. `RevitPalette.IsFromRevit` tells you which one you got.

## Resources

Each color is published as `Revit.<Name>Color`, with a frozen `Revit.<Name>Brush` next to it:

| Key | Revit color | Light | Dark |
| --- | --- | --- | --- |
| `Revit.BackgroundColor` | `HigStatusBarBkg` | `#F5F5F5` | `#3B4453` |
| `Revit.ControlBackgroundColor` | `PushButtonFaceColor` | `#EEEEEE` | `#2E3440` |
| `Revit.InputBackgroundColor` | `AGridInteriorColor` | `#FFFFFF` | `#3B4453` |
| `Revit.HeaderBackgroundColor` | `AGridHeaderColor` | `#E6E6E6` | `#222933` |
| `Revit.HoverColor` | `PushButtonHoverColor` | `#DEDEDE` | `#434C5A` |
| `Revit.PressedColor` | `PushButtonPressedColor` | `#D2D2D2` | `#515C6B` |
| `Revit.SelectionColor` | `ScheduleHeaderSelectedColor` | `#D4D4D4` | `#444E5C` |
| `Revit.BorderColor` | `ComboBorderColor` | `#CCCCCC` | `#687689` |
| `Revit.SubtleBorderColor` | `AGridBorderColor` | `#ECECEC` | `#485263` |
| `Revit.FocusBorderColor` | `PushButtonBorderFocusColor` | `#0696D7` | `#38ABDF` |
| `Revit.ForegroundColor` | `PushButtonTextColor` | `#3C3C3C` | `#F5F5F5` |
| `Revit.SecondaryForegroundColor` | `AGridDisabledTextColor` | `#808080` | `#989CA4` |
| `Revit.DisabledForegroundColor` | `PushButtonDisabledTextColor` | `#A7A7A7` | `#7E8188` |
| `Revit.HighlightColor` (accent) | `OKButtonFaceColor` | `#0696D7` | `#0696D7` |
| `Revit.AccentHoverColor` | `OKButtonHoverColor` | `#1893CA` | `#21A0DB` |
| `Revit.AccentPressedColor` | `OKButtonPressedColor` | `#2490C1` | `#33A5DC` |
| `Revit.AccentDisabledColor` | `OKButtonDisabledColor` | `#83CBEB` | `#26739C` |
| `Revit.HyperlinkColor` | `HigHyperlinkText` | `#006EAF` | `#6DD2FF` |

Every other Revit theme color is available in code through `RevitPalette.RevitColors` or `palette["OKButtonFaceColor"]`.

## Usage

```csharp
using Revit.Themes;

// On Revit's thread: read the theme and colors, then add them to a window or the application.
var palette = RevitThemeService.GetCurrentPalette();
window.Resources.MergedDictionaries.Add(new RevitThemeResourceDictionary(palette));
```

`RevitThemeResourceDictionary.ApplyTo(Application.Current.Resources)` adds the dictionary once and refreshes it on later calls.

### Colors only

When another control library styles the controls (for example WPF-UI), turn off the implicit styles and use the colors and brushes:

```csharp
var resources = new RevitThemeResourceDictionary(palette, includeControlStyles: false);
```

You can also take the colors in code, for example to map them onto another library's resource keys:

```csharp
var palette = RevitThemeService.GetCurrentPalette();
var accent = palette.Accent;
var panel = palette.Background;
```

### Threads

Reading Revit's colors needs an STA thread, such as Revit's main thread. If your windows run on their own UI threads, read the palette on Revit's thread and pass it in, as above.

## Automatic refresh on theme change

```csharp
RevitThemeService.Attach(uiApplication); // optional: listen on your UIApplication (Revit 2024+)
var themeResources = RevitThemeResourceDictionary.ApplyTo(window.Resources);
themeResources.EnableAutoRefresh();

// To stop listening:
themeResources.DisableAutoRefresh();
```

Revit raises `ThemeChanged` on its own thread. The dictionary reads the new colors there and applies them on the thread that called `EnableAutoRefresh`, so windows that run on their own UI threads stay safe.

To react yourself, subscribe to the service event. It is raised on Revit's thread:

```csharp
RevitThemeService.ThemeChanged += (sender, e) =>
{
    // called after Revit changes its UI theme
};
```
