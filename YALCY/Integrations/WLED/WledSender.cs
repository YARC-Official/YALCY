using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace YALCY.Integrations.WLED;

/// <summary>
/// High-performance, low-latency UDP socket transmitter for WLED devices.
/// Supports both the modern DDP (Distributed Display Protocol) and WLED Realtime DRGB/DRGBW protocols.
/// Employs buffer reuse and zero-allocation frame encoding to minimize GC pressure during 60 FPS gameplay.
/// </summary>
public sealed class WledSender : IDisposable
{
    // Default UDP ports
    public const int DefaultDdpPort = 4048;
    public const int DefaultRealtimePort = 21324;

    // DDP Protocol Constants (Specification from 3waylabs / WLED)
    private const byte DdpFlagsVer1 = 0x40; // Version 1 (bits 7-6 = 01)
    private const byte DdpFlagsPush = 0x01; // Push display buffer immediately
    private const byte DdpTypeRgb = 0x0B;   // 24-bit RGB (3waylabs / WLED standard: TTT=001, SSS=011 -> 0x0B)
    private const byte DdpTypeRgbw = 0x1B;  // 32-bit RGBW (3waylabs / WLED standard: TTT=011, SSS=011 -> 0x1B)
    private const byte DdpDefaultDestinationId = 0x01;
    private const int DdpHeaderSize = 10;

    // WLED Realtime UDP Constants
    private const byte WledRealtimeDrgb = 2;  // 3 bytes per LED [R, G, B]
    private const byte WledRealtimeDrgbw = 3; // 4 bytes per LED [R, G, B, W]
    private const int RealtimeHeaderSize = 2;

    private readonly object _socketLock = new();
    private UdpClient? _udpClient;
    private IPEndPoint? _targetEndPoint;
    private byte[] _txBuffer = new byte[2048]; // Preallocated buffer, dynamically resized if needed

    // Rolling 4-bit sequence counter for DDP (1 to 15; 0 means unsequenced)
    private byte _ddpSequence = 1;

    // Transmission telemetry
    private long _packetsSent;
    private long _lastFpsCalculationTimestamp;
    private long _packetsSinceLastFpsCalc;
    private double _currentFps;
    private string _lastError = string.Empty;

    public long TotalPacketsSent => Interlocked.Read(ref _packetsSent);
    public double PacketsPerSecond => Volatile.Read(ref _currentFps);
    public string LastError => Volatile.Read(ref _lastError);
    public bool IsActive => _udpClient != null && _targetEndPoint != null;

    /// <summary>
    /// Synchronously configures endpoint if valid IP address (0 ms, non-blocking).
    /// Does not perform blocking DNS lookups to keep UI thread fluid.
    /// </summary>
    public bool ConfigureEndpoint(string ipAddress, int port)
    {
        lock (_socketLock)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                _targetEndPoint = null;
                return false;
            }

            if (IPAddress.TryParse(ipAddress.Trim(), out var parsedIp))
            {
                _targetEndPoint = new IPEndPoint(parsedIp, port);
                _lastError = string.Empty;
                EnsureUdpClient();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Asynchronously configures the target WLED endpoint, supporting both raw IP addresses
    /// and hostnames (e.g. "wled-stage.local") without blocking the UI thread.
    /// </summary>
    public async Task<bool> ConfigureEndpointAsync(string ipOrHost, int port)
    {
        if (string.IsNullOrWhiteSpace(ipOrHost))
        {
            lock (_socketLock) { _targetEndPoint = null; }
            return false;
        }

        string trimmed = ipOrHost.Trim();

        // Fast path: direct IP address (0 ms)
        if (IPAddress.TryParse(trimmed, out var parsedIp))
        {
            lock (_socketLock)
            {
                _targetEndPoint = new IPEndPoint(parsedIp, port);
                _lastError = string.Empty;
                EnsureUdpClient();
            }
            return true;
        }

        // Hostname resolution performed asynchronously off the UI thread
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(trimmed);
            if (hostEntry.AddressList.Length > 0)
            {
                lock (_socketLock)
                {
                    _targetEndPoint = new IPEndPoint(hostEntry.AddressList[0], port);
                    _lastError = string.Empty;
                    EnsureUdpClient();
                }
                return true;
            }

            lock (_socketLock)
            {
                _lastError = $"Unable to resolve host: {trimmed}";
                _targetEndPoint = null;
            }
            return false;
        }
        catch (Exception ex)
        {
            lock (_socketLock)
            {
                _lastError = $"DNS resolution failed: {ex.Message}";
                _targetEndPoint = null;
            }
            return false;
        }
    }

    private void EnsureUdpClient()
    {
        if (_udpClient == null)
        {
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
        }
    }

