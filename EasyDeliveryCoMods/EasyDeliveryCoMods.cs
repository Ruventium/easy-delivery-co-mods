using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NAudio.Wave;
using UnityEngine;
using UnityEngine.Networking;

namespace EasyDeliveryCoMods
{
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio, Wheel & FPS", "3.2.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        private static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== RADIO CONFIG ====================
        private static ConfigEntry<bool> radioEnabled;
        private static ConfigEntry<string> musicFolderPath;
        private static ConfigEntry<bool> radioShuffle;
        private static ConfigEntry<bool> replaceNewsChannel;

        private static ConfigEntry<KeyCode> keyNextTrack;
        private static ConfigEntry<KeyCode> keyPrevTrack;
        private static ConfigEntry<KeyCode> keyRadioToggle;
        private static ConfigEntry<KeyCode> keyNextStation;
        private static ConfigEntry<KeyCode> keyPrevStation;

        private static ConfigEntry<int> wheelBtnNextTrack;
        private static ConfigEntry<int> wheelBtnPrevTrack;
        private static ConfigEntry<int> wheelBtnRadioToggle;

        // ==================== FPS CONFIG ====================
        private static ConfigEntry<bool> fpsUnlockEnabled;
        private static ConfigEntry<int> targetFrameRate;
        private static ConfigEntry<bool> disableVSync;

        // ==================== WHEEL CONFIG ====================
        private static ConfigEntry<bool> wheelEnabled;
        private static ConfigEntry<int> wheelSteerAxis;
        private static ConfigEntry<bool> wheelInvertSteer;
        private static ConfigEntry<float> wheelSteerDeadzone;
        private static ConfigEntry<float> wheelSteerSensitivity;
        private static ConfigEntry<float> wheelSteerLinearity;

        private static ConfigEntry<bool> wheelSeparatePedals;
        private static ConfigEntry<int> wheelThrottleAxis;
        private static ConfigEntry<int> wheelBrakeAxis;
        private static ConfigEntry<bool> wheelInvertThrottle;
        private static ConfigEntry<bool> wheelInvertBrake;
        private static ConfigEntry<bool> wheelPedalRestAtMinusOne;
        private static ConfigEntry<float> wheelPedalDeadzone;

        private static ConfigEntry<int> wheelShiftUpButton;
        private static ConfigEntry<int> wheelShiftDownButton;
        private static ConfigEntry<int> wheelHandbrakeButton;

        private static ConfigEntry<bool> wheelShowOverlay;
        private static ConfigEntry<KeyCode> wheelOverlayToggleKey;

        // ==================== RUNTIME STATE ====================
        private static List<AudioClip> loadedCustomClips = new List<AudioClip>();
        private static int currentTrackIndex = 0;
        private static string radioStatusText = "Initializing...";
        private static string currentTrackTitle = "None";
        private static bool isCustomChannelPlaying = false;

        // Wheel inputs calculated per frame
        private static float currentSteerOut = 0f;
        private static float currentThrottleOut = 0f;
        private static float currentBrakeOut = 0f;
        private static bool currentHandbrakeOut = false;
        private static bool currentShiftUpOut = false;
        private static bool currentShiftDownOut = false;

        // Baseline resting values for pedals to prevent phantom driving
        private static float throttleRestValue = -1f;
        private static float brakeRestValue = -1f;
        private static bool pedalsInitialized = false;

        private static float[] cachedRawAxes = new float[16];
        private static bool[] cachedRawButtons = new bool[32];
        private static string connectedJoyName = "None";
        private static float fpsDeltaTime = 0f;

        static EasyDeliveryCoModsPlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string name = new System.Reflection.AssemblyName(args.Name).Name;
                    if (name == "NAudio.Core" || name == "NAudio.Wasapi")
                    {
                        var asm = typeof(EasyDeliveryCoModsPlugin).Assembly;
                        using (var stream = asm.GetManifestResourceStream("EasyDeliveryCoMods." + name + ".dll"))
                        {
                            if (stream != null)
                            {
                                byte[] data = new byte[stream.Length];
                                stream.Read(data, 0, data.Length);
                                return System.Reflection.Assembly.Load(data);
                            }
                        }
                    }
                }
                catch { }
                return null;
            };
        }

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            InitConfig();

            ApplyFpsSettings();

            if (radioEnabled.Value)
            {
                StartCoroutine(LoadMusicFolderAsync());
            }

            Harmony harmony = new Harmony("opencode.easydeliveryco.mods");
            harmony.PatchAll(typeof(EasyDeliveryCoModsPlugin));

            Logger.LogInfo("Easy Delivery Co Mods 3.2.0 loaded successfully!");
        }

        private void ApplyFpsSettings()
        {
            if (fpsUnlockEnabled.Value)
            {
                Application.targetFrameRate = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (disableVSync.Value)
                {
                    QualitySettings.vSyncCount = 0;
                }
                Logger.LogInfo($"[FPS] Target set to {(targetFrameRate.Value <= 0 ? "Unlimited" : targetFrameRate.Value.ToString())}, VSync={(disableVSync.Value ? "Off" : "On")}");
            }
        }

        private void Update()
        {
            // FPS lock retention
            if (fpsUnlockEnabled.Value)
            {
                int desired = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (Application.targetFrameRate != desired)
                {
                    Application.targetFrameRate = desired;
                }
            }

            // Toggle overlay
            if (Input.GetKeyDown(wheelOverlayToggleKey.Value))
            {
                wheelShowOverlay.Value = !wheelShowOverlay.Value;
            }

            // Radio hotkeys
            HandleRadioInput();

            // Wheel polling
            if (wheelEnabled.Value)
            {
                PollWheelInput();
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom radio system from local folder.");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music",
                "Folder containing your music (FLAC, M4A, AAC, MP3, WAV, WMA, OGG). Decoded on the fly in memory.");
            radioShuffle = Config.Bind("1. Custom Radio", "Shuffle", true,
                "Shuffle playback order of your tracks.");
            replaceNewsChannel = Config.Bind("1. Custom Radio", "ReplaceNewsChannel", true,
                "Replace talk/news channel (99.1 FM) with your custom music.");

            keyNextTrack = Config.Bind("1. Custom Radio", "KeyNextTrack", KeyCode.RightBracket,
                "Keyboard key for Next Track (default: ']')");
            keyPrevTrack = Config.Bind("1. Custom Radio", "KeyPrevTrack", KeyCode.LeftBracket,
                "Keyboard key for Previous Track (default: '[')");
            keyRadioToggle = Config.Bind("1. Custom Radio", "KeyRadioToggle", KeyCode.Backslash,
                "Keyboard key to toggle radio on/off (default: '\\')");
            keyNextStation = Config.Bind("1. Custom Radio", "KeyNextStation", KeyCode.Period,
                "Keyboard key to tune station up (default: '.')");
            keyPrevStation = Config.Bind("1. Custom Radio", "KeyPrevStation", KeyCode.Comma,
                "Keyboard key to tune station down (default: ',')");

            wheelBtnNextTrack = Config.Bind("1. Custom Radio", "WheelButtonNextTrack", -1,
                "Joystick button for Next Track (-1 to disable)");
            wheelBtnPrevTrack = Config.Bind("1. Custom Radio", "WheelButtonPrevTrack", -1,
                "Joystick button for Prev Track (-1 to disable)");
            wheelBtnRadioToggle = Config.Bind("1. Custom Radio", "WheelButtonRadioToggle", -1,
                "Joystick button to toggle radio (-1 to disable)");

            // FPS
            fpsUnlockEnabled = Config.Bind("2. Frame Rate", "UnlockFPS", true,
                "Unlock frame rate limit (blocks game's internal 60 FPS limiter).");
            targetFrameRate = Config.Bind("2. Frame Rate", "TargetFPS", 240,
                "Target frame rate (e.g. 240, 144, 120, or 0 for unlimited uncapped FPS).");
            disableVSync = Config.Bind("2. Frame Rate", "DisableVSync", true,
                "Disable vertical sync to allow frame rates higher than monitor refresh rate.");

            // Wheel
            wheelEnabled = Config.Bind("3. Steering Wheel", "Enabled", true,
                "Enable DirectInput / PXN steering wheel support.");

            wheelSteerAxis = Config.Bind("3. Steering Wheel", "SteeringAxisNumber", 1,
                "Joystick axis number for steering (usually 1). Check F7 overlay.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteering", false,
                "Invert steering direction.");
            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteeringDeadzone", 0.02f,
                "Deadzone around wheel center (0.0 to 0.5).");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteeringSensitivity", 1.0f,
                "Steering response multiplier (0.5 to 2.0).");
            wheelSteerLinearity = Config.Bind("3. Steering Wheel", "SteeringLinearity", 1.0f,
                "1.0 = linear, higher values give finer control around center.");

            wheelSeparatePedals = Config.Bind("3. Steering Wheel", "SeparatePedals", true,
                "True if gas and brake are separate pedals. False for single combined axis.");
            wheelThrottleAxis = Config.Bind("3. Steering Wheel", "ThrottleAxisNumber", 2,
                "Joystick axis number for gas pedal (usually 2). Check F7 overlay.");
            wheelBrakeAxis = Config.Bind("3. Steering Wheel", "BrakeAxisNumber", 3,
                "Joystick axis number for brake pedal (usually 3). Check F7 overlay.");
            wheelInvertThrottle = Config.Bind("3. Steering Wheel", "InvertThrottle", false,
                "Invert throttle pedal behavior.");
            wheelInvertBrake = Config.Bind("3. Steering Wheel", "InvertBrake", false,
                "Invert brake pedal behavior.");
            wheelPedalRestAtMinusOne = Config.Bind("3. Steering Wheel", "PedalRestAtMinusOne", false,
                "Set to True ONLY if pedal axis rests at -1.0 and moves to +1.0 when pressed. If it rests at 0.0 or 1.0, leave False.");
            wheelPedalDeadzone = Config.Bind("3. Steering Wheel", "PedalDeadzone", 0.08f,
                "Deadzone threshold before pedal registers (prevents phantom gas/brake).");

            wheelShiftUpButton = Config.Bind("3. Steering Wheel", "ShiftUpButton", 5,
                "Joystick button for Shift Up (Right paddle). 0 to 31, or -1 to disable.");
            wheelShiftDownButton = Config.Bind("3. Steering Wheel", "ShiftDownButton", 4,
                "Joystick button for Shift Down (Left paddle). 0 to 31, or -1 to disable.");
            wheelHandbrakeButton = Config.Bind("3. Steering Wheel", "HandbrakeButton", 2,
                "Joystick button for handbrake. 0 to 31, or -1 to disable.");

            wheelShowOverlay = Config.Bind("3. Steering Wheel", "ShowLiveOverlay", true,
                "Show real-time on-screen diagnostics overlay. Press F7 to toggle in-game.");
            wheelOverlayToggleKey = Config.Bind("3. Steering Wheel", "OverlayToggleKey", KeyCode.F7,
                "Keyboard key to toggle diagnostics overlay.");
        }

        // ==================== RADIO LOGIC ====================

        private void HandleRadioInput()
        {
            if (!radioEnabled.Value) return;

            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null) return;

            // 1. Next Track
            if (Input.GetKeyDown(keyNextTrack.Value) || CheckWheelButtonDown(wheelBtnNextTrack.Value))
            {
                PlayNextCustomTrack();
            }

            // 2. Previous Track
            if (Input.GetKeyDown(keyPrevTrack.Value) || CheckWheelButtonDown(wheelBtnPrevTrack.Value))
            {
                PlayPreviousCustomTrack();
            }

            // 3. Toggle Radio On/Off
            if (Input.GetKeyDown(keyRadioToggle.Value) || CheckWheelButtonDown(wheelBtnRadioToggle.Value))
            {
                radio.ToggleRadio();
            }

            // 4. Next Station
            if (Input.GetKeyDown(keyNextStation.Value))
            {
                TuneNextStation(radio);
            }

            // 5. Prev Station
            if (Input.GetKeyDown(keyPrevStation.Value))
            {
                TunePrevStation(radio);
            }

            // Auto-advance track on finish when on custom station
            if (isCustomChannelPlaying && radio.source != null && radio.source.enabled && radio.source.clip != null)
            {
                if (!radio.source.isPlaying && radio.source.time == 0f)
                {
                    // Track finished, auto advance
                    PlayNextCustomTrack();
                }
            }
        }

        private static bool CheckWheelButtonDown(int btnIndex)
        {
            if (btnIndex < 0 || btnIndex >= 20) return false;
            return Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + btnIndex));
        }

        private static void TuneNextStation(sRadioSystem radio)
        {
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;
            int next = (radio.currentChannelIndex + 1) % radio.channels.Count;
            radio.SetFrequency(next, radio.channels[next].frequency);
            Logger.LogInfo($"[Radio] Tuned to station: {radio.channels[next].name} ({radio.Frequency()} FM)");
        }

        private static void TunePrevStation(sRadioSystem radio)
        {
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;
            int prev = (radio.currentChannelIndex - 1 + radio.channels.Count) % radio.channels.Count;
            radio.SetFrequency(prev, radio.channels[prev].frequency);
            Logger.LogInfo($"[Radio] Tuned to station: {radio.channels[prev].name} ({radio.Frequency()} FM)");
        }

        public static void PlayNextCustomTrack()
        {
            if (loadedCustomClips == null || loadedCustomClips.Count == 0) return;
            currentTrackIndex = (currentTrackIndex + 1) % loadedCustomClips.Count;
            PlayTrackByIndex(currentTrackIndex);
        }

        public static void PlayPreviousCustomTrack()
        {
            if (loadedCustomClips == null || loadedCustomClips.Count == 0) return;
            currentTrackIndex = (currentTrackIndex - 1 + loadedCustomClips.Count) % loadedCustomClips.Count;
            PlayTrackByIndex(currentTrackIndex);
        }

        private static void PlayTrackByIndex(int index)
        {
            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null || radio.source == null) return;

            if (index >= 0 && index < loadedCustomClips.Count)
            {
                AudioClip clip = loadedCustomClips[index];
                currentTrackTitle = clip.name;
                radio.source.clip = clip;
                radio.source.time = 0f;
                if (radio.source.enabled)
                {
                    radio.source.Play();
                }
                isCustomChannelPlaying = true;
                Logger.LogInfo($"[CustomRadio] Playing [{index + 1}/{loadedCustomClips.Count}]: {currentTrackTitle}");
            }
        }

        private IEnumerator LoadMusicFolderAsync()
        {
            string folder = musicFolderPath.Value;

            if (!Directory.Exists(folder))
            {
                radioStatusText = $"Folder not found: {folder}";
                Logger.LogWarning($"[CustomRadio] Folder not found: {folder}");
                yield break;
            }

            string[] searchPatterns = { "*.flac", "*.m4a", "*.aac", "*.mp3", "*.wav", "*.wma", "*.ogg" };
            List<string> fileList = new List<string>();

            foreach (string pattern in searchPatterns)
            {
                try
                {
                    fileList.AddRange(Directory.GetFiles(folder, pattern, SearchOption.AllDirectories));
                }
                catch { }
            }

            if (fileList.Count == 0)
            {
                radioStatusText = $"No audio files found in {folder}";
                yield break;
            }

            if (radioShuffle.Value)
            {
                var rnd = new System.Random();
                fileList = fileList.OrderBy(x => rnd.Next()).ToList();
            }

            radioStatusText = $"Loading {fileList.Count} tracks...";
            Logger.LogInfo($"[CustomRadio] Found {fileList.Count} tracks. Decoding...");

            List<AudioClip> newClips = new List<AudioClip>();

            foreach (string filePath in fileList)
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string trackName = Path.GetFileNameWithoutExtension(filePath);

                if (ext == ".ogg")
                {
                    yield return LoadOggAsync(filePath, trackName, (clip) =>
                    {
                        if (clip != null) newClips.Add(clip);
                    });
                }
                else
                {
                    DecodedAudioData decodedData = null;
                    Task decodeTask = Task.Run(() =>
                    {
                        try
                        {
                            decodedData = DecodeAudioWithMediaFoundation(filePath);
                        }
                        catch { }
                    });

                    while (!decodeTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (decodedData != null && decodedData.Samples != null && decodedData.Samples.Length > 0)
                    {
                        try
                        {
                            int totalSamplesPerChannel = decodedData.Samples.Length / decodedData.Channels;
                            AudioClip clip = AudioClip.Create(trackName, totalSamplesPerChannel, decodedData.Channels, decodedData.SampleRate, false);
                            clip.SetData(decodedData.Samples, 0);
                            newClips.Add(clip);
                        }
                        catch { }
                    }
                }

                radioStatusText = $"Loaded {newClips.Count}/{fileList.Count} tracks";
                yield return null;
            }

            loadedCustomClips = newClips;
            radioStatusText = $"Ready ({loadedCustomClips.Count} tracks)";
            Logger.LogInfo($"[CustomRadio] All {loadedCustomClips.Count} tracks decoded and ready!");

            if (loadedCustomClips.Count > 0)
            {
                currentTrackTitle = loadedCustomClips[0].name;
            }
        }

        private class DecodedAudioData
        {
            public float[] Samples;
            public int Channels;
            public int SampleRate;
        }

        private static DecodedAudioData DecodeAudioWithMediaFoundation(string filePath)
        {
            using (var reader = new MediaFoundationReader(filePath))
            {
                var sampleProvider = reader.ToSampleProvider();
                int channels = sampleProvider.WaveFormat.Channels;
                int sampleRate = sampleProvider.WaveFormat.SampleRate;

                List<float> allSamples = new List<float>();
                float[] buffer = new float[8192 * channels];
                int read;

                while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        allSamples.Add(buffer[i]);
                    }
                }

                return new DecodedAudioData
                {
                    Samples = allSamples.ToArray(),
                    Channels = channels,
                    SampleRate = sampleRate
                };
            }
        }

        private IEnumerator LoadOggAsync(string filePath, string trackName, Action<AudioClip> callback)
        {
            string uri = "file:///" + filePath.Replace("\\", "/");
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    if (clip != null)
                    {
                        clip.name = trackName;
                        callback(clip);
                        yield break;
                    }
                }
            }
            callback(null);
        }

        // Custom Radio Playback Hooks
        [HarmonyPatch(typeof(sRadioSystem), "UpdateTracks")]
        [HarmonyPrefix]
        private static bool Prefix_UpdateTracks(sRadioSystem __instance)
        {
            if (!radioEnabled.Value || loadedCustomClips == null || loadedCustomClips.Count == 0) return true;
            if (!replaceNewsChannel.Value) return true;

            // If we are tuned to channel 0 (news / custom station 99.1 FM)
            if (__instance.currentChannelIndex == 0)
            {
                isCustomChannelPlaying = true;
                __instance.signalStrength = 1f;

                if (!__instance.source.enabled)
                {
                    return false;
                }

                if (__instance.source.clip == null || !loadedCustomClips.Contains(__instance.source.clip))
                {
                    PlayTrackByIndex(currentTrackIndex);
                }

                return false; // Skip original game track updater
            }
            else
            {
                isCustomChannelPlaying = false;
                return true; // Let other stations play normally
            }
        }

        // ==================== FPS PATCH ====================

        [HarmonyPatch(typeof(LimitFrameRate), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_LimitFrameRate_Update()
        {
            return !fpsUnlockEnabled.Value;
        }

        // ==================== WHEEL LOGIC ====================

        private void PollWheelInput()
        {
            string[] joys = Input.GetJoystickNames();
            bool hasJoy = (joys != null && joys.Length > 0 && !string.IsNullOrEmpty(joys[0]));
            connectedJoyName = hasJoy ? joys[0] : "No Joystick detected";

            if (!hasJoy)
            {
                currentSteerOut = 0f;
                currentThrottleOut = 0f;
                currentBrakeOut = 0f;
                currentHandbrakeOut = false;
                currentShiftUpOut = false;
                currentShiftDownOut = false;
                for (int i = 0; i < cachedRawAxes.Length; i++) cachedRawAxes[i] = 0f;
                for (int b = 0; b < cachedRawButtons.Length; b++) cachedRawButtons[b] = false;
                pedalsInitialized = false;
                return;
            }

            // Read raw axes
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    cachedRawAxes[i] = Input.GetAxisRaw("Joystick Axis " + i);
                }
                catch
                {
                    cachedRawAxes[i] = 0f;
                }
            }

            // Read raw buttons
            for (int b = 0; b < 20; b++)
            {
                try
                {
                    cachedRawButtons[b] = Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + b));
                }
                catch
                {
                    cachedRawButtons[b] = false;
                }
            }

            // Learn resting positions of pedals once when wheel connects to guarantee zero phantom throttle
            if (!pedalsInitialized)
            {
                throttleRestValue = GetRawAxis(wheelThrottleAxis.Value);
                brakeRestValue = GetRawAxis(wheelBrakeAxis.Value);
                pedalsInitialized = true;
                Logger.LogInfo($"[Pedals Calibrated] Throttle rest: {throttleRestValue:F2}, Brake rest: {brakeRestValue:F2}");
            }

            // 1. Steering
            float rawSteer = GetRawAxis(wheelSteerAxis.Value);
            if (wheelInvertSteer.Value) rawSteer = -rawSteer;

            float absSteer = Mathf.Abs(rawSteer);
            float deadzone = wheelSteerDeadzone.Value;
            if (absSteer < deadzone)
            {
                currentSteerOut = 0f;
            }
            else
            {
                float normalized = (absSteer - deadzone) / (1f - deadzone);
                if (wheelSteerLinearity.Value != 1f && wheelSteerLinearity.Value > 0.1f)
                {
                    normalized = Mathf.Pow(normalized, wheelSteerLinearity.Value);
                }
                currentSteerOut = Mathf.Clamp(normalized * Mathf.Sign(rawSteer) * wheelSteerSensitivity.Value, -1f, 1f);
            }

            // 2. Pedals
            if (wheelSeparatePedals.Value)
            {
                float rawGas = GetRawAxis(wheelThrottleAxis.Value);
                float rawBrake = GetRawAxis(wheelBrakeAxis.Value);

                currentThrottleOut = ProcessPedalInput(rawGas, throttleRestValue, wheelInvertThrottle.Value, wheelPedalRestAtMinusOne.Value, wheelPedalDeadzone.Value);
                currentBrakeOut = ProcessPedalInput(rawBrake, brakeRestValue, wheelInvertBrake.Value, wheelPedalRestAtMinusOne.Value, wheelPedalDeadzone.Value);
            }
            else
            {
                // Combined single axis
                float rawCombined = GetRawAxis(wheelThrottleAxis.Value);
                if (wheelInvertThrottle.Value) rawCombined = -rawCombined;
                currentThrottleOut = Mathf.Clamp01(rawCombined);
                currentBrakeOut = Mathf.Clamp01(-rawCombined);
            }

            // 3. Shifters & Handbrake
            currentShiftUpOut = (wheelShiftUpButton.Value >= 0 && wheelShiftUpButton.Value < 20 && cachedRawButtons[wheelShiftUpButton.Value]);
            currentShiftDownOut = (wheelShiftDownButton.Value >= 0 && wheelShiftDownButton.Value < 20 && cachedRawButtons[wheelShiftDownButton.Value]);
            currentHandbrakeOut = (wheelHandbrakeButton.Value >= 0 && wheelHandbrakeButton.Value < 20 && cachedRawButtons[wheelHandbrakeButton.Value]);
        }

        private static float GetRawAxis(int axisNumber)
        {
            if (axisNumber >= 1 && axisNumber <= 10)
            {
                return cachedRawAxes[axisNumber];
            }
            return 0f;
        }

        private static float ProcessPedalInput(float raw, float restValue, bool invert, bool restAtMinusOne, float deadzone)
        {
            // If explicit restAtMinusOne is configured
            if (restAtMinusOne)
            {
                float norm = (raw + 1f) / 2f;
                if (invert) norm = 1f - norm;
                norm = Mathf.Clamp01(norm);
                return (norm < deadzone) ? 0f : Mathf.Clamp01((norm - deadzone) / (1f - deadzone));
            }

            // Intelligent resting baseline detection:
            // Pedal output is 0.0 unless axis travels significantly away from its rest position!
            float travel = Mathf.Abs(raw - restValue);
            if (travel < deadzone)
            {
                return 0f; // 100% Guaranteed zero when pedal is not pressed
            }

            // Direct mapping when pressed
            float value = Mathf.Clamp01((travel - deadzone) / (1f - deadzone));
            if (invert) value = 1f - value;
            return value;
        }

        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        private static void Postfix_GetInput(sInputManager __instance)
        {
            if (!wheelEnabled.Value) return;

            string[] joys = Input.GetJoystickNames();
            if (joys == null || joys.Length == 0 || string.IsNullOrEmpty(joys[0])) return;

            // Only override steering if wheel is actively turned
            if (Mathf.Abs(currentSteerOut) > 0.02f)
            {
                __instance.driveInput.x = currentSteerOut;
            }

            // Only override throttle/brake if pedal is actively pushed
            if (currentThrottleOut > 0.02f || currentBrakeOut > 0.02f)
            {
                if (currentBrakeOut > 0.05f)
                {
                    __instance.brakePressed = true;
                    __instance.driveInput.y = currentThrottleOut > 0.05f ? currentThrottleOut : -currentBrakeOut;
                }
                else
                {
                    __instance.driveInput.y = currentThrottleOut;
                }
            }

            if (currentHandbrakeOut)
            {
                __instance.brakePressed = true;
            }

            if (currentShiftUpOut)
            {
                __instance.shiftUp = true;
            }
            if (currentShiftDownOut)
            {
                __instance.shiftDown = true;
            }
        }

        // ==================== ON-SCREEN DIAGNOSTIC OVERLAY ====================

        private void OnGUI()
        {
            if (!wheelShowOverlay.Value) return;

            GUI.color = Color.white;
            int width = 380;
            int height = 310;
            Rect boxRect = new Rect(Screen.width - width - 15, 15, width, height);

            GUI.Box(boxRect, "");

            GUILayout.BeginArea(new Rect(boxRect.x + 10, boxRect.y + 10, boxRect.width - 20, boxRect.height - 20));

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.yellow }
            };
            GUILayout.Label("=== EASY DELIVERY CO MODS [F7: Hide] ===", titleStyle);

            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            fpsDeltaTime += (Time.unscaledDeltaTime - fpsDeltaTime) * 0.1f;
            float currentFps = 1.0f / Mathf.Max(0.0001f, fpsDeltaTime);

            sRadioSystem radio = sRadioSystem.instance;
            string stationStr = (radio != null) ? $"{radio.Frequency()} FM" : "N/A";

            GUILayout.Label($"Device: {connectedJoyName}", textStyle);
            GUILayout.Label($"FPS: {currentFps:0.}  |  Station: {stationStr}", textStyle);
            GUILayout.Label($"Track [{(loadedCustomClips.Count > 0 ? (currentTrackIndex + 1).ToString() : "0")}/{loadedCustomClips.Count}]: {currentTrackTitle}", textStyle);
            GUILayout.Label($"Keys: ']' Next | '[' Prev | '\\' Radio | '.' / ',' Station", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Vehicle Control ---", textStyle);
            GUILayout.Label($"Steer: {currentSteerOut:+0.00;-0.00;0.00} | Gas: {currentThrottleOut:0.00} | Brake: {currentBrakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Raw Axes (press pedals to check #) ---", textStyle);
            for (int i = 1; i <= 6; i += 2)
            {
                string ax1 = $"Axis {i}: {cachedRawAxes[i]:+0.00;-0.00;0.00}";
                string ax2 = (i + 1 <= 6) ? $"Axis {i + 1}: {cachedRawAxes[i + 1]:+0.00;-0.00;0.00}" : "";
                GUILayout.Label($"{ax1.PadRight(18)}  {ax2}", textStyle);
            }

            GUILayout.Space(2);
            string pressedButtons = "";
            for (int b = 0; b < 16; b++)
            {
                if (cachedRawButtons[b]) pressedButtons += $"Btn{b} ";
            }
            GUILayout.Label($"Buttons: {(string.IsNullOrEmpty(pressedButtons) ? "None" : pressedButtons)}", textStyle);

            GUILayout.EndArea();
        }
    }
}
