using HarmonyLib;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>Your pickaxe hitting a vein or big rock (MineRock5) follows it.</summary>
    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    internal static class VeinHitPatch
    {
        private static bool Prefix(MineRock5 __instance, HitData hit)
        {
            return !Follow.TryMine(Rock.Of(__instance), hit);
        }
    }

    /// <summary>Your pickaxe hitting a multi-part rock (MineRock) follows it.</summary>
    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    internal static class PartRockHitPatch
    {
        private static bool Prefix(MineRock __instance, HitData hit)
        {
            return !Follow.TryMine(Rock.Of(__instance), hit);
        }
    }

    /// <summary>Your pickaxe hitting the ground next to a chunk (buried or not) mines the chunk instead of digging.</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain))]
    internal static class GroundHitPatch
    {
        private static bool Prefix(Vector3 hitPoint, Character character, ItemDrop.ItemData weapon, ref GameObject __result)
        {
            if (!Follow.TryMineBelow(hitPoint, character, weapon))
            {
                return true;
            }
            __result = null;
            return false;
        }
    }

    /// <summary>Loot from a followed swing lands in front of you.</summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.OnCreateNew), typeof(GameObject), typeof(bool))]
    internal static class LootPlacePatch
    {
        private static void Prefix(GameObject go)
        {
            LootDrop.Place(go);
        }
    }
}
