# KOTAMON FPS Unlock

BepInEx 6 IL2CPP plugin for KOTAMON.

## Configuration

After the first launch, edit:

`KOTAMON/BepInEx/config/opencode.kotamon.fpsunlock.cfg`

```ini
[Frame Rate]
TargetFPS = 240
```

- `TargetFPS = 240` limits the game to 240 FPS.
- `TargetFPS = 0` removes the application FPS limit.
- VSync remains controlled by the game's own setting.

The monitor must support the selected refresh rate to display more than 60 frames per second.

## Removal

Delete `KotamonFPSUnlock.dll` from `BepInEx/plugins/`.
