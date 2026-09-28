using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Cartographer
{
    /// <summary>
    /// Parts that add a draft sheet to a map case: craft one, then use it from the inventory to fit it into your case
    /// (the part is used up). Each needs the next biome's materials: a troll-hide sheet (Black Forest) and a vellum sheet
    /// sized with guck (Swamp). A case takes each part once.
    /// </summary>
    internal static class CaseUpgrades
    {
        public sealed class Part
        {
            public readonly string ItemName;
            public readonly string DisplayName;
            /// <summary>Saved in the case's list of fitted parts.</summary>
            public readonly string Key;
            public readonly string Hide;
            public readonly string Description;
            public ConfigEntry<string> Recipe;
            public ConfigEntry<string> Station;
            public CustomItem Item;

            public Part(string itemName, string displayName, string key, string hide, string description)
            {
                ItemName = itemName;
                DisplayName = displayName;
                Key = key;
                Hide = hide;
                Description = description;
            }

            public bool Is(ItemDrop.ItemData item)
            {
                if (item == null || item.m_shared.m_name != DisplayName)
                {
                    return false;
                }
                return item.m_dropPrefab == null || item.m_dropPrefab.name == ItemName;
            }
        }

        private const string VisualName = "IM_CaseSheetVisual";

        public static readonly Part[] Parts =
        {
            new Part("IM_CaseSheetTroll", "Troll-hide sheet", "sheet_troll", "TrollHide",
                "A sheet of scraped troll hide, rolled. Use it to fit it into your map case: one more draft sheet."),
            new Part("IM_CaseSheetVellum", "Vellum sheet", "sheet_vellum", "DeerHide",
                "Fine deer vellum sized with guck, rolled. Use it to fit it into your map case: one more draft sheet."),
        };

        public static void Bind(ConfigFile cfg)
        {
            const string section = "1 - Map case";
            Parts[0].Recipe = cfg.Bind(section, "TrollSheetRecipe", "TrollHide:1,Resin:2",
                Synced("Troll-hide sheet (4th draft sheet), Black Forest. Item:Amount pairs. Applies after a restart."));
            Parts[0].Station = cfg.Bind(section, "TrollSheetStation", "piece_workbench", Synced("Where the troll-hide sheet is crafted."));
            Parts[1].Recipe = cfg.Bind(section, "VellumSheetRecipe", "DeerHide:2,Guck:3",
                Synced("Vellum sheet (5th draft sheet), Swamp. Item:Amount pairs. Applies after a restart."));
            Parts[1].Station = cfg.Bind(section, "VellumSheetStation", "piece_workbench", Synced("Where the vellum sheet is crafted."));
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        public static Part PartOf(ItemDrop.ItemData item)
        {
            foreach (Part part in Parts)
            {
                if (part.Is(item))
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>Using a part: fits it into your map case (the one in hand, else the first one you carry).</summary>
        public static bool TryFit(Player player, Inventory inventory, ItemDrop.ItemData item, Part part)
        {
            ItemDrop.ItemData mapCase = MapCaseItem.Active(player);
            if (mapCase == null)
            {
                player.Message(MessageHud.MessageType.Center, "You need a map case to fit this into");
                return true;
            }
            if (MapCaseItem.HasUpgrade(mapCase, part.Key))
            {
                player.Message(MessageHud.MessageType.Center, "Your map case already has this sheet");
                return true;
            }
            MapCaseItem.AddUpgrade(mapCase, part.Key);
            inventory.RemoveOneItem(item);
            player.Message(MessageHud.MessageType.Center, $"Your map case now holds {MapCaseItem.SheetCount(mapCase)} sheets");
            CaseInHand.Reload();
            return true;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            Material tie = Models.Darker(Models.MaterialOf("LeatherScraps", new Color(0.45f, 0.3f, 0.18f)), 0.6f);
            foreach (Part part in Parts)
            {
                var config = new ItemConfig
                {
                    Name = part.DisplayName,
                    Description = part.Description,
                    CraftingStation = part.Station.Value,
                    Requirements = Models.Recipe(part.Recipe.Value),
                    Weight = 0.5f,
                };
                part.Item = new CustomItem(part.ItemName, "LeatherScraps", config);
                GameObject prefab = part.Item.ItemPrefab;
                if (prefab == null)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = part.Item.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_maxStackSize = 5;
                shared.m_teleportable = true;
                Material hide = Models.MaterialOf(part.Hide, new Color(0.8f, 0.72f, 0.58f));
                Models.ReplaceLook(prefab, VisualName, root => BuildScroll(root, hide, tie), Quaternion.Euler(0f, 0f, 90f), new Vector3(0f, 0.03f, 0f));
                Sprite icon = Models.Icon(prefab, part.DisplayName);
                if (icon != null)
                {
                    shared.m_icons = new[] { icon };
                }
                ItemManager.Instance.AddItem(part.Item);
            }
        }

        /// <summary>A rolled sheet about 26 cm long, tied in the middle.</summary>
        private static void BuildScroll(Transform root, Material hide, Material tie)
        {
            Models.Part(root, "roll", hide, Vector3.zero, new Vector3(0.05f, 0.13f, 0.05f), Quaternion.identity);
            Models.Part(root, "tie", tie, Vector3.zero, new Vector3(0.056f, 0.008f, 0.056f), Quaternion.identity);
        }

        private static ConfigDescription Synced(string text)
        {
            return new ConfigDescription(text, null, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
