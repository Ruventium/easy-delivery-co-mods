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
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "3.3.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        private static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== CONFIG ====================
        private static ConfigEntry<bool> radioEnabled;
        private static ConfigEntry<string> musicFolderPath;
        private static ConfigEntry<bool> radioShuffle;
        private static ConfigEntry<bool> replaceNewsChannel;

        private static ConfigEntry<bool> fpsUnlockEnabled;
        private static ConfigEntry<int> targetFrameRate;
        private static ConfigEntry<bool> disableVSync;

        private static ConfigEntry<bool> wheelEnabled;
        private static ConfigEntry<string> wheelDeviceFilter;
        private static ConfigEntry<string> wheelSteerControlName;
        private static ConfigEntry<string> wheelThrottleControlName;
        private static ConfigEntry<string> wheelBrakeControlName;
        private static ConfigEntry<float> wheelSteerDeadzone;
        private static ConfigEntry<float> wheelSteerSensitivity;
        private static ConfigEntry<bool> wheelInvertSteer;
        private static ConfigEntry<bool> wheelInvertThrottle;
        private static ConfigEntry<bool> wheelInvertBrake;

        private static ConfigEntry<bool> showOverlay;
        private static ConfigEntry<KeyCode> overlayKey;

        // ==================== RADIO RUNTIME ====================
        private static List<string> musicFiles = new List<string>();
        private static int currentTrackIndex = 0;
        private static AudioClip currentCustomClip = null;
        private static string currentTrackTitle = "None";
        private static bool isDecodingTrack = false;
        private static string radioStatusText = "Idle";

        // ==================== WHEEL RUNTIME ====================
        private static InputDevice activeWheelDevice = null;
        private static List<AxisControl> activeAxes = new List<AxisControl>();
        private static List<ButtonControl> activeButtons = new List<ButtonControl>();

        private static AxisControl steerControl = null;
        private static AxisControl throttleControl = null;
        private static AxisControl brakeControl = null;

        private static float steerOut = 0f;
        private static float throttleOut = 0f;
        private static float brakeOut = 0f;

        private static float throttleRestValue = 0f;
        private static float brakeRestValue = 0f;
        private static bool pedalsZeroed = false;

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

            Logger.LogInfo("Easy Delivery Co Mods 3.3.0 initialized!");

            if (radioEnabled.Value)
            {
                ScanMusicFilesQuick();
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true, "Enable custom music system.");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music", "Folder with music files (FLAC, M4A, AAC, MP3, WAV, OGG, WMA).");
            radioShuffle = Config.Bind("1. Custom Radio", "Shuffle", true, "Shuffle playback order.");
            replaceNewsChannel = Config.Bind("1. Custom Radio", "ReplaceNewsChannel", true, "Play custom music on news station 99.1 FM. Other stations (101.7 D&B etc) work normally!");

            // FPS
            fpsUnlockEnabled = Config.Bind("2. Frame Rate", "UnlockFPS", true, "Unlock 60 FPS cap.");
            targetFrameRate = Config.Bind("2. Frame Rate", "TargetFPS", 240, "Target frame rate (0 = unlimited).");
            disableVSync = Config.Bind("2. Frame Rate", "DisableVSync", true, "Disable VSync.");

            // Wheel
            wheelEnabled = Config.Bind("3. Steering Wheel", "Enabled", true, "Enable steering wheel input via Unity InputSystem.");
            wheelDeviceFilter = Config.Bind("3. Steering Wheel", "DeviceNameFilter", "pxn", "Name or substring of wheel device (e.g. 'pxn', 'wheel', 'v12', 'joystick').");
            wheelSteerControlName = Config.Bind("3. Steering Wheel", "SteerControl", "x", "Control name for steering (usually 'x' or 'stick/x'). Check F7 overlay.");
            wheelThrottleControlName = Config.Bind("3. Steering Wheel", "ThrottleControl", "y", "Control name for throttle pedal. Check F7 overlay.");
            wheelBrakeControlName = Config.Bind("3. Steering Wheel", "BrakeControl", "z", "Control name for brake pedal (or 'rz'). Check F7 overlay.");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.02f, "Deadzone for steering.");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteerSensitivity", 1.0f, "Steering sensitivity multiplier.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteer", false, "Invert steering.");
            wheelInvertThrottle = Config.Bind("3. Steering Wheel", "InvertThrottle", false, "Invert throttle.");
            wheelInvertBrake = Config.Bind("3. Steering Wheel", "InvertBrake", false, "Invert brake.");

            showOverlay = Config.Bind("4. Overlay", "ShowOverlay", true, "Show live calibration overlay on F7.");
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
            Logger.LogInfo($"[InputSystem] Device changed: {device.displayName} -> {change}");
            FindWheelDevice();
        }

        private void Start()
        {
            FindWheelDevice();
        }

        private void Update()
        {
            // Keep FPS unlocked
            if (fpsUnlockEnabled.Value)
            {
                int desired = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (Application.targetFrameRate != desired) Application.targetFrameRate = desired;
            }

            if (Input.GetKeyDown(overlayKey.Value))
            {
                showOverlay.Value = !showOverlay.Value;
            }

            // Next / Prev track shortcuts on keyboard
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                NextCustomTrack();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                PrevCustomTrack();
            }

            if (wheelEnabled.Value)
            {
                if (activeWheelDevice == null)
                {
                    FindWheelDevice();
                }
                PollWheelDevice();
            }
        }

        // ==================== WHEEL DETECTION & POLLING ====================

        private void FindWheelDevice()
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
                        dName.Contains("pxn") || dName.Contains("wheel") || dName.Contains("v12") ||
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

                    activeAxes.Clear();
                    activeButtons.Clear();

                    foreach (var ctrl in activeWheelDevice.allControls)
                    {
                        if (ctrl is AxisControl axis && !(ctrl is ButtonControl))
                        {
                            activeAxes.Add(axis);
                        }
                        else if (ctrl is ButtonControl btn)
                        {
                            activeButtons.Add(btn);
                        }
                    }

                    // Auto-bind controls
                    steerControl = FindAxisByName(wheelSteerControlName.Value) ?? activeAxes.FirstOrDefault(a => a.name.EndsWith("x"));
                    throttleControl = FindAxisByName(wheelThrottleControlName.Value) ?? (activeAxes.Count > 1 ? activeAxes[1] : null);
                    brakeControl = FindAxisByName(wheelBrakeControlName.Value) ?? (activeAxes.Count > 2 ? activeAxes[2] : null);

                    pedalsZeroed = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error finding wheel device: {ex.Message}");
            }
        }

        private AxisControl FindAxisByName(string name)
        {
            if (activeWheelDevice == null || string.IsNullOrEmpty(name)) return null;
            return activeAxes.FirstOrDefault(a => a.name.Equals(name, StringComparison.OrdinalIgnoreCase) || a.path.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase));
        }

        private void PollWheelDevice()
        {
            if (activeWheelDevice == null || !activeWheelDevice.added) return;

            // Zero baseline on first poll
            if (!pedalsZeroed && throttleControl != null && brakeControl != null)
            {
                throttleRestValue = throttleControl.ReadValue();
                brakeRestValue = brakeControl.ReadValue();
                pedalsZeroed = true;
                Logger.LogInfo($"[Wheel] Pedals zeroed: ThrottleRest={throttleRestValue:F2}, BrakeRest={brakeRestValue:F2}");
            }

            // 1. Steering
            if (steerControl != null)
            {
                float raw = steerControl.ReadValue();
                if (wheelInvertSteer.Value) raw = -raw;

                float abs = Mathf.Abs(raw);
                float dz = wheelSteerDeadzone.Value;
                if (abs < dz)
                {
                    steerOut = 0f;
                }
                else
                {
                    float norm = (abs - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(raw) * wheelSteerSensitivity.Value, -1f, 1f);
                }
            }

            // 2. Throttle
            if (throttleControl != null)
            {
                float raw = throttleControl.ReadValue();
                float delta = Mathf.Abs(raw - throttleRestValue);
                if (delta < 0.08f)
                {
                    throttleOut = 0f;
                }
                else
                {
                    float val = Mathf.Clamp01((delta - 0.08f) / (1f - 0.08f));
                    if (wheelInvertThrottle.Value) val = 1f - val;
                    throttleOut = val;
                }
            }

            // 3. Brake
            if (brakeControl != null)
            {
                float raw = brakeControl.ReadValue();
                float delta = Mathf.Abs(raw - brakeRestValue);
                if (delta < 0.08f)
                {
                    brakeOut = 0f;
                }
                else
                {
                    float val = Mathf.Clamp01((delta - 0.08f) / (1f - 0.08f));
                    if (wheelInvertBrake.Value) val = 1f - val;
                    brakeOut = val;
                }
            }
        }

        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        private static void Postfix_GetInput(sInputManager __instance)
        {
            if (!wheelEnabled.Value || activeWheelDevice == null) return;

            // Only override if wheel/pedals are actively being pressed
            if (Mathf.Abs(steerOut) > 0.015f)
            {
                __instance.driveInput.x = steerOut;
            }

            if (throttleOut > 0.02f || brakeOut > 0.02f)
            {
                if (brakeOut > 0.05f)
                {
                    __instance.brakePressed = true;
                    __instance.driveInput.y = throttleOut > 0.05f ? throttleOut : -brakeOut;
                }
                else
                {
                    __instance.driveInput.y = throttleOut;
                }
            }
        }

        // ==================== CUSTOM RADIO (LIGHTWEIGHT & ON-DEMAND) ====================

        private void ScanMusicFilesQuick()
        {
            string folder = musicFolderPath.Value;
            if (!Directory.Exists(folder))
            {
                radioStatusText = $"Folder not found: {folder}";
                return;
            }

            string[] exts = { ".flac", ".m4a", ".aac", ".mp3", ".wav", ".wma", ".ogg" };
            musicFiles.Clear();

            try
            {
                var dirInfo = new DirectoryInfo(folder);
                foreach (var file in dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories))
                {
                    if (exts.Contains(file.Extension.ToLowerInvariant()))
                    {
                        musicFiles.Add(file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error scanning music files: {ex.Message}");
            }

            if (radioShuffle.Value && musicFiles.Count > 0)
            {
                var rnd = new System.Random();
                musicFiles = musicFiles.OrderBy(x => rnd.Next()).ToList();
            }

            radioStatusText = $"Found {musicFiles.Count} tracks";
            Logger.LogInfo($"[CustomRadio] Scanned {musicFiles.Count} tracks from {folder}");

            if (musicFiles.Count > 0)
            {
                currentTrackTitle = Path.GetFileNameWithoutExtension(musicFiles[0]);
            }
        }

        public static void NextCustomTrack()
        {
            if (musicFiles.Count == 0) return;
            currentTrackIndex = (currentTrackIndex + 1) % musicFiles.Count;
            LoadAndPlayCurrentTrack();
        }

        public static void PrevCustomTrack()
        {
            if (musicFiles.Count == 0) return;
            currentTrackIndex = (currentTrackIndex - 1 + musicFiles.Count) % musicFiles.Count;
            LoadAndPlayCurrentTrack();
        }

        private static void LoadAndPlayCurrentTrack()
        {
            if (Instance == null || musicFiles.Count == 0 || isDecodingTrack) return;
            Instance.StartCoroutine(DecodeAndPlayTrackCoroutine(musicFiles[currentTrackIndex]));
        }

        private static IEnumerator DecodeAndPlayTrackCoroutine(string filePath)
        {
            isDecodingTrack = true;
            string trackName = Path.GetFileNameWithoutExtension(filePath);
            currentTrackTitle = trackName;
            radioStatusText = $"Loading: {trackName}...";

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            AudioClip clip = null;

            if (ext == ".ogg")
            {
                string uri = "file:///" + filePath.Replace("\\", "/");
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        clip = DownloadHandlerAudioClip.GetContent(www);
                        if (clip != null) clip.name = trackName;
                    }
                }
            }
            else
            {
                DecodedAudioData decodedData = null;
                Task task = Task.Run(() =>
                {
                    try
                    {
                        using (var reader = new MediaFoundationReader(filePath))
                        {
                            var sp = reader.ToSampleProvider();
                            int ch = sp.WaveFormat.Channels;
                            int sr = sp.WaveFormat.SampleRate;
                            List<float> samples = new List<float>();
                            float[] buf = new float[8192 * ch];
                            int r;
                            while ((r = sp.Read(buf, 0, buf.Length)) > 0)
                            {
                                for (int i = 0; i < r; i++) samples.Add(buf[i]);
                            }
                            decodedData = new DecodedAudioData { Samples = samples.ToArray(), Channels = ch, SampleRate = sr };
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed decoding {trackName}: {ex.Message}");
                    }
                });

                while (!task.IsCompleted) yield return null;

                if (decodedData != null && decodedData.Samples != null && decodedData.Samples.Length > 0)
                {
                    try
                    {
                        int totalSamplesPerChannel = decodedData.Samples.Length / decodedData.Channels;
                        clip = AudioClip.Create(trackName, totalSamplesPerChannel, decodedData.Channels, decodedData.SampleRate, false);
                        clip.SetData(decodedData.Samples, 0);
                    }
                    catch { }
                }
            }

            if (clip != null)
            {
                if (currentCustomClip != null)
                {
                    Destroy(currentCustomClip); // Clean up memory immediately!
                }
                currentCustomClip = clip;

                sRadioSystem radio = sRadioSystem.instance;
                if (radio != null && radio.source != null)
                {
                    radio.source.clip = currentCustomClip;
                    radio.source.time = 0f;
                    if (radio.source.enabled)
                    {
                        radio.source.Play();
                    }
                }

                radioStatusText = $"Playing: {trackName}";
                Logger.LogInfo($"[CustomRadio] Now playing: {trackName} ({clip.length:F1}s)");
            }

            isDecodingTrack = false;
        }

        private class DecodedAudioData
        {
            public float[] Samples;
            public int Channels;
            public int SampleRate;
        }

        // ==================== HARMONY HOOKS ====================

        // FPS Cap blocker
        [HarmonyPatch(typeof(LimitFrameRate), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_LimitFrameRate_Update()
        {
            return !fpsUnlockEnabled.Value;
        }

        // Seamless Radio Hook: ONLY intercept when tuned to News station 99.1 FM
        [HarmonyPatch(typeof(sRadioSystem), "UpdateTracks")]
        [HarmonyPrefix]
        private static bool Prefix_RadioUpdateTracks(sRadioSystem __instance)
        {
            if (!radioEnabled.Value || musicFiles.Count == 0 || !replaceNewsChannel.Value) return true;

            // If we are tuned to 99.1 FM (Channel 0 / News)
            if (__instance.currentChannelIndex == 0)
            {
                __instance.signalStrength = 1f;

                if (!__instance.source.enabled)
                {
                    return false;
                }

                // If nothing playing or track ended, play current track
                if (__instance.source.clip == null || __instance.source.clip != currentCustomClip)
                {
                    if (currentCustomClip == null && !isDecodingTrack)
                    {
                        LoadAndPlayCurrentTrack();
                    }
                    else if (currentCustomClip != null)
                    {
                        __instance.source.clip = currentCustomClip;
                        if (!__instance.source.isPlaying) __instance.source.Play();
                    }
                }
                else if (!__instance.source.isPlaying && !isDecodingTrack && __instance.source.time == 0f)
                {
                    // Track finished, advance
                    NextCustomTrack();
                }

                return false; // Handled by our custom player
            }

            // For all other stations (101.7 D&B, etc.) -> let the game's native radio code play them!
            return true;
        }

        // ==================== ON-SCREEN DIAGNOSTIC OVERLAY ====================

        private void OnGUI()
        {
            if (!showOverlay.Value) return;

            GUI.color = Color.white;
            int width = 420;
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

            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };

            fpsDeltaTime += (Time.unscaledDeltaTime - fpsDeltaTime) * 0.1f;
            float currentFps = 1.0f / Mathf.Max(0.0001f, fpsDeltaTime);

            sRadioSystem radio = sRadioSystem.instance;
            string stationStr = (radio != null) ? $"{radio.Frequency()} FM (Ch {radio.currentChannelIndex})" : "N/A";

            string devName = activeWheelDevice != null ? activeWheelDevice.displayName : "No Wheel/Joystick detected";
            GUILayout.Label($"Device: {devName}", textStyle);
            GUILayout.Label($"FPS: {currentFps:0.}  |  Station: {stationStr}", textStyle);
            GUILayout.Label($"Radio: {radioStatusText}", textStyle);
            GUILayout.Label($"Controls: ']' Next Track | '[' Prev Track | Normal radio keys tune station", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Vehicle Control ---", textStyle);
            GUILayout.Label($"Steer: {steerOut:+0.00;-0.00;0.00} | Gas: {throttleOut:0.00} | Brake: {brakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Device Axes (from InputSystem) ---", textStyle);
            if (activeAxes.Count > 0)
            {
                string axesLine = "";
                for (int i = 0; i < Math.Min(6, activeAxes.Count); i++)
                {
                    float val = activeAxes[i].ReadValue();
                    axesLine += $"{activeAxes[i].name}: {val:+0.00;-0.00;0.00}   ";
                }
                GUILayout.Label(axesLine, textStyle);
            }
            else
            {
                GUILayout.Label("No axes available on device", textStyle);
            }

            GUILayout.Space(2);
            string pressedButtons = "";
            for (int b = 0; b < Math.Min(16, activeButtons.Count); b++)
            {
                if (activeButtons[b].isPressed) pressedButtons += $"{activeButtons[b].name} ";
            }
            GUILayout.Label($"Buttons: {(string.IsNullOrEmpty(pressedButtons) ? "None" : pressedButtons)}", textStyle);

            GUILayout.EndArea();
        }
    }
}
