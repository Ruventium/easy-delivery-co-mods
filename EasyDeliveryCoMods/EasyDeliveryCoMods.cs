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
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "3.4.0")]
    public class EasyDeliveryCoModsPlugin : BaseUnityPlugin
    {
        public static EasyDeliveryCoModsPlugin Instance { get; private set; }
        private static new BepInEx.Logging.ManualLogSource Logger;

        // ==================== CONFIG ====================
        private static ConfigEntry<bool> radioEnabled;
        private static ConfigEntry<string> musicFolderPath;
        private static ConfigEntry<float> customStationFreq;
        private static ConfigEntry<bool> radioShuffle;
        private static ConfigEntry<bool> unlockAllStations;

        private static ConfigEntry<bool> fpsUnlockEnabled;
        private static ConfigEntry<int> targetFrameRate;
        private static ConfigEntry<bool> disableVSync;

        private static ConfigEntry<bool> wheelEnabled;
        private static ConfigEntry<string> wheelDeviceFilter;
        private static ConfigEntry<string> wheelSteerAxisName;
        private static ConfigEntry<string> wheelGasAxisName;
        private static ConfigEntry<string> wheelBrakeAxisName;

        private static ConfigEntry<float> wheelSteerDeadzone;
        private static ConfigEntry<float> wheelSteerSensitivity;
        private static ConfigEntry<bool> wheelInvertSteer;
        private static ConfigEntry<bool> wheelInvertGas;
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
        private static RadioChannel customRadioChannel = null;

        // ==================== WHEEL RUNTIME ====================
        private static InputDevice activeWheelDevice = null;
        private static List<AxisControl> availableAxes = new List<AxisControl>();
        private static List<ButtonControl> availableButtons = new List<ButtonControl>();

        private static AxisControl steerAxis = null;
        private static AxisControl gasAxis = null;
        private static AxisControl brakeAxis = null;

        private static float steerOut = 0f;
        private static float gasOut = 0f;
        private static float brakeOut = 0f;

        private static float gasRestValue = -1f;
        private static float brakeRestValue = -1f;
        private static bool pedalsCalibrated = false;

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

            Logger.LogInfo("Easy Delivery Co Mods 3.4.0 (Radio + Wheel + FPS) initialized!");

            if (radioEnabled.Value)
            {
                ScanMusicFolder();
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom radio station from your music folder.");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music",
                "Folder containing your music (FLAC, M4A, AAC, MP3, WAV, WMA, OGG). Decoded on the fly.");
            customStationFreq = Config.Bind("1. Custom Radio", "CustomStationFrequency", 105.5f,
                "FM Frequency for your custom station (e.g. 105.5 FM). You can tune between News, D&B, and Custom Radio!");
            radioShuffle = Config.Bind("1. Custom Radio", "Shuffle", true,
                "Shuffle playback order of tracks.");
            unlockAllStations = Config.Bind("1. Custom Radio", "UnlockAllGameStations", true,
                "Unlock all radio stations in the game immediately so you can tune between them!");

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
                "Search term for wheel device name in InputSystem (e.g. 'pxn', 'v12', 'wheel', 'joystick').");

            wheelSteerAxisName = Config.Bind("3. Steering Wheel", "SteerAxisName", "x",
                "Axis name for steering (usually 'x'). See F7 overlay.");
            wheelGasAxisName = Config.Bind("3. Steering Wheel", "GasAxisName", "auto",
                "Axis name for gas pedal ('auto' or specific name like 'rz', 'y', 'slider'). See F7 overlay.");
            wheelBrakeAxisName = Config.Bind("3. Steering Wheel", "BrakeAxisName", "auto",
                "Axis name for brake pedal ('auto' or specific name like 'z', 'rz'). See F7 overlay.");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.02f, "Deadzone for steering.");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteerSensitivity", 1.0f, "Steering sensitivity.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteering", false, "Invert steering.");
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
            Logger.LogInfo($"[InputDevice] Changed: {device.displayName} -> {change}");
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

            // Keyboard skip track when on custom radio
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                SkipToNextTrack();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                SkipToPrevTrack();
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

        // ==================== WHEEL LOGIC ====================

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

                    Logger.LogInfo($"--- Listing all controls on {activeWheelDevice.displayName} ---");
                    foreach (var ctrl in activeWheelDevice.allControls)
                    {
                        if (ctrl is AxisControl axis && !(ctrl is ButtonControl))
                        {
                            availableAxes.Add(axis);
                            Logger.LogInfo($"[Axis] Name='{axis.name}', Path='{axis.path}', Value={axis.ReadValue():F2}");
                        }
                        else if (ctrl is ButtonControl btn)
                        {
                            availableButtons.Add(btn);
                        }
                    }

                    // 1. Steer axis: usually 'x'
                    steerAxis = FindAxis(wheelSteerAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("x", StringComparison.OrdinalIgnoreCase) || a.name.EndsWith("/x", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Contains("x"));

                    // 2. Gas axis: on PXN/DirectInput it is typically 'rz' or 'slider' or 'y'
                    if (wheelGasAxisName.Value != "auto")
                    {
                        gasAxis = FindAxis(wheelGasAxisName.Value);
                    }
                    if (gasAxis == null)
                    {
                        gasAxis = availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase))
                                  ?? availableAxes.FirstOrDefault(a => a.name.Equals("slider", StringComparison.OrdinalIgnoreCase))
                                  ?? availableAxes.FirstOrDefault(a => a.name.Equals("y", StringComparison.OrdinalIgnoreCase));
                    }

                    // 3. Brake axis: on PXN/DirectInput it is 'z'
                    if (wheelBrakeAxisName.Value != "auto")
                    {
                        brakeAxis = FindAxis(wheelBrakeAxisName.Value);
                    }
                    if (brakeAxis == null)
                    {
                        brakeAxis = availableAxes.FirstOrDefault(a => a.name.Equals("z", StringComparison.OrdinalIgnoreCase) && !a.name.Contains("rz"))
                                    ?? availableAxes.FirstOrDefault(a => a.name.Equals("y", StringComparison.OrdinalIgnoreCase) && a != gasAxis)
                                    ?? (availableAxes.Count > 2 ? availableAxes[2] : null);
                    }

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

            // Sample baseline rest values
            if (!pedalsCalibrated && gasAxis != null && brakeAxis != null)
            {
                gasRestValue = gasAxis.ReadValue();
                brakeRestValue = brakeAxis.ReadValue();
                pedalsCalibrated = true;
                Logger.LogInfo($"[Pedals Calibrated] GasRest={gasRestValue:F2}, BrakeRest={brakeRestValue:F2}");
            }

            // 1. Steering
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
                    float norm = (abs - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(raw) * wheelSteerSensitivity.Value, -1f, 1f);
                }
            }

            // 2. Gas Pedal (detect movement away from rest value)
            if (gasAxis != null)
            {
                float raw = gasAxis.ReadValue();
                float travel = Mathf.Abs(raw - gasRestValue);
                if (travel < 0.08f)
                {
                    gasOut = 0f;
                }
                else
                {
                    float val = Mathf.Clamp01((travel - 0.08f) / (1f - 0.08f));
                    if (wheelInvertGas.Value) val = 1f - val;
                    gasOut = val;
                }
            }

            // 3. Brake Pedal
            if (brakeAxis != null)
            {
                float raw = brakeAxis.ReadValue();
                float travel = Mathf.Abs(raw - brakeRestValue);
                if (travel < 0.08f)
                {
                    brakeOut = 0f;
                }
                else
                {
                    float val = Mathf.Clamp01((travel - 0.08f) / (1f - 0.08f));
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

            // Only override steering if wheel is actively turned past deadzone
            if (Mathf.Abs(steerOut) > 0.015f)
            {
                __instance.driveInput.x = steerOut;
            }

            // Only override throttle and brake if pedals are physically pushed
            if (gasOut > 0.02f || brakeOut > 0.02f)
            {
                if (brakeOut > 0.05f)
                {
                    __instance.brakePressed = true;
                    // Forward gas has priority if pressed; otherwise negative driveInput for brake/reverse
                    __instance.driveInput.y = gasOut > 0.05f ? gasOut : -brakeOut;
                }
                else
                {
                    __instance.driveInput.y = gasOut;
                }
            }
        }

        // ==================== RADIO (DEDICATED CHANNEL & UNLOCKED STATIONS) ====================

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

        // When Radio starts in game, setup Custom Radio station & unlock other stations!
        [HarmonyPatch(typeof(sRadioSystem), "Start")]
        [HarmonyPostfix]
        private static void Postfix_RadioStart(sRadioSystem __instance)
        {
            Logger.LogInfo($"=== Radio Initialized ===");
            Logger.LogInfo($"Initial stations: {__instance.channels.Count}, Frequency: {__instance.frequency} FM");

            // 1. Unlock all radio stations from the scene so the player can switch between them!
            if (unlockAllStations.Value)
            {
                var addChannels = UnityEngine.Object.FindObjectsOfType<sAddRadioChannel>();
                foreach (var ac in addChannels)
                {
                    if (ac.channel != null && !__instance.channels.Contains(ac.channel))
                    {
                        __instance.channels.Add(ac.channel);
                        Logger.LogInfo($"[Radio Unlocked] Added station: '{ac.channel.name}' ({ac.channel.frequency} FM)");
                    }
                }
            }

            // 2. Add our Custom Radio as its own separate station on the dial!
            if (radioEnabled.Value && musicFiles.Count > 0)
            {
                if (customRadioChannel == null)
                {
                    customRadioChannel = ScriptableObject.CreateInstance<RadioChannel>();
                    customRadioChannel.name = "Custom Radio";
                    customRadioChannel.frequency = customStationFreq.Value;
                    customRadioChannel.signal = 1f;
                }

                if (!__instance.channels.Contains(customRadioChannel))
                {
                    __instance.channels.Add(customRadioChannel);
                    Logger.LogInfo($"[Radio Added] Added '{customRadioChannel.name}' at {customRadioChannel.frequency} FM! Total stations: {__instance.channels.Count}");
                }
            }
        }

        // Custom Radio Player: ONLY intercepts when tuned to our Custom Radio frequency!
        [HarmonyPatch(typeof(sRadioSystem), "UpdateTracks")]
        [HarmonyPrefix]
        private static bool Prefix_RadioUpdateTracks(sRadioSystem __instance)
        {
            if (!radioEnabled.Value || customRadioChannel == null || musicFiles.Count == 0) return true;

            // Check if current tuned station is our Custom Radio
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
                    // Track ended, play next
                    SkipToNextTrack();
                }

                return false; // Handled by custom player
            }

            // If on ANY other station (News, D&B, etc.) -> completely normal vanilla radio behavior!
            return true;
        }

        public static void SkipToNextTrack()
        {
            if (musicFiles.Count == 0) return;
            currentTrackIndex = (currentTrackIndex + 1) % musicFiles.Count;
            LoadAndPlayTrack(currentTrackIndex);
        }

        public static void SkipToPrevTrack()
        {
            if (musicFiles.Count == 0) return;
            currentTrackIndex = (currentTrackIndex - 1 + musicFiles.Count) % musicFiles.Count;
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
            int width = 430;
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
            GUILayout.Label($"Custom Radio: {radioStatusText} (Press ']' / '[' to skip)", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Vehicle Control ---", textStyle);
            string steerName = steerAxis != null ? steerAxis.name : "none";
            string gasName = gasAxis != null ? gasAxis.name : "none";
            string brakeName = brakeAxis != null ? brakeAxis.name : "none";
            GUILayout.Label($"Steer({steerName}): {steerOut:+0.00;-0.00;0.00} | Gas({gasName}): {gasOut:0.00} | Brake({brakeName}): {brakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Live Device Axes (watch numbers when pressing pedals) ---", textStyle);
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
