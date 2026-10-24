# Glow Worm Luciferin

YALCY's **Glow Worm** tab streams RGB lighting over the local network to Glow Worm Luciferin FULL firmware. It uses the firmware's UDP protocol, with optional HTTP control to turn the controller on and select **Bias light** mode. USB serial and MQTT are not part of this integration.

1. Configure your LED hardware in Glow Worm first and find the controller's IP address.
2. Stop Firefly Luciferin capture and any other lighting sender that controls the same device.
3. Enter the IP in YALCY. The default UDP port is **4210**, HTTP port **80**, and frame rate **30 FPS**. Match the LED count to your strip.
4. Click **Reset to four banks** after changing the LED count, or configure your own ranges. Bank mappings repeat their eight Stage Kit lights; numbered assignments control a whole range together. Strobe ranges flash white. LED numbers start at 1; ranges cannot overlap.
5. Leave automatic Bias light control enabled, or select Bias light mode manually in the controller's interface. Enable YALCY output and use the two-second color test.

Brightness is sent to the controller once per packet, rather than also dimming the RGB values. A firmware light sensor may override that brightness. Output is RGB; there is no independent RGBW white channel. Settings use YALCY's normal save/load flow, including headless startup.

The status indicator means YALCY is sending UDP, which has no delivery acknowledgement. Check the log for HTTP control failures or network errors. HTTP retries do not block the lighting stream. If the controller restarts or another app changes its mode, disable and re-enable YALCY output to request Bias light mode again.

Blackout overrides color tests. Disabling output or exiting attempts a final black frame. UDP cannot guarantee that a powered-off or disconnected device receives it, and the controller's own firmware determines its subsequent idle behavior.

Protocol references: [Glow Worm firmware](https://github.com/sblantipodi/glow_worm_luciferin/blob/master/src/NetManager.cpp) and [Ape's YARG bridge](https://github.com/Ape/yarg-glowworm). Packets contain `DPsoftware`, total LED count, brightness, total chunks, zero-based chunk index, and decimal packed RGB colors. Each chunk contains up to 140 LEDs, with a small pause between chunks.
