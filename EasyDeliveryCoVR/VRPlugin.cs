using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace EasyDeliveryCoVR
{
    [BepInPlugin("opencode.easydeliveryco.vr", "Easy Delivery Co VR Support", "1.0.0")]
    public class VRPlugin : BaseUnityPlugin
    {
        // Configuration
        private static ConfigEntry<bool> enableVR;
        private static ConfigEntry<bool> enableHeadTracking;
        private static ConfigEntry<float> headTrackingScale;
        private static ConfigEntry<float> cameraHeightOffset;
        private static ConfigEntry<bool> debugMode;
        private static ConfigEntry<bool> disableVRControllers;
        
        private static Camera mainCamera;
        private static Transform originalCameraParent;
        private static Vector3 originalCameraLocalPosition;
        private static Quaternion originalCameraLocalRotation;
        
        private static bool vrInitialized = false;

        void Awake()
        {
            // Configuration bindings
            enableVR = Config.Bind("General", "EnableVR", true,
                "Enable VR mode");
            
            enableHeadTracking = Config.Bind("Tracking", "EnableHeadTracking", true,
                "Enable head tracking (camera follows HMD rotation and position)");
            
            headTrackingScale = Config.Bind("Tracking", "HeadTrackingScale", 1.0f,
                "Head tracking movement scale (0.5 = half movement, 2.0 = double movement)");
            
            cameraHeightOffset = Config.Bind("Camera", "CameraHeightOffset", 0.0f,
                "Camera height offset in meters (adjust if camera feels too low or high)");
            
            disableVRControllers = Config.Bind("Controllers", "DisableVRControllers", true,
                "Disable VR controllers (use keyboard/mouse/gamepad/wheel instead)");
            
            debugMode = Config.Bind("Debug", "DebugMode", false,
                "Show debug information about VR state");

            var harmony = new Harmony("opencode.easydeliveryco.vr");
            harmony.PatchAll();

            Logger.LogInfo("Easy Delivery Co VR Support loaded!");
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
            if (!enableVR.Value || !vrInitialized) return;

            // Find main camera if not found
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

            // Apply VR tracking to camera
            if (enableHeadTracking.Value && mainCamera != null)
            {
                ApplyVRTracking();
            }
        }

        void OnGUI()
        {
            if (debugMode.Value && enableVR.Value)
            {
                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 12;
                style.normal.textColor = Color.cyan;

                float y = 100;
                
                GUI.Label(new Rect(10, y, 400, 20), 
                    $"VR Initialized: {vrInitialized}", style);
                y += 20;

                if (vrInitialized)
                {
                    GUI.Label(new Rect(10, y, 400, 20), 
                        $"XR Device: {XRSettings.loadedDeviceName}", style);
                    y += 20;
                    
                    GUI.Label(new Rect(10, y, 400, 20), 
                        $"Head Tracking: {enableHeadTracking.Value}", style);
                    y += 20;

                    if (mainCamera != null)
                    {
                        GUI.Label(new Rect(10, y, 400, 20), 
                            $"Camera Pos: {mainCamera.transform.position}", style);
                        y += 20;
                        GUI.Label(new Rect(10, y, 400, 20), 
                            $"Camera Rot: {mainCamera.transform.rotation.eulerAngles}", style);
                    }
                }
            }
        }

        private void InitializeVR()
        {
            try
            {
                // Check if VR is available
                if (!XRSettings.enabled)
                {
                    Logger.LogInfo("Enabling XR...");
                    XRSettings.enabled = true;
                }

                if (XRSettings.loadedDeviceName == "None" || string.IsNullOrEmpty(XRSettings.loadedDeviceName))
                {
                    Logger.LogWarning("No VR device detected. Make sure your headset is connected and SteamVR/Oculus is running.");
                    Logger.LogInfo("Available XR devices: " + string.Join(", ", XRSettings.supportedDevices));
                    
                    // Try to load OpenVR
                    XRSettings.LoadDeviceByName("OpenVR");
                }

                vrInitialized = true;
                Logger.LogInfo($"VR initialized successfully! Device: {XRSettings.loadedDeviceName}");
                
                // Set rendering settings
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

            // Get HMD tracking data
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            
            if (hmd.isValid)
            {
                Vector3 headPosition = Vector3.zero;
                Quaternion headRotation = Quaternion.identity;

                // Get position
                if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out headPosition))
                {
                    // Apply tracking scale and height offset
                    headPosition *= headTrackingScale.Value;
                    headPosition.y += cameraHeightOffset.Value;
                    
                    // Apply to camera in local space
                    mainCamera.transform.localPosition = originalCameraLocalPosition + headPosition;
                }

                // Get rotation
                if (hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out headRotation))
                {
                    // Apply to camera
                    mainCamera.transform.localRotation = originalCameraLocalRotation * headRotation;
                }
            }
        }

        // Patch to disable VR controller input if requested
        [HarmonyPatch]
        class DisableVRControllersPatch
        {
            static bool Prepare()
            {
                return disableVRControllers.Value;
            }

            // This prevents the game from processing VR controller input
            // The game will only respond to traditional input (keyboard/mouse/gamepad/wheel)
            [HarmonyPrefix]
            [HarmonyPatch(typeof(UnityEngine.XR.InputTracking), "GetLocalPosition")]
            static bool GetLocalPositionPrefix(XRNode node, ref Vector3 __result)
            {
                // Only allow head tracking, block controller tracking
                if (node == XRNode.LeftHand || node == XRNode.RightHand)
                {
                    __result = Vector3.zero;
                    return false;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(UnityEngine.XR.InputTracking), "GetLocalRotation")]
            static bool GetLocalRotationPrefix(XRNode node, ref Quaternion __result)
            {
                // Only allow head tracking, block controller tracking
                if (node == XRNode.LeftHand || node == XRNode.RightHand)
                {
                    __result = Quaternion.identity;
                    return false;
                }
                return true;
            }
        }

        // Patch Camera to enable stereo rendering
        [HarmonyPatch(typeof(Camera), "stereoEnabled", MethodType.Getter)]
        class CameraStereoEnabledPatch
        {
            static bool Prefix(ref bool __result)
            {
                if (enableVR.Value && vrInitialized)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}
