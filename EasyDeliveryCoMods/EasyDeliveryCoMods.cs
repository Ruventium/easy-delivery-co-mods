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
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "4.2.0")]
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
        public static List<string> allMusicFilePaths = new List<string>();
        public static List<AudioClip> fullPlaylistClips = new List<AudioClip>();
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
        public static float rawSteerValue = 0f;

        private static float[] lastLoggedAxisValues = new float[32];
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

            Logger.LogInfo("Easy Delivery Co Mods 4.2.0 loaded!");

            if (radioEnabled.Value)
            {
                StartCoroutine(InitFullPlaylistAsync());
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom music on 88.1 FM with 100% full signal always. All tracks available!");
            musicFolderPath = Config.Bind("1. Custom Radio", "MusicFolder", @"C:\Music",
                "Folder containing your music (FLAC, M4A, AAC, MP3, WAV, WMA, OGG).");
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
                "Enable steering wheel support.");
            wheelDeviceFilter = Config.Bind("3. Steering Wheel", "DeviceFilter", "pxn",
                "Search term for wheel device name in InputSystem.");

            wheelSteerAxisName = Config.Bind("3. Steering Wheel", "SteerAxisName", "x",
                "Axis name for steering (usually 'x' or 'stick/x').");
            wheelGasAxisName = Config.Bind("3. Steering Wheel", "GasAxisName", "z",
                "Axis name for gas pedal (usually 'z' on PXN V12 Lite).");
            wheelBrakeAxisName = Config.Bind("3. Steering Wheel", "BrakeAxisName", "rz",
                "Axis name for brake pedal (usually 'rz' on PXN V12 Lite).");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.03f,
                "Deadzone around wheel center.");
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

            // Keyboard direct tune station: Period (.) and Comma (,)
            if (Input.GetKeyDown(KeyCode.Period))
            {
                TuneRadioStation(1);
            }
            if (Input.GetKeyDown(KeyCode.Comma))
            {
                TuneRadioStation(-1);
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
                    Logger.LogInfo($"[Wheel] Selected device: '{activeWheelDevice.displayName}' ({activeWheelDevice.layout})");

                    availableAxes.Clear();

                    Logger.LogInfo($"=== ALL AVAILABLE CONTROLS ON {activeWheelDevice.displayName} ===");
                    foreach (var ctrl in activeWheelDevice.allControls)
                    {
                        if (ctrl is AxisControl axis && !(ctrl is ButtonControl))
                        {
                            availableAxes.Add(axis);
                            Logger.LogInfo($"[Wheel Axis Control] Name='{axis.name}', Path='{axis.path}', Value={axis.ReadValue():F3}");
                        }
                    }

                    // Steer: look for main analog 'x', EXCLUDING dpad and hatswitch!
                    steerAxis = FindAxis(wheelSteerAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("x", StringComparison.OrdinalIgnoreCase) && !a.path.Contains("dpad") && !a.path.Contains("hat"))
                                ?? availableAxes.FirstOrDefault(a => a.name.Contains("x") && !a.path.Contains("dpad") && !a.path.Contains("hat"));

                    // Gas: 'z' on PXN V12 Lite
                    gasAxis = FindAxis(wheelGasAxisName.Value)
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("z", StringComparison.OrdinalIgnoreCase) && !a.name.Contains("rz"))
                              ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase));

                    // Brake: 'rz' on PXN V12 Lite
                    brakeAxis = FindAxis(wheelBrakeAxisName.Value)
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("rz", StringComparison.OrdinalIgnoreCase))
                                ?? availableAxes.FirstOrDefault(a => a.name.Equals("slider", StringComparison.OrdinalIgnoreCase));

                    Logger.LogInfo($"[Wheel Configured] Steer='{(steerAxis != null ? steerAxis.path : "NULL")}', Gas='{(gasAxis != null ? gasAxis.path : "NULL")}', Brake='{(brakeAxis != null ? brakeAxis.path : "NULL")}'");
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
            return availableAxes.FirstOrDefault(a => (a.name.Equals(name, StringComparison.OrdinalIgnoreCase) || a.path.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase)) && !a.path.Contains("dpad") && !a.path.Contains("hat"));
        }

        private void PollWheel()
        {
            if (activeWheelDevice == null || !activeWheelDevice.added) return;

            // Live change logger for diagnostics when user turns wheel
            for (int i = 0; i < availableAxes.Count && i < lastLoggedAxisValues.Length; i++)
            {
                float v = availableAxes[i].ReadValue();
                if (Mathf.Abs(v - lastLoggedAxisValues[i]) > 0.05f)
                {
                    lastLoggedAxisValues[i] = v;
                    Logger.LogInfo($"[AXIS LIVE] {availableAxes[i].name} ({availableAxes[i].path}) = {v:+0.00;-0.00;0.00}");
                }
            }

            // 1. STEERING: Direct physical axis centered at 0.000
            if (steerAxis != null)
            {
                rawSteerValue = steerAxis.ReadValue();
                float val = rawSteerValue;
                if (wheelInvertSteer.Value) val = -val;

                float abs = Mathf.Abs(val);
                float dz = wheelSteerDeadzone.Value;

                if (abs < dz)
                {
                    steerOut = 0f;
                }
                else
                {
                    float norm = (abs - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(val) * wheelSteerSensitivity.Value, -1f, 1f);
                }
            }

            // 2. GAS PEDAL (DirectInput standard: -1.0 released to +1.0 pressed)
            if (gasAxis != null)
            {
                float raw = gasAxis.ReadValue();
                float norm = (raw + 1f) / 2f; // 0.0 released -> 1.0 pressed
                if (wheelInvertGas.Value) norm = 1f - norm;

                if (norm < 0.06f)
                {
                    gasOut = 0f;
                }
                else
                {
                    gasOut = Mathf.Clamp01((norm - 0.06f) / 0.94f);
                }
            }

            // 3. BRAKE PEDAL (DirectInput standard: -1.0 released to +1.0 pressed)
            if (brakeAxis != null)
            {
                float raw = brakeAxis.ReadValue();
                float norm = (raw + 1f) / 2f; // 0.0 released -> 1.0 pressed
                if (wheelInvertBrake.Value) norm = 1f - norm;

                if (norm < 0.06f)
                {
                    brakeOut = 0f;
                }
                else
                {
                    brakeOut = Mathf.Clamp01((norm - 0.06f) / 0.94f);
                }
            }
        }

        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        private static void Postfix_GetInput(sInputManager __instance)
        {
            if (!wheelEnabled.Value || activeWheelDevice == null) return;

            bool keyboardSteering = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

            if (!keyboardSteering)
            {
                __instance.driveInput.x = steerOut;
            }

            bool keyboardThrottle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                                     Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);

            if (!keyboardThrottle)
            {
                if (gasOut > 0.02f || brakeOut > 0.02f)
                {
                    if (brakeOut > 0.05f)
                    {
                        __instance.brakePressed = true;
                        __instance.driveInput.y = gasOut > 0.05f ? gasOut : -brakeOut;
                    }
                    else
                    {
                        __instance.driveInput.y = gasOut;
                    }
                }
                else
                {
                    __instance.driveInput.y = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(sCarController), "Move")]
        [HarmonyPrefix]
        private static void Prefix_CarController_Move(sCarController __instance)
        {
            if (!wheelEnabled.Value || activeWheelDevice == null) return;

            bool keyboardSteering = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

            if (!keyboardSteering)
            {
                __instance.input.x = steerOut;
            }

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
                else
                {
                    __instance.input.y = 0f;
                }
            }
        }

        // ==================== RADIO: FULL PLAYLIST & 88.1 FM 100% SIGNAL ====================

        public static void TuneRadioStation(int direction)
        {
            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;

            radio.forcedRadio = false;

            if (!radio.source.enabled)
            {
                radio.ToggleRadio();
            }

            int next = (radio.currentChannelIndex + direction + radio.channels.Count) % radio.channels.Count;
            radio.SetFrequency(next, radio.channels[next].frequency);
            Logger.LogInfo($"[Radio Tuned] Station [{next}]: '{radio.channels[next].name}' at {radio.channels[next].frequency:F1} FM");
        }

        [HarmonyPatch(typeof(sRadioSystem), "SetInput", new Type[] { typeof(Vector2) })]
        [HarmonyPrefix]
        private static bool Prefix_RadioSetInput(sRadioSystem __instance, Vector2 v)
        {
            __instance.forcedRadio = false;

            if (Mathf.Abs(v.x) > 0.25f)
            {
                int dir = (v.x > 0f) ? 1 : -1;
                TuneRadioStation(dir);
                return false;
            }

            if (v.y < -0.25f)
            {
                __instance.ToggleRadio();
                return false;
            }

            return true;
        }

        // Custom Radio (88.1 FM) ALWAYS gets 100% full signal!
        // News (99.1 FM), D&B (101.7), Lofi (99.9), EasyCo (91.1) use their normal story signal!
        [HarmonyPatch(typeof(sRadioSystem), "DoSignal")]
        [HarmonyPrefix]
        private static void Prefix_DoSignal(sRadioSystem __instance)
        {
            if (__instance.currentChannelIndex >= 0 && __instance.currentChannelIndex < __instance.channels.Count)
            {
                var ch = __instance.channels[__instance.currentChannelIndex];
                if (ch != null && (ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("custom")))
                {
                    __instance.signalStrength = 1f;
                    ch.signal = 1f; // 100% ALWAYS ON CUSTOM RADIO!
                }
            }
        }

        // Streaming Track class: decodes on demand with ZERO RAM footprint for all 3500+ tracks!
        public class StreamingAudioTrack
        {
            public string FilePath;
            public int Channels = 2;
            public int SampleRate = 44100;
            public int TotalSamples = 44100 * 180; // default 3 min fallback

            private MediaFoundationReader reader;
            private ISampleProvider sampleProvider;

            public AudioClip CreateClip()
            {
                string name = Path.GetFileNameWithoutExtension(FilePath);

                try
                {
                    using (var r = new MediaFoundationReader(FilePath))
                    {
                        Channels = r.WaveFormat.Channels;
                        SampleRate = r.WaveFormat.SampleRate;
                        long bytesPerSec = r.WaveFormat.AverageBytesPerSecond;
                        if (bytesPerSec > 0)
                        {
                            TotalSamples = (int)((double)r.Length / bytesPerSec * SampleRate);
                        }
                    }
                }
                catch { }

                AudioClip clip = AudioClip.Create(
                    name,
                    TotalSamples,
                    Channels,
                    SampleRate,
                    true, // STREAMING = TRUE!
                    OnAudioRead,
                    OnAudioSetPosition
                );

                return clip;
            }

            private void OnAudioRead(float[] data)
            {
                try
                {
                    if (sampleProvider == null)
                    {
                        reader = new MediaFoundationReader(FilePath);
                        sampleProvider = reader.ToSampleProvider();
                    }

                    int count = sampleProvider.Read(data, 0, data.Length);
                    if (count < data.Length)
                    {
                        Array.Clear(data, count, data.Length - count);
                        // Loop or close
                        reader.Position = 0;
                    }
                }
                catch
                {
                    Array.Clear(data, 0, data.Length);
                }
            }

            private void OnAudioSetPosition(int position)
            {
                try
                {
                    if (reader != null)
                    {
                        long bytePos = (long)position * Channels * (reader.WaveFormat.BitsPerSample / 8);
                        if (bytePos >= 0 && bytePos < reader.Length)
                        {
                            reader.Position = bytePos;
                        }
                    }
                }
                catch { }
            }
        }

        private IEnumerator InitFullPlaylistAsync()
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
                radioStatusText = $"No music files in {folder}";
                yield break;
            }

            if (radioShuffle.Value)
            {
                var rnd = new System.Random();
                allMusicFilePaths = allMusicFilePaths.OrderBy(x => rnd.Next()).ToList();
            }

            radioStatusText = $"Initializing {allMusicFilePaths.Count} tracks for 88.1 FM...";
            Logger.LogInfo($"[CustomRadio] Creating streaming playlist of {allMusicFilePaths.Count} tracks from {folder}...");

            isDecoderRunning = true;
            fullPlaylistClips.Clear();

            int batchSize = 100;
            for (int i = 0; i < allMusicFilePaths.Count; i++)
            {
                try
                {
                    var streamer = new StreamingAudioTrack { FilePath = allMusicFilePaths[i] };
                    AudioClip clip = streamer.CreateClip();
                    if (clip != null)
                    {
                        fullPlaylistClips.Add(clip);
                    }
                }
                catch { }

                if (i % batchSize == 0)
                {
                    radioStatusText = $"Loaded {i}/{allMusicFilePaths.Count} tracks on 88.1 FM";
                    yield return null;
                }
            }

            isDecoderRunning = false;
            radioStatusText = $"All {fullPlaylistClips.Count} tracks loaded on 88.1 FM!";
            Logger.LogInfo($"[CustomRadio] Done! Total {fullPlaylistClips.Count} tracks live on 88.1 FM Custom Radio!");

            ApplyFullPlaylistToCustomChannel();
        }

        private static void ApplyFullPlaylistToCustomChannel()
        {
            if (fullPlaylistClips == null || fullPlaylistClips.Count == 0) return;

            sRadioSystem radio = UnityEngine.Object.FindFirstObjectByType<sRadioSystem>();
            if (radio == null || radio.channels == null) return;

            // ONLY apply our music to 88.1 FM ('custom')! News (99.1) and all other stations are untouched!
            foreach (var ch in radio.channels)
            {
                if (ch != null && (ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("custom")))
                {
                    ch.externalTracks.Clear();
                    ch.externalTracks.AddRange(fullPlaylistClips);
                    ch.queue = fullPlaylistClips.ToArray();
                    ch.signal = 1f;
                    Logger.LogInfo($"[CustomRadio] Populated 88.1 FM '{ch.name}' with FULL PLAYLIST of {fullPlaylistClips.Count} tracks!");
                }
            }
        }

        [HarmonyPatch(typeof(sRadioSystem), "Start")]
        [HarmonyPostfix]
        private static void Postfix_RadioStart(sRadioSystem __instance)
        {
            ApplyFullPlaylistToCustomChannel();
        }

        [HarmonyPatch(typeof(AudioLoader), "Start")]
        [HarmonyPostfix]
        private static void Postfix_AudioLoader_Start(AudioLoader __instance)
        {
            if (fullPlaylistClips != null && fullPlaylistClips.Count > 0 && __instance.customChannel != null)
            {
                __instance.customChannel.externalTracks.Clear();
                __instance.customChannel.externalTracks.AddRange(fullPlaylistClips);
                __instance.customChannel.queue = fullPlaylistClips.ToArray();
                __instance.customChannel.signal = 1f;
                Logger.LogInfo($"[CustomRadio] AudioLoader 88.1 FM populated with FULL PLAYLIST of {fullPlaylistClips.Count} tracks!");
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
            int width = 480;
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
            GUILayout.Label($"88.1 FM Custom: {radioStatusText}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Vehicle Control ---", textStyle);
            string steerName = steerAxis != null ? steerAxis.name : "none";
            string gasName = gasAxis != null ? gasAxis.name : "none";
            string brakeName = brakeAxis != null ? brakeAxis.name : "none";
            GUILayout.Label($"Steer({steerName}): Raw={rawSteerValue:+0.00;-0.00;0.00} -> Out={steerOut:+0.00;-0.00;0.00}", textStyle);
            GUILayout.Label($"Gas({gasName}): {gasOut:0.00} | Brake({brakeName}): {brakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- LIVE DEVICE AXES (turn wheel to watch) ---", textStyle);
            if (availableAxes.Count > 0)
            {
                string line = "";
                for (int i = 0; i < availableAxes.Count; i++)
                {
                    float val = availableAxes[i].ReadValue();
                    line += $"{availableAxes[i].name}:{val:+0.00;-0.00;0.00}  ";
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
