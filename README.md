# Easy Delivery Co Mods

A collection of BepInEx mods for Easy Delivery Co that enhance gameplay and add new features.

## Mods Included

### 1. FPS Unlocker
Removes FPS cap and disables VSync for smoother gameplay.

**Features:**
- Configurable target FPS (default: 240, set to 0 for unlimited)
- VSync toggle
- Real-time FPS display

**Config:** `BepInEx/config/opencode.easydeliveryco.fpsunlock.cfg`

### 2. HD Rendering
Disables PS1-style effects and increases render resolution for modern look.

**Features:**
- Disables CRT/retro post-processing effects
- Increases render texture resolution to full HD
- Changes texture filtering to bilinear for crisp visuals
- Configurable settings for each effect

**Config:** `BepInEx/config/opencode.easydeliveryco.hdrendering.cfg`

### 3. Steering Wheel Support
Native support for PXN V12 Lite and other DirectInput steering wheels.

**Features:**
- Automatic steering wheel detection
- Configurable steering sensitivity and dead zones
- Pedal support (accelerator and brake)
- Manual gear shifting with paddle shifters
- Force feedback support (if wheel supports it)
- In-game overlay showing wheel input (toggle with F2)

**Config:** `BepInEx/config/opencode.easydeliveryco.steeringwheel.cfg`

**Controls:**
- Steering wheel: Turn vehicle
- Right trigger/pedal: Accelerate
- Left trigger/pedal: Brake
- Paddle shifters: Change gears (if manual mode enabled)
- F2: Toggle input display overlay

### 4. VR Support
Adds VR headtracking support while keeping traditional input methods.

**Features:**
- Full VR headtracking (6DOF)
- Stereo rendering for VR headsets
- No VR controllers required - use keyboard/mouse/gamepad/wheel
- Configurable head tracking scale and camera offset
- Debug mode for troubleshooting

**Config:** `BepInEx/config/opencode.easydeliveryco.vr.cfg`

**Requirements:**
- SteamVR or Oculus Runtime
- Compatible VR headset

**Usage:**
1. Start SteamVR
2. Launch the game
3. Put on your headset
4. Use your preferred input device (wheel/gamepad/keyboard)

## Installation

1. Download and install [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases) for the game
2. Download the mod DLLs from [Releases](../../releases)
3. Place the DLL files in `Easy Delivery Co/BepInEx/plugins/`
4. Launch the game

## Configuration

All mods are configurable through their respective config files in `BepInEx/config/`. Edit these files to customize settings.

Configs are created after first launch with default values.

## Building from Source

Requirements:
- .NET 6.0 SDK
- Game installed with BepInEx

```bash
# Clone the repository
git clone https://github.com/Ruventium/easy-delivery-co-mods.git
cd easy-delivery-co-mods

# Build FPS Unlocker
cd EasyDeliveryCoFpsUnlock
dotnet build
cp bin/Debug/net6.0/EasyDeliveryCoFpsUnlock.dll "path/to/game/BepInEx/plugins/"

# Build HD Rendering
cd ../EasyDeliveryCoHDRendering
dotnet build
cp bin/Debug/net6.0/EasyDeliveryCoHDRendering.dll "path/to/game/BepInEx/plugins/"

# Build Steering Wheel Support
cd ../EasyDeliveryCoSteeringWheel
dotnet build
cp bin/Debug/net6.0/EasyDeliveryCoSteeringWheel.dll "path/to/game/BepInEx/plugins/"

# Build VR Support
cd ../EasyDeliveryCoVR
dotnet build
cp bin/Debug/net6.0/EasyDeliveryCoVR.dll "path/to/game/BepInEx/plugins/"
```

## Troubleshooting

### FPS Unlocker not working
- Check that VSync is disabled in game settings
- Try different target FPS values

### HD Rendering makes game look wrong
- Adjust individual settings in the config
- Some effects may need to stay enabled for certain art styles

### Steering wheel not detected
- Make sure wheel is connected and detected by Windows
- Check Device Manager for "Human Interface Devices"
- Try adjusting dead zones in config

### VR not working
- Ensure SteamVR or Oculus is running before launching game
- Check BepInEx console for error messages
- Enable debug mode in config to see VR state
- Verify your headset is tracked (green light in SteamVR)

## License

MIT License - see LICENSE file for details

## Credits

Created by OpenCode
