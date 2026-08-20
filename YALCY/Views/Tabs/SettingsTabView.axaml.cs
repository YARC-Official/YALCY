using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace YALCY.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    public SettingsTabView()
    {
        InitializeComponent();
    }

    private void OnResetDmxChannelsClicked(object? sender, RoutedEventArgs e)
    {
        if (ResetConfirmationOverlay != null)
        {
            ResetConfirmationOverlay.IsVisible = true;
        }
    }

    private void OnCancelResetDmxClicked(object? sender, RoutedEventArgs e)
    {
        if (ResetConfirmationOverlay != null)
        {
            ResetConfirmationOverlay.IsVisible = false;
        }
    }

    private void OnConfirmResetDmxClicked(object? sender, RoutedEventArgs e)
    {
        if (ResetConfirmationOverlay != null)
        {
            ResetConfirmationOverlay.IsVisible = false;
        }

        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.ResetDmxChannelsToDefaults();
        }

        ShowResetFeedback();
    }

    private async void ShowResetFeedback()
    {
        if (DmxResetFeedbackBanner != null)
        {
            DmxResetFeedbackBanner.IsVisible = true;
            await Task.Delay(4000);
            DmxResetFeedbackBanner.IsVisible = false;
        }
    }
}
