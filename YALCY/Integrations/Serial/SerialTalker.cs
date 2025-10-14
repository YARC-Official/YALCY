using System;
using Avalonia;
using Dmx.Net.Controllers;
using System.Timers;
using HidSharp;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.Serial;

public class SerialTalker: IDisposable
{
    //private DmxTimer timer;
    private OpenDmxController controller;
    // DMX spec says 44 updates per second is the max
    private const float TargetFps = 44f;
    private const float TimeBetweenCalls = 1f / TargetFps;
    private static Timer? _timer;
    private static Timer? _checkerTimer;
    private static App app = (App)Application.Current!;
    private static MainWindowViewModel mainViewModel = app.MainViewModel;
    private static bool SerialEnabled = false;
    public void EnableSerialTalker(bool isEnabled)
    {
        SerialEnabled = isEnabled;
        if (SerialEnabled)
        {
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
            StatusFooter.UpdateStatus("Serial", IntegrationStatus.Connected);

            controller = new OpenDmxController();

            try
            {
                controller.Open(0);
                UsbDeviceMonitor.SerialDeviceAdded -= SerialDeviceAdded;  //disable the watchdog.

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.Elapsed += (sender, e) => Sender();
                _timer.Start();
            }
            catch (Exception e)
            {
                mainViewModel.SerialMessage = $"Error: {e.Message}";
                StatusFooter.UpdateStatus("Serial", IntegrationStatus.Error);
                UsbDeviceMonitor.SerialDeviceAdded += SerialDeviceAdded;  //start the watchdog.
            }
        }
        else
        {
            Dispose();
        }
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (controller.IsOpen == false)
        {
            try
            {
                controller.Open(0);
            }
            catch (Exception e)
            {
            }
        }
        else
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.BlueChannels.Channel != null)
                        {
                            controller.SetChannel(mainViewModel.BlueChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.GreenChannels.Channel != null)
                        {
                            controller.SetChannel(mainViewModel.GreenChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.YellowChannels.Channel != null)
                        {
                            controller.SetChannel(mainViewModel.YellowChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.RedLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.RedChannels.Channel != null)
                        {
                            controller.SetChannel(mainViewModel.RedChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;
            }
        }
    }

    private void Sender()
    {
        if (!SerialEnabled || !controller.IsOpen)
        {
            return;
        }

        controller.SetChannel(mainViewModel.GuitarNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.GuitarNotes);
        controller.SetChannel(mainViewModel.BassNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BassNotes);
        controller.SetChannel(mainViewModel.DrumNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.DrumsNotes);
        controller.SetChannel(mainViewModel.VocalsNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.VocalsNote);
        controller.SetChannel(mainViewModel.Harmony0NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony0Note);
        controller.SetChannel(mainViewModel.Harmony1NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony1Note);
        controller.SetChannel(mainViewModel.Harmony2NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony2Note);

        controller.SetChannel(mainViewModel.BpmChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BeatsPerMinute);
        controller.SetChannel(mainViewModel.KeyFrameChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Keyframe);
        controller.SetChannel(mainViewModel.VenueSizeSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.VenueSize);
        controller.SetChannel(mainViewModel.CueChangeChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.LightingCue);
        controller.SetChannel(mainViewModel.PostProcessingChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.PostProcessing);
        controller.SetChannel(mainViewModel.PauseStateSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.PauseState);
        controller.SetChannel(mainViewModel.BeatLineChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Beat);
        controller.SetChannel(mainViewModel.CurrentSingalongSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Singalong);
        controller.SetChannel(mainViewModel.CurrentSpotlightSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Spotlight);
        controller.SetChannel(mainViewModel.SongSectionSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.SongSection);
        controller.SetChannel(mainViewModel.BonusEffectChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BonusEffect);
        controller.SetChannel(mainViewModel.CurrentSceneSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.CurrentScene);

        for (int i = 0; i < 8; i++)
        {
            controller.SetChannel(mainViewModel.MasterDimmerSettings.Channel[i], (byte)mainViewModel.MasterDimmerValues.Channel[i]);
        }

        controller.WriteSafe();
    }

    public void Dispose()
    {
        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        StatusFooter.UpdateStatus("Serial", IntegrationStatus.Off);
        if (controller != null)
        {
            controller.Dispose();
        }
        _timer?.Stop();
        _timer?.Dispose();

        SerialEnabled = false;
        _timer = null;
    }

    private void SerialDeviceAdded(SerialDevice device)
    {
        //When a serial device is added, will try to start again, if the state is enabled.
        EnableSerialTalker(SettingsManager.SerialEnabledSettingIsEnabled);
    }
}
