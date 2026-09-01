using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Project.Code.Gameplay.Controllers;

using Project.Code.Gameplay.Interactions;
using Project.Code.Gameplay.Services;

namespace KotamonCollectiblePileUnlock;

[BepInPlugin("opencode.kotamon.collectible-pile-unlock", "Kotamon Collectible Pile Unlock", "1.0.0")]
public sealed class CollectiblePileUnlockPlugin : BasePlugin
{
    public override void Load()
    {
        new Harmony("opencode.kotamon.collectible-pile-unlock").PatchAll();
        Log.LogInfo("Junk pile cleanup lock disabled; pile contents are unchanged.");
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

    [HarmonyPatch(typeof(JunkZonesProgressService), "ClearPreviousZoneContents")]
    private static class ClearPreviousZoneContentsPatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }
}
