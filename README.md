# Easy Delivery Co — Custom Radio, Wheel & FPS Unlock Mod

> 🤖 **100% AI-Generated Mod**  
> Этот мод полностью разработан искусственным интеллектом (OpenCode AI Agent / Gemini 3.7 & Claude) автономно: от анализа декомпилированного кода игры до реверс-инжиниринга ввода и написания DLL.  
> *This entire mod was created autonomously by an AI agent (OpenCode AI): from decompiled game code analysis and input reverse-engineering to C# DLL development.*

---

## 🇷🇺 Описание (RU)

Комплексный BepInEx-мод для **Easy Delivery Co**, исправляющий проблемы управления и расширяющий возможности игры:

### 1. 📻 Кастомное радио (88.1 FM)
- Потоковый стриминг всей вашей музыки из `C:\Music` (FLAC, M4A, AAC, MP3, WAV, WMA, OGG) без расхода оперативной памяти.
- Выделенная станция **88.1 FM (Custom)** со 100% чистым сигналом всегда.
- Оригинальные станции (99.1 Новости, 101.7 D&B, 99.9 Lofi, 91.1 EasyCo) сохранены: по мере приближения к вышкам сигнал плавно вырастает до 100% прямо под ними.
- Быстрое переключение станций штатными кнопками машины или клавишами `.` и `,`.

### 2. 🏎️ Поддержка руля (PXN V12 Lite, Logitech, Thrustmaster, DirectInput)
- Честный симуляторный ввод 1:1 на полные **900 градусов** в обход геймпадовских кривых.
- Исправлено знаковое переполнение Unity HID: нейтраль держит **строгий ноль `0.00`** без дрейфа в сторону.
- Педали: плавный газ (`z`) и тормоз (`rz`).
- Ручной тормоз: верхний лепесток или кнопка вызывают настоящее действие Пробела (`Break`) с блокировкой задней оси.
- Клавиатура и геймпад работают параллельно без конфликтов.
- **F7**: экран живой диагностики осей и кнопок.

### 3. ⚡ Разблокировка FPS
- Блокирует встроенный скрипт игры `LimitFrameRate`, удерживая 240 FPS (или безлимит) без сброса на 60.

---

## 🇬🇧 Description (EN)

A clean BepInEx mod for **Easy Delivery Co** adding wheel support, audio streaming, and uncapped FPS:

### 1. 📻 Custom Radio (88.1 FM)
- On-demand streaming from `C:\Music` (FLAC, M4A, AAC, MP3, WAV, WMA, OGG) with minimal RAM usage.
- Dedicated **88.1 FM (Custom)** station with 100% clear signal across the map.
- Original radio stations kept intact with realistic proximity signal scaling up to 100% near towers.
- Clean station tuning with in-car controls or keyboard shortcuts (`.` and `,`).

### 2. 🏎️ Steering Wheel Support (PXN V12 Lite & DirectInput)
- Direct 1:1 steering across full **900 degrees** (bypasses gamepad smoothing curve).
- Fixed 16-bit signed/unsigned HID overflow: holds an exact **`0.00` center** with zero phantom drift.
- Progressive throttle (`z`) and brake (`rz`) pedal curves.
- Handbrake paddle: triggers true in-game Spacebar (`Break`) action to lock rear wheels.
- Seamless multi-device priority: keyboard/gamepad always work alongside the wheel.
- **F7**: in-game live diagnostics overlay.

### 3. ⚡ FPS Unlocker
- Overrides the internal `LimitFrameRate` component to maintain 240 FPS (or uncapped) without dropping to 60.

---

## 📦 Установка / Installation

1. Установите / Install [BepInEx 5.4.23.5 (x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip).
2. Скачайте архив / Download `EasyDeliveryCo-Mods-v1.0.0.zip` из [Releases](../../releases/latest).
3. Распакуйте файлы в / Extract all files to `Easy Delivery Co/BepInEx/plugins/`:
   - `EasyDeliveryCoMods.dll`
   - `NAudio.Core.dll`
   - `NAudio.Wasapi.dll`
4. Положите треки в / Put music in `C:\Music\`.
5. Играйте / Run the game!

---

## 📄 License
MIT License
