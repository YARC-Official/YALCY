# 💡 YALCY
The official repository for Yet Another Lighting Control for YARG.

YALCY IS IN ALPHA! EXPECT BUGS, SETTING CHANGES, INCOMPLETE FEATURES, CRASHES, LOOKING BAD, AND MORE!
Help test YALCY!

## Summary
YALCY is YARG's offical light controller for real world lighting effects. It currently supports:
|Protocol|Status|
| --- | --- |
|Stage Kit and compatible hardware such as FatsCo devices| Working!|
|DMX output over ethernet via sACN| Working!|
|Phillips hue entertainment api| Working!|
|RB3E datastream, partial, just the lighting data|Working!|
|OpenRGB| Working!|
|Serial| In testing|

## 📥 Downloading and Playing

Download the release for your operating system.
Run it, can be minimized.
Run YARG (a versison that outputs the datasstream).
Rock on.

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
