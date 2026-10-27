# Art-Net output

Open **DMX / sACN / Art-Net**, enter your receiver's IPv4 address in the Art-Net
card, select its universe, and enable Art-Net. The receiver must be configured to
accept Art-Net on that universe. No discovery or receiver configuration is performed.

- Art-Net is independent of the sACN toggle and can run alongside it.
- Both outputs use the existing DMX channel assignments, master dimmers, cue
  channels, and strobe behavior. Set these to match your fixtures.
- The Art-Net universe is a **zero-based 15-bit Port-Address (0–32767)**:
  `Net * 256 + Sub-Net * 16 + Universe`. Net 0, Sub-Net 0, Universe 0 means **0**.
  If your receiver displays universes starting at 1, check its numbering convention.
  The sACN universe is configured separately.
- Output uses 512-channel ArtDmx packets at approximately 44 frames per second,
  with sequencing, over UDP port **6454**. Use a receiver's unicast address.
- Auto adapter selection uses system routing. Select a specific IPv4 adapter on
  computers with multiple networks. Disable Art-Net before changing connection settings.
- Settings are saved with the application's other settings and used by the CLI too.
- Global blackout and output suppression apply. Disabling Art-Net sends a final
  zero frame before closing the socket.

This is manual unicast ArtDmx output, not a full Art-Net discovery/RDM controller.
The sending indicator confirms local packet transmission, not receiver acknowledgment.
If no light responds, check the receiver address, universe, fixture channels, and
firewall access to UDP 6454. Another application occupying that local port can
prevent startup. Physical hardware validation is still needed.

If startup fails, the toggle stays enabled and the error appears beside the
header toggles. Correct the connection settings, then click **Start / retry
Art-Net**. The header's combined status only lists outputs that are running.

Art-Net™ is designed by and Copyright Artistic Licence.
[Art-Net specification](https://art-net.org.uk/downloads/art-net.pdf)

Run protocol and loopback checks with:

```sh
dotnet run --project Tests/YALCY.ArtNetTests
```
