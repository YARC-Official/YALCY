using System;
using System.Collections.Concurrent;
using System.Timers;
using Avalonia;
using DynamicData.Tests;
using Haukcode.sACN;
using YALCY.ViewModels;

namespace YALCY.Scripts.DMX
{
    public class DmxTalker
    {
        // DMX spec says 44 updates per second is the max
        private const float TargetFps = 44f;
        private const float TimeBetweenCalls = 1f / TargetFps;

        // Each universe supports up to 512 channels
        private const int UniverseSize = 512;

        private const string AcnSourceName = "YARG";

        // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private static readonly Guid AcnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");

        private static SACNClient? _sendClient;

        private readonly byte[] _currentDataPacket = new byte[UniverseSize];
        private ConcurrentQueue<byte>[] byteQueues;

        private static Timer? _timer;

        private byte BpmLastItemAdded = 0;
        private byte CueChangeLastItemAdded = 0;
        private byte BeatLineLastItemAdded = 0;
        private byte BonusEffectLastItemAdded = 0;
        private byte KeyFrameLastItemAdded = 0;
        private byte DrumNoteLastItemAdded = 0;
        private byte PostProcessingLastItemAdded = 0;
        private byte GuitarNoteLastItemAdded = 0;
        private byte BassNoteLastItemAdded = 0;
        private byte CurrentPerformerLastItemAdded = 0;
        private byte KeysNoteLastItemAdded = 0;
        private byte VocalsNoteLastItemAdded = 0;
        private byte Harmony0NoteLastItemAdded = 0;
        private byte Harmony1NoteLastItemAdded = 0;
        private byte Harmony2NoteLastItemAdded = 0;
        private byte CurrentSceneLastItemAdded = 0;
        private byte VenueSizeLastItemAdded = 0;
        private byte PauseStateLastItemAdded = 0;
        private byte SongSectionLastItemAdded = 0;

        // Access the MainViewModel instance
        App app;
        MainWindowViewModel mainViewModel;

        public void EnableDmxTalker(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;

                _sendClient = new SACNClient(senderId: AcnSourceId, senderName: AcnSourceName,
                    localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

                // Access the MainViewModel instance
                app = (App)Application.Current!;
                mainViewModel = app.MainViewModel;

                byteQueues = new ConcurrentQueue<byte>[UniverseSize];

                // Initialize each ConcurrentQueue in the array
                for (int i = 0; i < UniverseSize; i++)
                {
                    byteQueues[i] = new ConcurrentQueue<byte>();
                }

                //The three parts of the dmx output: stage kit channels, master dimmers, and the channels read from the udp packet
                UpdateMasterDimmers();
                mainViewModel.UdpIntake.PacketProcessed += (packet) => UpdateDataPacket(packet, mainViewModel);
                USBDeviceMonitor.OnStageKitCommand += OnStageKitEvent;

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.Elapsed += (sender, e) => Sender();
                _timer.Start();
            }
            else
            {
                if (_sendClient == null) return;

                USBDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;

                _timer?.Stop();
                _timer?.Dispose();

                // Turn everything off directly
                Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);

                // Access the MainViewModel instance, can't assume it was set in enable
                var app = (App)Application.Current!;
                var mainViewModel = app.MainViewModel;

                // Force send final packet.
                _sendClient.SendMulticast((ushort)mainViewModel.BroadcastUniverseSetting.Value, _currentDataPacket);

                _sendClient.Dispose();
                _sendClient = null;
            }
        }

        private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.BlueChannels.Channel != null)
                        {
                            byteQueues[mainViewModel.BlueChannels.Channel[i] - 1]
                                .Enqueue((parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
                        }
                    }

                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.GreenChannels.Channel != null)
                        {
                            byteQueues[mainViewModel.GreenChannels.Channel[i] - 1]
                                .Enqueue((parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
                        }
                    }

                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.BlueChannels.Channel != null)
                        {
                            byteQueues[mainViewModel.BlueChannels.Channel[i] - 1]
                                .Enqueue((parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
                        }
                    }

                    break;

                case StageKitTalker.CommandId.RedLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mainViewModel.RedChannels.Channel != null)
                        {
                            byteQueues[mainViewModel.RedChannels.Channel[i] - 1]
                                .Enqueue((parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
                        }
                    }

                    break;
            }
        }

        private void Sender()
        {
            for (int i = 0; i < UniverseSize; i++)
            {
                if (byteQueues[i].TryDequeue(out byte value))
                {
                    _currentDataPacket[i] = value;
                }
            }

            // Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
            _sendClient?.SendMulticast((ushort)mainViewModel.BroadcastUniverseSetting.Value, _currentDataPacket);
        }

        public void UpdateMasterDimmers()
        {
            for (int i = 0; i < mainViewModel.MasterDimmerSettings.Channel.Length; i++)
            {
                SetChannelToValue(mainViewModel.MasterDimmerSettings.Channel[i],
                    (byte)mainViewModel.MasterDimmerValues.Channel[i]);
            }
        }

        public void SetChannelToValue(int DmxChannel, byte value)
        {
            _currentDataPacket[DmxChannel - 1] = value;
        }

        private void UpdateDataPacket(byte[] udpBuffer, MainWindowViewModel viewModel)
        {
            if (udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute] != BpmLastItemAdded)
            {
                byteQueues[viewModel.BpmChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                BpmLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue] != CueChangeLastItemAdded)
            {
                byteQueues[viewModel.CueChangeChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue]);
                CueChangeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Beat] != BeatLineLastItemAdded)
            {
                byteQueues[viewModel.BeatLineChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Beat]);
                BeatLineLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Beat];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect] != BonusEffectLastItemAdded)
            {
                byteQueues[viewModel.BonusEffectChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect]);
                BonusEffectLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe] != KeyFrameLastItemAdded)
            {
                byteQueues[viewModel.KeyFrameChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe]);
                KeyFrameLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes] != DrumNoteLastItemAdded)
            {
                byteQueues[viewModel.DrumNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes]);
                DrumNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing] != PostProcessingLastItemAdded)
            {
                byteQueues[viewModel.PostProcessingChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing]);
                PostProcessingLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes] != GuitarNoteLastItemAdded)
            {
                byteQueues[viewModel.GuitarNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes]);
                GuitarNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes] != BassNoteLastItemAdded)
            {
                byteQueues[viewModel.BassNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes]);
                BassNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Performer] != CurrentPerformerLastItemAdded)
            {
                byteQueues[viewModel.CurrentPerformerSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Performer]);
                CurrentPerformerLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Performer];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes] != KeysNoteLastItemAdded)
            {
                byteQueues[viewModel.KeysNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes]);
                KeysNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote] != VocalsNoteLastItemAdded)
            {
                byteQueues[viewModel.VocalsNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote]);
                VocalsNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note] != Harmony0NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony0NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note]);
                Harmony0NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note] != Harmony1NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony1NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note]);
                Harmony1NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note] != Harmony2NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony2NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note]);
                Harmony2NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene] != CurrentSceneLastItemAdded)
            {
                byteQueues[viewModel.CurrentSceneSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene]);
                CurrentSceneLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize] != VenueSizeLastItemAdded)
            {
                byteQueues[viewModel.VenueSizeSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize]);
                VenueSizeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.PauseState] != PauseStateLastItemAdded)
            {
                byteQueues[viewModel.PauseStateSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PauseState]);
                PauseStateLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PauseState];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.SongSection] != SongSectionLastItemAdded)
            {
                byteQueues[viewModel.SongSectionSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.SongSection]);
                SongSectionLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.SongSection];
            }
        }
    }
}
