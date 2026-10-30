using YALCY.Lighting;
using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Safety;
using YALCY.Views.Components;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string _yargStreamStatus = "Waiting for YARG";
    private bool _isManualBlackout;
    private bool _isBlackoutActive;
    private string _blackoutExplanation = string.Empty;
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

    public string BlackoutButtonBackground => IsManualBlackout ? "#FFFFFF" : "#B00020";
    public string BlackoutButtonForeground => IsManualBlackout ? "#000000" : "#FFFFFF";

    public ICommand ToggleBlackoutCommand { get; private set; } = null!;
    public ICommand ResumeOutputCommand { get; private set; } = null!;
    public bool IsBlackoutActive
    {
        get => _isBlackoutActive;
        private set => this.RaiseAndSetIfChanged(ref _isBlackoutActive, value);
    }
    public string BlackoutExplanation
    {
        get => _blackoutExplanation;
        private set => this.RaiseAndSetIfChanged(ref _blackoutExplanation, value);
    }

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
        ResumeOutputCommand = ReactiveCommand.Create(() => SafetyController.SetManualBlackout(false));
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
            IsBlackoutActive = state is LightingSafetyState.ManualBlackout or LightingSafetyState.TimedOut;
            BlackoutExplanation = IsManualBlackout
                ? "All lighting output and color tests are suppressed. Select Resume Output to release manual blackout."
                : "The YARG stream stopped or timed out. All lighting output is suppressed and will resume automatically when valid YARG data returns.";
            BlackoutButtonText = IsManualBlackout ? "RESUME OUTPUT" : "BLACKOUT";
            this.RaisePropertyChanged(nameof(BlackoutButtonBackground));
            this.RaisePropertyChanged(nameof(BlackoutButtonForeground));
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
            LightingOutput.EnterSafetyBlackout();
            _viewModel.LightingEngine.SuspendCurrentCue();
            _viewModel.UdpIntake.EnterSafetyBlackout();
        }

        public void ExitSafeMode(bool replayState)
        {
            LightingOutput.ExitSafetyBlackout();
            if (replayState)
            {
                _viewModel.UdpIntake.ReplayOutputState();
            }
        }
    }
}
