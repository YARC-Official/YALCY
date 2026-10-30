# 💡 YALCY
The official repository for Yet Another Lighting Control for YARG.
YARG outputs a UDP of various stuff it is doing, such as venue lighting, at any given moment. YALCY takes that data and converts it to real-world lighting protocols so you can get down with your bad self in funky fresh light shows synced to the song, right in your living room / bar / airport terminal gate / etc!

YALCY IS IN ALPHA! EXPECT BUGS, SETTING CHANGES, INCOMPLETE FEATURES, CRASHES, LOOKING BAD, AND MORE!
Help test YALCY!

## Supported Protocols & Hardware

Each output protocol can be enabled independently. All protocols share the YARG cue stream and global blackout controls.

## Summary
YALCY currently supports:
| Protocol / Hardware | Connection | Status |
| :--- | :--- | :--- |
| **Stage Kit** (PDP, FatsCo) | USB HID | ![Working][working] |
| **DMX512** | Ethernet (sACN / E1.31) | ![Working][working] |
| **[Art-Net](docs/Art-Net.md)** | Ethernet (ArtDmx unicast) | ![In Testing][testing] |
| **Philips Hue** | Entertainment API (UDP) | ![Working][working] |
| **LIFX** | HTTP API | ![Working][working] |
| **[LIFX](docs/LIFX.md)** | LAN Protocol | ![In Testing][testing] |
| **WLED** | DDP & Realtime UDP & Serial | ![In Testing][testing] |
| **[Glow Worm Luciferin](docs/Glow-Worm.md)** | RGB UDP + HTTP Bias light control | ![In Testing][testing] |
| **[Govee LAN](docs/LAN-Lights.md#govee-lan)** | Local UDP (whole-device RGB) | ![In Testing][testing] |
| **[WiZ](docs/LAN-Lights.md#wiz)** | Local UDP (RGB bulbs) | ![In Testing][testing] |
| **[Nanoleaf](docs/LAN-Lights.md#nanoleaf)** | Local Panels OpenAPI (whole-controller color) | ![In Testing][testing] |
| **OpenRGB** | Network Client (SDK) | ![Working][working] |
| **RB3E** (Lighting DATA) | UDP Stream (Partial) | ![Working][working] |
| **Serial** | USB-UART / COM | ![Working][working] |


Govee LAN, WiZ, and Nanoleaf are **in testing**: automated protocol and loopback tests are included, but physical hardware has not been validated. See the [setup guide and limitations](docs/LAN-Lights.md).

## 📥 Downloading and Playing

Download the release for your operating system.

Run YALCY. On your first launch, the setup window explains each lighting output
and lets you choose which ones to enable. All outputs start **off**; listening
to YARG starts **on**. Click **Finish setup** to apply and save your choices,
then use the protocol tabs for pairing, addresses, or channel mappings.
**Not now** leaves your choices unapplied and offers setup again next launch.
Existing installations keep their saved settings and skip the wizard.

YALCY can be minimized after setup.

Run YARG.

Turn on UDP data stream in experimental settings.

Some protocols need some setup, see those tabs for more info.

Rock on!

## 🔨 Building

Git clone the repo.
Build in IDE of choice.

## 🛡️ License

YALCY is licensed under the [GNU Lesser General Public License v3.0](https://www.gnu.org/licenses/lgpl-3.0.en.html) (or later) - see the [`LICENSE`](LICENSE) file for details.

## 🧰 External Licenses

Some libraries/assets are **packaged** with the source code have licenses that must be included.

| Link | License | Use |
| --- | --- | --- |
|[Avalonia](https://github.com/AvaloniaUI/Avalonia)|MIT|Multi-platform UI framework|
|[Haukcode.sACN](https://github.com/HakanL/Haukcode.sACN)|MIT|sACN (DMX) handling|
|[HidSharp](https://github.com/IntergatedCircuits/HidSharp)|Apache 2.0|USB device managment|
|[HueApi](https://github.com/michielpost/Q42.HueApi)|MIT|Phillips Hue handling|
|[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)|MIT|Settings saving and loading|
|[OpenRGB.net](https://github.com/diogotr7/OpenRGB.NET)|MIT|OpenRGB handling|
|[DMX.NET](https://github.com/Nyxyxylyth/DMX.NET)|MIT| USB to serial handling|


⚠️ Warning:
Once you go lights, you never go back!

<!--- IMAGES SOURCES -->
[working]: https://shieldcn.dev/badge/Working%20!.svg?size=xs&theme=emerald&logo=ri%3AFaCheck
[testing]: https://shieldcn.dev/badge/In%20Testing.svg?size=xs&theme=amber&logo=ri%3ATbTestPipe
