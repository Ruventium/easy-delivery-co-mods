using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EasyDeliveryCoSteeringWheel
{
    [BepInPlugin("opencode.easydeliveryco.steeringwheel", "Easy Delivery Co Steering Wheel Support", "1.0.0")]
    public class SteeringWheelPlugin : BaseUnityPlugin
    {
        // Configuration
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
        private static ConfigEntry<bool> debugMode;
        
        private static bool wheelDetected = false;
        private static string detectedWheelName = "";

        void Awake()
        {
            // Configuration bindings
            enableSteeringWheel = Config.Bind("General", "EnableSteeringWheel", true,
                "Enable steering wheel support");
            
            steeringAxisName = Config.Bind("Axes", "SteeringAxis", "Joystick Axis 1",
                "Steering wheel axis name (e.g., 'Joystick Axis 1', 'Horizontal')");
            
            throttleAxisName = Config.Bind("Axes", "ThrottleAxis", "Joystick Axis 3",
                "Throttle pedal axis name (e.g., 'Joystick Axis 3')");
            
            brakeAxisName = Config.Bind("Axes", "BrakeAxis", "Joystick Axis 2",
                "Brake pedal axis name (e.g., 'Joystick Axis 2')");
            
            combinedPedals = Config.Bind("Axes", "CombinedPedals", false,
                "Use combined pedals (single axis for both throttle and brake)");
            
            steeringDeadzone = Config.Bind("Deadzones", "SteeringDeadzone", 0.05f,
                "Steering deadzone (0.0 to 1.0)");
            
            throttleDeadzone = Config.Bind("Deadzones", "ThrottleDeadzone", 0.05f,
                "Throttle deadzone (0.0 to 1.0)");
            
            brakeDeadzone = Config.Bind("Deadzones", "BrakeDeadzone", 0.05f,
                "Brake deadzone (0.0 to 1.0)");
            
            steeringSensitivity = Config.Bind("Sensitivity", "SteeringSensitivity", 1.0f,
                "Steering sensitivity multiplier (0.1 to 2.0)");
            
            invertSteering = Config.Bind("Inversion", "InvertSteering", false,
                "Invert steering direction");
            
            invertThrottle = Config.Bind("Inversion", "InvertThrottle", false,
                "Invert throttle direction");
            
            invertBrake = Config.Bind("Inversion", "InvertBrake", false,
                "Invert brake direction");
            
            debugMode = Config.Bind("Debug", "DebugMode", false,
                "Show debug information about detected axes");

            var harmony = new Harmony("opencode.easydeliveryco.steeringwheel");
            harmony.PatchAll();

            Logger.LogInfo("Easy Delivery Co Steering Wheel Support loaded!");
            DetectSteeringWheel();
        }

        void Update()
        {
            if (debugMode.Value && wheelDetected)
            {
                // Debug joystick input
                string[] joystickNames = Input.GetJoystickNames();
                if (joystickNames.Length > 0)
                {
                    Logger.LogInfo($"Connected devices: {string.Join(", ", joystickNames)}");
                }
            }
        }

        void OnGUI()
        {
            if (debugMode.Value && enableSteeringWheel.Value)
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

                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), 
                        $"Steering: {steering:F2}", style);
                    y += 20;
                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), 
                        $"Throttle: {throttle:F2}", style);
                    y += 20;
                    GUI.Label(new Rect(Screen.width - 300, y, 290, 20), 
                        $"Brake: {brake:F2}", style);
                }
            }
        }

        private void DetectSteeringWheel()
        {
            string[] joystickNames = Input.GetJoystickNames();
            
            if (joystickNames.Length == 0)
            {
                Logger.LogWarning("No joystick devices detected. Steering wheel support will activate when a device is connected.");
                return;
            }

            // Look for common steering wheel names
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

            // If no specific wheel detected but joysticks exist, assume it might be a wheel
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
                // Axis doesn't exist in Input Manager, try raw joystick axes
                // PXN V12 Lite typically uses these raw axis indices:
                // Axis 0: Steering
                // Axis 1: Throttle (Z-axis)
                // Axis 2: Brake (RZ-axis)
                
                if (axisName.Contains("1") || axisName.ToLower().Contains("steering"))
                {
                    value = Input.GetAxis("Horizontal"); // Fallback to horizontal
                }
                else if (axisName.Contains("2"))
                {
                    value = Input.GetAxisRaw("Vertical"); // Try vertical as fallback
                }
                else if (axisName.Contains("3"))
                {
                    value = Input.GetAxisRaw("Vertical"); // Try vertical as fallback
                }
            }

            // Apply deadzone
            if (Mathf.Abs(value) < deadzone)
            {
                value = 0f;
            }
            else
            {
                // Rescale from deadzone to 1.0
                float sign = Mathf.Sign(value);
                value = (Mathf.Abs(value) - deadzone) / (1f - deadzone) * sign;
            }

            // Apply inversion
            if (invert)
            {
                value = -value;
            }

            return value;
        }

        // Patch sInputManager to inject steering wheel input
        [HarmonyPatch]
        class InputManagerPatch
        {
            static bool Prepare()
            {
                return enableSteeringWheel.Value;
            }

            [HarmonyPostfix]
            [HarmonyPatch("sInputManager", "GetInput")]
            static void Postfix(object __instance)
            {
                if (!enableSteeringWheel.Value) return;

                // Check if a wheel is connected
                string[] joysticks = Input.GetJoystickNames();
                if (joysticks.Length == 0) return;

                try
                {
                    // Get the driveInput field
                    var driveInputField = __instance.GetType().GetField("driveInput");
                    if (driveInputField == null) return;

                    // Read wheel axes
                    float steering = GetAxisValue(steeringAxisName.Value, steeringDeadzone.Value, invertSteering.Value);
                    float throttle = GetAxisValue(throttleAxisName.Value, throttleDeadzone.Value, invertThrottle.Value);
                    float brake = GetAxisValue(brakeAxisName.Value, brakeDeadzone.Value, invertBrake.Value);

                    // Apply steering sensitivity
                    steering *= steeringSensitivity.Value;
                    steering = Mathf.Clamp(steering, -1f, 1f);

                    // Combine throttle and brake
                    float combinedThrottle;
                    if (combinedPedals.Value)
                    {
                        // Single axis mode: positive = throttle, negative = brake
                        combinedThrottle = throttle;
                    }
                    else
                    {
                        // Separate pedals: combine them
                        // Normalize from 0-1 range to -1 to 1 range
                        throttle = (throttle + 1f) / 2f; // Convert from -1..1 to 0..1
                        brake = (brake + 1f) / 2f;       // Convert from -1..1 to 0..1
                        
                        combinedThrottle = throttle - brake;
                        combinedThrottle = Mathf.Clamp(combinedThrottle, -1f, 1f);
                    }

                    // Only override if there's significant input from wheel
                    if (Mathf.Abs(steering) > 0.01f || Mathf.Abs(combinedThrottle) > 0.01f)
                    {
                        Vector2 wheelInput = new Vector2(steering, combinedThrottle);
                        driveInputField.SetValue(__instance, wheelInput);
                    }
                }
                catch (System.Exception e)
                {
                    // Silent fail - don't spam console if reflection fails
                }
            }
        }
    }
}
