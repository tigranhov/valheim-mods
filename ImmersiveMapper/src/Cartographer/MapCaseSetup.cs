using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

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
        private const string LeatherItem = "LeatherScraps";

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
                    + "Take it out to read your sketches on the move; right-click to stop and draw.",
                CraftingStation = KitConfig.CaseStation.Value,
                Requirements = ParseRecipe(KitConfig.CaseRecipe.Value),
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

            ReplaceVisuals(prefab);
            Sprite icon = CreateIcon(prefab);
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            ItemManager.Instance.AddItem(_item);
        }

        private static void ReplaceVisuals(GameObject prefab)
        {
            Plugin.Log.LogInfo($"{ItemBase} children: {string.Join(", ", prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name))}");
            foreach (LODGroup lod in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(lod);
            }
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Object.DestroyImmediate(renderer);
                    if (filter != null)
                    {
                        Object.DestroyImmediate(filter);
                    }
                }
            }

            Material leather = FindLeather();
            // Lying on its side when dropped.
            Transform dropped = AddTube(prefab.transform, leather);
            dropped.localRotation = Quaternion.Euler(0f, 0f, 90f);
            dropped.localPosition = new Vector3(0f, 0.04f, 0f);

            Transform attach = prefab.transform.Find("attach");
            if (attach == null)
            {
                attach = new GameObject("attach").transform;
                attach.SetParent(prefab.transform, false);
            }
            // Only the copy made for the hand is shown (VisEquipment activates it), never the one inside the item.
            attach.gameObject.SetActive(false);
            AddTube(attach, leather);
        }

        /// <summary>A leather tube about 32 cm long with darker end caps. Its pivot is its middle.</summary>
        private static Transform AddTube(Transform parent, Material leather)
        {
            Mesh cylinder = CylinderMesh();
            var root = new GameObject(VisualName).transform;
            root.gameObject.layer = parent.gameObject.layer;
            root.SetParent(parent, false);

            Material caps = new Material(leather) { name = leather.name + " (IM caps)" };
            if (caps.HasProperty("_Color"))
            {
                caps.color = caps.color * 0.55f;
            }
            AddPart(root, "body", cylinder, leather, Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f));
            AddPart(root, "cap_top", cylinder, caps, new Vector3(0f, 0.155f, 0f), new Vector3(0.078f, 0.012f, 0.078f));
            AddPart(root, "cap_bottom", cylinder, caps, new Vector3(0f, -0.155f, 0f), new Vector3(0.078f, 0.012f, 0.078f));
            return root;
        }

        private static void AddPart(Transform root, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var part = new GameObject(name);
            part.layer = root.gameObject.layer;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh _cylinder;

        private static Mesh CylinderMesh()
        {
            if (_cylinder == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _cylinder = primitive.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(primitive);
            }
            return _cylinder;
        }

        private static Material FindLeather()
        {
            GameObject scraps = PrefabManager.Cache.GetPrefab<GameObject>(LeatherItem);
            Renderer renderer = scraps != null ? scraps.GetComponentInChildren<MeshRenderer>(true) : null;
            if (renderer != null && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial;
            }
            Plugin.Log.LogWarning($"No material on {LeatherItem}; the map case uses a plain one.");
            return new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.3f, 0.18f) };
        }

        private static Sprite CreateIcon(GameObject prefab)
        {
            try
            {
                return RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 30f, 45f));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't render the map case icon: {e.Message}");
                return null;
            }
        }

        private static RequirementConfig[] ParseRecipe(string recipe)
        {
            var requirements = new List<RequirementConfig>();
            foreach (string part in (recipe ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split(':');
                string item = pair[0].Trim();
                if (item.Length == 0)
                {
                    continue;
                }
                int amount = pair.Length > 1 && int.TryParse(pair[1].Trim(), out int parsed) ? parsed : 1;
                requirements.Add(new RequirementConfig(item, amount, 0, true));
            }
            return requirements.ToArray();
        }
    }
}
