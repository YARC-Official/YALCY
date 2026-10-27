using System;
using ReactiveUI;
using YALCY.Integrations.DMX;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public DmxTalker ArtNetTalker { get; } = new(useArtNet: true);
    public EnableSetting ArtNetEnabledSetting { get; private set; } = null!;
    public System.Windows.Input.ICommand RetryArtNetCommand { get; private set; } = null!;
    public bool CanConfigureArtNet => !ArtNetTalker.IsEnabled && !ArtNetTalker.IsTransitioning;
    public bool ArtNetNeedsSetup => ArtNetEnabledSetting?.IsEnabled == true && CanConfigureArtNet;
    public string DmxOutputStatus => DmxTalker.IsTransitioning || ArtNetTalker.IsTransitioning
        ? "Updating lighting outputs…"
        : FormatDmxOutputStatus(DmxTalker.IsSending, ArtNetTalker.IsSending);

    internal static string FormatDmxOutputStatus(bool sacn, bool artNet) => (sacn, artNet) switch
    {
        (true, true) => "YALCY is talking sACN and Art-Net!",
        (true, false) => "YALCY is talking sACN!",
        (false, true) => "YALCY is talking Art-Net!",
        _ => "YALCY is NOT talking sACN or Art-Net!"
    };

    internal void RefreshDmxOutputStatus()
    {
        if (!_isHeadless && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshDmxOutputStatus);
            return;
        }
        this.RaisePropertyChanged(nameof(DmxOutputStatus));
        this.RaisePropertyChanged(nameof(CanConfigureArtNet));
        this.RaisePropertyChanged(nameof(ArtNetNeedsSetup));
    }

    private string _artNetDestinationIp = "";
    public string ArtNetDestinationIp
    {
        get => _artNetDestinationIp;
        set => this.RaiseAndSetIfChanged(ref _artNetDestinationIp, value?.Trim() ?? "");
    }

    private int _artNetUniverse;
    public int ArtNetUniverse
    {
        get => _artNetUniverse;
        set => this.RaiseAndSetIfChanged(ref _artNetUniverse, Math.Clamp(value, 0, 32767));
    }

    private SacnAdapterOption? _selectedArtNetAdapter;
    public SacnAdapterOption? SelectedArtNetAdapter
    {
        get => _selectedArtNetAdapter;
        set => this.RaiseAndSetIfChanged(ref _selectedArtNetAdapter, value);
    }

    private string _artNetStatus = "Enter the receiver IP and its zero-based Art-Net universe.";
    public string ArtNetStatus
    {
        get => _artNetStatus;
        set
        {
            if (!_isHeadless && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ArtNetStatus = value);
                return;
            }
            this.RaiseAndSetIfChanged(ref _artNetStatus, value);
            RefreshDmxOutputStatus();
        }
    }

    private void InitializeArtNetSettings()
    {
        ArtNetDestinationIp = SettingsManager.ArtNetDestinationIp;
        ArtNetUniverse = SettingsManager.ArtNetUniverse;
        SelectedArtNetAdapter = new SacnAdapterOption("Auto (system routing)", null);
        foreach (var adapter in SacnAdapterOptions)
        {
            if (adapter.IpAddress == SettingsManager.ArtNetAdapterIp && adapter.IpAddress != null)
                SelectedArtNetAdapter = adapter;
        }
        // Reuse the enumerated interfaces, with an explicit automatic routing choice.
        ArtNetAdapterOptions.Add(new SacnAdapterOption("Auto (system routing)", null));
        foreach (var adapter in SacnAdapterOptions)
            if (adapter.IpAddress != null) ArtNetAdapterOptions.Add(adapter);
        if (SelectedArtNetAdapter.IpAddress == null) SelectedArtNetAdapter = ArtNetAdapterOptions[0];

        ArtNetEnabledSetting = new EnableSetting("Art-Net Enabled", SettingsManager.ArtNetEnabled,
            "Art-Net enabled", "Art-Net disabled",
            enabled => ArtNetTalker.EnableDmxTalkerAsync(enabled, this),
            "Enable Art-Net output independently of sACN");
        RetryArtNetCommand = ReactiveCommand.CreateFromTask(RetryArtNetAsync);
    }

    internal System.Threading.Tasks.Task RetryArtNetAsync() =>
        ArtNetEnabledSetting.IsEnabled && CanConfigureArtNet
            ? ArtNetTalker.EnableDmxTalkerAsync(true, this)
            : System.Threading.Tasks.Task.CompletedTask;

    public System.Collections.ObjectModel.ObservableCollection<SacnAdapterOption> ArtNetAdapterOptions { get; } = new();
}
