# Revit.Themes

A lightweight WPF library that applies Revit-like control styling and automatically switches between light and dark themes based on the current Revit `UITheme`.

## Supported Revit versions

- Revit 2019-2027

Theme detection uses reflection so the library can run across versions where theme APIs differ.

## Usage

```csharp
using Revit.Themes;

// Add to application or window resources.
RevitThemeResourceDictionary.ApplyTo(Application.Current.Resources);

// Optional manual refresh when Revit theme is changed.
var themeResources = RevitThemeResourceDictionary.ApplyTo(Application.Current.Resources);
themeResources.Refresh();
```

You can also set the theme explicitly:

```csharp
var resources = new RevitThemeResourceDictionary();
resources.SetTheme(RevitTheme.Dark);
```

## Automatic refresh on theme change

Call `EnableAutoRefresh` to automatically re-apply the theme whenever Revit raises its `ThemeChanged` event:

```csharp
var themeResources = RevitThemeResourceDictionary.ApplyTo(Application.Current.Resources);
themeResources.EnableAutoRefresh();

// To stop listening:
themeResources.DisableAutoRefresh();
```

You can also subscribe directly to the service event:

```csharp
RevitThemeService.ThemeChanged += (sender, e) =>
{
    // called after Revit changes the theme
};
```
