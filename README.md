# Easy Delivery Co — Custom Radio, Wheel & FPS Unlock Mod

> 🤖 **100% AI-Generated Mod**  
> Этот мод полностью разработан искусственным интеллектом (OpenCode AI Agent / Gemini 3.7 & Claude) автономно: от анализа декомпилированного кода игры до реверс-инжиниринга ввода и написания DLL.  
> *This entire mod was created autonomously by an AI agent (OpenCode AI): from decompiled game code analysis and input reverse-engineering to C# DLL development.*

---

## 🇷🇺 Возможности (RU)

- **📻 Кастомное радио (88.1 FM)**: стриминг всей вашей музыки из `C:\Music` (FLAC, M4A, AAC, MP3, WAV, WMA, OGG) с нулевым расходом оперативной памяти. Станция 88.1 FM работает со 100% сигналом всегда, а на оригинальных станциях сигнал нарастает до 100% прямо у вышек.
- **🏎️ Руль (PXN V12 Lite, Logitech, Thrustmaster, DirectInput)**: честный симуляторный ввод 1:1 на **900 градусов**, строгий **ноль `0.00`** в центре, плавные педали газа (`z`) и тормоза (`rz`).
- **🅿️ Ручник на лепестке**: зажатие верхнего лепестка (или кнопки) вызывает настоящее действие Пробела (`Break`) с блокировкой задней оси.
- **⌨️ Приоритет клавиатуры и геймпада**: клавиатура и геймпад работают параллельно без конфликтов.
- **⚡ Разблокировка FPS**: держит 240 FPS (или безлимит) без сброса на 60.
- **📊 Экран F7**: живой оверлей осей, статуса радио и нажатых кнопок.

---

## 🇬🇧 Features (EN)

- **📻 Custom Radio (88.1 FM)**: on-demand streaming from `C:\Music` (FLAC, M4A, AAC, MP3, WAV, WMA, OGG) with near-zero RAM usage. Dedicated 88.1 FM station with 100% signal always; vanilla stations scale up to 100% near towers.
- **🏎️ Steering Wheel Support**: direct 1:1 input across **900 degrees**, true **`0.00` center** with zero drift, progressive gas (`z`) and brake (`rz`) pedals.
- **🅿️ Spacebar Handbrake**: paddle/button triggers true in-game Spacebar (`Break`) action to lock rear wheels.
- **⌨️ Multi-Device Priority**: keyboard and gamepad work seamlessly alongside the wheel.
- **⚡ FPS Unlocker**: sustains 240 FPS (or uncapped) without dropping to 60.
- **📊 F7 Overlay**: real-time diagnostics overlay.

---

## ⚙️ Кастомизация / Customization

Все параметры мода полностью настраиваются через конфигурационный файл, который создаётся после первого запуска:  
*All mod parameters are fully customizable via config file generated after first run:*  
📁 `Easy Delivery Co\BepInEx\config\opencode.easydeliveryco.mods.cfg`

```ini
[1. Custom Radio]
Enabled = true
MusicFolder = C:\Music          # Путь к вашей папке с музыкой / Path to your music folder
Shuffle = true                  # Перемешивание треков / Shuffle tracks

[2. Frame Rate]
UnlockFPS = true                # Разблокировка 60 FPS / Unlock 60 FPS cap
TargetFPS = 240                 # Целевой FPS (0 = безлимит) / Target FPS (0 = uncapped)
DisableVSync = true             # Отключение VSync / Disable VSync

[3. Steering Wheel]
Enabled = true                  # Включить поддержку руля / Enable wheel
DeviceFilter = pxn              # Имя устройства для поиска / Wheel name filter
SteerDeadzone = 0.02            # Мертвая зона руля / Steering deadzone
SteerSensitivity = 1            # Чувствительность руля / Steering sensitivity
InvertSteering = false          # Инверсия руля / Invert steering
InvertGas = false               # Инверсия газа / Invert gas
InvertBrake = false             # Инверсия тормоза / Invert brake
HandbrakeButtonIndex = 4        # Номер кнопки ручника / Handbrake button index
HandbrakeAxisName = rx          # Ось лепестка ручника / Handbrake paddle axis

[4. Overlay]
ShowOverlay = true              # Показывать экран диагностики / Show F7 overlay
ToggleKey = F7                  # Кнопка открытия оверлея / Overlay toggle key
```

---

## 📦 Установка / Installation

1. Установите / Install [BepInEx 5.4.23.5 (x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip).
2. Скачайте архив / Download `EasyDeliveryCo-Mods-v1.0.0.zip` из [Releases](../../releases/latest).
3. Распакуйте все 3 файла в / Extract all 3 DLLs to `Easy Delivery Co/BepInEx/plugins/`:
   - `EasyDeliveryCoMods.dll`
   - `NAudio.Core.dll`
   - `NAudio.Wasapi.dll`
4. Положите треки в / Put music in `C:\Music\`.
5. Играйте / Run the game!

---

## 📄 License
MIT License
