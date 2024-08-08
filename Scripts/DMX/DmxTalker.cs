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

        private byte _bpmLastItemAdded = 0;
        private byte _cueChangeLastItemAdded = 0;
        private byte _beatLineLastItemAdded = 0;
        private byte _bonusEffectLastItemAdded = 0;
        private byte _keyFrameLastItemAdded = 0;
        private byte _drumNoteLastItemAdded = 0;
        private byte _postProcessingLastItemAdded = 0;
        private byte _guitarNoteLastItemAdded = 0;
        private byte _bassNoteLastItemAdded = 0;
        private byte _currentPerformerLastItemAdded = 0;
        private byte _keysNoteLastItemAdded = 0;
        private byte _vocalsNoteLastItemAdded = 0;
        private byte _harmony0NoteLastItemAdded = 0;
        private byte _harmony1NoteLastItemAdded = 0;
        private byte _harmony2NoteLastItemAdded = 0;
        private byte _currentSceneLastItemAdded = 0;
        private byte _venueSizeLastItemAdded = 0;
        private byte _pauseStateLastItemAdded = 0;
        private byte _songSectionLastItemAdded = 0;

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
                //UpdateMasterDimmers();
                mainViewModel.UdpIntake.PacketProcessed += (packet) => UpdateDataPacket(packet, mainViewModel);
                UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.Elapsed += (sender, e) => Sender();
                _timer.Start();
            }
            else
            {
                if (_sendClient == null) return;

                UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;

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

        public void SetChannelToValue(int dmxChannel, byte value)
        {
            _currentDataPacket[dmxChannel - 1] = value;
        }

        private void UpdateDataPacket(byte[] udpBuffer, MainWindowViewModel viewModel)
        {
            if (udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute] != _bpmLastItemAdded)
            {
                byteQueues[viewModel.BpmChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                _bpmLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue] != _cueChangeLastItemAdded)
            {
                byteQueues[viewModel.CueChangeChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue]);
                _cueChangeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Beat] != _beatLineLastItemAdded)
            {
                byteQueues[viewModel.BeatLineChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Beat]);
                _beatLineLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Beat];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect] != _bonusEffectLastItemAdded)
            {
                byteQueues[viewModel.BonusEffectChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect]);
                _bonusEffectLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe] != _keyFrameLastItemAdded)
            {
                byteQueues[viewModel.KeyFrameChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe]);
                _keyFrameLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes] != _drumNoteLastItemAdded)
            {
                byteQueues[viewModel.DrumNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes]);
                _drumNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing] != _postProcessingLastItemAdded)
            {
                byteQueues[viewModel.PostProcessingChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing]);
                _postProcessingLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes] != _guitarNoteLastItemAdded)
            {
                byteQueues[viewModel.GuitarNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes]);
                _guitarNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes] != _bassNoteLastItemAdded)
            {
                byteQueues[viewModel.BassNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes]);
                _bassNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Performer] != _currentPerformerLastItemAdded)
            {
                byteQueues[viewModel.CurrentPerformerSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Performer]);
                _currentPerformerLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Performer];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes] != _keysNoteLastItemAdded)
            {
                byteQueues[viewModel.KeysNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes]);
                _keysNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote] != _vocalsNoteLastItemAdded)
            {
                byteQueues[viewModel.VocalsNoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote]);
                _vocalsNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.VocalsNote];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note] != _harmony0NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony0NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note]);
                _harmony0NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony0Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note] != _harmony1NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony1NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note]);
                _harmony1NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony1Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note] != _harmony2NoteLastItemAdded)
            {
                byteQueues[viewModel.Harmony2NoteChannelSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note]);
                _harmony2NoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Harmony2Note];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene] != _currentSceneLastItemAdded)
            {
                byteQueues[viewModel.CurrentSceneSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene]);
                _currentSceneLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize] != _venueSizeLastItemAdded)
            {
                byteQueues[viewModel.VenueSizeSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize]);
                _venueSizeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.PauseState] != _pauseStateLastItemAdded)
            {
                byteQueues[viewModel.PauseStateSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PauseState]);
                _pauseStateLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PauseState];
            }

            if (udpBuffer[(int)UdpIntake.ByteIndexName.SongSection] != _songSectionLastItemAdded)
            {
                byteQueues[viewModel.SongSectionSetting.Value - 1]
                    .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.SongSection]);
                _songSectionLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.SongSection];
            }
        }
    }
}