    /// <summary>
    /// Transmits a lighting frame to the WLED controller using the specified protocol.
    /// </summary>
    /// <param name="leds">Span of RGBW colors representing the entire strip.</param>
    /// <param name="protocol">Protocol to serialize into (DDP or Realtime UDP).</param>
    /// <param name="timeoutSeconds">WLED timeout before reverting to internal preset (Realtime UDP mode).</param>
    public void SendFrame(ReadOnlySpan<WledRgbwColor> leds, WledProtocol protocol, byte timeoutSeconds = 2)
    {
        if (leds.IsEmpty) return;

        UdpClient? client;
        IPEndPoint? endPoint;

        lock (_socketLock)
        {
            client = _udpClient;
            endPoint = _targetEndPoint;
        }

        if (client == null || endPoint == null) return;

        bool isRgbw = protocol is WledProtocol.DdpRgbw or WledProtocol.RealtimeDrgbw;
        bool isDdp = protocol is WledProtocol.DdpRgbw or WledProtocol.DdpRgb;

        int bytesPerLed = isRgbw ? 4 : 3;
        int payloadSize = leds.Length * bytesPerLed;
        int headerSize = isDdp ? DdpHeaderSize : RealtimeHeaderSize;
        int packetSize = headerSize + payloadSize;

        // Ensure buffer is large enough
        if (_txBuffer.Length < packetSize)
        {
            Array.Resize(ref _txBuffer, Math.Max(packetSize, _txBuffer.Length * 2));
        }

        if (isDdp)
        {
            EncodeDdpHeader(_txBuffer, payloadSize, isRgbw);
        }
        else
        {
            EncodeRealtimeHeader(_txBuffer, isRgbw, timeoutSeconds);
        }

        // Pack LED payload
        int writeIndex = headerSize;
        for (int i = 0; i < leds.Length; i++)
        {
            var c = leds[i];
            if (isRgbw)
            {
                _txBuffer[writeIndex++] = c.R;
                _txBuffer[writeIndex++] = c.G;
                _txBuffer[writeIndex++] = c.B;
                _txBuffer[writeIndex++] = c.W;
            }
            else
            {
                // In 3-channel RGB mode (WS2812B/WS2811), if the color has a dedicated White (W) component,
                // blend the White channel evenly across R, G, and B to generate authentic white light on RGB hardware.
                if (c.W > 0)
                {
                    _txBuffer[writeIndex++] = (byte)Math.Min(255, c.R + c.W);
                    _txBuffer[writeIndex++] = (byte)Math.Min(255, c.G + c.W);
                    _txBuffer[writeIndex++] = (byte)Math.Min(255, c.B + c.W);
                }
                else
                {
                    _txBuffer[writeIndex++] = c.R;
                    _txBuffer[writeIndex++] = c.G;
                    _txBuffer[writeIndex++] = c.B;
                }
            }
        }

        try
        {
            client.Send(_txBuffer, packetSize, endPoint);
            RecordPacketSent();
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _lastError, ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously pings or sends a single test frame to verify connectivity.
    /// </summary>
    public async Task<bool> SendTestFrameAsync(ReadOnlyMemory<WledRgbwColor> leds, WledProtocol protocol, byte timeoutSeconds = 2)
    {
        UdpClient? client;
        IPEndPoint? endPoint;

        lock (_socketLock)
        {
            client = _udpClient;
            endPoint = _targetEndPoint;
        }

        if (client == null || endPoint == null) return false;

        try
        {
            await Task.Run(() => SendFrame(leds.Span, protocol, timeoutSeconds));
            return true;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _lastError, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Encodes a 10-byte DDP header according to the Distributed Display Protocol standard.
    /// </summary>
    private void EncodeDdpHeader(byte[] buffer, int payloadLength, bool isRgbw)
    {
        // Byte 0: Flags (Version 1 + Push flag)
        buffer[0] = DdpFlagsVer1 | DdpFlagsPush;

        // Byte 1: Sequence number (1 to 15, roll over)
        buffer[1] = _ddpSequence;
        _ddpSequence = (byte)((_ddpSequence % 15) + 1);

        // Byte 2: Data type (RGBW = 0x02, RGB = 0x01)
        buffer[2] = isRgbw ? DdpTypeRgbw : DdpTypeRgb;

        // Byte 3: Destination ID (Default device = 0x01)
        buffer[3] = DdpDefaultDestinationId;

        // Bytes 4-7: Data offset (32-bit big endian uint)
        buffer[4] = 0;
        buffer[5] = 0;
        buffer[6] = 0;
        buffer[7] = 0;

        // Bytes 8-9: Data length (16-bit big endian ushort)
        buffer[8] = (byte)((payloadLength >> 8) & 0xFF);
        buffer[9] = (byte)(payloadLength & 0xFF);
    }

    /// <summary>
    /// Encodes a 2-byte WLED Realtime UDP header.
    /// </summary>
    private static void EncodeRealtimeHeader(byte[] buffer, bool isRgbw, byte timeoutSeconds)
    {
        // Byte 0: Protocol mode (3 = DRGBW, 2 = DRGB)
        buffer[0] = isRgbw ? WledRealtimeDrgbw : WledRealtimeDrgb;

        // Byte 1: Timeout in seconds
        buffer[1] = timeoutSeconds;
    }

    private void RecordPacketSent()
    {
        Interlocked.Increment(ref _packetsSent);

        long now = Environment.TickCount64;
        long elapsed = now - Volatile.Read(ref _lastFpsCalculationTimestamp);

        if (elapsed >= 1000)
        {
            lock (_socketLock)
            {
                if (now - _lastFpsCalculationTimestamp >= 1000)
                {
                    double seconds = (now - _lastFpsCalculationTimestamp) / 1000.0;
                    _currentFps = _packetsSinceLastFpsCalc / seconds;
                    _packetsSinceLastFpsCalc = 0;
                    _lastFpsCalculationTimestamp = now;
                }
            }
        }
        else
        {
            Interlocked.Increment(ref _packetsSinceLastFpsCalc);
        }
    }

    public void Dispose()
    {
        lock (_socketLock)
        {
            _udpClient?.Dispose();
            _udpClient = null;
            _targetEndPoint = null;
        }
    }
}
