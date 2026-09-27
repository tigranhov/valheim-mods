using HarmonyLib;
using UnityEngine;

// Compiled into every mod whose things ride ships (see ShipPassenger). With several of those mods installed, each
// applies these patches for its own passengers only.
namespace ImmersiveMapper.Shared
{
    [HarmonyPatch(typeof(Ship), "Awake")]
    internal static class ShipAwakePatch
    {
        private static void Postfix(Ship __instance)
        {
            ShipKey.Register(__instance);
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

    // Standing on something that rides a ship counts as standing on the ship, so the game carries you along
    // (Character.ApplyGroundForce) and syncs your position relative to the ship, as on the deck.
    [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
    internal static class StandOnPassengerPatch
    {
        private static void Postfix(Character __instance)
        {
            Rigidbody ground = __instance.m_lastGroundBody;
            if (ground == null)
            {
                return;
            }
            // In parents too: a cart's wheels are bodies of their own.
            ShipPassenger passenger = ground.GetComponentInParent<ShipPassenger>();
            Rigidbody ship = passenger != null ? passenger.ShipBody : null;
            if (ship != null)
            {
                __instance.m_lastGroundBody = ship;
            }
        }
    }

    // Riding a ship makes a building piece (a crate, a cart) part of the ship: a hit on it hits the ship instead.
    // Checked where the hit starts (the attacker's side) and again where it lands (the owner), in case the two
    // disagree about the riding.
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    internal static class PassengerDamagePatch
    {
        private static bool Prefix(WearNTear __instance, HitData hit)
        {
            ShipPassenger passenger = __instance.GetComponent<ShipPassenger>();
            if (passenger == null || !passenger.IsAttached)
            {
                return true;
            }
            Ship ship = passenger.CurrentShip;
            if (ship != null && ship.m_destructible != null)
            {
                ship.m_destructible.Damage(hit);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
    internal static class PassengerDamageReceivedPatch
    {
        private static bool Prefix(WearNTear __instance)
        {
            ShipPassenger passenger = __instance.GetComponent<ShipPassenger>();
            return passenger == null || !passenger.IsAttached;
        }
    }
}
