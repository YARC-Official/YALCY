using System;
using System.Net;
using System.Net.Sockets;

namespace YALCY.Integrations.DMX;

// ArtDmx output to a manually configured receiver. Art-Net is designed by
// Artistic Licence. Packet layout: https://art-net.org.uk/downloads/art-net.pdf
internal sealed class ArtNetSender : IDisposable
{
    internal const int Port = 6454;
    private readonly UdpClient _client;
    private readonly IPEndPoint _destination;
    private readonly int _universe;
    private readonly byte[] _packet = new byte[530];
    private byte _sequence;

    public ArtNetSender(string destination, IPAddress localAddress, int universe)
    {
        if (!IPAddress.TryParse(destination, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast) ||
            address.GetAddressBytes()[0] >= 224)
            throw new ArgumentException("Enter the Art-Net receiver's unicast IPv4 address.", nameof(destination));
        if (universe is < 0 or > 32767)
            throw new ArgumentOutOfRangeException(nameof(universe), "Art-Net universes must be 0–32767.");

        _destination = new IPEndPoint(address, Port);
        _universe = universe;
        _client = new UdpClient(new IPEndPoint(localAddress, Port));
    }

    public void Send(byte[] channels)
    {
        _sequence = _sequence == 255 ? (byte)1 : (byte)(_sequence + 1);
        WritePacket(_packet, channels, _universe, _sequence);
        _client.Send(_packet, _packet.Length, _destination);
    }

    internal static void WritePacket(Span<byte> packet, ReadOnlySpan<byte> channels, int universe, byte sequence)
    {
        if (channels.Length != 512 || packet.Length != 530)
            throw new ArgumentException("ArtDmx output requires 512 channels and a 530-byte packet.");
        if (universe is < 0 or > 32767)
            throw new ArgumentOutOfRangeException(nameof(universe));

        "Art-Net\0"u8.CopyTo(packet);
        packet[8] = 0x00; // OpDmx, little endian
        packet[9] = 0x50;
        packet[10] = 0;
        packet[11] = 14; // Protocol version, big endian
        packet[12] = sequence;
        packet[13] = 0; // Physical input port
        packet[14] = (byte)(universe & 0xff);
        packet[15] = (byte)(universe >> 8);
        packet[16] = 2; // 512 channel bytes, big endian; no DMX start code
        packet[17] = 0;
        channels.CopyTo(packet[18..]);
    }

    public void Dispose() => _client.Dispose();
}
