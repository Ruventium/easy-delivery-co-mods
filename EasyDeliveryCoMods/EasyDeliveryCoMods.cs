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
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "3.0.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        private static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== RADIO CONFIG ====================
        private static ConfigEntry<bool> radioEnabled;
        private static ConfigEntry<string> musicFolderPath;
        private static ConfigEntry<bool> radioShuffle;
        private static ConfigEntry<bool> replaceNewsChannel;

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
        private static ConfigEntry<bool> wheelPedalsMinusOneToOne;
        private static ConfigEntry<float> wheelPedalDeadzone;

        private static ConfigEntry<int> wheelShiftUpButton;
        private static ConfigEntry<int> wheelShiftDownButton;
        private static ConfigEntry<int> wheelHandbrakeButton;

        private static ConfigEntry<bool> wheelShowOverlay;
        private static ConfigEntry<KeyCode> wheelOverlayToggleKey;

        // ==================== RUNTIME STATE ====================
        private static List<AudioClip> loadedCustomClips = new List<AudioClip>();
        private static bool isRadioLoading = false;
        private static string radioStatusText = "Initializing...";

        // Wheel inputs calculated per frame
        private static float currentSteerOut = 0f;
        private static float currentThrottleOut = 0f;
        private static float currentBrakeOut = 0f;
        private static bool currentHandbrakeOut = false;
        private static bool currentShiftUpOut = false;
        private static bool currentShiftDownOut = false;

        private static float[] cachedRawAxes = new float[16];
        private static bool[] cachedRawButtons = new bool[32];
        private static string connectedJoyName = "None";

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            InitConfig();

            Harmony harmony = new Harmony("opencode.easydeliveryco.mods");
            harmony.PatchAll(typeof(EasyDeliveryCoModsPlugin));

            Logger.LogInfo("Easy Delivery Co Mods 3.0.0 (Radio + Wheel) loaded successfully!");
        }

        private void Start()
        {
            if (radioEnabled.Value)
            {
                StartCoroutine(LoadMusicFolderAsync());
            }
        }

        private void Update()
        {
            // Toggle overlay
            if (Input.GetKeyDown(wheelOverlayToggleKey.Value))
            {
                wheelShowOverlay.Value = !wheelShowOverlay.Value;
            }

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

            // Wheel
            wheelEnabled = Config.Bind("2. Steering Wheel", "Enabled", true,
                "Enable DirectInput / PXN steering wheel support.");

            wheelSteerAxis = Config.Bind("2. Steering Wheel", "SteeringAxisNumber", 1,
                "Joystick axis number for steering (usually 1). See F7 overlay to check which axis moves.");
            wheelInvertSteer = Config.Bind("2. Steering Wheel", "InvertSteering", false,
                "Invert steering direction.");
            wheelSteerDeadzone = Config.Bind("2. Steering Wheel", "SteeringDeadzone", 0.02f,
                "Deadzone around wheel center (0.0 to 0.5).");
            wheelSteerSensitivity = Config.Bind("2. Steering Wheel", "SteeringSensitivity", 1.0f,
                "Steering response multiplier (0.5 to 2.0).");
            wheelSteerLinearity = Config.Bind("2. Steering Wheel", "SteeringLinearity", 1.0f,
                "1.0 = linear, higher values give finer control around center.");

            wheelSeparatePedals = Config.Bind("2. Steering Wheel", "SeparatePedals", true,
                "True if gas and brake are separate pedals. False for single combined axis.");
            wheelThrottleAxis = Config.Bind("2. Steering Wheel", "ThrottleAxisNumber", 3,
                "Joystick axis number for gas pedal (usually 2 or 3). Check with F7 overlay.");
            wheelBrakeAxis = Config.Bind("2. Steering Wheel", "BrakeAxisNumber", 2,
                "Joystick axis number for brake pedal (usually 2 or 3). Check with F7 overlay.");
            wheelInvertThrottle = Config.Bind("2. Steering Wheel", "InvertThrottle", false,
                "Invert throttle pedal behavior.");
            wheelInvertBrake = Config.Bind("2. Steering Wheel", "InvertBrake", false,
                "Invert brake pedal behavior.");
            wheelPedalsMinusOneToOne = Config.Bind("2. Steering Wheel", "PedalRestAtMinusOne", true,
                "Set to True if pedal axis rests at -1.0 and goes to +1.0 when pressed (standard DirectInput wheels like PXN/Logitech).");
            wheelPedalDeadzone = Config.Bind("2. Steering Wheel", "PedalDeadzone", 0.05f,
                "Deadzone threshold before pedal starts registering.");

            wheelShiftUpButton = Config.Bind("2. Steering Wheel", "ShiftUpButton", 5,
                "Joystick button for Shift Up (Right paddle). 0 to 31, or -1 to disable.");
            wheelShiftDownButton = Config.Bind("2. Steering Wheel", "ShiftDownButton", 4,
                "Joystick button for Shift Down (Left paddle). 0 to 31, or -1 to disable.");
            wheelHandbrakeButton = Config.Bind("2. Steering Wheel", "HandbrakeButton", 2,
                "Joystick button for handbrake. 0 to 31, or -1 to disable.");

            wheelShowOverlay = Config.Bind("2. Steering Wheel", "ShowLiveOverlay", true,
                "Show real-time on-screen diagnostics overlay for wheel axes and buttons. Press F7 to toggle in-game.");
            wheelOverlayToggleKey = Config.Bind("2. Steering Wheel", "OverlayToggleKey", KeyCode.F7,
                "Keyboard key to toggle diagnostics overlay.");
        }

        // ==================== RADIO LOGIC ====================

        private IEnumerator LoadMusicFolderAsync()
        {
            isRadioLoading = true;
            string folder = musicFolderPath.Value;

            if (!Directory.Exists(folder))
            {
                radioStatusText = $"Folder not found: {folder}";
                Logger.LogWarning($"[CustomRadio] Folder not found: {folder}");
                isRadioLoading = false;
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
                catch (Exception ex)
                {
                    Logger.LogWarning($"[CustomRadio] Error scanning for {pattern}: {ex.Message}");
                }
            }

            if (fileList.Count == 0)
            {
                radioStatusText = $"No audio files found in {folder}";
                Logger.LogWarning($"[CustomRadio] No audio files in {folder}");
                isRadioLoading = false;
                yield break;
            }

            if (radioShuffle.Value)
            {
                var rnd = new System.Random();
                fileList = fileList.OrderBy(x => rnd.Next()).ToList();
            }

            radioStatusText = $"Loading {fileList.Count} tracks...";
            Logger.LogInfo($"[CustomRadio] Found {fileList.Count} tracks. Beginning on-the-fly decoding...");

            List<AudioClip> newClips = new List<AudioClip>();

            foreach (string filePath in fileList)
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string trackName = Path.GetFileNameWithoutExtension(filePath);

                if (ext == ".ogg")
                {
                    // OGG handled directly by UnityWebRequest
                    yield return LoadOggAsync(filePath, trackName, (clip) =>
                    {
                        if (clip != null)
                        {
                            newClips.Add(clip);
                            Logger.LogInfo($"[CustomRadio] Loaded OGG: {trackName} ({clip.length:F1}s)");
                        }
                    });
                }
                else
                {
                    // FLAC, M4A, AAC, MP3, WAV, WMA handled via NAudio / Windows Media Foundation
                    DecodedAudioData decodedData = null;
                    string decodeError = null;

                    // Decode in background thread to eliminate any stuttering
                    Task decodeTask = Task.Run(() =>
                    {
                        try
                        {
                            decodedData = DecodeAudioWithMediaFoundation(filePath);
                        }
                        catch (Exception ex)
                        {
                            decodeError = ex.Message;
                        }
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
                            Logger.LogInfo($"[CustomRadio] Decoded ({ext.ToUpper()}): {trackName} ({clip.length:F1}s)");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[CustomRadio] Failed creating AudioClip for {trackName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"[CustomRadio] Could not decode {filePath}: {decodeError ?? "Unknown error"}");
                    }
                }

                radioStatusText = $"Loaded {newClips.Count}/{fileList.Count} tracks";
                yield return null;
            }

            loadedCustomClips = newClips;
            isRadioLoading = false;
            radioStatusText = $"Ready ({loadedCustomClips.Count} tracks)";
            Logger.LogInfo($"[CustomRadio] Finished! Total custom tracks ready: {loadedCustomClips.Count}");

            ApplyCustomTracksToRadio();
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

        private void ApplyCustomTracksToRadio()
        {
            if (loadedCustomClips == null || loadedCustomClips.Count == 0) return;

            sRadioSystem radio = UnityEngine.Object.FindFirstObjectByType<sRadioSystem>();
            if (radio != null && radio.channels != null && radio.channels.Count > 0)
            {
                RadioChannel targetChannel = radio.channels[0];
                targetChannel.externalTracks = loadedCustomClips;
                targetChannel.queue = targetChannel.GetRandomizedClone();
                Logger.LogInfo($"[CustomRadio] Injected {loadedCustomClips.Count} tracks into {targetChannel.name} ({targetChannel.frequency} FM)");
            }
        }

        [HarmonyPatch(typeof(RadioChannel), "GetRandomizedClone")]
        [HarmonyPostfix]
        private static void Postfix_GetRandomizedClone(RadioChannel __instance, ref AudioClip[] __result)
        {
            if (!radioEnabled.Value || loadedCustomClips == null || loadedCustomClips.Count == 0) return;
            if (!replaceNewsChannel.Value) return;

            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;

            if (radio.channels[0] == __instance)
            {
                __result = loadedCustomClips.ToArray();
            }
        }

        // ==================== WHEEL LOGIC ====================

        private void PollWheelInput()
        {
            string[] joys = Input.GetJoystickNames();
            connectedJoyName = (joys != null && joys.Length > 0 && !string.IsNullOrEmpty(joys[0])) ? joys[0] : "No Joystick detected";

            // Cache raw axes for display and input
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

            // Cache raw buttons
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

                currentThrottleOut = NormalizePedal(rawGas, wheelInvertThrottle.Value, wheelPedalsMinusOneToOne.Value, wheelPedalDeadzone.Value);
                currentBrakeOut = NormalizePedal(rawBrake, wheelInvertBrake.Value, wheelPedalsMinusOneToOne.Value, wheelPedalDeadzone.Value);
            }
            else
            {
                // Combined single axis
                float rawCombined = GetRawAxis(wheelThrottleAxis.Value);
                if (wheelInvertThrottle.Value) rawCombined = -rawCombined;
                currentThrottleOut = Mathf.Clamp01(rawCombined);
                currentBrakeOut = Mathf.Clamp01(-rawCombined);
            }

            // 3. Shifters & Handbrake buttons
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

        private static float NormalizePedal(float raw, bool invert, bool minusOneToOne, float deadzone)
        {
            float norm;
            if (minusOneToOne)
            {
                // Range [-1.0 .. 1.0] where -1.0 is released, 1.0 is pressed
                norm = (raw + 1f) / 2f;
            }
            else
            {
                norm = Mathf.Abs(raw);
            }

            if (invert)
            {
                norm = 1f - norm;
            }

            norm = Mathf.Clamp01(norm);

            if (norm < deadzone)
            {
                return 0f;
            }

            return Mathf.Clamp01((norm - deadzone) / (1f - deadzone));
        }

        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        private static void Postfix_GetInput(sInputManager __instance)
        {
            if (!wheelEnabled.Value) return;

            // Only override if wheel is connected or input is being registered
            if (Mathf.Abs(currentSteerOut) > 0.001f || currentThrottleOut > 0.001f || currentBrakeOut > 0.001f || currentHandbrakeOut)
            {
                // Steer
                __instance.driveInput.x = currentSteerOut;

                // Throttle / Brake
                // In game: positive Y is forward, negative Y is reverse / brake when moving
                if (currentBrakeOut > 0.05f)
                {
                    __instance.brakePressed = true;
                    // When brake is pressed and car is near stop, allow reverse with brake
                    __instance.driveInput.y = currentThrottleOut > 0.05f ? currentThrottleOut : -currentBrakeOut;
                }
                else
                {
                    __instance.driveInput.y = currentThrottleOut;
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
        }

        // ==================== ON-SCREEN DIAGNOSTIC OVERLAY ====================

        private void OnGUI()
        {
            if (!wheelEnabled.Value || !wheelShowOverlay.Value) return;

            GUI.color = Color.white;
            int width = 360;
            int height = 280;
            Rect boxRect = new Rect(Screen.width - width - 15, 15, width, height);

            GUI.Box(boxRect, "");

            GUILayout.BeginArea(new Rect(boxRect.x + 10, boxRect.y + 10, boxRect.width - 20, boxRect.height - 20));

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.yellow }
            };
            GUILayout.Label("=== WHEEL DIAGNOSTICS [F7 to Hide] ===", titleStyle);

            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            GUILayout.Label($"Device: {connectedJoyName}", textStyle);
            GUILayout.Label($"Radio: {radioStatusText}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Inputs to Game ---", textStyle);
            GUILayout.Label($"Steering: {currentSteerOut:+0.00;-0.00;0.00}  |  Throttle: {currentThrottleOut:0.00}  |  Brake: {currentBrakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Raw Joystick Axes (press pedals to check #) ---", textStyle);
            for (int i = 1; i <= 6; i += 2)
            {
                string ax1 = $"Axis {i}: {cachedRawAxes[i]:+0.00;-0.00;0.00}";
                string ax2 = (i + 1 <= 6) ? $"Axis {i + 1}: {cachedRawAxes[i + 1]:+0.00;-0.00;0.00}" : "";
                GUILayout.Label($"{ax1.PadRight(18)}  {ax2}", textStyle);
            }

            GUILayout.Space(4);
            string pressedButtons = "";
            for (int b = 0; b < 16; b++)
            {
                if (cachedRawButtons[b]) pressedButtons += $"Btn{b} ";
            }
            GUILayout.Label($"Pressed Buttons: {(string.IsNullOrEmpty(pressedButtons) ? "None" : pressedButtons)}", textStyle);

            GUILayout.EndArea();
        }
    }
}
