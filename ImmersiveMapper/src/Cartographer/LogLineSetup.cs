using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Creates the log line item (a reel of knotted line) and the reel model that sits on a ship's stern once fitted.
    /// Built from Unity's cylinder with the game's wood and leather materials.
    /// </summary>
    internal static class LogLineSetup
    {
        public const string ItemName = "IM_LogLine";
        public const string DisplayName = "Log line";
        private const string VisualName = "IM_LogLineVisual";
        private const string ItemBase = "LeatherScraps";

        private static CustomItem _item;
        private static Material _wood;
        private static Material _line;

        public static GameObject Prefab => _item?.ItemPrefab;

        /// <summary>The line's own material, for the rope trailing behind a ship.</summary>
        public static Material LineMaterial => _line;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        public static bool IsLogLine(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared.m_name != DisplayName)
            {
                return false;
            }
            return item.m_dropPrefab == null || ReferenceEquals(item.m_dropPrefab, Prefab) || item.m_dropPrefab.name == ItemName;
        }

        /// <summary>A reel about 30 cm across, lying along its X axis; its pivot is its middle.</summary>
        public static void BuildReel(Transform root)
        {
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            Models.Part(root, "axle", _wood, Vector3.zero, new Vector3(0.035f, 0.13f, 0.035f), alongX);
            Models.Part(root, "line", _line, Vector3.zero, new Vector3(0.14f, 0.07f, 0.14f), alongX);
            Models.Part(root, "flange_left", _wood, new Vector3(-0.08f, 0f, 0f), new Vector3(0.24f, 0.012f, 0.24f), alongX);
            Models.Part(root, "flange_right", _wood, new Vector3(0.08f, 0f, 0f), new Vector3(0.24f, 0.012f, 0.24f), alongX);
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            _wood = Models.MaterialOf("Wood", new Color(0.5f, 0.36f, 0.22f));
            _line = Models.Darker(Models.MaterialOf("LeatherScraps", new Color(0.45f, 0.3f, 0.18f)), 0.8f);
            var config = new ItemConfig
            {
                Name = DisplayName,
                Description = "A reel of knotted line with a wooden float, to count how far a ship sails. Use it standing on a ship's "
                    + "deck to fit it at the stern. Read it at the reel; with your map case out, End leg notes the run as a sea leg.",
                CraftingStation = KitConfig.LogLineStation.Value,
                Requirements = Models.Recipe(KitConfig.LogLineRecipe.Value),
                Weight = 2f,
            };
            _item = new CustomItem(ItemName, ItemBase, config);
            GameObject prefab = _item.ItemPrefab;
            if (prefab == null)
            {
                Plugin.Log.LogError($"Vanilla item {ItemBase} not found; the log line is disabled.");
                return;
            }
            ItemDrop.ItemData.SharedData shared = _item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_maxStackSize = 1;
            shared.m_teleportable = true;

            Models.ReplaceLook(prefab, VisualName, BuildReel, Quaternion.identity, new Vector3(0f, 0.12f, 0f));
            Sprite icon = Models.Icon(prefab, "log line");
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            ItemManager.Instance.AddItem(_item);
        }
    }
}
