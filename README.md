# Controller Deadzone

Controller Deadzone is a Dalamud plugin that remaps FFXIV's normalized gamepad stick input before the game consumes it.

It supports per-stick radial and axial input remapping with:

- Dead zone
- Max zone
- Anti-dead zone
- Live original and remapped input visualization

Open the configuration window with `/pdeadzone`.

## Behavior

FFXIV already normalizes controller-specific raw input into a circular gamepad input range. This plugin intercepts that normalized `GamepadInputData`, applies the configured remap, and writes the remapped stick values back for the game to use.

Radial mode remaps along the input direction. Axial mode remaps each axis independently, then clamps the resulting output to the unit circle.

## License

MIT
