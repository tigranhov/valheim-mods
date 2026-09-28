using HarmonyLib;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Building a crate with the hammer. The game's build ghost follows the crate's rules instead of the building
    /// rules: it goes where a crate being set down would (<see cref="CratePose"/>: snapping to other crates, Shift for
    /// none, the hammer's rotation), fits where <see cref="CrateFit"/> says (on a ship's deck, or on top of another
    /// crate, which the game refuses for buildings), and says why when it doesn't. The game's own zone rules still
    /// count: no-build zones, wards, someone standing in the way. A crate built on a deck rides the ship
    /// (<see cref="CargoCrate.OnPlaced"/>).
    /// </summary>
    internal static class CrateBuild
    {
        private const float RotationStep = 22.5f;

        // Why the crate ghost doesn't fit, shown instead of the game's "invalid placement".
        private static string _reason;

        public static bool IsCrateGhost(Player player)
        {
            GameObject ghost = player.m_placementGhost;
            return ghost != null && ghost.GetComponent<CargoCrate>() != null;
        }

        public static void UpdateGhost(Player player, bool flashGuardStone)
        {
            GameObject ghost = player.m_placementGhost;
            if (!CratePose.Find(player, player.m_placeRotation * RotationStep, CratePose.Snapping, out Vector3 position, out Quaternion rotation, out Ship ship))
            {
                ghost.SetActive(false);
                player.m_placementStatus = Player.PlacementStatus.NoRayHits;
                _reason = null;
                return;
            }
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(position, rotation);
            // The game's marker shows where it aimed, not where the crate goes.
            if (player.m_placementMarkerInstance != null)
            {
                player.m_placementMarkerInstance.SetActive(false);
            }
            bool fits = CrateFit.Fits(position, rotation, ref ship, out string reason);
            _reason = fits ? null : reason;
            Player.PlacementStatus status;
            if (player.m_placementStatus == Player.PlacementStatus.NotInDungeon)
            {
                status = Player.PlacementStatus.NotInDungeon;
            }
            else if (Location.IsInsideNoBuildLocation(position))
            {
                status = Player.PlacementStatus.NoBuildZone;
            }
            else if (!PrivateArea.CheckAccess(position, 0f, flashGuardStone))
            {
                status = Player.PlacementStatus.PrivateZone;
            }
            else if (player.CheckPlacementGhostVSPlayers())
            {
                status = Player.PlacementStatus.BlockedbyPlayer;
            }
            else
            {
                status = fits ? Player.PlacementStatus.Valid : Player.PlacementStatus.Invalid;
            }
            player.m_placementStatus = status;
            player.SetPlacementGhostValid(status == Player.PlacementStatus.Valid);
        }

        public static void ExplainRefusal(Player player)
        {
            if (player.m_placementStatus == Player.PlacementStatus.Invalid && _reason != null)
            {
                player.Message(MessageHud.MessageType.Center, _reason);
            }
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class CrateBuildGhostPatch
    {
        private static void Postfix(Player __instance, bool flashGuardStone)
        {
            if (CrateBuild.IsCrateGhost(__instance))
            {
                CrateBuild.UpdateGhost(__instance, flashGuardStone);
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class CrateBuildPlacePatch
    {
        private static void Postfix(Player __instance, bool __result)
        {
            if (!__result && CrateBuild.IsCrateGhost(__instance))
            {
                CrateBuild.ExplainRefusal(__instance);
            }
        }
    }
}
