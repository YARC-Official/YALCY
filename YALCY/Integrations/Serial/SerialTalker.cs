using System;
using Dmx.Net.Controllers;
using System.Timers;
using HidSharp;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;
#if LINUX || MACOS
using System.Linq;
#endif
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
    private MainWindowViewModel? _mainViewModel;
    private static bool SerialEnabled = false;
    public void EnableSerialTalker(bool isEnabled, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("SerialTalker: No ViewModel provided and none cached.");
            return;
        }

        SerialEnabled = isEnabled;
        if (SerialEnabled)
        {
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
            StatusFooter.UpdateStatus("Serial", IntegrationStatus.Connected);

            controller = new OpenDmxController();

            try
            {
#if LINUX || MACOS
                var devicePath = GetLinuxSerialDevicePath();
                if (string.IsNullOrWhiteSpace(devicePath))
                {
                    _mainViewModel.SerialMessage = "Error: No serial devices found for DMX output.";
                    StatusFooter.UpdateStatus("Serial", IntegrationStatus.Error);
                    UsbDeviceMonitor.SerialDeviceAdded += SerialDeviceAdded;
                    return;
                }

                controller.Open(devicePath);
#else
                controller.Open(0);
#endif
                UsbDeviceMonitor.SerialDeviceAdded -= SerialDeviceAdded;  //disable the watchdog.

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.Elapsed += (sender, e) => Sender();
                _timer.Start();
            }
            catch (Exception e)
            {
                _mainViewModel.SerialMessage = $"Error: {e.Message}";
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
        if (_mainViewModel == null) return;

        if (controller.IsOpen == false)
        {
            try
            {
#if LINUX || MACOS
                var devicePath = GetLinuxSerialDevicePath();
                if (!string.IsNullOrWhiteSpace(devicePath))
                {
                    controller.Open(devicePath);
                }
#else
                controller.Open(0);
#endif
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
                        if (_mainViewModel.BlueChannels.Channel != null)
                        {
                            controller.SetChannel(_mainViewModel.BlueChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (_mainViewModel.GreenChannels.Channel != null)
                        {
                            controller.SetChannel(_mainViewModel.GreenChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (_mainViewModel.YellowChannels.Channel != null)
                        {
                            controller.SetChannel(_mainViewModel.YellowChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;

                case StageKitTalker.CommandId.RedLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (_mainViewModel.RedChannels.Channel != null)
                        {
                            controller.SetChannel(_mainViewModel.RedChannels.Channel[i], (byte)((parameter & (1 << i)) != 0 ? 255 : 0));
                        }
                    }

                    break;
            }
        }
    }

    private void Sender()
    {
        if (!SerialEnabled || !controller.IsOpen || _mainViewModel == null)
        {
            return;
        }

        controller.SetChannel(_mainViewModel.GuitarNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.GuitarNotes);
        controller.SetChannel(_mainViewModel.BassNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BassNotes);
        controller.SetChannel(_mainViewModel.DrumNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.DrumsNotes);
        controller.SetChannel(_mainViewModel.VocalsNoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.VocalsNote);
        controller.SetChannel(_mainViewModel.Harmony0NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony0Note);
        controller.SetChannel(_mainViewModel.Harmony1NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony1Note);
        controller.SetChannel(_mainViewModel.Harmony2NoteChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Harmony2Note);

        controller.SetChannel(_mainViewModel.BpmChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BeatsPerMinute);
        controller.SetChannel(_mainViewModel.KeyFrameChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Keyframe);
        controller.SetChannel(_mainViewModel.VenueSizeSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.VenueSize);
        controller.SetChannel(_mainViewModel.CueChangeChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.LightingCue);
        controller.SetChannel(_mainViewModel.PostProcessingChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.PostProcessing);
        controller.SetChannel(_mainViewModel.PauseStateSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.PauseState);
        controller.SetChannel(_mainViewModel.BeatLineChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Beat);
        controller.SetChannel(_mainViewModel.CurrentSingalongSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Singalong);
        controller.SetChannel(_mainViewModel.CurrentSpotlightSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Spotlight);
        controller.SetChannel(_mainViewModel.SongSectionSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.SongSection);
        controller.SetChannel(_mainViewModel.BonusEffectChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BonusEffect);
        controller.SetChannel(_mainViewModel.CurrentSceneSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.CurrentScene);

        for (int i = 0; i < 8; i++)
        {
            controller.SetChannel(_mainViewModel.MasterDimmerSettings.Channel[i], (byte)_mainViewModel.MasterDimmerValues.Channel[i]);
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

#if LINUX || MACOS
    private static string? GetLinuxSerialDevicePath()
    {
        var devices = DeviceList.Local.GetSerialDevices().ToList();
        if (devices.Count == 0)
        {
            return null;
        }

        var byIdDevice = devices.FirstOrDefault(device => device.GetFileSystemName().StartsWith("/dev/serial/by-id/", StringComparison.OrdinalIgnoreCase));
        if (byIdDevice != null)
        {
            return byIdDevice.GetFileSystemName();
        }

        var ttyDevice = devices.FirstOrDefault(device => device.GetFileSystemName().StartsWith("/dev/tty", StringComparison.OrdinalIgnoreCase));
        return ttyDevice?.GetFileSystemName() ?? devices[0].GetFileSystemName();
    }
#endif

    private void SerialDeviceAdded(SerialDevice device)
    {
        //When a serial device is added, will try to start again, if the state is enabled.
        EnableSerialTalker(SettingsManager.SerialEnabledSettingIsEnabled, _mainViewModel);
    }
}
