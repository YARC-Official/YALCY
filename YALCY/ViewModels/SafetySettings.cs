using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Safety;
using YALCY.Usb;
using YALCY.Views.Components;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string _yargStreamStatus = "Waiting for YARG";
    private bool _isManualBlackout;
    private string _blackoutButtonText = "BLACKOUT";

    internal LightingSafetyController SafetyController { get; private set; } = null!;

    public string YargStreamStatus
    {
        get => _yargStreamStatus;
        private set => this.RaiseAndSetIfChanged(ref _yargStreamStatus, value);
    }

    public bool IsManualBlackout
    {
        get => _isManualBlackout;
        private set => this.RaiseAndSetIfChanged(ref _isManualBlackout, value);
    }

    public string BlackoutButtonText
    {
        get => _blackoutButtonText;
        private set => this.RaiseAndSetIfChanged(ref _blackoutButtonText, value);
    }

    public ICommand ToggleBlackoutCommand { get; private set; } = null!;

    private void InitializeSafety()
    {
        SafetyController = new LightingSafetyController(
            TimeProvider.System,
            new MainWindowLightingSafetyActions(this),
            TimeSpan.FromSeconds(SettingsManager.YargPacketTimeoutSeconds));
        SafetyController.StateChanged += OnSafetyStateChanged;
        UdpIntake.SetSafetyController(SafetyController);
        ToggleBlackoutCommand = ReactiveCommand.Create(() =>
            SafetyController.SetManualBlackout(!SafetyController.IsManualBlackout));
        OnSafetyStateChanged(SafetyController.State);
    }

    private void OnSafetyStateChanged(LightingSafetyState state)
    {
        void ApplyState()
        {
            YargStreamStatus = state switch
            {
                LightingSafetyState.WaitingForYarg => "Waiting for YARG",
                LightingSafetyState.Receiving => "Receiving",
                LightingSafetyState.TimedOut => "Timed out—outputs blacked out",
                LightingSafetyState.ManualBlackout => "Manual blackout",
                _ => "Waiting for YARG"
            };
            IsManualBlackout = state == LightingSafetyState.ManualBlackout;
            BlackoutButtonText = IsManualBlackout ? "RESUME OUTPUT" : "BLACKOUT";
            StatusFooter.UpdateStatus("UDP", state switch
            {
                LightingSafetyState.WaitingForYarg => IntegrationStatus.Connecting,
                LightingSafetyState.Receiving => IntegrationStatus.Connected,
                LightingSafetyState.TimedOut => IntegrationStatus.Error,
                LightingSafetyState.ManualBlackout => IntegrationStatus.Error,
                _ => IntegrationStatus.Off
            });
        }

        if (Application.Current?.ApplicationLifetime != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyState);
        }
        else
        {
            ApplyState();
        }
    }

    private sealed class MainWindowLightingSafetyActions : ILightingSafetyActions
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindowLightingSafetyActions(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void EnterSafeMode()
        {
            UsbDeviceMonitor.EnterSafetyBlackout();
            _viewModel.StageKitTalker.SuspendCurrentCue();
            _viewModel.UdpIntake.EnterSafetyBlackout();
        }

        public void ExitSafeMode(bool replayState)
        {
            UsbDeviceMonitor.ExitSafetyBlackout();
            if (replayState)
            {
                _viewModel.UdpIntake.ReplayOutputState();
            }
        }
    }
}
