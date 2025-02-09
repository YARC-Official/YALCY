using System;
using Avalonia;
using Dmx.Net.Controllers;
using System.Timers;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using YALCY.ViewModels;

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
            Console.WriteLine("Serial Enable called.");
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;

            controller = new OpenDmxController();

            try
            {
                controller.Open(0);
                mainViewModel.SerialEnabledSetting.IsEnabled = true; // Enable the UI button
                StartChecker(); // Start checking connection status
            }
            catch (Exception e)
            {
                mainViewModel.SerialMessage = $"Error: {e.Message}";
                mainViewModel.SerialEnabledSetting.IsEnabled = false; // Disable the button
                StartChecker(); // Start checking if a new device is plugged in
            }

            _timer = new Timer(TimeBetweenCalls * 1000);
            _timer.Elapsed += (sender, e) => Sender();
            _timer.Start();
        }
        else
        {
            mainViewModel.SerialEnabledSetting.IsEnabled = false; // Disable the button
            StopChecker();
            Dispose();
        }
    }

    private void StartChecker()
    {
        if (_checkerTimer != null) return; // Prevent multiple timers from running

        _checkerTimer = new Timer(3000); // Check every 3 seconds
        _checkerTimer.Elapsed += CheckSerialStatus;
        _checkerTimer.Start();
    }

    private void StopChecker()
    {
        _checkerTimer?.Stop();
        _checkerTimer?.Dispose();
        _checkerTimer = null;
    }

    private void CheckSerialStatus(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (!SerialEnabled)
            {
                return;
            }

            if (controller != null && !controller.IsOpen) // Only try to open if it's not open already
            {
                try
                {
                    controller.Open(0); // Reattempt to open if it's not open
                    Console.WriteLine("Serial reconnected!");
                    mainViewModel.SerialEnabledSetting.IsEnabled = true; // Enable UI button
                    StopChecker(); // Stop checking once it's working again
                }
                catch
                {
                    // Do not log too often or reattempt too aggressively
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking serial status: {ex.Message}");
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
        else
        {
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
            controller.SetChannel(mainViewModel.CurrentPerformerSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.Performer);
            controller.SetChannel(mainViewModel.SongSectionSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.SongSection);
            controller.SetChannel(mainViewModel.BonusEffectChannelSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.BonusEffect);
            controller.SetChannel(mainViewModel.CurrentSceneSetting.Value, (byte)Udp.UdpIntake.ByteIndexName.CurrentScene);

            for (int i = 0; i < 8; i++)
            {
                controller.SetChannel(mainViewModel.MasterDimmerSettings.Channel[i], (byte)mainViewModel.MasterDimmerValues.Channel[i]);
            }

            controller.WriteSafe();
        }
    }

    public void Dispose()
    {
        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        if (controller != null)
        {
            controller.Dispose();
        }
        _timer?.Stop();
        _timer?.Dispose();

        SerialEnabled = false;
        StopChecker();
        _timer = null;
    }
}
