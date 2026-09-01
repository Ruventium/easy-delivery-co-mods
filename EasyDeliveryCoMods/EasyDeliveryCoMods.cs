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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EasyDeliveryCoMods
{
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "3.7.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        public static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== CONFIG ====================
        public static ConfigEntry<bool> radioEnabled;
        public static ConfigEntry<string> musicFolderPath;
        public static ConfigEntry<bool> radioShuffle;
        public static ConfigEntry<int> maxCachedSongs;

        public static ConfigEntry<bool> fpsUnlockEnabled;
        public static ConfigEntry<int> targetFrameRate;
        public static ConfigEntry<bool> disableVSync;

        public static ConfigEntry<bool> wheelEnabled;
        public static ConfigEntry<string> wheelDeviceFilter;
        public static ConfigEntry<string> wheelSteerAxisName;
        public static ConfigEntry<string> wheelGasAxisName;
        public static ConfigEntry<string> wheelBrakeAxisName;

        public static ConfigEntry<float> wheelSteerDeadzone;
        public static ConfigEntry<float> wheelSteerSensitivity;
        public static ConfigEntry<bool> wheelInvertSteer;
        public static ConfigEntry<bool> wheelInvertGas;
        public static ConfigEntry<bool> wheelInvertBrake;

        public static ConfigEntry<bool> showOverlay;
        public static ConfigEntry<KeyCode> overlayKey;

        // ==================== RADIO RUNTIME ====================
        public static List<string> allMusicFilePaths = new List<string>();
        public static List<AudioClip> decodedRadioClips = new List<AudioClip>();
        public static string radioStatusText = "Idle";
        public static bool isDecoderRunning = false;

        // ==================== WHEEL RUNTIME ====================
        public static InputDevice activeWheelDevice = null;
        public static List<AxisControl> availableAxes = new List<AxisControl>();

        public static AxisControl steerAxis = null;
        public static AxisControl gasAxis = null;
        public static AxisControl brakeAxis = null;

        public static float steerOut = 0f;
        public static float gasOut = 0f;
        public static float brakeOut = 0f;

        // Auto-calibrated resting positions
        public static float steerRestValue = 0f;
        public static float gasRestValue = -1f;
        public static float brakeRestValue = -1f;
        public static bool wheelCalibrated = false;

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

            InputSystem.onDeviceChange += OnDeviceChange;

            Harmony harmony = new Harmony("opencode.easydeliveryco.mods");
            harmony.PatchAll(typeof(EasyDeliveryCoModsPlugin));

            Logger.LogInfo("Easy Delivery Co Mods 3.7.0 loaded successfully!");

            if (radioEnabled.Value)
            {
                StartCoroutine(InitRadioPipelineAsync());
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom music from local folder on 88.1 FM and 99.1 FM.");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music",
                "Folder containing your music (FLAC, M4A, AAC, MP3, WAV, WMA, OGG). Decoded in background.");
            radioShuffle = Config.Bind("1. Custom Radio", "Shuffle", true,
                "Shuffle playback order of tracks.");
            maxCachedSongs = Config.Bind("1. Custom Radio", "PlaylistSize", 40,
                "Number of tracks decoded into radio rotation (keeps memory low and game fast).");

            // FPS
            fpsUnlockEnabled = Config.Bind("2. Frame Rate", "UnlockFPS", true,
                "Unlock frame rate limit (blocks game's internal 60 FPS limiter).");
            targetFrameRate = Config.Bind("2. Frame Rate", "TargetFPS", 240,
                "Target frame rate (0 = unlimited).");
            disableVSync = Config.Bind("2. Frame Rate", "DisableVSync", true,
                "Disable vertical sync.");

            // Wheel
            wheelEnabled = Config.Bind("3. Steering Wheel", "Enabled", true,
                "Enable steering wheel support.");
            wheelDeviceFilter = Config.Bind("3. Steering Wheel", "DeviceFilter", "pxn",
                "Search term for wheel device name in InputSystem.");

            wheelSteerAxisName = Config.Bind("3. Steering Wheel", "SteerAxisName", "x",
                "Axis name for steering (usually 'x').");
            wheelGasAxisName = Config.Bind("3. Steering Wheel", "GasAxisName", "z",
                "Axis name for gas pedal (usually 'z' on PXN V12 Lite).");
            wheelBrakeAxisName = Config.Bind("3. Steering Wheel", "BrakeAxisName", "rz",
                "Axis name for brake pedal (usually 'rz' on PXN V12 Lite).");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.03f,
                "Deadzone around wheel center. When wheel is within deadzone, keyboard/gamepad have 100% free control.");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteerSensitivity", 1.0f,
                "1:1 steering multiplier.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteering", true,
                "Invert steering direction.");
            wheelInvertGas = Config.Bind("3. Steering Wheel", "InvertGas", false,
                "Invert gas pedal.");
            wheelInvertBrake = Config.Bind("3. Steering Wheel", "InvertBrake", false,
                "Invert brake pedal.");

            showOverlay = Config.Bind("4. Overlay", "ShowOverlay", true, "Show live diagnostics overlay on F7.");
            overlayKey = Config.Bind("4. Overlay", "ToggleKey", KeyCode.F7, "Key to toggle overlay.");
        }

        private void ApplyFpsSettings()
        {
            if (fpsUnlockEnabled.Value)
            {
                Application.targetFrameRate = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (disableVSync.Value) QualitySettings.vSyncCount = 0;
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            FindAndSetupWheel();
        }

        private void Start()
        {
            FindAndSetupWheel();
        }

        private void Update()
        {
            if (fpsUnlockEnabled.Value)
            {
                int desired = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (Application.targetFrameRate != desired) Application.targetFrameRate = desired;
            }

            if (Input.GetKeyDown(overlayKey.Value))
            {
                showOverlay.Value = !showOverlay.Value;
            }

            if (wheelEnabled.Value)
            {
                if (activeWheelDevice == null)
                {
                    FindAndSetupWheel();
                }
                PollWheel();
            }
        }

        // ==================== WHEEL LOGIC: BULLETPROOF CALIBRATION & 1:1 DIRECT INPUT ====================

        private void FindAndSetupWheel()
        {
            try
            {
                string filter = wheelDeviceFilter.Value.ToLowerInvariant();
                InputDevice match = null;

                foreach (var dev in InputSystem.devices)
                {
                    string dName = dev.displayName.ToLowerInvariant();
                    string lName = dev.layout.ToLowerInvariant();
                    string pName = dev.name.ToLowerInvariant();

                    if (dName.Contains(filter) || lName.Contains(filter) || pName.Contains(filter) ||
                        dName.Contains("pxn") || dName.Contains("v12") || dName.Contains("wheel") ||
                        dev is UnityEngine.InputSystem.Joystick)
                    {
                        match = dev;
                        break;
                    }
                }

                if (match != null && match != activeWheelDevice)
                {
                    activeWheelDevice = match;
                    Logger.LogInfo($"[Wheel] Selected device: '{activeWheelDevice.displayName}' ({activeWheelDevice.layout})");

                    availableAxes.Clear();

                    foreach (var ctrl in activeWheelDevice.allControls)
                    {
                        if (ctrl is AxisControl axis && !(ctrl is ButtonControl))
                        {
                            availableAxes.Add(axis);
                        }
                    }

                    // Steer: 'x'
                    steerAxis = FindAxis(wheelSteerAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("x", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Contains("x"));

                    // Gas: 'z' on PXN V12 Lite
                    gasAxis = FindAxis(wheelGasAxisName.Value)
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("z", StringComparison.OrdinalIgnoreCase) && !a.name.Contains("rz"))
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase));

                    // Brake: 'rz' on PXN V12 Lite
                    brakeAxis = FindAxis(wheelBrakeAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("slider", StringComparison.OrdinalIgnoreCase));

                    wheelCalibrated = false;
                    Logger.LogInfo($"[Wheel Mapped] Steer='{(steerAxis != null ? steerAxis.name : "NULL")}', Gas='{(gasAxis != null ? gasAxis.name : "NULL")}', Brake='{(brakeAxis != null ? brakeAxis.name : "NULL")}'");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error setting up wheel: {ex.Message}");
            }
        }

        private AxisControl FindAxis(string name)
        {
            if (activeWheelDevice == null || string.IsNullOrEmpty(name)) return null;
            return availableAxes.FirstOrDefault(a => a.name.Equals(name, StringComparison.OrdinalIgnoreCase) || a.path.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase));
        }

        private void PollWheel()
        {
            if (activeWheelDevice == null || !activeWheelDevice.added) return;

            // Auto-calibrate center/resting values once when wheel connects
            if (!wheelCalibrated && steerAxis != null && gasAxis != null && brakeAxis != null)
            {
                steerRestValue = steerAxis.ReadValue();
                gasRestValue = gasAxis.ReadValue();
                brakeRestValue = brakeAxis.ReadValue();
                wheelCalibrated = true;
                Logger.LogInfo($"[Wheel Calibrated] SteerCenter={steerRestValue:F3}, GasRest={gasRestValue:F3}, BrakeRest={brakeRestValue:F3}");
            }

            // 1. Steering: Relative to calibrated center position
            if (steerAxis != null)
            {
                float raw = steerAxis.ReadValue();
                // Deviation from physical center
                float delta = raw - steerRestValue;

                if (wheelInvertSteer.Value) delta = -delta;

                float absDelta = Mathf.Abs(delta);
                float dz = wheelSteerDeadzone.Value;

                if (absDelta < dz)
                {
                    // IN DEADZONE: Output is EXACTLY ZERO. Keyboard and gamepad have 100% priority!
                    steerOut = 0f;
                }
                else
                {
                    // Linear 1:1 steering across 900 degrees
                    float norm = (absDelta - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(delta) * wheelSteerSensitivity.Value, -1f, 1f);
                }
            }

            // 2. Gas Pedal (rests at -1.0, presses to +1.0)
            if (gasAxis != null)
            {
                float raw = gasAxis.ReadValue();
                float travel = raw - gasRestValue; // Movement away from rest position
                if (wheelInvertGas.Value) travel = -travel;

                if (travel < 0.08f)
                {
                    gasOut = 0f;
                }
                else
                {
                    gasOut = Mathf.Clamp01((travel - 0.08f) / 1.84f);
                }
            }

            // 3. Brake Pedal (rests at -1.0, presses to +1.0)
            if (brakeAxis != null)
            {
                float raw = brakeAxis.ReadValue();
                float travel = raw - brakeRestValue; // Movement away from rest position
                if (wheelInvertBrake.Value) travel = -travel;

                if (travel < 0.08f)
                {
                    brakeOut = 0f;
                }
                else
                {
                    brakeOut = Mathf.Clamp01((travel - 0.08f) / 1.84f);
                }
            }
        }

        // DIRECT WHEEL INJECTION: Bypasses keyboard Lerp filter and gamepad S-curve!
        [HarmonyPatch(typeof(sCarController), "Move")]
        [HarmonyPrefix]
        private static void Prefix_CarController_Move(sCarController __instance)
        {
            if (!wheelEnabled.Value || activeWheelDevice == null) return;

            // 1. Check if user is pressing Keyboard (A/D or Arrows)
            bool keyboardSteering = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

            // If NOT steering with keyboard, and wheel is turned past deadzone:
            if (!keyboardSteering && Mathf.Abs(steerOut) > 0.005f)
            {
                __instance.input.x = steerOut;
            }

            // 2. Check if user is pressing Keyboard (W/S or Arrows)
            bool keyboardThrottle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                                     Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);

            if (!keyboardThrottle)
            {
                if (gasOut > 0.02f || brakeOut > 0.02f)
                {
                    if (brakeOut > 0.05f)
                    {
                        __instance.SetBreaking(true);
                        __instance.input.y = gasOut > 0.05f ? gasOut : -brakeOut;
                    }
                    else
                    {
                        __instance.input.y = gasOut;
                        __instance.SetBreaking(false);
                    }
                }
            }
        }

        // ==================== CUSTOM RADIO: 100% NATIVE GAME INTEGRATION ====================

        private IEnumerator InitRadioPipelineAsync()
        {
            string folder = musicFolderPath.Value;
            if (!Directory.Exists(folder))
            {
                radioStatusText = $"Folder not found: {folder}";
                yield break;
            }

            string[] exts = { ".flac", ".m4a", ".aac", ".mp3", ".wav", ".wma", ".ogg" };
            allMusicFilePaths.Clear();

            try
            {
                var dirInfo = new DirectoryInfo(folder);
                foreach (var file in dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories))
                {
                    if (exts.Contains(file.Extension.ToLowerInvariant()))
                    {
                        allMusicFilePaths.Add(file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error scanning music folder: {ex.Message}");
            }

            if (allMusicFilePaths.Count == 0)
            {
                radioStatusText = $"No music found in {folder}";
                yield break;
            }

            if (radioShuffle.Value)
            {
                var rnd = new System.Random();
                allMusicFilePaths = allMusicFilePaths.OrderBy(x => rnd.Next()).ToList();
            }

            int countToDecode = Math.Min(allMusicFilePaths.Count, maxCachedSongs.Value);
            radioStatusText = $"Loading {countToDecode} tracks into radio...";
            Logger.LogInfo($"[CustomRadio] Loading {countToDecode} tracks from {folder}...");

            isDecoderRunning = true;
            decodedRadioClips.Clear();

            for (int i = 0; i < countToDecode; i++)
            {
                string path = allMusicFilePaths[i];
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path).ToLowerInvariant();

                AudioClip clip = null;

                if (ext == ".ogg")
                {
                    string uri = "file:///" + path.Replace("\\", "/");
                    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
                    {
                        yield return www.SendWebRequest();
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            clip = DownloadHandlerAudioClip.GetContent(www);
                            if (clip != null) clip.name = name;
                        }
                    }
                }
                else
                {
                    DecodedAudioData data = null;
                    Task task = Task.Run(() =>
                    {
                        try
                        {
                            using (var reader = new MediaFoundationReader(path))
                            {
                                var sp = reader.ToSampleProvider();
                                int ch = sp.WaveFormat.Channels;
                                int sr = sp.WaveFormat.SampleRate;
                                List<float> samples = new List<float>();
                                float[] buf = new float[8192 * ch];
                                int r;
                                while ((r = sp.Read(buf, 0, buf.Length)) > 0)
                                {
                                    for (int s = 0; s < r; s++) samples.Add(buf[s]);
                                }
                                data = new DecodedAudioData { Samples = samples.ToArray(), Channels = ch, SampleRate = sr };
                            }
                        }
                        catch { }
                    });

                    while (!task.IsCompleted) yield return null;

                    if (data != null && data.Samples != null && data.Samples.Length > 0)
                    {
                        try
                        {
                            int total = data.Samples.Length / data.Channels;
                            clip = AudioClip.Create(name, total, data.Channels, data.SampleRate, false);
                            clip.SetData(data.Samples, 0);
                        }
                        catch { }
                    }
                }

                if (clip != null)
                {
                    decodedRadioClips.Add(clip);
                }

                yield return null;
            }

            isDecoderRunning = false;
            radioStatusText = $"{decodedRadioClips.Count} tracks loaded on radio!";
            Logger.LogInfo($"[CustomRadio] Successfully loaded {decodedRadioClips.Count} tracks into game radio!");

            ApplyTracksToGameRadio();
        }

        private class DecodedAudioData
        {
            public float[] Samples;
            public int Channels;
            public int SampleRate;
        }

        private static void ApplyTracksToGameRadio()
        {
            if (decodedRadioClips == null || decodedRadioClips.Count == 0) return;

            sRadioSystem radio = UnityEngine.Object.FindFirstObjectByType<sRadioSystem>();
            if (radio == null || radio.channels == null) return;

            // Feed tracks into Station [4] ('88.1 custom') and Station [1] ('News' 99.1 FM)
            foreach (var ch in radio.channels)
            {
                if (ch != null && (ch.name.ToLowerInvariant().Contains("custom") || ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("news") || ch.frequency == 99.1f))
                {
                    ch.externalTracks.Clear();
                    ch.externalTracks.AddRange(decodedRadioClips);
                    ch.queue = decodedRadioClips.ToArray();
                    Logger.LogInfo($"[CustomRadio] Applied {decodedRadioClips.Count} tracks to station '{ch.name}' ({ch.frequency} FM)");
                }
            }
        }

        // When radio scene starts, populate custom stations with our decoded music
        [HarmonyPatch(typeof(sRadioSystem), "Start")]
        [HarmonyPostfix]
        private static void Postfix_RadioStart_InjectTracks(sRadioSystem __instance)
        {
            ApplyTracksToGameRadio();
        }

        // When AudioLoader finishes, populate customChannel with our music
        [HarmonyPatch(typeof(AudioLoader), "Start")]
        [HarmonyPostfix]
        private static void Postfix_AudioLoader_Start(AudioLoader __instance)
        {
            if (decodedRadioClips != null && decodedRadioClips.Count > 0 && __instance.customChannel != null)
            {
                __instance.customChannel.externalTracks.Clear();
                __instance.customChannel.externalTracks.AddRange(decodedRadioClips);
                __instance.customChannel.queue = decodedRadioClips.ToArray();
                Logger.LogInfo($"[CustomRadio] Populated AudioLoader customChannel with {decodedRadioClips.Count} tracks!");
            }
        }

        // ==================== FPS PATCH ====================

        [HarmonyPatch(typeof(LimitFrameRate), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_LimitFrameRate_Update()
        {
            return !fpsUnlockEnabled.Value;
        }

        // ==================== ON-SCREEN DIAGNOSTIC OVERLAY ====================

        private void OnGUI()
        {
            if (!showOverlay.Value) return;

            GUI.color = Color.white;
            int width = 450;
            int height = 280;
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

            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };

            fpsDeltaTime += (Time.unscaledDeltaTime - fpsDeltaTime) * 0.1f;
            float currentFps = 1.0f / Mathf.Max(0.0001f, fpsDeltaTime);

            sRadioSystem radio = sRadioSystem.instance;
            string stationStr = "N/A";
            if (radio != null && radio.channels != null && radio.channels.Count > 0)
            {
                string chName = (radio.currentChannelIndex >= 0 && radio.currentChannelIndex < radio.channels.Count)
                    ? radio.channels[radio.currentChannelIndex].name : "Scanning...";
                stationStr = $"{radio.Frequency()} FM ({chName})";
            }

            string devName = activeWheelDevice != null ? activeWheelDevice.displayName : "No Wheel detected";
            GUILayout.Label($"Device: {devName}", textStyle);
            GUILayout.Label($"FPS: {currentFps:0.}  |  Station: {stationStr}", textStyle);
            GUILayout.Label($"Radio Status: {radioStatusText}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Vehicle Control ---", textStyle);
            string steerName = steerAxis != null ? steerAxis.name : "none";
            string gasName = gasAxis != null ? gasAxis.name : "none";
            string brakeName = brakeAxis != null ? brakeAxis.name : "none";
            GUILayout.Label($"Steer({steerName}): {steerOut:+0.00;-0.00;0.00} (Center: {steerRestValue:F2})", textStyle);
            GUILayout.Label($"Gas({gasName}): {gasOut:0.00} | Brake({brakeName}): {brakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Device Axes ---", textStyle);
            if (availableAxes.Count > 0)
            {
                string line = "";
                for (int i = 0; i < availableAxes.Count; i++)
                {
                    float val = availableAxes[i].ReadValue();
                    line += $"{availableAxes[i].name}:{val:+0.00;-0.00;0.00} ";
                    if ((i + 1) % 4 == 0)
                    {
                        GUILayout.Label(line, textStyle);
                        line = "";
                    }
                }
                if (!string.IsNullOrEmpty(line)) GUILayout.Label(line, textStyle);
            }
            else
            {
                GUILayout.Label("No axes available", textStyle);
            }

            GUILayout.EndArea();
        }
    }
}
