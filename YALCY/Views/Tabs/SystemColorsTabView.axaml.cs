using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using YALCY.ViewModels;

namespace YALCY.Views.Tabs;

public partial class SystemColorsTabView : UserControl
{
    public SystemColorsTabView()
    {
        InitializeComponent();
        DataContext = new SystemColorsViewModel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is SystemColorsViewModel vm)
        {
            AccentHelper.UpdateApplicationAccentColors(Application.Current);
            vm.RefreshColors();
        }
    }

    private void OnRefreshColorsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SystemColorsViewModel vm)
        {
            AccentHelper.UpdateApplicationAccentColors(Application.Current);
            vm.RefreshColors();
        }
    }

    private async void OnCopyColorHexClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SystemColorItem item)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(item.HexRgb);
            }
        }
    }

    private void OnCategoryFilterClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string category && DataContext is SystemColorsViewModel vm)
        {
            vm.SelectedCategory = category;
        }
    }
}
