# LIFX setup and troubleshooting

YALCY controls LIFX lights locally. No cloud account or API token is required.

1. Keep your lights powered on and on the same non-guest network as YALCY.
2. In the LIFX tab, leave output disabled while configuring it.
3. Enable **Whole-room song colors** for ordinary smart bulbs. Leave it off
   for advanced Stage Kit zone mapping; existing mappings are preserved.
4. Select **Discover lights**. Check the lights YALCY should control.
5. Use **Identify** on an individual device, or choose a color and test all
   included lights. Identify works on unchecked devices too. Tests restore
   the previous colors and power after two seconds. Blackout takes precedence.
6. Enable LIFX output, then enable YARG's UDP data stream and play a song.

Selections are saved with the other settings, by serial number. They survive
renaming a light, changing its IP address, or a discovery scan that misses it.
The advanced legacy filter remains supported; explicit checkboxes override it.
Unchecked lights receive no gameplay output. Unassigned zones remain unchanged
in Stage Kit mode.

## Whole-room update limits

Whole-room mode defaults to 20 state updates per second per light. Each device
can override that limit (1–60); zero uses the default. An update can contain
both a color packet and a power packet, so this is not a UDP packet count.
Use lower rates if a bulb struggles with fast changes. Short strobe pulses
can be skipped at low update rates.

Output runs on a background worker with only the latest desired state for each
light. Ordinary color changes do not resend power unnecessarily. A periodic
refresh resends the current color and power to recover lost UDP commands or
a bulb restart. Blackouts bypass normal rate limiting. Socket errors are
isolated per light and retried after a one-second delay. The worker stops
before original colors/power are restored on disable, so stale frames cannot
undo restoration. An active blackout keeps lights off instead of restoring
their original powered state.

These scheduling controls apply to whole-room mode. Advanced Stage Kit zone
output retains its existing transport and strobe behavior.

## When discovery fails

- Allow YALCY through the firewall on your private network (LIFX uses UDP 56700).
- Avoid guest Wi-Fi/client isolation. Verify the computer can reach the bulbs'
  network; VPNs and extra network adapters can affect broadcast discovery.
- Increase the discovery window and scan again. Discovery retries each phase
  within its configured window. A scan has service, state, and zone phases.
- A device shown with **No color/power response** is not ready for control.
  YALCY needs its state so it can restore it after tests or gameplay.
- Check device selection if discovery succeeds but nothing responds. Automatic
  rediscovery can pick up newly available lights and changed addresses.

Discovery confirms a response at scan time. Gameplay UDP sends are not
acknowledgements that a bulb applied a color. Status deliberately distinguishes
output being enabled from devices having answered discovery.
