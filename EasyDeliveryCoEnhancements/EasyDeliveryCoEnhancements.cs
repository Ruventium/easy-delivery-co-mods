using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EasyDeliveryCoEnhancements
{
    [BepInPlugin("opencode.easydeliveryco.enhancements", "Easy Delivery Co Enhancements", "1.0.0")]
    public class EnhancementsPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> disablePostProcessing;
        private static ConfigEntry<bool> showFpsCounter;
        private static ConfigEntry<float> renderDistance;
        private static ConfigEntry<bool> disablePS1Effects;
        private static ConfigEntry<int> renderWidth;
        private static ConfigEntry<int> renderHeight;
        
        private static GUIStyle fpsStyle;
        private static float deltaTime = 0.0f;

        void Awake()
        {
            disablePostProcessing = Config.Bind("Graphics", "DisablePostProcessing", true, 
                "Disable motion blur, chromatic aberration, lens distortion and vignette effects");
            
            showFpsCounter = Config.Bind("UI", "ShowFPSCounter", true, 
                "Show FPS counter in top-left corner");
            
            renderDistance = Config.Bind("Graphics", "RenderDistance", 5000f, 
                "Maximum render distance (default: dynamic based on fog, set to 5000 for max)");
            
            disablePS1Effects = Config.Bind("Graphics", "DisablePS1Effects", true,
                "Disable PS1-style effects (CRT, low resolution, pixelation)");
            
            renderWidth = Config.Bind("Graphics", "RenderWidth", 1920,
                "Render texture width (default PS1: 256, modern: 1920+)");
            
            renderHeight = Config.Bind("Graphics", "RenderHeight", 1080,
                "Render texture height (default PS1: 256, modern: 1080+)");

            var harmony = new Harmony("opencode.easydeliveryco.enhancements");
            harmony.PatchAll();

            Logger.LogInfo("Easy Delivery Co Enhancements loaded!");
        }

        void OnGUI()
        {
            if (!showFpsCounter.Value) return;

            if (fpsStyle == null)
            {
                fpsStyle = new GUIStyle();
                fpsStyle.alignment = TextAnchor.UpperLeft;
                fpsStyle.fontSize = 14;
                fpsStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); // серый полупрозрачный
            }

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            float fps = 1.0f / deltaTime;
            
            Rect rect = new Rect(10, 10, 200, 30);
            string text = string.Format("FPS: {0:0.}", fps);
            
            GUI.Label(rect, text, fpsStyle);
        }

        // Patch ChromaticAberration to disable it
        [HarmonyPatch(typeof(ChromaticAberration), "IsActive")]
        class ChromaticAberrationPatch
        {
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

        // Patch LensDistortion to disable it
        [HarmonyPatch(typeof(LensDistortion), "IsActive")]
        class LensDistortionPatch
        {
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

        // Patch Vignette to disable it
        [HarmonyPatch(typeof(Vignette), "IsActive")]
        class VignettePatch
        {
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

        // Patch camera farClipPlane
        [HarmonyPatch(typeof(Camera), "farClipPlane", MethodType.Setter)]
        class CameraFarClipPlanePatch
        {
            static void Prefix(Camera __instance, ref float value)
            {
                if (renderDistance.Value > 0)
                {
                    value = Mathf.Max(value, renderDistance.Value);
                }
            }
        }
        
        // Disable CRT effect by setting Volume weight to 0
        [HarmonyPatch]
        class DisableCRTEffectPatch
        {
            static bool Prepare()
            {
                return disablePS1Effects.Value;
            }
            
            [HarmonyPostfix]
            [HarmonyPatch("sOptionsMenu", "Start")]
            static void Postfix(object __instance)
            {
                var volumeField = __instance.GetType().GetField("volume");
                if (volumeField != null)
                {
                    var volume = volumeField.GetValue(__instance) as Volume;
                    if (volume != null)
                    {
                        volume.weight = 0f;
                    }
                }
            }
        }
        
        // Increase MiniRenderer resolution
        [HarmonyPatch]
        class MiniRendererResolutionPatch
        {
            static bool Prepare()
            {
                return disablePS1Effects.Value;
            }
            
            [HarmonyPrefix]
            [HarmonyPatch("MiniRenderer", "Start")]
            static void Prefix(object __instance)
            {
                var widthField = __instance.GetType().GetField("width");
                var heightField = __instance.GetType().GetField("height");
                
                if (widthField != null) widthField.SetValue(__instance, renderWidth.Value);
                if (heightField != null) heightField.SetValue(__instance, renderHeight.Value);
            }
        }
        
        // Change FilterMode to Trilinear for smooth rendering
        [HarmonyPatch]
        class MiniRendererFilterModePatch
        {
            static bool Prepare()
            {
                return disablePS1Effects.Value;
            }
            
            [HarmonyPostfix]
            [HarmonyPatch("MiniRenderer", "Start")]
            static void Postfix(object __instance)
            {
                var rtField = __instance.GetType().GetField("rt");
                if (rtField != null)
                {
                    var rt = rtField.GetValue(__instance) as RenderTexture;
                    if (rt != null)
                    {
                        rt.filterMode = FilterMode.Trilinear;
                    }
                }
            }
        }
    }
}
