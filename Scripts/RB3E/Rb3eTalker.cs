using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using YALCY;

namespace YARG.Integration.RB3E
{
    public class Rb3eTalker
    {
        private static IPAddress IPAddress = IPAddress.Parse("255.255.255.255"); // "this" network's broadcast address
        private const int Port = 21070; // That is what RB3E uses
        private static UdpClient? _sendClient;

        public void EnableRb3eTalker(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;
                USBDeviceMonitor.OnStageKitCommand += SendPacket;
                _sendClient = new UdpClient();
            }
            else
            {
                if (_sendClient == null) return;
                USBDeviceMonitor.OnStageKitCommand -= SendPacket;
                SendPacket(StageKitTalker.CommandId.DisableAll, 0x00);

                _sendClient.Dispose();
            }
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
}
