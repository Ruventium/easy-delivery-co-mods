using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Project.Code.Gameplay.UI.Settings;
using UnityEngine;

namespace KotamonFPSUnlock;

[BepInPlugin("opencode.kotamon.fpsunlock", "Kotamon FPS Unlock", "1.1.0")]
public sealed class FpsUnlockPlugin : BepInEx.Unity.IL2CPP.BasePlugin
{
    private ConfigEntry<int> _targetFps;
    private ConfigEntry<string> _vSyncMode;

    public override void Load()
    {
        Instance = this;
        _targetFps = Config.Bind("Frame Rate", "TargetFPS", 240,
            "Frame limit. Set to 0 for unlimited FPS.");
        _vSyncMode = Config.Bind("Frame Rate", "VSyncMode", "Game",
            "Game follows the in-game setting; Off disables VSync; On enables VSync.");

        Application.targetFrameRate = _targetFps.Value <= 0 ? -1 : _targetFps.Value;
        ApplyVSyncMode();
        if (!string.Equals(_vSyncMode.Value, "Game", StringComparison.OrdinalIgnoreCase))
            new Harmony("opencode.kotamon.fpsunlock").PatchAll();

        Log.LogInfo($"Target frame rate set to {(_targetFps.Value <= 0 ? "unlimited" : _targetFps.Value + " FPS")}; VSyncMode={_vSyncMode.Value}.");
    }

    private void ApplyVSyncMode()
    {
        if (string.Equals(_vSyncMode.Value, "Off", StringComparison.OrdinalIgnoreCase))
            QualitySettings.vSyncCount = 0;
        else if (string.Equals(_vSyncMode.Value, "On", StringComparison.OrdinalIgnoreCase))
            QualitySettings.vSyncCount = 1;
    }

    [HarmonyPatch(typeof(SettingsView), "HandleVsyncChanged")]
    private static class InGameVSyncPatch
    {
        private static void Postfix(bool value)
        {
            if (Instance != null && !string.Equals(Instance._vSyncMode.Value, "Game", StringComparison.OrdinalIgnoreCase))
                Instance.ApplyVSyncMode();
        }
    }

    private static FpsUnlockPlugin Instance { get; set; }
}
