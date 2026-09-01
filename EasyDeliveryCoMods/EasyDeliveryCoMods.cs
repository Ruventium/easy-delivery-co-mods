using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NAudio.Wave;
using SharpDX.DirectInput;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EasyDeliveryCoMods
{
    [BepInPlugin("opencode.easydeliveryco.mods", "Easy Delivery Co - Custom Radio & Wheel", "5.1.0")]
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
        public static ConfigEntry<float> wheelSteerDeadzone;
        public static ConfigEntry<float> wheelSteerSensitivity;
        public static ConfigEntry<bool> wheelInvertSteer;
        public static ConfigEntry<bool> wheelInvertGas;
        public static ConfigEntry<bool> wheelInvertBrake;

        // Force Feedback
        public static ConfigEntry<bool> ffbEnabled;
        public static ConfigEntry<float> ffbGain;
        public static ConfigEntry<bool> ffbInvert;

        public static ConfigEntry<bool> showOverlay;
        public static ConfigEntry<KeyCode> overlayKey;

        // ==================== RADIO RUNTIME ====================
        public static List<string> allMusicFilePaths = new List<string>();
        public static List<AudioClip> fullPlaylistClips = new List<AudioClip>();
        public static string radioStatusText = "Idle";

        // ==================== DIRECTINPUT & FFB RUNTIME ====================
        private static DirectInput directInput = null;
        private static SharpDX.DirectInput.Joystick dinputJoystick = null;
        private static Effect ffbEffect = null;
        private static SharpDX.DirectInput.ConstantForce constantForceParams = null;
        private static bool dinputInitialized = false;

        public static float steerOut = 0f;
        public static float gasOut = 0f;
        public static float brakeOut = 0f;
        public static float rawSteerValue = 0f;

        private static float fpsDeltaTime = 0f;

        static EasyDeliveryCoModsPlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string name = new System.Reflection.AssemblyName(args.Name).Name;
                    string[] embedded = { "NAudio.Core", "NAudio.Wasapi", "SharpDX", "SharpDX.DirectInput" };
                    if (embedded.Contains(name))
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

            Harmony harmony = new Harmony("opencode.easydeliveryco.mods");
            harmony.PatchAll(typeof(EasyDeliveryCoModsPlugin));

            Logger.LogInfo("Easy Delivery Co Mods 5.1.0 initialized!");

            if (radioEnabled.Value)
            {
                StartCoroutine(InitFullPlaylistAsync());
            }
        }

        private void InitConfig()
        {
            // Radio
            radioEnabled = Config.Bind("1. Custom Radio", "Enabled", true,
                "Enable custom music from C:\\Music on 88.1 FM with 100% full signal always.");
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
                "Enable Windows DirectInput 8 steering wheel support.");
            wheelDeviceFilter = Config.Bind("3. Steering Wheel", "DeviceFilter", "pxn",
                "Search term for wheel device name in DirectInput.");

            wheelSteerDeadzone = Config.Bind("3. Steering Wheel", "SteerDeadzone", 0.02f,
                "Deadzone around wheel center (0.00 to 0.10).");
            wheelSteerSensitivity = Config.Bind("3. Steering Wheel", "SteerSensitivity", 1.0f,
                "1:1 steering multiplier across 900 degrees.");
            wheelInvertSteer = Config.Bind("3. Steering Wheel", "InvertSteering", false,
                "Invert steering direction.");
            wheelInvertGas = Config.Bind("3. Steering Wheel", "InvertGas", false,
                "Invert gas pedal.");
            wheelInvertBrake = Config.Bind("3. Steering Wheel", "InvertBrake", false,
                "Invert brake pedal.");

            // Force Feedback
            ffbEnabled = Config.Bind("4. Force Feedback", "Enabled", true,
                "Enable DirectInput Force Feedback (FFB) on the wheel motor.");
            ffbGain = Config.Bind("4. Force Feedback", "Gain", 1.0f,
                "Force Feedback strength multiplier (0.0 to 2.0).");
            ffbInvert = Config.Bind("4. Force Feedback", "InvertForce", false,
                "Invert force feedback direction if resistance pushes instead of centers.");

            showOverlay = Config.Bind("5. Overlay", "ShowOverlay", true, "Show live diagnostics overlay on F7.");
            overlayKey = Config.Bind("5. Overlay", "ToggleKey", KeyCode.F7, "Key to toggle overlay.");
        }

        private void ApplyFpsSettings()
        {
            if (fpsUnlockEnabled.Value)
            {
                Application.targetFrameRate = targetFrameRate.Value <= 0 ? -1 : targetFrameRate.Value;
                if (disableVSync.Value) QualitySettings.vSyncCount = 0;
            }
        }

        private void Start()
        {
            InitDirectInputWheel();
        }

        private void OnDestroy()
        {
            ShutdownDirectInput();
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
                TuneRadioNext();
            }
            if (Input.GetKeyDown(KeyCode.Comma))
            {
                TuneRadioPrev();
            }

            if (wheelEnabled.Value)
            {
                if (!dinputInitialized || dinputJoystick == null)
                {
                    InitDirectInputWheel();
                }
                PollDirectInputWheel();
            }
        }

        // ==================== DIRECTINPUT 8 INITIALIZATION & POLLING ====================

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private void InitDirectInputWheel()
        {
            try
            {
                if (directInput == null)
                {
                    directInput = new DirectInput();
                }

                // Use AllDevices so DirectDrive wheels are always discovered!
                var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);
                DeviceInstance targetDevice = null;
                string filter = wheelDeviceFilter.Value.ToLowerInvariant();

                foreach (var d in devices)
                {
                    string name = d.InstanceName.ToLowerInvariant();
                    string prod = d.ProductName.ToLowerInvariant();
                    if (name.Contains(filter) || prod.Contains(filter) || name.Contains("pxn") || prod.Contains("pxn") ||
                        name.Contains("v12") || prod.Contains("v12") || name.Contains("wheel") || prod.Contains("wheel"))
                    {
                        targetDevice = d;
                        break;
                    }
                }

                if (targetDevice == null && devices.Count > 0)
                {
                    targetDevice = devices[0];
                }

                if (targetDevice != null)
                {
                    if (dinputJoystick != null)
                    {
                        try { dinputJoystick.Unacquire(); dinputJoystick.Dispose(); } catch { }
                    }

                    dinputJoystick = new SharpDX.DirectInput.Joystick(directInput, targetDevice.InstanceGuid);

                    IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                    if (hwnd == IntPtr.Zero) hwnd = GetForegroundWindow();

                    dinputJoystick.SetCooperativeLevel(hwnd, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    dinputJoystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                    dinputJoystick.Acquire();

                    dinputInitialized = true;
                    Logger.LogInfo($"[DirectInput 8] Connected to '{targetDevice.InstanceName}'!");

                    // Setup Force Feedback
                    if (ffbEnabled.Value)
                    {
                        InitForceFeedback();
                    }
                }
                else
                {
                    Logger.LogWarning("[DirectInput] No game controllers detected.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DirectInput] Init error: {ex.Message}");
            }
        }

        private void InitForceFeedback()
        {
            try
            {
                if (dinputJoystick == null) return;

                constantForceParams = new SharpDX.DirectInput.ConstantForce { Magnitude = 0 };
                var effectParameters = new EffectParameters
                {
                    Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets,
                    Duration = int.MaxValue,
                    SamplePeriod = 0,
                    Gain = 10000,
                    TriggerButton = -1,
                    TriggerRepeatInterval = 0,
                    Axes = new[] { 0 }, // X Axis
                    Directions = new[] { 0 },
                    StartDelay = 0,
                    Parameters = constantForceParams
                };

                ffbEffect = new Effect(dinputJoystick, EffectGuid.ConstantForce, effectParameters);
                ffbEffect.Start(1, EffectPlayFlags.None);
                Logger.LogInfo("[DirectInput] Force Feedback (FFB) active on wheel motor!");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[DirectInput FFB] Setup: {ex.Message}");
            }
        }

        public static void SetFFB(float force) // -1.0 to +1.0
        {
            if (ffbEffect == null || constantForceParams == null || !ffbEnabled.Value) return;

            try
            {
                if (ffbInvert.Value) force = -force;
                int magnitude = (int)Mathf.Clamp(force * 10000f * ffbGain.Value, -10000f, 10000f);
                constantForceParams.Magnitude = magnitude;

                ffbEffect.SetParameters(new EffectParameters { Parameters = constantForceParams },
                    EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.Start);
            }
            catch { }
        }

        private void ShutdownDirectInput()
        {
            try
            {
                if (ffbEffect != null) { ffbEffect.Stop(); ffbEffect.Dispose(); ffbEffect = null; }
                if (dinputJoystick != null) { dinputJoystick.Unacquire(); dinputJoystick.Dispose(); dinputJoystick = null; }
                if (directInput != null) { directInput.Dispose(); directInput = null; }
            }
            catch { }
        }

        private void PollDirectInputWheel()
        {
            if (dinputJoystick == null || !dinputInitialized) return;

            try
            {
                dinputJoystick.Poll();
                var state = dinputJoystick.GetCurrentState();

                // 1. STEERING: DirectInput Native X Axis (0 to 65535)
                // In DirectInput on Windows:
                // Full Left = 0, Exact Center = 32767, Full Right = 65535!
                rawSteerValue = (state.X - 32767.5f) / 32767.5f;
                float val = rawSteerValue;
                if (wheelInvertSteer.Value) val = -val;

                float abs = Mathf.Abs(val);
                float dz = wheelSteerDeadzone.Value;

                if (abs < dz)
                {
                    steerOut = 0f; // TRUE DEAD-PERFECT 0.00 IN CENTER!
                }
                else
                {
                    float norm = (abs - dz) / (1f - dz);
                    steerOut = Mathf.Clamp(norm * Mathf.Sign(val) * wheelSteerSensitivity.Value, -1f, 1f);
                }

                // 2. GAS PEDAL: DirectInput Z Axis (32767 unpressed -> 0 or 65535 pressed)
                // In DirectInput on Windows:
                // When unpressed, pedals rest at 32767 or 65535 or 0.
                // Let's normalize from state:
                // On PXN: Z is gas, RotationZ is brake
                float rawGasNorm = 1f - (state.Z / 65535f); // 0.0 unpressed -> 1.0 pressed
                if (wheelInvertGas.Value) rawGasNorm = 1f - rawGasNorm;
                gasOut = (rawGasNorm < 0.06f) ? 0f : Mathf.Clamp01((rawGasNorm - 0.06f) / 0.94f);

                // 3. BRAKE PEDAL: DirectInput RotationZ Axis
                float rawBrakeNorm = 1f - (state.RotationZ / 65535f); // 0.0 unpressed -> 1.0 pressed
                if (wheelInvertBrake.Value) rawBrakeNorm = 1f - rawBrakeNorm;
                brakeOut = (rawBrakeNorm < 0.06f) ? 0f : Mathf.Clamp01((rawBrakeNorm - 0.06f) / 0.94f);
            }
            catch
            {
                try { dinputJoystick.Acquire(); } catch { }
            }
        }

        // Apply inputs to sInputManager and preserve keyboard priority
        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        private static void Postfix_GetInput(sInputManager __instance)
        {
            if (!wheelEnabled.Value) return;

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

        // CarController Move hook: direct steering + FORCE FEEDBACK CALCULATION
        [HarmonyPatch(typeof(sCarController), "Move")]
        [HarmonyPrefix]
        private static void Prefix_CarController_Move(sCarController __instance)
        {
            if (!wheelEnabled.Value) return;

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

            // CALCULATE FORCE FEEDBACK (FFB) FOR WHEEL MOTOR
            if (ffbEnabled.Value && dinputInitialized)
            {
                try
                {
                    float speed = __instance.rb.linearVelocity.magnitude;
                    float speedFactor = Mathf.Clamp01(speed / 15f);

                    // 1. Centering return torque (caster angle pushing wheel back to center)
                    float centeringForce = -steerOut * Mathf.Lerp(0.15f, 0.65f, speedFactor);

                    // 2. Lateral G-Force / tire cornering resistance
                    float lateralVel = Vector3.Dot(__instance.rb.linearVelocity, __instance.transform.right);
                    float lateralResistance = -Mathf.Clamp(lateralVel / 8f, -0.6f, 0.6f);

                    // 3. Loss of grip when sliding (wheel goes light in oversteer)
                    float gripFactor = 1f;
                    if (__instance.wheels != null && __instance.wheels.Length > 0)
                    {
                        float slide = (__instance.wheels[0].slide + __instance.wheels[1].slide) / 2f;
                        gripFactor = Mathf.Clamp01(1f - slide * 0.7f);
                    }

                    // 4. In air: zero resistance
                    if (__instance.Airbourne())
                    {
                        gripFactor = 0f;
                    }

                    float totalFFB = (centeringForce + lateralResistance) * gripFactor;
                    SetFFB(totalFFB);
                }
                catch { }
            }
        }

        // ==================== RADIO: PROXIMITY SIGNAL & UNLOCKED STATIONS ====================

        public static void TuneRadioNext()
        {
            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;

            radio.forcedRadio = false;
            if (!radio.source.enabled) radio.ToggleRadio();

            int nextIndex = (radio.currentChannelIndex + 1) % radio.channels.Count;
            SwitchToStation(radio, nextIndex);
        }

        public static void TuneRadioPrev()
        {
            sRadioSystem radio = sRadioSystem.instance;
            if (radio == null || radio.channels == null || radio.channels.Count == 0) return;

            radio.forcedRadio = false;
            if (!radio.source.enabled) radio.ToggleRadio();

            int prevIndex = (radio.currentChannelIndex - 1 + radio.channels.Count) % radio.channels.Count;
            SwitchToStation(radio, prevIndex);
        }

        private static void SwitchToStation(sRadioSystem radio, int targetIndex)
        {
            radio.currentChannelIndex = targetIndex;
            radio.frequency = radio.channels[targetIndex].frequency;

            var channel = radio.channels[targetIndex];
            if (channel.queue != null && channel.queue.Length > 0)
            {
                AudioClip clip = channel.queue[(int)(UnityEngine.Random.value * channel.queue.Length) % channel.queue.Length];
                radio.source.clip = clip;
                radio.source.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, clip.length - 5f));
                radio.source.Play();
            }

            Logger.LogInfo($"[Radio Switched] -> [{targetIndex}] '{channel.name}' ({channel.frequency:F1} FM)");
        }

        [HarmonyPatch(typeof(sRadioSystem), "SetInput", new Type[] { typeof(Vector2) })]
        [HarmonyPrefix]
        private static bool Prefix_RadioSetInput(sRadioSystem __instance, Vector2 v)
        {
            __instance.forcedRadio = false;

            if (v.x > 0.25f)
            {
                TuneRadioNext();
                return false;
            }
            if (v.x < -0.25f)
            {
                TuneRadioPrev();
                return false;
            }
            if (v.y < -0.25f)
            {
                __instance.ToggleRadio();
                return false;
            }

            return true;
        }

        // FULL SIGNAL CALCULATION:
        // 1. 88.1 FM (Custom) ALWAYS gets 100% full clear signal!
        // 2. Any game station near its tower gets 100% crystal clear signal!
        [HarmonyPatch(typeof(sRadioSystem), "DoSignal")]
        [HarmonyPrefix]
        private static void Prefix_DoSignal(sRadioSystem __instance)
        {
            if (__instance.currentChannelIndex >= 0 && __instance.currentChannelIndex < __instance.channels.Count)
            {
                var ch = __instance.channels[__instance.currentChannelIndex];
                if (ch != null)
                {
                    if (ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("custom"))
                    {
                        __instance.signalStrength = 1f;
                        ch.signal = 1f;
                    }
                }
            }
        }

        // Fix RadioSignalManager proximity:
        // As you get closer to a tower, signal smoothly scales up to 100% (1.0f) crystal clear!
        // When away from tower, it drops to atmospheric static (0.1f)!
        // NEVER forcibly resets the station frequency!
        [HarmonyPatch(typeof(RadioSignalManager), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_RadioSignalManager_Update(RadioSignalManager __instance)
        {
            var radio = sRadioSystem.instance;
            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            if (radio == null || car == null || __instance.signalActivators == null) return false;

            radio.forcedRadio = false; // Never lock the player's radio!

            for (int j = 0; j < radio.channels.Count; j++)
            {
                var ch = radio.channels[j];
                if (ch == null) continue;

                if (ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("custom"))
                {
                    ch.signal = 1f;
                    continue;
                }

                if (j < __instance.signalActivators.Length && __instance.signalActivators[j] != null)
                {
                    var tower = __instance.signalActivators[j];
                    if (tower.activeSelf)
                    {
                        ch.signal = 1f; // Activated tower: 100% full clear signal!
                        continue;
                    }

                    float dist = Vector3.Distance(car.transform.position, tower.transform.position);
                    if (dist < __instance.signalDistance)
                    {
                        // STANDING AT TOWER: Signal scales all the way up to 100% (1.0f) crystal clear!
                        ch.signal = Mathf.Lerp(0.1f, 1.0f, 1f - (dist / __instance.signalDistance));
                    }
                    else
                    {
                        ch.signal = 0.1f; // Out of range: normal faint signal
                    }
                }
            }

            return false; // Suppress original method so it never forces frequency change!
        }

        // Streaming audio track for all 3500+ files with zero RAM overhead
        public class StreamingAudioTrack
        {
            public string FilePath;
            public int Channels = 2;
            public int SampleRate = 44100;
            public int TotalSamples = 44100 * 180;

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
                    true,
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

            radioStatusText = $"Loading {allMusicFilePaths.Count} tracks into 88.1 FM...";
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

            radioStatusText = $"All {fullPlaylistClips.Count} tracks ready on 88.1 FM!";
            Logger.LogInfo($"[CustomRadio] Ready! {fullPlaylistClips.Count} tracks live on 88.1 FM Custom Radio.");

            ApplyFullPlaylistToCustomChannel();
        }

        private static void ApplyFullPlaylistToCustomChannel()
        {
            if (fullPlaylistClips == null || fullPlaylistClips.Count == 0) return;

            sRadioSystem radio = UnityEngine.Object.FindFirstObjectByType<sRadioSystem>();
            if (radio == null || radio.channels == null) return;

            foreach (var ch in radio.channels)
            {
                if (ch != null && (ch.frequency == 88.1f || ch.name.ToLowerInvariant().Contains("custom")))
                {
                    ch.externalTracks.Clear();
                    ch.externalTracks.AddRange(fullPlaylistClips);
                    ch.queue = fullPlaylistClips.ToArray();
                    ch.signal = 1f;
                    Logger.LogInfo($"[CustomRadio] Populated 88.1 FM with FULL {fullPlaylistClips.Count} TRACKS!");
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
                Logger.LogInfo($"[CustomRadio] AudioLoader 88.1 FM populated with FULL {fullPlaylistClips.Count} TRACKS!");
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
                float sig = (radio.currentChannelIndex >= 0 && radio.currentChannelIndex < radio.channels.Count)
                    ? radio.channels[radio.currentChannelIndex].signal * 100f : 0f;
                stationStr = $"{radio.Frequency()} FM ({chName}) - Signal: {sig:0}%";
            }

            string devName = dinputJoystick != null ? dinputJoystick.Information.InstanceName : "Searching...";
            GUILayout.Label($"Device: {devName} (DirectInput 8 + FFB)", textStyle);
            GUILayout.Label($"FPS: {currentFps:0.}  |  Station: {stationStr}", textStyle);
            GUILayout.Label($"88.1 FM Custom: {radioStatusText}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label("--- Vehicle Control ---", textStyle);
            GUILayout.Label($"Steer: Raw={rawSteerValue:+0.00;-0.00;0.00} -> Out={steerOut:+0.00;-0.00;0.00}", textStyle);
            GUILayout.Label($"Gas: {gasOut:0.00} | Brake: {brakeOut:0.00}", textStyle);

            GUILayout.Space(4);
            GUILayout.Label($"Force Feedback (FFB): {(ffbEnabled.Value && dinputInitialized ? "ACTIVE (ConstantForce)" : "Inactive")}", textStyle);

            GUILayout.EndArea();
        }
    }
}
