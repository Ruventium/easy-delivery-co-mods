# Easy Delivery Co - Complete Enhancement Pack

> **🤖 AI-Generated Mod** - This entire mod was created by OpenCode AI Agent using Claude Opus 5. The code, configuration, and documentation were written autonomously based on game analysis and modding requirements.

Универсальный мод для Easy Delivery Co, объединяющий все улучшения в одном пакете с модульной архитектурой.

## Возможности

### 1. FPS Unlock
- Разблокировка частоты кадров (до 240 FPS или безлимит)
- Отключение VSync
- Настройка целевого FPS

### 2. Graphics Enhancements
- **HD рендеринг**: увеличение внутреннего разрешения с 256x256 до 1920x1080+
- **Отключение PS1 эффектов**: убирает CRT, пикселизацию, низкое разрешение
- **Фильтрация текстур**: Point (пиксели), Bilinear, Trilinear
- **Отключение пост-обработки**: chromatic aberration, lens distortion, vignette
- **Дальность прорисовки**: настройка render distance
- **FPS счётчик**: показ текущего FPS в углу экрана

### 3. Steering Wheel Support
- Полная поддержка рулей (Logitech, Thrustmaster, Fanatec, PXN и др.)
- Настройка осей для руля, газа и тормоза
- Мёртвые зоны для каждой оси
- Инверсия осей
- Чувствительность руля
- Раздельные/объединённые педали
- Debug режим с показом всех значений

### 4. VR Support
- Поддержка VR шлемов через OpenVR/SteamVR
- 6DOF отслеживание головы (позиция + вращение)
- Отключение VR контроллеров (управление через клавиатуру/мышь/геймпад/руль)
- Настройка масштаба и высоты камеры
- Debug режим для VR

## Установка

1. Скачай [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) (x64 версию для Windows)
2. Распакуй BepInEx в папку с игрой
3. Запусти игру один раз для инициализации BepInEx
4. Скачай **[EasyDeliveryCoEnhancements.dll](../../releases/latest)** из Releases
5. Помести DLL в `BepInEx\plugins\`
6. Запусти игру

## Настройка

После первого запуска создаётся конфиг:
`BepInEx\config\opencode.easydeliveryco.enhancements.cfg`

### Модульная система

Каждый модуль можно включить/выключить независимо:

```ini
[1. FPS Unlock]
Enable = false              # Включить FPS unlock
TargetFPS = 60             # 0 = безлимит, 60 = дефолт
DisableVSync = false       # Отключить VSync

[2. Graphics]
Enable = false             # Включить графические улучшения
DisablePS1Effects = false  # Убрать PS1 стиль
RenderWidth = 256          # Дефолт: 256, HD: 1920+
RenderHeight = 256         # Дефолт: 256, HD: 1080+
TextureFilterMode = 0      # 0=Point (пиксели), 1=Bilinear, 2=Trilinear
DisablePostProcessing = false
RenderDistance = 1000

[3. UI]
ShowFPSCounter = false     # FPS счётчик

[4. Steering Wheel]
Enable = false             # Включить поддержку руля
SteeringAxis = Joystick Axis 1
ThrottleAxis = Joystick Axis 3
BrakeAxis = Joystick Axis 2
SteeringDeadzone = 0.05
ThrottleDeadzone = 0.05
BrakeDeadzone = 0.05
SteeringSensitivity = 1.0
CombinedPedals = false
InvertSteering = false
InvertThrottle = false
InvertBrake = false
DebugMode = false          # Показать значения осей

[5. VR Support]
Enable = false             # Включить VR режим
EnableHeadTracking = true  # 6DOF трекинг
HeadTrackingScale = 1.0
CameraHeightOffset = 0.0
DisableVRControllers = true  # Не использовать VR контроллеры
DebugMode = false
```

## Примеры настроек

### Максимальное качество графики
```ini
[2. Graphics]
Enable = true
DisablePS1Effects = true
RenderWidth = 1920
RenderHeight = 1080
TextureFilterMode = 2
DisablePostProcessing = true
RenderDistance = 5000
```

### Разблокировка FPS
```ini
[1. FPS Unlock]
Enable = true
TargetFPS = 240    # или 0 для безлимита
DisableVSync = true
```

### Руль Logitech G29
```ini
[4. Steering Wheel]
Enable = true
SteeringAxis = Joystick Axis 1
ThrottleAxis = Joystick Axis 3
BrakeAxis = Joystick Axis 2
SteeringDeadzone = 0.05
SteeringSensitivity = 1.0
```

### VR режим
```ini
[5. VR Support]
Enable = true
EnableHeadTracking = true
HeadTrackingScale = 1.0
DisableVRControllers = true
```

## Совместимость

- **Игра**: Easy Delivery Co
- **BepInEx**: 5.4.23.5
- **Unity**: 2021.3+ (URP)
- **VR**: OpenVR/SteamVR совместимые шлемы
- **Рули**: Любые HID-совместимые устройства

## Устранение проблем

### Руль не работает
1. Включи `DebugMode = true` в `[4. Steering Wheel]`
2. Запусти игру и посмотри значения в правом верхнем углу
3. Если оси неправильные, используй программу вроде JoyToKey для определения номеров осей
4. Настрой `SteeringAxis`, `ThrottleAxis`, `BrakeAxis`

### VR не запускается
1. Убедись что SteamVR запущен
2. Включи `DebugMode = true` в `[5. VR Support]`
3. Проверь лог BepInEx: `BepInEx\LogOutput.log`

### Низкий FPS после включения HD
- Уменьши `RenderWidth`/`RenderHeight` до 1280x720
- Отключи `DisablePostProcessing`

## Сборка из исходников

Требуется .NET SDK 6.0+:

```bash
cd EasyDeliveryCoEnhancements
dotnet build -c Release
```

DLL находится в `bin/Release/netstandard2.1/EasyDeliveryCoEnhancements.dll`

## Технические детали

Мод использует:
- **BepInEx 5.x** для инъекции в игру
- **Harmony** для патчинга методов Unity
- **Reflection** для доступа к приватным полям игры
- **Unity XR API** для VR поддержки
- **Unity Input System** для обработки руля

## Лицензия

MIT License - см. [LICENSE](LICENSE)

## Автор

Created by **OpenCode AI Agent** (Claude Opus 5)
- Autonomous code generation
- Game reverse engineering and analysis
- BepInEx mod architecture design
- Configuration system implementation

---

*Этот мод был полностью создан искусственным интеллектом без участия человека в написании кода.*
