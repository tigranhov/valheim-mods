using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Creates the map case item: a copy of the hammer (a tool held in the right hand) with its build menu removed and
    /// its model swapped for a leather map tube built from Unity's cylinder and the leather scraps' material.
    /// </summary>
    internal static class MapCaseSetup
    {
        public const string ItemName = "IM_MapCase";
        public const string DisplayName = "Map case";
        public const string VisualName = "IM_MapCaseVisual";
        private const string ItemBase = "Hammer";

        private static CustomItem _item;

        public static GameObject Prefab => _item?.ItemPrefab;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            var config = new ItemConfig
            {
                Name = DisplayName,
                Description = "A leather case of parchment sheets and charcoal, with a tally cord that counts your paces while you carry it. "
                    + "Take it out, then left-click for the small map or right-click to stop and draw.",
                CraftingStation = KitConfig.CaseStation.Value,
                Requirements = Models.Recipe(KitConfig.CaseRecipe.Value),
                Weight = 1f,
            };
            _item = new CustomItem(ItemName, ItemBase, config);
            GameObject prefab = _item.ItemPrefab;
            if (prefab == null)
            {
                Plugin.Log.LogError($"Vanilla item {ItemBase} not found; the map case is disabled.");
                return;
            }

            ItemDrop.ItemData.SharedData shared = _item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
            shared.m_buildPieces = null;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = 1;
            shared.m_useDurability = false;
            shared.m_canBeReparied = false;
            shared.m_teleportable = true;

            Plugin.Log.LogInfo($"{ItemBase} children: {string.Join(", ", prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name))}");
            Material leather = Models.MaterialOf("LeatherScraps", new Color(0.45f, 0.3f, 0.18f));
            Material caps = Models.Darker(leather, 0.55f);
            // Lying on its side when dropped.
            Models.ReplaceLook(prefab, VisualName, root => BuildTube(root, leather, caps), Quaternion.Euler(0f, 0f, 90f), new Vector3(0f, 0.04f, 0f));
            Sprite icon = Models.Icon(prefab, "map case");
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            ItemManager.Instance.AddItem(_item);
        }

        /// <summary>A leather tube about 32 cm long with darker end caps. Its pivot is its middle.</summary>
        private static void BuildTube(Transform root, Material leather, Material caps)
        {
            Models.Part(root, "body", leather, Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f), Quaternion.identity);
            Models.Part(root, "cap_top", caps, new Vector3(0f, 0.155f, 0f), new Vector3(0.078f, 0.012f, 0.078f), Quaternion.identity);
            Models.Part(root, "cap_bottom", caps, new Vector3(0f, -0.155f, 0f), new Vector3(0.078f, 0.012f, 0.078f), Quaternion.identity);
        }
    }
}
