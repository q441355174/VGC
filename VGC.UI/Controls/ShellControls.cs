using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace VGC.Views.Controls;

public sealed class ToolDrawerHost : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ToolDrawerHost, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> IconTextProperty =
        AvaloniaProperty.Register<ToolDrawerHost, string>(nameof(IconText), string.Empty);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ToolDrawerHost, bool>(nameof(IsOpen));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconText
    {
        get => GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }
}

public sealed class SettingsPageHost : ContentControl
{
    public static readonly StyledProperty<int> SectionFilterProperty =
        AvaloniaProperty.Register<SettingsPageHost, int>(nameof(SectionFilter), -1);

    public int SectionFilter
    {
        get => GetValue(SectionFilterProperty);
        set => SetValue(SectionFilterProperty, value);
    }
}

public sealed class SectionButton : ToggleButton
{
    public static readonly StyledProperty<bool> ExpandableProperty =
        AvaloniaProperty.Register<SectionButton, bool>(nameof(Expandable));

    public static readonly StyledProperty<bool> ExpandedProperty =
        AvaloniaProperty.Register<SectionButton, bool>(nameof(Expanded));

    public bool Expandable
    {
        get => GetValue(ExpandableProperty);
        set => SetValue(ExpandableProperty, value);
    }

    public bool Expanded
    {
        get => GetValue(ExpandedProperty);
        set => SetValue(ExpandedProperty, value);
    }
}
