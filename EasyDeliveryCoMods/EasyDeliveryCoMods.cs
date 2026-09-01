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
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "3.6.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        public static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== CONFIG ====================
        public static ConfigEntry<bool> radioEnabled;
        public static ConfigEntry<string> musicFolderPath;
        public static ConfigEntry<bool> radioShuffle;

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
        public static List<string> musicFiles = new List<string>();
        public static int currentTrackIndex = 0;
        public static AudioClip currentCustomClip = null;
        public static string currentTrackTitle = "None";
        public static bool isDecodingTrack = false;
        public static string radioStatusText = "Idle";
        public static RadioChannel customRadioChannel = null;

        // ==================== WHEEL RUNTIME ====================
        public static InputDevice activeWheelDevice = null;
        public static List<AxisControl> availableAxes = new List<AxisControl>();
        public static List<ButtonControl> availableButtons = new List<ButtonControl>();

        public static AxisControl steerAxis = null;
        public static AxisControl gasAxis = null;
        public static AxisControl brakeAxis = null;

        public static float steerOut = 0f;
        public static float gasOut = 0f;
        public static float brakeOut = 0f;

        public static float gasRestValue = -1f;
        public static float brakeRestValue = -1f;
        public static bool pedalsCalibrated = false;

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

            Logger.LogInfo("Easy Delivery Co Mods 3.6.0 initialized!");

            if (radioEnabled.Value)
            {
                ScanMusicFolder();
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom radio. Music from MusicFolder will play on custom station 105.5 FM.");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music",
                "Folder containing your music (FLAC, M4A, AAC, MP3, WAV, WMA, OGG). Decoded on the fly.");
            radioShuffle = Config.Bind("1. Custom Radio", "Shuffle", true,
                "Shuffle playback order of tracks.");

            // FPS
            fpsUnlockEnabled = Config.Bind("2. Frame Rate", "UnlockFPS", true,
                "Unlock frame rate limit (blocks game's internal 60 FPS limiter).");
            targetFrameRate = Config.Bind("2. Frame Rate", "TargetFPS", 240,
                "Target frame rate (0 = unlimited).");
            disableVSync = Config.Bind("2. Frame Rate", "DisableVSync", true,
                "Disable vertical sync.");

            // Wheel
            wheelEnabled = Config.Bind("3. Steering Wheel", "Enabled", true,
                "Enable direct 1:1 steering wheel support.");
            wheelDeviceFilter = Config.Bind("3. Steering Wheel", "DeviceFilter", "pxn",
                "Search term for wheel device name in InputSystem.");

            wheelSteerAxisName = Config.Bind("3. Steering Wheel", "SteerAxisName", "x",
                "Axis name for steering (usually 'x').");
            wheelGasAxisName = Config.Bind("3. Steering Wheel", "GasAxisName", "z",
                "Axis name for gas pedal (usually 'z' on PXN V12 Lite).");
            wheelBrakeAxisName = Config.Bind("3. Steering Wheel", "BrakeAxisName", "rz",
                "Axis name for brake pedal (usually 'rz' on PXN V12 Lite).");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.02f, "Deadzone for steering.");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteerSensitivity", 1.0f, "Direct 1:1 steering sensitivity multiplier.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteering", true, "Invert steering direction (True matches real wheel).");
            wheelInvertGas = Config.Bind("3. Steering Wheel", "InvertGas", false, "Invert gas pedal.");
            wheelInvertBrake = Config.Bind("3. Steering Wheel", "InvertBrake", false, "Invert brake pedal.");

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

        // ==================== WHEEL LOGIC (1:1 DIRECT SIMULATOR STEERING) ====================

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
                    Logger.LogInfo($"[Wheel] Selected: '{activeWheelDevice.displayName}' ({activeWheelDevice.layout})");

                    availableAxes.Clear();
                    availableButtons.Clear();

                    foreach (var ctrl in activeWheelDevice.allControls)
                    {
                        if (ctrl is AxisControl axis && !(ctrl is ButtonControl))
                        {
                            availableAxes.Add(axis);
                        }
                        else if (ctrl is ButtonControl btn)
                        {
                            availableButtons.Add(btn);
                        }
                    }

                    // Steer: 'x'
                    steerAxis = FindAxis(wheelSteerAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("x", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Contains("x"));

                    // Gas: 'z'
                    gasAxis = FindAxis(wheelGasAxisName.Value)
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("z", StringComparison.OrdinalIgnoreCase) && !a.name.Contains("rz"))
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase));

                    // Brake: 'rz'
                    brakeAxis = FindAxis(wheelBrakeAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("slider", StringComparison.OrdinalIgnoreCase));

                    pedalsCalibrated = false;
                    Logger.LogInfo($"[Wheel Configured] Steer='{(steerAxis != null ? steerAxis.name : "NULL")}', Gas='{(gasAxis != null ? gasAxis.name : "NULL")}', Brake='{(brakeAxis != null ? brakeAxis.name : "NULL")}'");
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

            // Zero baseline on first poll
            if (!pedalsCalibrated && gasAxis != null && brakeAxis != null)
            {
                gasRestValue = gasAxis.ReadValue();
                brakeRestValue = brakeAxis.ReadValue();
                pedalsCalibrated = true;
                Logger.LogInfo($"[Pedals Calibrated] GasRest={gasRestValue:F2}, BrakeRest={brakeRestValue:F2}");
            }

            // 1. Steering: PURE LINEAR 1:1 DIRECT INPUT
            // No fake power curves, no deadzone cutoff beyond center!
            if (steerAxis != null)
            {
                float raw = steerAxis.ReadValue();
                if (wheelInvertSteer.Value) raw = -raw;

                float abs = Mathf.Abs(raw);
                float dz = wheelSteerDeadzone.Value;
                if (abs < dz)
                {
                    steerOut = 0f;
                }
                else
                {
                    // Linear mapping from deadzone to 1.0
                    float norm = (abs - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(raw) * wheelSteerSensitivity.Value, -1f, 1f);
                }
            }

            // 2. Gas Pedal
            if (gasAxis != null)
            {
                float raw = gasAxis.ReadValue();
                float travel = Mathf.Abs(raw - gasRestValue);
                if (travel < 0.06f)
                {
                    gasOut = 0f;
                }
                else
                {
                    float maxTravel = (Mathf.Abs(gasRestValue) > 0.4f) ? 1.88f : 0.94f;
                    float val = Mathf.Clamp01((travel - 0.06f) / (maxTravel - 0.06f));
                    if (wheelInvertGas.Value) val = 1f - val;
                    gasOut = val;
                }
            }

            // 3. Brake Pedal
            if (brakeAxis != null)
            {
                float raw = brakeAxis.ReadValue();
                float travel = Mathf.Abs(raw - brakeRestValue);
                if (travel < 0.06f)
                {
                    brakeOut = 0f;
                }
                else
                {
                    float maxTravel = (Mathf.Abs(brakeRestValue) > 0.4f) ? 1.88f : 0.94f;
                    float val = Mathf.Clamp01((travel - 0.06f) / (maxTravel - 0.06f));
                    if (wheelInvertBrake.Value) val = 1f - val;
                    brakeOut = val;
                }
            }
        }

        // DIRECT WHEEL INJECTION INTO CAR PHYSICS:
        // Bypasses the gamepad Lerp filter and gamepad S-curve that caused sharp steering and sudden stops!
        [HarmonyPatch(typeof(sCarController), "Move")]
        [HarmonyPrefix]
        private static void Prefix_CarController_Move(sCarController __instance)
        {
            if (!wheelEnabled.Value || activeWheelDevice == null) return;

            // Check if player is actively steering via keyboard or gamepad
            bool keyboardSteering = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

            // Apply direct 1:1 steering from wheel if not using keyboard
            if (!keyboardSteering && Mathf.Abs(steerOut) > 0.005f)
            {
                __instance.input.x = steerOut;
            }

            // Check keyboard throttle
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

        // ==================== RADIO: NATIVE SCANNING WITH CUSTOM STATION ====================

        private void ScanMusicFolder()
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
                Logger.LogError($"Music scan error: {ex.Message}");
            }

            if (radioShuffle.Value && musicFiles.Count > 0)
            {
                var rnd = new System.Random();
                musicFiles = musicFiles.OrderBy(x => rnd.Next()).ToList();
            }

            radioStatusText = $"{musicFiles.Count} tracks ready";
            Logger.LogInfo($"[CustomRadio] Found {musicFiles.Count} tracks in {folder}");

            if (musicFiles.Count > 0)
            {
                currentTrackTitle = Path.GetFileNameWithoutExtension(musicFiles[0]);
            }
        }

        // Radio scene start: add Custom Radio (105.5 FM) properly into game radio channels
        [HarmonyPatch(typeof(sRadioSystem), "Start")]
        [HarmonyPostfix]
        private static void Postfix_RadioStart(sRadioSystem __instance)
        {
            Logger.LogInfo($"=== Radio Starting === Initial stations: {__instance.channels.Count}, Frequency: {__instance.frequency} FM");

            // Also make sure all existing stations in scene are unlocked and active
            var addChannels = UnityEngine.Object.FindObjectsOfType<sAddRadioChannel>();
            foreach (var ac in addChannels)
            {
                if (ac.channel != null && !__instance.channels.Contains(ac.channel))
                {
                    ac.channel.queue = ac.channel.GetRandomizedClone();
                    __instance.channels.Add(ac.channel);
                    Logger.LogInfo($"[Station Unlocked] '{ac.channel.name}' ({ac.channel.frequency} FM)");
                }
            }

            if (radioEnabled.Value && musicFiles.Count > 0)
            {
                if (customRadioChannel == null)
                {
                    customRadioChannel = ScriptableObject.CreateInstance<RadioChannel>();
                    customRadioChannel.name = "Custom Radio";
                    customRadioChannel.frequency = 105.5f;
                    customRadioChannel.signal = 1f;
                    customRadioChannel.queue = new AudioClip[0];
                }

                if (!__instance.channels.Contains(customRadioChannel))
                {
                    __instance.channels.Add(customRadioChannel);
                    Logger.LogInfo($"[Custom Radio Added] 105.5 FM! Total stations: {__instance.channels.Count}");
                }
            }

            // Log all channels
            for (int i = 0; i < __instance.channels.Count; i++)
            {
                var c = __instance.channels[i];
                Logger.LogInfo($"Station [{i}]: '{c.name}' at {c.frequency:F1} FM");
            }
        }

        // FIX FOR RADIO SCANNING:
        // In vanilla game, DoScanning() jumps frequency by 0.1f or more per frame.
        // At 240 FPS, it skips over stations because it checks exact equality!
        // We patch GetCurrentChannel so that it catches stations within 0.15 FM tolerance!
        [HarmonyPatch(typeof(sRadioSystem), "GetCurrentChannel")]
        [HarmonyPrefix]
        private static bool Prefix_GetCurrentChannel(sRadioSystem __instance, ref int __result)
        {
            float currentFreq = __instance.frequency;

            for (int i = 0; i < __instance.channels.Count; i++)
            {
                float chFreq = __instance.channels[i].frequency;
                // Within 0.15 MHz tolerance so scanning catches it smoothly at 240 FPS!
                if (Mathf.Abs(currentFreq - chFreq) <= 0.15f)
                {
                    // Snap to exact station frequency
                    __instance.frequency = chFreq;
                    __result = i;
                    return false; // Skip vanilla method
                }
            }

            __result = -1;
            return false;
        }

        // Custom Radio Playback: ONLY intercepts when tuned to 105.5 FM (Custom Radio)
        // All other stations (99.1 News, 101.7 D&B) are 100% untouched and play natively!
        [HarmonyPatch(typeof(sRadioSystem), "UpdateTracks")]
        [HarmonyPrefix]
        private static bool Prefix_RadioUpdateTracks(sRadioSystem __instance)
        {
            if (!radioEnabled.Value || customRadioChannel == null || musicFiles.Count == 0) return true;

            bool onCustomStation = (__instance.currentChannelIndex >= 0 &&
                                    __instance.currentChannelIndex < __instance.channels.Count &&
                                    __instance.channels[__instance.currentChannelIndex] == customRadioChannel);

            if (onCustomStation)
            {
                __instance.signalStrength = 1f;
                if (!__instance.source.enabled) return false;

                if (__instance.source.clip == null || __instance.source.clip != currentCustomClip)
                {
                    if (currentCustomClip == null && !isDecodingTrack)
                    {
                        LoadAndPlayTrack(currentTrackIndex);
                    }
                    else if (currentCustomClip != null)
                    {
                        __instance.source.clip = currentCustomClip;
                        if (!__instance.source.isPlaying) __instance.source.Play();
                    }
                }
                else if (!__instance.source.isPlaying && !isDecodingTrack && __instance.source.time == 0f)
                {
                    // Track finished, auto advance
                    SkipToNextTrack();
                }

                return false; // Handled by custom player
            }

            // Normal stations: let vanilla game audio play!
            return true;
        }

        public static void SkipToNextTrack()
        {
            if (musicFiles.Count == 0) return;
            currentTrackIndex = (currentTrackIndex + 1) % musicFiles.Count;
            LoadAndPlayTrack(currentTrackIndex);
        }

        private static void LoadAndPlayTrack(int index)
        {
            if (Instance == null || musicFiles.Count == 0 || isDecodingTrack) return;
            Instance.StartCoroutine(DecodeAndPlayTrackCoroutine(musicFiles[index]));
        }

        private static IEnumerator DecodeAndPlayTrackCoroutine(string filePath)
        {
            isDecodingTrack = true;
            string trackName = Path.GetFileNameWithoutExtension(filePath);
            currentTrackTitle = trackName;
            radioStatusText = $"Decoding: {trackName}";

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
                        Logger.LogError($"Decode failed for {trackName}: {ex.Message}");
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
                    Destroy(currentCustomClip);
                }
                currentCustomClip = clip;

                sRadioSystem radio = sRadioSystem.instance;
                if (radio != null && radio.source != null)
                {
                    radio.source.clip = currentCustomClip;
                    radio.source.time = 0f;
                    if (radio.source.enabled) radio.source.Play();
                }

                radioStatusText = $"Playing: {trackName}";
                Logger.LogInfo($"[CustomRadio] Playing [{currentTrackIndex + 1}/{musicFiles.Count}]: {trackName}");
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
            int height = 300;
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
            GUILayout.Label($"Custom Radio: {radioStatusText}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Vehicle Control ---", textStyle);
            string steerName = steerAxis != null ? steerAxis.name : "none";
            string gasName = gasAxis != null ? gasAxis.name : "none";
            string brakeName = brakeAxis != null ? brakeAxis.name : "none";
            GUILayout.Label($"Steer({steerName}): {steerOut:+0.00;-0.00;0.00} | Gas({gasName}): {gasOut:0.00} | Brake({brakeName}): {brakeOut:0.00}", textStyle);

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
