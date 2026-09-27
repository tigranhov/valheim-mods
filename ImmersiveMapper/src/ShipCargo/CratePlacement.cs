using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>Using (or dropping) a crate item sets the crate down in front of the player, on the ground or a deck.</summary>
    internal static class CratePlacement
    {
        private const float Reach = 1.6f;
        private const float ProbeHeight = 2f;
        private const float ProbeDepth = 5f;
        private const float WaterMargin = 0.1f;
        private const float Clearance = 0.02f;

        private static int _mask;

        public static bool TryPlace(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (CrateSetup.CratePrefab == null || !inventory.ContainsItem(item))
            {
                return false;
            }
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            }
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            Vector3 probe = player.transform.position + forward * Reach + Vector3.up * ProbeHeight;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, ProbeDepth, _mask))
            {
                player.Message(MessageHud.MessageType.Center, "No room to put the crate down here");
                return false;
            }
            Ship ship = hit.collider.GetComponentInParent<Ship>();
            if (ship == null)
            {
                // Stacked on a crate that rides a ship: ride that ship too.
                ShipPassenger below = hit.collider.GetComponentInParent<ShipPassenger>();
                ship = below != null ? below.CurrentShip : null;
            }
            if (ship == null && hit.point.y < ZoneSystem.instance.m_waterLevel - WaterMargin)
            {
                player.Message(MessageHud.MessageType.Center, "The crate would sink here");
                return false;
            }

            GameObject crate = Object.Instantiate(CrateSetup.CratePrefab, hit.point, Quaternion.LookRotation(forward, Vector3.up));
            RestOnSurface(crate, hit.point.y);
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

        // The crate's pivot isn't at its bottom (the vanilla crate is made to float), so lift it onto the surface.
        // Only meshes count: effect renderers (like a water splash) have unrelated bounds.
        private static void RestOnSurface(GameObject crate, float surfaceY)
        {
            float bottom = float.MaxValue;
            foreach (MeshRenderer r in crate.GetComponentsInChildren<MeshRenderer>())
            {
                bottom = Mathf.Min(bottom, r.bounds.min.y);
            }
            if (bottom < float.MaxValue)
            {
                crate.transform.position += Vector3.up * (surfaceY - bottom + Clearance);
            }
        }
    }
}
