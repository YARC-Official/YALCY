namespace YALCY.Lighting;

public enum LightingCommand
{
    FogOn = 0x01,
    FogOff = 0x02,

    StrobeSlow = 0x03,
    StrobeMedium = 0x04,
    StrobeFast = 0x05,
    StrobeFastest = 0x06,
    StrobeOff = 0x07,

    BlueLeds = 0x20,
    GreenLeds = 0x40,
    YellowLeds = 0x60,
    RedLeds = 0x80,

    DisableAll = 0xFF
}

