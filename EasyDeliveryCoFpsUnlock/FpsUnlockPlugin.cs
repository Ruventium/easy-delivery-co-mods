using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoFpsUnlock;

[BepInPlugin("opencode.easydeliveryco.fpsunlock", "Easy Delivery Co FPS Unlock", "1.0.0")]
public sealed class FpsUnlockPlugin : BaseUnityPlugin
{
    private ConfigEntry<int> _targetFps;
    private ConfigEntry<bool> _disableVSync;

    private void Awake()
    {
        _targetFps = Config.Bind("Frame Rate", "TargetFPS", 240,
            "Frame limit. Set to 0 for unlimited FPS.");
        _disableVSync = Config.Bind("Frame Rate", "DisableVSync", true,
            "Disable VSync to allow higher frame rates.");

        Application.targetFrameRate = _targetFps.Value <= 0 ? -1 : _targetFps.Value;
        
        if (_disableVSync.Value)
            QualitySettings.vSyncCount = 0;

        Logger.LogInfo($"FPS Unlock loaded! Target FPS: {(_targetFps.Value <= 0 ? "Unlimited" : _targetFps.Value.ToString())}, VSync: {(QualitySettings.vSyncCount == 0 ? "Disabled" : "Enabled")}");
    }
}
