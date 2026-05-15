using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.RB3E;

    public class Rb3eTalker
    {
        private static IPAddress IPAddress = IPAddress.Parse("255.255.255.255"); // "this" network's broadcast address
        private const int Port = 21070; // That is what RB3E uses
        private static UdpClient? _sendClient;
        private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => Console.WriteLine($"RB3E manual strobe error: {ex.Message}"));
        private MainWindowViewModel? _mainViewModel;

        public void EnableRb3eTalker(bool isEnabled, MainWindowViewModel? viewModel = null)
        {
            if (viewModel != null)
            {
                _mainViewModel = viewModel;
            }

            if (isEnabled)
            {
                if (_sendClient != null) return;
                UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
                StatusFooter.UpdateStatus("RB3E", IntegrationStatus.Connected);
                _sendClient = new UdpClient();
            }
            else
            {
                if (_sendClient == null) return;
                UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
                _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                StatusFooter.UpdateStatus("RB3E", IntegrationStatus.Off);
                SendPacket(StageKitTalker.CommandId.DisableAll, 0x00);

                _sendClient.Dispose();
                _sendClient = null;
            }
        }

        private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.StrobeSlow:
                case StageKitTalker.CommandId.StrobeMedium:
                case StageKitTalker.CommandId.StrobeFast:
                case StageKitTalker.CommandId.StrobeFastest:
                    if (_mainViewModel?.Rb3eStrobeMode == StrobeOutputModes.ManualFlash)
                    {
                        _manualStrobeFlasher.Start(
                            commandId,
                            UdpIntake.BeatsPerMinute.Value,
                            SetManualStrobeStateAsync);
                        return;
                    }

                    _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                    SendPacket(commandId, parameter);
                    return;

                case StageKitTalker.CommandId.StrobeOff:
                    _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                    SendPacket(commandId, parameter);
                    return;

                case StageKitTalker.CommandId.DisableAll:
                    _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                    SendPacket(commandId, parameter);
                    return;

                default:
                    SendPacket(commandId, parameter);
                    return;
            }
        }

        private Task SetManualStrobeStateAsync(bool isOn, CancellationToken cancellationToken)
        {
            SendPacket(isOn ? StageKitTalker.CommandId.StrobeFastest : StageKitTalker.CommandId.StrobeOff, 0x00);
            return Task.CompletedTask;
        }

        private static void SendPacket(StageKitTalker.CommandId commandId, byte parameter)
        {
            var packetData = new byte[]
            {
                0x52, 0x42, 0x33, 0x45, // Magic
                0x00, // Version
                0x06, // Packet type (RB3E_EVENT_STAGEKIT)
                0x02, // Packet size
                0x80, // Platform (RB3E_PLATFORM_YARG)
                parameter, // Left stagekit channel, parameter ID
                (byte)commandId // Right stagekit channel, command ID
            };

            _sendClient?.Send(packetData, packetData.Length, IPAddress.ToString(), Port);
        }
    }
