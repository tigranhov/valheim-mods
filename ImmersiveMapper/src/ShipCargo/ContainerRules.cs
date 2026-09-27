using System.Runtime.CompilerServices;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Packed crates may only be carried: never put into chests, carts, ship holds or other crates.
    /// Tombstones are the exception, so dying with a crate doesn't destroy it.
    /// </summary>
    internal static class ContainerRules
    {
        private const string BlockedMessage = "A packed crate can't go in there";
        private static readonly ConditionalWeakTable<Inventory, object> NoCrates = new ConditionalWeakTable<Inventory, object>();
        private static readonly object Marker = new object();
        private static float _lastMessage;

        public static void Register(Container container)
        {
            Inventory inventory = container.GetInventory();
            if (inventory == null || container.GetComponentInParent<TombStone>() != null || NoCrates.TryGetValue(inventory, out _))
            {
                return;
            }
            NoCrates.Add(inventory, Marker);
        }

        public static bool Blocks(Inventory inventory, ItemDrop.ItemData item)
        {
            if (!CrateItem.IsPacked(item) || !NoCrates.TryGetValue(inventory, out _))
            {
                return false;
            }
            if (Player.m_localPlayer != null && Time.time - _lastMessage > 1f)
            {
                _lastMessage = Time.time;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, BlockedMessage);
            }
            return true;
        }
    }
}
