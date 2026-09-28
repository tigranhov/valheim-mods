using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cartographer
{
    /// <summary>
    /// The colours a cartography table knows. Lampblack comes with every table; each other colour is learned once per
    /// table by bringing something from its biome (you can only paint a place once you've been there). Saved on the
    /// table, so the group's table learns them for everyone, and an outpost's table has to be taught again.
    /// </summary>
    internal static class Pigments
    {
        public const string DefaultCosts =
            "Red ochre=Raspberry:5, Meadows=Dandelion:5, Woad=Blueberries:5, Black Forest=PineCone:3, Verdigris=Copper:2, "
            + "Swamp=Guck:3, Ocean=Chitin:2, Mountain=FreezeGland:2, Deep North=Crystal:1, Plains=Cloudberry:5, "
            + "Mistlands=MushroomJotunPuffs:3, Ashlands=CharredBone:2";

        private const string LearnedKey = "IM_Map_Pigments";

        public readonly struct Cost
        {
            public readonly string Item;
            public readonly int Amount;

            public Cost(string item, int amount)
            {
                Item = item;
                Amount = amount;
            }
        }

        private static Dictionary<byte, Cost> _costs;
        private static string _source;

        public static int Learned(MapTable table)
        {
            return table.m_nview.GetZDO().GetInt(LearnedKey);
        }

        public static bool Known(int learned, byte color)
        {
            return !KitConfig.PigmentsEnabled.Value || color == Inks.Black || !TryGetCost(color, out _) || (learned & (1 << color)) != 0;
        }

        /// <summary>
        /// Teaches the table a colour if the player carries its material (used up). Returns the table's colours after,
        /// and says in the middle of the screen what happened.
        /// </summary>
        public static int Teach(MapTable table, Player player, byte color, int learned)
        {
            if (Known(learned, color) || !TryGetCost(color, out Cost cost))
            {
                return learned;
            }
            string swatch = Inks.All[color].Name;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(cost.Item) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                player.Message(MessageHud.MessageType.Center, $"{swatch}: unknown material \"{cost.Item}\" in the config");
                return learned;
            }
            string token = drop.m_itemData.m_shared.m_name;
            string material = Localization.instance.Localize(token);
            Inventory inventory = player.GetInventory();
            int carried = inventory.CountItems(token);
            if (carried < cost.Amount)
            {
                player.Message(MessageHud.MessageType.Center, $"{swatch} is made with {cost.Amount} {material} (you carry {carried})");
                return learned;
            }
            inventory.RemoveItem(token, cost.Amount);
            learned |= 1 << color;
            ZNetView nview = table.m_nview;
            if (!nview.IsOwner())
            {
                nview.ClaimOwnership();
            }
            nview.GetZDO().Set(LearnedKey, learned);
            player.Message(MessageHud.MessageType.Center, $"The table learned {swatch} ({cost.Amount} {material} used)");
            return learned;
        }

        private static bool TryGetCost(byte color, out Cost cost)
        {
            string source = KitConfig.PigmentCosts.Value ?? "";
            if (_costs == null || source != _source)
            {
                _source = source;
                _costs = Parse(source);
            }
            return _costs.TryGetValue(color, out cost);
        }

        // "Swatch name=Item:Amount" pairs separated by commas.
        private static Dictionary<byte, Cost> Parse(string source)
        {
            var costs = new Dictionary<byte, Cost>();
            foreach (string entry in source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] sides = entry.Split('=');
                if (sides.Length != 2)
                {
                    continue;
                }
                string name = sides[0].Trim();
                string[] pair = sides[1].Split(':');
                int amount = pair.Length > 1 && int.TryParse(pair[1].Trim(), out int parsed) ? Mathf.Max(1, parsed) : 1;
                for (byte i = 0; i < Inks.All.Length; i++)
                {
                    if (string.Equals(Inks.All[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        costs[i] = new Cost(pair[0].Trim(), amount);
                        break;
                    }
                }
            }
            return costs;
        }
    }
}
