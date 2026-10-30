# Govee LAN, WiZ, and Nanoleaf — in testing

These integrations have automated protocol, lifecycle, and loopback emulator tests. **They have not been tested on physical lights.** Compatibility, latency, device update limits, discovery on multiple adapters, and restoration need hardware testing. They are disabled by default and are available in the first-run wizard, Settings, and their own tabs. Saved configurations also work in the CLI.

## Shared setup

1. Configure the light in its manufacturer's app and connect it to the same local network as YALCY.
2. Open the integration tab. With output disabled, discover devices (Govee/WiZ) or add their IPv4 addresses manually. DHCP reservations are recommended because configuration is keyed by address.
3. Check **Use** for each device to control. Discovery leaves new devices unchecked and preserves existing selections/settings.
4. Choose **All colors**, one color bank, or **Light 1–8**. All colors combines the active banks; numbered lights combine the colors at that Stage Kit position. Each device/controller displays one color. Fog and rapid strobe commands are ignored.
5. Set brightness and update rate, enable output, and inspect each device's status. A light must supply its initial state before any color commands are sent. Select a checked row and use the two-second color test.
6. Save settings to retain addresses, selections, assignments, and Nanoleaf pairing tokens.

The default maximum is five color updates per device per second, configurable from one to ten. Duplicate colors are suppressed; current state is refreshed every two seconds. Govee uses up to three UDP commands for a color update. These are ambient-light integrations, not guaranteed high-frame-rate streaming outputs. Offline devices retry independently and do not block YARG intake or other lights.

Blackout overrides cues and tests. Disabling output restores the captured state when restoration is enabled; otherwise it requests lights off. Quitting YALCY activates its global safety blackout, so these lights receive off requests on application exit. During global blackout, shutdown requests off instead of restoring a lit state. Restoration is best effort, cannot occur after a crash/power loss, and depends on a reachable device. Already-in-flight network requests cannot be recalled; an off request follows when they finish or time out. Check devices manually after reported failures.

## Govee LAN

- Enable **LAN Control** in Govee Home for each compatible RGB device. Not all Govee products expose this API.
- Discovery uses multicast `239.255.255.250:4001`; replies arrive at local UDP port `4002`; commands target UDP `4003`.
- Close other software occupying port 4002. Allow local network access in your firewall. If multicast cannot cross your network, enter the light's address manually.
- Supports whole-device power, RGB color, and brightness. **No RGBIC segment mapping, proprietary effects, cloud API, or Bluetooth support.**
- Restores basic color/color temperature, brightness, and power reported by `devStatus`. The documented LAN API does not expose enough information to restore a previous animated scene.
- A discovery/state reply proves the device answered; subsequent UDP color writes are not acknowledged. “Sending” does not guarantee the light displayed every command.

Reference: [Govee LAN API guide](https://app-h5.govee.com/user-manual/wlan-guide).

## WiZ

- Enable local communication in the WiZ app. Use compatible RGB bulbs on the standard local UDP API, port `38899`.
- Discovery broadcasts `getPilot`. Manual IP entry is available when broadcasts are blocked or sent through the wrong adapter.
- Uses `getPilot`/`setPilot` for state, RGB, brightness, and power. Device error replies and timeouts are surfaced in the row status.
- Restores reported scene/speed, color temperature, or RGB/white channels, plus brightness/power. Dual-head independent control, white-only devices, and firmware requiring signed local commands are not supported.

Protocol reference: [pywizlight implementation](https://github.com/sbidy/pywizlight), the client used by [Home Assistant's WiZ integration](https://www.home-assistant.io/integrations/wiz/).

## Nanoleaf

- Enter the controller's IPv4 address manually. This version does not provide Nanoleaf discovery.
- Intended for RGB **Light Panels, Canvas, Shapes, and Lines** controllers using the Panels OpenAPI on HTTP port `16021`. Essentials/Matter, Elements, USB strips, and other product APIs are not supported by this integration.
- Select the row, hold the controller's power button for 5–7 seconds until its indicator flashes, then click **Pair selected** within the pairing window. Alternatively paste an existing token into the masked field.
- Tokens are saved in YALCY's local settings file; treat that file as containing credentials and remove tokens before sharing it. Tokens are not written to diagnostic messages.
- Controls the entire controller using hue/saturation, brightness, and power. **No individual-panel mapping or external-control UDP streaming yet.**
- Restores the captured static color/color temperature or selected effect and power state. If a factory reset invalidates the token, pair again.

References: [Nanoleaf pairing](https://support.nanoleaf.me/hc/en-us/articles/41108368751892-API-Authentication-Security), [Panels OpenAPI](https://nanoleaf.atlassian.net/wiki/x/QoBBpg).

## Verification

Run `dotnet run --project Tests/YALCY.RoomLightTests/YALCY.RoomLightTests.csproj`. The harness uses simulated connections and local UDP/TCP emulators, never discovery or real hardware. `-- --unit-only` skips loopback network tests.

Before calling an integration hardware-tested, record model and firmware and verify discovery/manual setup, pairing where applicable, checkbox isolation, all cue assignments, brightness, loss/recovery of network, blackout during a test, restart/settings reload, and restoration after disabling/quitting. For Govee, also test contention on port 4002; for Nanoleaf, test both an initially-off controller and an active scene.
