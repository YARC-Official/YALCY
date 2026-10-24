# WLED over USB serial

The **WLED Serial** tab sends RGB Adalight frames to stock WLED firmware. It is separate from network WLED and the OpenDMX Serial integration.

1. Configure the LED hardware and LED count in WLED first. Connect the controller to the computer with a USB **data** cable and install its USB serial driver if needed.
2. In YALCY's WLED Serial tab, refresh ports and select the controller's COM port (or enter its device path on Linux/macOS).
3. Match the baud rate to WLED's Config → Sync Interfaces serial baud setting. The default is **115200**. Close serial monitors and other programs using the port.
4. Set the LED count and brightness. Click **Reset to four banks** to distribute the configured LEDs across blue, red, green and yellow. Edit ranges as needed, then enable output.
5. Use the two-second color test to check reception before running YARG. Settings are saved with YALCY's normal settings save. Disable output before editing the configuration.

Ranges are 1-based and cannot overlap. A bank repeats the eight Stage Kit light bits across its range; a numbered assignment drives the whole range from one bit. Strobe ranges flash white with the Stage Kit strobe command and BPM. Unassigned LEDs stay black. This protocol carries RGB, without a separate RGBW white channel.

Long strips require more serial bandwidth. YALCY limits frame rate to the selected baud rate, skips frames while the port is busy, and always sends the latest lighting state. Increase the baud rate in **both** applications for faster updates. A green status means the serial port opened; Adalight does not acknowledge reception.

Connection failures include details in the visible log and retry every three seconds. Blackout overrides the color test. Disabling or exiting attempts a final black frame and releases the port. WLED may return to its own effect after its realtime timeout. A disconnected controller cannot receive the final blackout.

If the port opens but LEDs do not respond, check the matching baud rate, WLED LED configuration and serial receive support. Boards with native USB or custom firmware may differ; stock WLED's serial receive pin must not be allocated to another function. See the [official WLED serial documentation](https://kno.wled.ge/interfaces/serial/).

Developer checks: `dotnet run --project YALCY.WledSerialTests`. These exercise packet generation and a simulated serial connection; physical ESP32 testing is still needed.
