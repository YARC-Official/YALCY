using Avalonia;
using Avalonia.Controls;

namespace YALCY.Views.Components;

public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Heading));
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<EmptyState, object?>(nameof(Actions));
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    public EmptyState() => InitializeComponent();
}
