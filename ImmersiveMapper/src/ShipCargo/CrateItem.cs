using System;
using System.Globalization;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// A picked-up crate is one inventory item. Its contents travel in the item's custom data as the exact bytes the
    /// placed crate stored (Container saves its inventory to ZDOVars.s_items), so nothing is re-interpreted on the way.
    /// </summary>
    internal static class CrateItem
    {
        private const string ItemsKey = "IM_Crate_Items";
        private const string WeightKey = "IM_Crate_Weight";
        private const string CountKey = "IM_Crate_Count";
        private const string NoTeleportKey = "IM_Crate_NoTeleport";

        public static bool IsCrate(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }
            // Not a shared-data reference check: an item spawned in the world carries its own copy of the shared
            // data (ItemDrop.Awake only relinks it in the editor), and keeps it when picked up.
            if (item.m_dropPrefab != null)
            {
                return item.m_dropPrefab.name == CrateSetup.CrateItemName;
            }
            return item.m_shared.m_name == CrateSetup.CrateDisplayName;
        }

        /// <summary>A crate item with something inside. Empty crate items are allowed everywhere.</summary>
        public static bool IsPacked(ItemDrop.ItemData item)
        {
            return IsCrate(item) && item.m_customData.ContainsKey(ItemsKey);
        }

        public static float ContentsWeight(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(WeightKey, out string value)
                && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight) ? weight : 0f;
        }

        public static int ContentsCount(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(CountKey, out string value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : 0;
        }

        public static bool BlocksTeleport(ItemDrop.ItemData item)
        {
            return IsPacked(item) && item.m_customData.ContainsKey(NoTeleportKey);
        }

        public static byte[] ContentsBytes(ItemDrop.ItemData item)
        {
            return IsPacked(item) ? Convert.FromBase64String(item.m_customData[ItemsKey]) : null;
        }

        /// <param name="contents">The placed crate's saved inventory bytes, or null for an empty crate.</param>
        /// <param name="inventory">The same contents loaded, to summarize weight and count.</param>
        public static ItemDrop.ItemData Create(byte[] contents, Inventory inventory)
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab(CrateSetup.CrateItemName);
            ItemDrop.ItemData item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            item.m_worldLevel = (byte)Game.m_worldLevel;
            if (contents != null && inventory != null && inventory.NrOfItems() > 0)
            {
                item.m_customData[ItemsKey] = Convert.ToBase64String(contents);
                item.m_customData[WeightKey] = inventory.GetTotalWeight().ToString(CultureInfo.InvariantCulture);
                item.m_customData[CountKey] = inventory.NrOfItems().ToString(CultureInfo.InvariantCulture);
                if (!inventory.IsTeleportable(false))
                {
                    item.m_customData[NoTeleportKey] = "1";
                }
            }
            return item;
        }
    }
}
