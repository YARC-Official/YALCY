using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace YALCY.Views.Components;

public partial class TabHeroHeader : UserControl
{
    public static readonly StyledProperty<string?> HeaderTitleProperty =
        AvaloniaProperty.Register<TabHeroHeader, string?>(nameof(HeaderTitle));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<TabHeroHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<TabHeroHeader, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<object?> BadgeContentProperty =
        AvaloniaProperty.Register<TabHeroHeader, object?>(nameof(BadgeContent));

    public static readonly StyledProperty<IBrush?> BadgeBackgroundProperty =
        AvaloniaProperty.Register<TabHeroHeader, IBrush?>(nameof(BadgeBackground));

    public static readonly StyledProperty<IBrush?> BadgeBorderBrushProperty =
        AvaloniaProperty.Register<TabHeroHeader, IBrush?>(nameof(BadgeBorderBrush));

    public string? HeaderTitle
    {
        get => GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public object? BadgeContent
    {
        get => GetValue(BadgeContentProperty);
        set => SetValue(BadgeContentProperty, value);
    }

    public IBrush? BadgeBackground
    {
        get => GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public IBrush? BadgeBorderBrush
    {
        get => GetValue(BadgeBorderBrushProperty);
        set => SetValue(BadgeBorderBrushProperty, value);
    }

    public TabHeroHeader()
    {
        InitializeComponent();
    }
}
