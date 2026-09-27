using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    internal static class CratePlacement
    {
        private const float DropDistance = 1.4f;
        private const float DropProbeHeight = 1.5f;
        private const float DropProbeDepth = 4f;
        // Front first, then around the player: the first free spot wins.
        private static readonly float[] DropAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        /// <summary>
        /// Dragging a crate out of the inventory sets it straight down at the nearest free spot around the player
        /// (no placement ghost), so an over-encumbered player can always get rid of it.
        /// </summary>
        public static bool DropNearby(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (FindDropSpot(player, out Vector3 position, out Quaternion rotation, out Ship ship))
            {
                return Spawn(player, inventory, item, position, rotation, ship);
            }
            player.Message(MessageHud.MessageType.Center, "No room to put the crate down here");
            return false;
        }

        /// <summary>The nearest spot around the player where a crate fits: in front first, then around.</summary>
        public static bool FindDropSpot(Player player, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            foreach (float angle in DropAngles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 probe = player.transform.position + direction * DropDistance + Vector3.up * DropProbeHeight;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, DropProbeDepth, CrateFit.Mask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                // Square to the deck on a ship; on land, facing away from the player.
                float yaw = ShipOf(hit.collider) != null ? 0f : Quaternion.LookRotation(direction).eulerAngles.y;
                CrateFit.RestOn(hit, yaw, out position, out rotation, out ship);
                if (CrateFit.Fits(position, rotation, ref ship, out _))
                {
                    return true;
                }
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            ship = null;
            return false;
        }

        /// <summary>True if another crate sits on top of this one (it would be left hanging if this one moved).</summary>
        public static bool HasCrateOnTop(CargoCrate crate)
        {
            Transform t = crate.transform;
            Vector3 top = t.TransformPoint(CrateShape.Center + new Vector3(0f, CrateShape.Size.y * 0.5f, 0f));
            // Starts just inside this crate, so the ray skips it and finds whatever rests on it.
            return Physics.Raycast(top - t.up * 0.05f, t.up, out RaycastHit hit, 0.2f, CrateFit.Mask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<CargoCrate>() != null;
        }
        /// <summary>Sets a crate item down at a pose chosen by <see cref="CrateGhost"/>. Returns false if it couldn't.</summary>
        public static bool Spawn(Player player, Inventory inventory, ItemDrop.ItemData item, Vector3 position, Quaternion rotation, Ship ship)
        {
            if (CrateSetup.CratePrefab == null || !inventory.ContainsItem(item))
            {
                return false;
            }
            GameObject crate = Object.Instantiate(CrateSetup.CratePrefab, position, rotation);
            byte[] contents = CrateItem.ContentsBytes(item);
            if (contents != null)
            {
                crate.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_items, contents);
                crate.GetComponentInChildren<Container>().Load();
            }
            if (ship != null)
            {
                crate.GetComponent<ShipPassenger>().AttachTo(ship);
            }
            inventory.RemoveItem(item);
            player.Message(MessageHud.MessageType.TopLeft, ship != null ? "Crate set down on the deck" : "Crate set down");
            return true;
        }

        /// <summary>The ship a collider belongs to, directly or through a crate riding it.</summary>
        public static Ship ShipOf(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }
            Ship ship = collider.GetComponentInParent<Ship>();
            if (ship != null)
            {
                return ship;
            }
            ShipPassenger passenger = collider.GetComponentInParent<ShipPassenger>();
            return passenger != null ? passenger.CurrentShip : null;
        }
    }
}
