using HarmonyLib;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>With the map case out, both hands are on it: no attacking or blocking, and no running (ReadingPace).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class HeldCaseControlsPatch
    {
        private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold,
            ref bool block, ref bool blockHold, ref bool run)
        {
            if (__instance != Player.m_localPlayer || MapCaseItem.Held(__instance) == null)
            {
                return;
            }
            attack = false;
            attackHold = false;
            secondaryAttack = false;
            secondaryAttackHold = false;
            block = false;
            blockHold = false;
            if (KitConfig.Pace.Value != ReadingPace.Any)
            {
                run = false;
            }
        }
    }

    /// <summary>
    /// ReadingPace Walk: the game already slows you to a walk during "minor actions" (Character.UpdateWalking), so reading
    /// counts as one on land. Not while swimming, where a minor action stops you completely.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.InMinorActionSlowdown))]
    internal static class ReadingWalkPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (!__result && KitConfig.Pace.Value == ReadingPace.Walk && __instance == Player.m_localPlayer
                && !__instance.IsSwimming() && MapCaseItem.Held(__instance) != null)
            {
                __result = true;
            }
        }
    }

    /// <summary>Counts footfalls for the tally's Footsteps mode.</summary>
    [HarmonyPatch(typeof(FootStep), nameof(FootStep.OnFoot), typeof(Transform))]
    internal static class TallyFootstepPatch
    {
        private static void Postfix(FootStep __instance)
        {
            Tally.OnFootstep(__instance.m_character);
        }
    }

    /// <summary>Every ship can take a log line.</summary>
    [HarmonyPatch(typeof(Ship), "Awake")]
    internal static class ShipLogLinePatch
    {
        private static void Postfix(Ship __instance)
        {
            if (__instance.GetComponent<LogLine>() == null)
            {
                __instance.gameObject.AddComponent<LogLine>();
            }
        }
    }

    /// <summary>A ship is really being destroyed (not just unloaded) when its owner calls ZNetScene.Destroy on it.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
    internal static class ShipDestroyedPatch
    {
        private static void Prefix(GameObject go)
        {
            LogLine log = go != null ? go.GetComponent<LogLine>() : null;
            ZNetView nview = go != null ? go.GetComponent<ZNetView>() : null;
            if (log != null && nview != null && nview.GetZDO() != null && nview.IsOwner())
            {
                log.DropFree();
            }
        }
    }

    /// <summary>Using the kit's parts from the inventory: the log line is fitted to the ship you stand on.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class UseKitItemPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!(__instance is Player player) || player != Player.m_localPlayer)
            {
                return true;
            }
            if (LogLineSetup.IsLogLine(item))
            {
                return !LogLine.TryFit(player, inventory ?? player.GetInventory(), item);
            }
            CaseUpgrades.Part part = CaseUpgrades.PartOf(item);
            if (part != null)
            {
                return !CaseUpgrades.TryFit(player, inventory ?? player.GetInventory(), item, part);
            }
            return true;
        }
    }

    /// <summary>A map case's tooltip says how many draft sheets it holds.</summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class CaseTooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (MapCaseItem.IsCase(item))
            {
                __result += $"\nDraft sheets: {MapCaseItem.SheetCount(item)}";
            }
        }
    }

    /// <summary>Places the map case in the hand from the hold tuning settings, for every player who takes one out.</summary>
    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.AttachItem))]
    internal static class HoldPosePatch
    {
        private static void Postfix(GameObject __result)
        {
            Transform visual = __result != null ? __result.transform.Find(MapCaseSetup.VisualName) : null;
            if (visual == null)
            {
                return;
            }
            visual.localPosition = new Vector3(KitConfig.HoldX.Value, KitConfig.HoldY.Value, KitConfig.HoldZ.Value);
            visual.localRotation = Quaternion.Euler(KitConfig.HoldPitch.Value, KitConfig.HoldYaw.Value, KitConfig.HoldRoll.Value);
        }
    }
}
