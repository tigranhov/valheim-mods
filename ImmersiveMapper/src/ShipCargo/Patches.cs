using HarmonyLib;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance)
        {
            ContainerRules.Register(__instance);
        }
    }

    // Every way into an inventory (dragging, quick stack, take all, auto-store mods) ends in one of these three.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
    internal static class InventoryAddItemPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            return Allow(__instance, item, ref __result);
        }

        internal static bool Allow(Inventory inventory, ItemDrop.ItemData item, ref bool result)
        {
            if (!ContainerRules.Blocks(inventory, item))
            {
                return true;
            }
            result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(Vector2i))]
    internal static class InventoryAddItemAtPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            return InventoryAddItemPatch.Allow(__instance, item, ref __result);
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool))]
    internal static class InventoryAddItemAmountPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            return InventoryAddItemPatch.Allow(__instance, item, ref __result);
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
    internal static class ItemWeightPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            if (CrateItem.IsPacked(__instance))
            {
                __result += CrateItem.ContentsWeight(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsTeleportable))]
    internal static class InventoryTeleportPatch
    {
        private static void Postfix(Inventory __instance, bool allowAllItems, ref bool __result)
        {
            if (!__result || allowAllItems)
            {
                return;
            }
            foreach (ItemDrop.ItemData item in __instance.GetAllItems())
            {
                if (CrateItem.BlocksTeleport(item))
                {
                    __result = false;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class ItemTooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (!CrateItem.IsCrate(item))
            {
                return;
            }
            __result += CrateItem.IsPacked(item)
                ? $"\n\n<color=orange>Packed:</color> {CrateItem.ContentsCount(item)} stack(s), {CrateItem.ContentsWeight(item):0.#} weight"
                : "\n\n<color=orange>Empty</color>";
            __result += "\nUse it to choose where to set the crate down.";
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class UseItemPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!CrateItem.IsCrate(item) || !(__instance is Player player) || player != Player.m_localPlayer)
            {
                return true;
            }
            CrateGhost.Begin(player, inventory ?? player.GetInventory(), item);
            return false;
        }
    }

    // Dropping a crate starts placing it instead, so it never becomes a loose item on the ground.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
    internal static class DropItemPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (!CrateItem.IsCrate(item) || !(__instance is Player player) || player != Player.m_localPlayer)
            {
                return true;
            }
            CrateGhost.Begin(player, inventory ?? player.GetInventory(), item);
            __result = false;
            return false;
        }
    }

    // While placing a crate, the mouse buttons place/cancel instead of attacking or blocking.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class PlacingControlsPatch
    {
        private static void Prefix(ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!CrateGhost.Active)
            {
                return;
            }
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }
    }

    // While placing a crate, the scroll wheel turns it instead of zooming the camera.
    [HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
    internal static class PlacingScrollPatch
    {
        private static void Postfix(ref float __result)
        {
            if (CrateGhost.Active && !CrateGhost.ReadingScroll)
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class ContainerHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            CargoCrate crate = __instance.GetComponentInParent<CargoCrate>();
            if (crate != null)
            {
                __result += crate.HoverSuffix();
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class ContainerInteractPatch
    {
        private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            CargoCrate crate = alt && !hold ? __instance.GetComponentInParent<CargoCrate>() : null;
            if (crate == null)
            {
                return true;
            }
            crate.RequestPickup(character);
            __result = true;
            return false;
        }
    }

    // A ship is really being destroyed (not just unloaded) when its owner calls ZNetScene.Destroy on it.
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
    internal static class ShipDestroyPatch
    {
        private static void Prefix(GameObject go)
        {
            Ship ship = go != null ? go.GetComponent<Ship>() : null;
            if (ship == null)
            {
                return;
            }
            ZNetView nview = go.GetComponent<ZNetView>();
            if (nview != null && nview.GetZDO() != null && nview.IsOwner())
            {
                ShipPassenger.NotifyShipDestroyed(ship);
            }
        }
    }

    // Loaded ships, for passengers to find their ship again after a reload.
    [HarmonyPatch(typeof(Ship), "OnEnable")]
    internal static class ShipEnablePatch
    {
        private static void Postfix(Ship __instance)
        {
            ShipPassenger.ShipLoaded(__instance);
        }
    }

    [HarmonyPatch(typeof(Ship), "OnDisable")]
    internal static class ShipDisablePatch
    {
        private static void Postfix(Ship __instance)
        {
            ShipPassenger.ShipUnloaded(__instance);
        }
    }
}
