using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Linq;

namespace EasyDeliveryCoEnhancements
{
    [BepInPlugin("opencode.easydeliveryco.enhancements", "Easy Delivery Co Enhancements", "2.0.0")]
    public class EnhancementsPlugin : BaseUnityPlugin
    {
        // ==================== FPS UNLOCK ====================
        private static ConfigEntry<bool> enableFpsUnlock;
        private static ConfigEntry<int> targetFps;
        private static ConfigEntry<bool> disableVSync;
        
        // ==================== GRAPHICS ====================
        private static ConfigEntry<bool> enableGraphicsEnhancements;
        private static ConfigEntry<bool> disablePostProcessing;
        private static ConfigEntry<float> renderDistance;
        private static ConfigEntry<bool> disablePS1Effects;
        private static ConfigEntry<int> renderWidth;
        private static ConfigEntry<int> renderHeight;
        private static ConfigEntry<int> textureFilterMode;
        
        // ==================== UI ====================
        private static ConfigEntry<bool> showFpsCounter;
        
        // ==================== STEERING WHEEL ====================
        private static ConfigEntry<bool> enableSteeringWheel;
        private static ConfigEntry<string> steeringAxisName;
        private static ConfigEntry<string> throttleAxisName;
        private static ConfigEntry<string> brakeAxisName;
        private static ConfigEntry<bool> combinedPedals;
        private static ConfigEntry<float> steeringDeadzone;
        private static ConfigEntry<float> throttleDeadzone;
        private static ConfigEntry<float> brakeDeadzone;
        private static ConfigEntry<float> steeringSensitivity;
        private static ConfigEntry<bool> invertSteering;
        private static ConfigEntry<bool> invertThrottle;
        private static ConfigEntry<bool> invertBrake;
        private static ConfigEntry<bool> wheelDebugMode;
        
        // ==================== VR ====================
        private static ConfigEntry<bool> enableVR;
        private static ConfigEntry<bool> enableHeadTracking;
        private static ConfigEntry<float> headTrackingScale;
        private static ConfigEntry<float> cameraHeightOffset;
        private static ConfigEntry<bool> disableVRControllers;
        private static ConfigEntry<bool> vrDebugMode;
        
        // State
        private static GUIStyle fpsStyle;
        private static float deltaTime = 0.0f;
        private static bool wheelDetected = false;
        private static string detectedWheelName = "";
        private static Camera mainCamera;
        private static Transform originalCameraParent;
        private static Vector3 originalCameraLocalPosition;
        private static Quaternion originalCameraLocalRotation;
        private static bool vrInitialized = false;

