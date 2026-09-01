# KOTAMON Quality of Life Mods

Two separate BepInEx 6 IL2CPP plugins for KOTAMON.

## Requirements

- KOTAMON Steam version using Unity `6000.4.1f1`.
- BepInEx 6 IL2CPP x64, preferably build `be.785` or newer.
- Windows x64.
- Back up your saves before installing gameplay mods.

## Mods

### KotamonPileUnlock.dll

- Allows opening another junk pile before the current pile is fully cleaned.
- Keeps the contents of previously opened piles on the ground.
- Suppresses the misleading all-junk-cleaned notification caused by this mode.
- Does not auto-open piles.
- Does not change money, cards, collectible contents, cases, tapes, or ordinary junk generation.

### KotamonFPSUnlock.dll

- Sets the Unity application frame-rate limit from a user-editable config.
- Keeps VSync under the in-game setting by default.
- Includes an optional override for users who want VSync disabled.

Config file:

`BepInEx/config/opencode.kotamon.fpsunlock.cfg`

```ini
[Frame Rate]
TargetFPS = 240
VSyncMode = Off
```

Values:

- `TargetFPS = 240`: cap at 240 FPS.
- `TargetFPS = 0`: remove the application FPS limit.
- `VSyncMode = Game`: follow the in-game VSync setting.
- `VSyncMode = Off`: force VSync off.
- `VSyncMode = On`: force VSync on.

The display must run above 60 Hz to show more than 60 frames per second.

## Installation

1. Install BepInEx 6 IL2CPP x64 into the KOTAMON game directory.
2. Start KOTAMON once and close it. This creates the BepInEx folders and interop assemblies.
3. Copy the desired DLL files into `KOTAMON/BepInEx/plugins/`.
4. Start the game through Steam.
5. Edit the FPS config after the first start if needed.

## Removal

Delete the corresponding plugin DLL from `BepInEx/plugins/`. Do not delete save files.

## Compatibility

These plugins target the tested KOTAMON build and BepInEx 6 IL2CPP. A game update may require rebuilding the plugins.
