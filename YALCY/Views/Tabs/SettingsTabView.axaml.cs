using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace YALCY.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    public SettingsTabView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += (s, e) => UpdateLayoutState(this.Bounds.Width);
        this.SizeChanged += SettingsTabView_SizeChanged;
    }

    private void SettingsTabView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayoutState(e.NewSize.Width);
    }

    private void UpdateLayoutState(double width)
    {
        if (IntegrationsGrid != null)
        {
            if (width < 700)
            {
                IntegrationsGrid.Columns = 1;
            }
            else if (width < 1050)
            {
                IntegrationsGrid.Columns = 2;
            }
            else
            {
                IntegrationsGrid.Columns = 3;
            }
        }
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
