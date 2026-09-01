# Easy Delivery Co - Custom Radio, Steering Wheel & FPS Unlock Mod

Качественный и стабильный BepInEx-мод для игры **Easy Delivery Co**, добавляющий полноценную поддержку рулей, кастомного радио с декодированием любых аудиоформатов на лету и разблокировку частоты кадров.

---

## ⚡ 1. Разблокировка FPS (FPS Unlock)

- **Отключение скрытого лимитера**: игра содержит внутренний скрипт `LimitFrameRate`, который при загрузке локаций принудительно сбрасывает частоту кадров до 60 FPS. Мод блокирует этот скрипт и удерживает заданную частоту кадров.
- **Настройка частоты**: по умолчанию 240 FPS (можно установить любое значение, либо `0` для полностью неограниченного FPS).
- **Отключение VSync**: позволяет получать максимальную герцовку без задержек ввода.

---

## 📻 2. Кастомное радио (Custom Radio)

- **Конвертация на лету**: использует встроенный декодер Windows Media Foundation (NAudio) — декодирует треки прямо в оперативную память в фоновом потоке. Никаких внешних утилит (вроде ffmpeg), лагов и микрофризов.
- **Поддерживаемые форматы**:
  - `FLAC` (`.flac`)
  - `M4A / AAC` (`.m4a`, `.aac`)
  - `MP3` (`.mp3`)
  - `WAV` (`.wav`)
  - `OGG` (`.ogg`)
  - `WMA` (`.wma`)
- **Папка с музыкой**: по умолчанию читает `C:\Music` (включая все подпапки). Путь можно легко изменить в файле настроек.
- **Замена волны новостей**: автоматически заменяет станцию разговорного радио (99.1 FM) на твою музыку.
- **Перемешивание (Shuffle)**: рандомизирует порядок воспроизведения при каждом запуске.

---

## 🏎️ 3. Поддержка руля (Steering Wheel / PXN V12 Lite)

- **Совместимость**: PXN V12 Lite, Logitech G25/G27/G29/G920, Thrustmaster, Fanatec и любые DirectInput/XInput рули и геймпады.
- **Плавное управление**: прямое управление поворотом колес с настраиваемой чувствительностью, линейностью и мёртвой зоной.
- **Педали**:
  - Раздельные педали (газ и тормоз на независимых осях).
  - Поддержка стандартных DirectInput педалей (диапазон `-1.0` ... `+1.0` с авто-нормализацией).
  - Настраиваемые мёртвые зоны и инверсия для каждой педали.
- **Подрулевые лепестки и кнопки**:
  - Переключение передач (Shift Up / Shift Down).
  - Ручной тормоз.
- **Диагностический оверлей (F7)**:
  - Нажми **F7** прямо во время игры, чтобы открыть/скрыть экран диагностики.
  - В реальном времени показывает FPS, имя подключенного устройства, живые значения всех осей (Axis 1–6), номера нажатых кнопок и итоговый газ/тормоз/руль.

---

## 📦 Установка

1. Скачай и установи **[BepInEx 5.4.23.5 (x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)** в корневую папку игры:
   `...\steamapps\common\Easy Delivery Co\`
2. Скачай архив из последнего **[Releases](../../releases)**.
3. Помести содержимое архива в папку `BepInEx\plugins\`:
   - `EasyDeliveryCoMods.dll`
   - `NAudio.Core.dll`
   - `NAudio.Wasapi.dll`
4. Положи свои треки в `C:\Music` (или настрой свой путь в конфиге).
5. Запусти игру!

---

## ⚙️ Настройка

Конфигурационный файл генерируется автоматически после первого запуска игры:
`...\Easy Delivery Co\BepInEx\config\opencode.easydeliveryco.mods.cfg`

```ini
[1. Custom Radio]
Enabled = true
MusicFolder = C:\Music
Shuffle = true
ReplaceNewsChannel = true

[2. Frame Rate]
UnlockFPS = true
TargetFPS = 240
DisableVSync = true

[3. Steering Wheel]
Enabled = true
SteeringAxisNumber = 1
SteeringDeadzone = 0.02
SteeringSensitivity = 1
SteeringLinearity = 1
SeparatePedals = true
ThrottleAxisNumber = 3
BrakeAxisNumber = 2
PedalRestAtMinusOne = true
ShiftUpButton = 5
ShiftDownButton = 4
HandbrakeButton = 2
ShowLiveOverlay = true
```

---

## 🛠️ Сборка из исходников

Требуется .NET SDK 6.0+:

```bash
cd EasyDeliveryCoMods
dotnet build -c Release
```

---

## 📄 Лицензия

MIT License — см. файл [LICENSE](LICENSE).
