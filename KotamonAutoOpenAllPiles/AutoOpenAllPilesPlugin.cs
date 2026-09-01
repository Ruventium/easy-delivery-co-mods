using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Project.Code.Gameplay.Interactions;
using Project.Code.Gameplay.Services;

namespace KotamonPileUnlock;

[BepInPlugin("opencode.kotamon.pile-unlock", "Kotamon Pile Unlock", "1.1.0")]
public sealed class PileUnlockPlugin : BasePlugin
{
    public override void Load()
    {
        new Harmony("opencode.kotamon.pile-unlock").PatchAll();
        Log.LogInfo("Pile unlock loaded; manual opening is unrestricted and previous pile contents are preserved.");
    }

    [HarmonyPatch(typeof(JunkZonesProgressService), "ClearPreviousZoneContents")]
    private static class PreservePreviousPilePatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(JunkZonesProgressService), "SetZonesCompleteBubble")]
    private static class SetZonesCompleteBubblePatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(LockZone), "GetCleanupPercent")]
    private static class GetCleanupPercentPatch
    {
        private static bool Prefix(ref float __result)
        {
            __result = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(LockZone), "CanOpenByOtherZonesProgress")]
    private static class CanOpenByOtherZonesProgressPatch
    {
        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

}
