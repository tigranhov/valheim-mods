using HarmonyLib;
using ImmersiveMapper.Shared;
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

    // A crate loaded after its size was lowered: bring every item back inside the grid, and save that if it's ours.
    [HarmonyPatch(typeof(Container), "Load")]
    internal static class CrateLoadPatch
    {
        private static void Postfix(Container __instance, bool __result)
        {
            if (__result && __instance.GetComponentInParent<CargoCrate>() != null
                && CargoCrate.FitToGrid(__instance.GetInventory()) && __instance.IsOwner())
            {
                __instance.Save();
            }
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

    // Dragging a crate out of the inventory sets it straight down nearby (never a loose item on the ground), so an
    // over-encumbered player can always get rid of it.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
    internal static class DropItemPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (!CrateItem.IsCrate(item) || !(__instance is Player player) || player != Player.m_localPlayer)
            {
                return true;
            }
            __result = CratePlacement.DropNearby(player, inventory ?? player.GetInventory(), item);
            return false;
        }
    }

    // While placing a crate (and until the button that placed or cancelled it is let go), the mouse buttons don't
    // attack or block.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class PlacingControlsPatch
    {
        private static void Prefix(ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!CrateGhost.OwnsMouseButtons)
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

    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    internal static class ShipCargoWeightPatch
    {
        private static void Postfix(Ship __instance)
        {
            CargoWeight.Apply(__instance);
        }
    }

    // A carried crate can make you encumbered (slow walk, stamina drain, no dodging): see CargoConfig.Encumbrance.
    [HarmonyPatch(typeof(Player), nameof(Player.IsEncumbered))]
    internal static class CarryEncumberedPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            CrateCarry crate = CrateCarry.CarriedBy(__instance);
            if (crate == null)
            {
                return;
            }
            switch (CargoConfig.Encumbrance.Value)
            {
                case CarryEncumbrance.Always:
                    __result = true;
                    break;
                case CarryEncumbrance.Weight:
                    __result = __instance.GetInventory().GetTotalWeight() + crate.Weight > __instance.GetMaxCarryWeight();
                    break;
            }
        }
    }

    // No sprinting with a crate in your arms.
    [HarmonyPatch(typeof(Player), "CheckRun")]
    internal static class CarryNoRunPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result && CrateCarry.IsCarrying(__instance))
            {
                __result = false;
            }
        }
    }

    // A carried crate: both hands on its sides, wrists held at the grip angle and elbows out, moving with the torso,
    // so the walk cycle doesn't swing the arms (the game's own IK pass, which also does feet and head).
    [HarmonyPatch(typeof(CharacterAnimEvent), "OnAnimatorIK")]
    internal static class CarryHandsPatch
    {
        private static void Postfix(CharacterAnimEvent __instance)
        {
            if (!(__instance.m_character is Player player))
            {
                return;
            }
            CrateCarry crate = CrateCarry.CarriedBy(player);
            if (crate == null)
            {
                return;
            }
            Animator animator = __instance.m_animator;
            crate.TrackBody(animator.bodyPosition);
            crate.GetHands(out Vector3 left, out Quaternion leftRotation, out Vector3 right, out Quaternion rightRotation);
            crate.GetElbows(out Vector3 leftElbow, out Vector3 rightElbow);
            float wrist = CargoConfig.HandRotationWeight.Value;
            float elbow = CargoConfig.ElbowWeight.Value;
            Hold(animator, AvatarIKGoal.LeftHand, left, leftRotation, wrist);
            Hold(animator, AvatarIKGoal.RightHand, right, rightRotation, wrist);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, elbow);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbow);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, elbow);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbow);
        }

        private static void Hold(Animator animator, AvatarIKGoal hand, Vector3 position, Quaternion rotation, float rotationWeight)
        {
            animator.SetIKPositionWeight(hand, 1f);
            animator.SetIKPosition(hand, position);
            animator.SetIKRotationWeight(hand, rotationWeight);
            animator.SetIKRotation(hand, rotation);
        }
    }
}
