using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ReactiveUI;
using YALCY.Udp;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{

    public ObservableCollection<UdpIntake.DatapacketMember<byte>> LightingMessageBytes { get; private set; }
    public ObservableCollection<UdpIntake.DatapacketMember<bool>> LightingMessageBools { get; private set; }
    public ObservableCollection<UdpIntake.DatapacketMember<uint>> LightingMessageUints { get; private set; }
    public ObservableCollection<UdpIntake.DatapacketMember<ushort>> LightingMessageUshorts { get; private set; }
    public ObservableCollection<UdpIntake.DatapacketMember<float>> LightingMessageFloats { get; private set; }
    public ObservableCollection<UdpIntake.IDatapacketMember> CombinedCollection { get; set; }
    private ushort _udpListenPort;
    private int _fogDurationPercent;
    private int _yargPacketTimeoutSeconds;
    public ushort UdpListenPort
    {
        get => _udpListenPort;
        set => this.RaiseAndSetIfChanged(ref _udpListenPort, value);
    }

    public int FogDurationPercent
    {
        get => _fogDurationPercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            this.RaiseAndSetIfChanged(ref _fogDurationPercent, normalized);
            UdpIntake.FogDurationPercent = normalized;
        }
    }

    public int YargPacketTimeoutSeconds
    {
        get => _yargPacketTimeoutSeconds;
        set
        {
            var normalized = Math.Clamp(value, 1, 30);
            this.RaiseAndSetIfChanged(ref _yargPacketTimeoutSeconds, normalized);
            if (SafetyController != null)
            {
                SafetyController.Timeout = TimeSpan.FromSeconds(normalized);
            }
        }
    }

    public void ResetUdpListenPort()
    {
        UdpListenPort = SettingsManager.DefaultUdpListenPort;
    }

    private void FeedInUdpSettings()
    {
        UdpListenPort = SettingsManager.UdpListenPort;
        FogDurationPercent = SettingsManager.FogDurationPercent;
        YargPacketTimeoutSeconds = SettingsManager.YargPacketTimeoutSeconds;
    }

    private void InitializeUdpIntakeCollections()
    {

        LightingMessageUints = new ObservableCollection<UdpIntake.DatapacketMember<uint>>();
        LightingMessageUints.Add(UdpIntake.Header);

        LightingMessageUshorts = new ObservableCollection<UdpIntake.DatapacketMember<ushort>>();
        LightingMessageUshorts.Add(UdpIntake.PlayerStarPowerCount);
        LightingMessageUshorts.Add(UdpIntake.FogRemainingCentiseconds);

        LightingMessageBytes= new ObservableCollection<UdpIntake.DatapacketMember<byte>>();
        LightingMessageBytes.Add(UdpIntake.DatagramVersion);
        LightingMessageBytes.Add(UdpIntake.Platform);
        LightingMessageBytes.Add(UdpIntake.CurrentScene);
        LightingMessageBytes.Add(UdpIntake.Paused);
        LightingMessageBytes.Add(UdpIntake.Venue);
        LightingMessageBytes.Add(UdpIntake.CurrentSongSection);

        LightingMessageBytes.Add(UdpIntake.CurrentGuitarNotes);
        LightingMessageBytes.Add(UdpIntake.CurrentBassNotes);
        LightingMessageBytes.Add(UdpIntake.CurrentDrumNotes);
        LightingMessageBytes.Add(UdpIntake.CurrentKeysNotes);

        LightingMessageBytes.Add(UdpIntake.LightingCue);
        LightingMessageBytes.Add(UdpIntake.PostProcessing);
        LightingMessageBytes.Add(UdpIntake.StrobeState);
        LightingMessageBytes.Add(UdpIntake.Beat);
        LightingMessageBytes.Add(UdpIntake.Keyframe);
        LightingMessageBytes.Add(UdpIntake.Spotlight);
        LightingMessageBytes.Add(UdpIntake.Singalong);

        LightingMessageBytes.Add(UdpIntake.CameraCutConstraint);
        LightingMessageBytes.Add(UdpIntake.CameraCutPriority);
        LightingMessageBytes.Add(UdpIntake.CameraCutSubject);

        LightingMessageBools= new ObservableCollection<UdpIntake.DatapacketMember<bool>>();
        LightingMessageBools.Add(UdpIntake.BonusEffect);
        LightingMessageBools.Add(UdpIntake.FogState);
        LightingMessageBools.Add(UdpIntake.AutoGen);

        LightingMessageFloats = new ObservableCollection<UdpIntake.DatapacketMember<float>>();
        LightingMessageFloats.Add(UdpIntake.BeatsPerMinute);
        LightingMessageFloats.Add(UdpIntake.CurrentVocalNote);
        LightingMessageFloats.Add(UdpIntake.CurrentHarmony0Note);
        LightingMessageFloats.Add(UdpIntake.CurrentHarmony1Note);
        LightingMessageFloats.Add(UdpIntake.CurrentHarmony2Note);



        CombinedCollection = new ObservableCollection<UdpIntake.IDatapacketMember>(LightingMessageUints.Concat(LightingMessageBytes.Cast<UdpIntake.IDatapacketMember>()).Concat(LightingMessageUshorts).Concat(LightingMessageBools).Concat(LightingMessageFloats));

    }
}
