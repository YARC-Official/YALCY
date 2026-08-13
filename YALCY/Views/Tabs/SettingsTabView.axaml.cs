using Avalonia.Controls;
using Avalonia.Interactivity;
using YALCY.ViewModels;

namespace YALCY.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    public SettingsTabView()
    {
        InitializeComponent();
    }

    private void OnResetDmxChannelsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ResetDmxChannelsToDefaults();
        }
    }
}