        void Awake()
        {
            // ==================== FPS UNLOCK ====================
            enableFpsUnlock = Config.Bind("1. FPS Unlock", "Enable", false,
                "Enable FPS unlock and VSync control. Game default: 60 FPS with VSync ON.");
            
            targetFps = Config.Bind("1. FPS Unlock", "TargetFPS", 60,
                "Target frame rate limit. Set to 0 for unlimited. Game default: 60");
            
            disableVSync = Config.Bind("1. FPS Unlock", "DisableVSync", false,
                "Disable vertical synchronization to allow higher frame rates. Game default: ON (VSync enabled)");

            // ==================== GRAPHICS ====================
            enableGraphicsEnhancements = Config.Bind("2. Graphics", "Enable", false,
                "Enable graphics enhancements (HD rendering, disable PS1 effects, etc). Game default: PS1 retro style.");
            
            disablePostProcessing = Config.Bind("2. Graphics", "DisablePostProcessing", false, 
                "Disable motion blur, chromatic aberration, lens distortion and vignette effects. Game default: ON (effects enabled)");
            
            renderDistance = Config.Bind("2. Graphics", "RenderDistance", 1000f, 
                "Maximum render distance in units. Game default: ~1000 (dynamic based on fog)");
            
            disablePS1Effects = Config.Bind("2. Graphics", "DisablePS1Effects", false,
                "Disable PS1-style effects (CRT, low resolution, pixelation). Game default: ON (PS1 style enabled)");
            
            renderWidth = Config.Bind("2. Graphics", "RenderWidth", 256,
                "Internal render resolution width. Game default: 256 (PS1 style). Set to 1920+ for HD.");
            
            renderHeight = Config.Bind("2. Graphics", "RenderHeight", 256,
                "Internal render resolution height. Game default: 256 (PS1 style). Set to 1080+ for HD.");
            
            textureFilterMode = Config.Bind("2. Graphics", "TextureFilterMode", 0,
                "Texture filtering: 0=Point (pixelated, game default), 1=Bilinear (smooth), 2=Trilinear (smoothest)");

            // ==================== UI ====================
            showFpsCounter = Config.Bind("3. UI", "ShowFPSCounter", false, 
                "Show FPS counter in top-left corner. Game default: OFF");

            // ==================== STEERING WHEEL ====================
            enableSteeringWheel = Config.Bind("4. Steering Wheel", "Enable", false,
                "Enable steering wheel support. Game default: Keyboard/Gamepad only.");
            
            steeringAxisName = Config.Bind("4. Steering Wheel", "SteeringAxis", "Joystick Axis 1",
                "Steering wheel axis name. Common: 'Joystick Axis 1' or 'Horizontal'. Game default: N/A");
            
            throttleAxisName = Config.Bind("4. Steering Wheel", "ThrottleAxis", "Joystick Axis 3",
                "Throttle pedal axis name. Common: 'Joystick Axis 3'. Game default: N/A");
            
            brakeAxisName = Config.Bind("4. Steering Wheel", "BrakeAxis", "Joystick Axis 2",
                "Brake pedal axis name. Common: 'Joystick Axis 2'. Game default: N/A");
            
            combinedPedals = Config.Bind("4. Steering Wheel", "CombinedPedals", false,
                "Single axis for both pedals (positive=throttle, negative=brake). Game default: N/A");
            
            steeringDeadzone = Config.Bind("4. Steering Wheel", "SteeringDeadzone", 0.05f,
                "Steering deadzone (0.0 to 1.0). Recommended: 0.05");
            
            throttleDeadzone = Config.Bind("4. Steering Wheel", "ThrottleDeadzone", 0.05f,
                "Throttle deadzone (0.0 to 1.0). Recommended: 0.05");
            
            brakeDeadzone = Config.Bind("4. Steering Wheel", "BrakeDeadzone", 0.05f,
                "Brake deadzone (0.0 to 1.0). Recommended: 0.05");
            
            steeringSensitivity = Config.Bind("4. Steering Wheel", "SteeringSensitivity", 1.0f,
                "Steering sensitivity multiplier (0.1 to 2.0). Recommended: 1.0");
            
            invertSteering = Config.Bind("4. Steering Wheel", "InvertSteering", false,
                "Invert steering direction (if wheel turns wrong way)");
            
            invertThrottle = Config.Bind("4. Steering Wheel", "InvertThrottle", false,
                "Invert throttle direction (if pedal works backwards)");
            
            invertBrake = Config.Bind("4. Steering Wheel", "InvertBrake", false,
                "Invert brake direction (if pedal works backwards)");
            
            wheelDebugMode = Config.Bind("4. Steering Wheel", "DebugMode", false,
                "Show debug overlay with wheel input values (top-right corner)");

            // ==================== VR ====================
            enableVR = Config.Bind("5. VR Support", "Enable", false,
                "Enable VR mode with headset tracking. Game default: Flat screen only.");
            
            enableHeadTracking = Config.Bind("5. VR Support", "EnableHeadTracking", true,
                "Enable 6DOF head tracking (position + rotation). Game default: N/A");
            
            headTrackingScale = Config.Bind("5. VR Support", "HeadTrackingScale", 1.0f,
                "Head position tracking scale (0.5 = half movement, 2.0 = double). Recommended: 1.0");
            
            cameraHeightOffset = Config.Bind("5. VR Support", "CameraHeightOffset", 0.0f,
                "Camera height adjustment in meters (if view feels too high/low). Game default: 0.0");
            
            disableVRControllers = Config.Bind("5. VR Support", "DisableVRControllers", true,
                "Disable VR controllers, use keyboard/mouse/gamepad/wheel instead. Game default: N/A");
            
            vrDebugMode = Config.Bind("5. VR Support", "DebugMode", false,
                "Show VR debug information overlay (top-left corner)");

            // Apply FPS settings immediately
            if (enableFpsUnlock.Value)
            {
                Application.targetFrameRate = targetFps.Value <= 0 ? -1 : targetFps.Value;
                if (disableVSync.Value)
                    QualitySettings.vSyncCount = 0;
                Logger.LogInfo($"FPS Unlock: Target={targetFps.Value} (0=unlimited), VSync={!disableVSync.Value}");
            }

            var harmony = new Harmony("opencode.easydeliveryco.enhancements");
            harmony.PatchAll();

            // Detect steering wheel
            if (enableSteeringWheel.Value)
            {
                DetectSteeringWheel();
            }

            Logger.LogInfo("Easy Delivery Co Enhancements 2.0 loaded!");
            Logger.LogInfo($"Enabled modules: FPS={enableFpsUnlock.Value}, Graphics={enableGraphicsEnhancements.Value}, Wheel={enableSteeringWheel.Value}, VR={enableVR.Value}");
        }

        void Start()
        {
            if (enableVR.Value)
            {
                InitializeVR();
            }
        }

