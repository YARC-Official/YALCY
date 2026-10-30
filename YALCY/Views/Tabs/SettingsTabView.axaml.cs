using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using System;
using YALCY.Diagnostics;
using YALCY.ViewModels;
using YALCY.Views.Windows;

namespace YALCY.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    private bool _setupWizardOpen;
    public SettingsTabView()
    {
        InitializeComponent();
    }

    private async void OnRunSetupWizardClicked(object? sender, RoutedEventArgs e)
    {
        if (_setupWizardOpen || DataContext is not MainWindowViewModel main || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        _setupWizardOpen = true;
        try
        {
            await main.InitializationTask;
            if (!owner.IsVisible || main.IsShutdownComplete) return;
            var wizard = new FirstRunSetupWindow
            {
                DataContext = new FirstRunSetupViewModel(main, isFirstRun: false)
            };
            await wizard.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Settings", $"Could not open setup wizard: {ex.Message}");
        }
        finally { _setupWizardOpen = false; }
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
