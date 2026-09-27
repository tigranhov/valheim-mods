using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    internal static class CratePlacement
    {
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