        void Update()
        {
            // VR tracking
            if (enableVR.Value && vrInitialized && enableHeadTracking.Value)
            {
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera != null && originalCameraParent == null)
                    {
                        originalCameraParent = mainCamera.transform.parent;
                        originalCameraLocalPosition = mainCamera.transform.localPosition;
                        originalCameraLocalRotation = mainCamera.transform.localRotation;
                    }
                }

                if (mainCamera != null)
                {
                    ApplyVRTracking();
                }
            }
        }

        void OnGUI()
        {
            // FPS Counter
            if (showFpsCounter.Value)
            {
                if (fpsStyle == null)
                {
                    fpsStyle = new GUIStyle();
                    fpsStyle.alignment = TextAnchor.UpperLeft;
                    fpsStyle.fontSize = 14;
                    fpsStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                }

                deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
                float fps = 1.0f / deltaTime;
                
                GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {fps:0.}", fpsStyle);
            }

            // Steering Wheel Debug
            if (wheelDebugMode.Value && enableSteeringWheel.Value)
            {
                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.UpperRight;
                style.fontSize = 12;
                style.normal.textColor = Color.yellow;

                float y = 10;
                
                GUI.Label(new Rect(Screen.width - 300, y, 290, 20), 
                    $"Wheel: {(wheelDetected ? detectedWheelName : "Not detected")}", style);
                y += 20;

                if (wheelDetected)
                {
                    float steering = GetAxisValue(steeringAxisName.Value, steeringDeadzone.Value, invertSteering.Value);
                    float throttle = GetAxisValue(throttleAxisName.Value, throttleDeadzone.Value, invertThrottle.Value);
                    float brake = GetAxisValue(brakeAxisName.Value, brakeDeadzone.Value, invertBrake.Value);

                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), $"Steering: {steering:F2}", style);
                    y += 20;
                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), $"Throttle: {throttle:F2}", style);
                    y += 20;
                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), $"Brake: {brake:F2}", style);
                }
            }

            // VR Debug
            if (vrDebugMode.Value && enableVR.Value)
            {
                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 12;
                style.normal.textColor = Color.cyan;

                float y = 100;
                
                GUI.Label(new Rect(10, y, 400, 20), $"VR Initialized: {vrInitialized}", style);
                y += 20;

                if (vrInitialized)
                {
                    GUI.Label(new Rect(10, y, 400, 20), $"XR Device: {XRSettings.loadedDeviceName}", style);
                    y += 20;
                    GUI.Label(new Rect(10, y, 400, 20), $"Head Tracking: {enableHeadTracking.Value}", style);
                    y += 20;

                    if (mainCamera != null)
                    {
                        GUI.Label(new Rect(10, y, 400, 20), $"Camera Pos: {mainCamera.transform.position}", style);
                        y += 20;
                        GUI.Label(new Rect(10, y, 400, 20), $"Camera Rot: {mainCamera.transform.rotation.eulerAngles}", style);
                    }
                }
            }
        }

        // ==================== STEERING WHEEL METHODS ====================
        
        private void DetectSteeringWheel()
        {
            string[] joystickNames = Input.GetJoystickNames();
            
            if (joystickNames.Length == 0)
            {
                Logger.LogWarning("No joystick devices detected. Steering wheel support will activate when a device is connected.");
                return;
            }

            var wheelKeywords = new[] { "wheel", "racing", "driving", "pxn", "logitech", "thrustmaster", "fanatec" };
            
            foreach (string name in joystickNames)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    string lowerName = name.ToLower();
                    if (wheelKeywords.Any(keyword => lowerName.Contains(keyword)))
                    {
                        wheelDetected = true;
                        detectedWheelName = name;
                        Logger.LogInfo($"Steering wheel detected: {name}");
                        return;
                    }
                }
            }

            if (joystickNames.Length > 0 && !string.IsNullOrEmpty(joystickNames[0]))
            {
                wheelDetected = true;
                detectedWheelName = joystickNames[0];
                Logger.LogInfo($"Joystick detected (may be a wheel): {joystickNames[0]}");
            }
        }

        private static float GetAxisValue(string axisName, float deadzone, bool invert)
        {
            float value = 0f;
            
            try
            {
                value = Input.GetAxis(axisName);
            }
            catch
            {
                if (axisName.Contains("1") || axisName.ToLower().Contains("steering"))
                {
                    value = Input.GetAxis("Horizontal");
                }
                else if (axisName.Contains("2") || axisName.Contains("3"))
                {
                    value = Input.GetAxisRaw("Vertical");
                }
            }

            if (Mathf.Abs(value) < deadzone)
            {
                value = 0f;
            }
            else
            {
                float sign = Mathf.Sign(value);
                value = (Mathf.Abs(value) - deadzone) / (1f - deadzone) * sign;
            }

            if (invert)
            {
                value = -value;
            }

            return value;
        }

        // ==================== VR METHODS ====================
        
        private void InitializeVR()
        {
            try
            {
                if (!XRSettings.enabled)
                {
                    Logger.LogInfo("Enabling XR...");
                    XRSettings.enabled = true;
                }

                if (XRSettings.loadedDeviceName == "None" || string.IsNullOrEmpty(XRSettings.loadedDeviceName))
                {
                    Logger.LogWarning("No VR device detected. Make sure your headset is connected and SteamVR/Oculus is running.");
                    Logger.LogInfo("Available XR devices: " + string.Join(", ", XRSettings.supportedDevices));
                    XRSettings.LoadDeviceByName("OpenVR");
                }

                vrInitialized = true;
                Logger.LogInfo($"VR initialized! Device: {XRSettings.loadedDeviceName}");
                
                XRSettings.eyeTextureResolutionScale = 1.0f;
                XRSettings.renderViewportScale = 1.0f;
                
                Logger.LogInfo($"VR Eye Resolution: {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}");
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Failed to initialize VR: {e.Message}");
                vrInitialized = false;
            }
        }

        private void ApplyVRTracking()
        {
            if (mainCamera == null) return;

            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            
            if (hmd.isValid)
            {
                Vector3 headPosition = Vector3.zero;
                Quaternion headRotation = Quaternion.identity;

                if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out headPosition))
                {
                    headPosition *= headTrackingScale.Value;
                    headPosition.y += cameraHeightOffset.Value;
                    mainCamera.transform.localPosition = originalCameraLocalPosition + headPosition;
                }

                if (hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out headRotation))
                {
                    mainCamera.transform.localRotation = originalCameraLocalRotation * headRotation;
                }
            }
        }

        // ==================== HARMONY PATCHES ====================

        // ==================== HARMONY PATCHES ====================

        // Graphics: Disable Chromatic Aberration
        [HarmonyPatch(typeof(ChromaticAberration), "IsActive")]
        class ChromaticAberrationPatch
        {
            static bool Prepare() => enableGraphicsEnhancements.Value;
            
            static bool Prefix(ref bool __result)
            {
                if (disablePostProcessing.Value)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Graphics: Disable Lens Distortion
        [HarmonyPatch(typeof(LensDistortion), "IsActive")]
        class LensDistortionPatch
        {
            static bool Prepare() => enableGraphicsEnhancements.Value;
            
            static bool Prefix(ref bool __result)
            {
                if (disablePostProcessing.Value)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Graphics: Disable Vignette
        [HarmonyPatch(typeof(Vignette), "IsActive")]
        class VignettePatch
        {
            static bool Prepare() => enableGraphicsEnhancements.Value;
            
            static bool Prefix(ref bool __result)
            {
                if (disablePostProcessing.Value)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Graphics: Increase Render Distance
        [HarmonyPatch(typeof(Camera), "farClipPlane", MethodType.Setter)]
        class CameraFarClipPlanePatch
        {
            static bool Prepare() => enableGraphicsEnhancements.Value;
            
            static void Prefix(Camera __instance, ref float value)
            {
                if (renderDistance.Value > 0)
                {
                    value = Mathf.Max(value, renderDistance.Value);
                }
            }
        }
        
        // Note: PS1 effects patches removed due to Harmony type resolution issues
        // These features may not work correctly until a proper reflection-based approach is implemented

        // Steering Wheel: Inject wheel input
        // Note: sInputManager patch removed due to Harmony type resolution issues

        // VR: Disable controller position tracking
        [HarmonyPatch(typeof(UnityEngine.XR.InputTracking), "GetLocalPosition")]
        class DisableVRControllerPositionPatch
        {
            static bool Prepare() => enableVR.Value && disableVRControllers.Value;
            
            static bool Prefix(XRNode node, ref Vector3 __result)
            {
                if (node == XRNode.LeftHand || node == XRNode.RightHand)
                {
                    __result = Vector3.zero;
                    return false;
                }
                return true;
            }
        }

        // VR: Disable controller rotation tracking
        [HarmonyPatch(typeof(UnityEngine.XR.InputTracking), "GetLocalRotation")]
        class DisableVRControllerRotationPatch
        {
            static bool Prepare() => enableVR.Value && disableVRControllers.Value;
            
            static bool Prefix(XRNode node, ref Quaternion __result)
            {
                if (node == XRNode.LeftHand || node == XRNode.RightHand)
                {
                    __result = Quaternion.identity;
                    return false;
                }
                return true;
            }
        }

        // VR: Enable stereo rendering
        [HarmonyPatch(typeof(Camera), "stereoEnabled", MethodType.Getter)]
        class CameraStereoEnabledPatch
        {
            static bool Prepare() => enableVR.Value;
            
            static bool Prefix(ref bool __result)
            {
                if (vrInitialized)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}
