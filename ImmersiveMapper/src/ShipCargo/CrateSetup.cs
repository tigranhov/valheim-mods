using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Creates the two forms of a cargo crate: the placed crate (a copy of the vanilla shipwreck crate, made static)
    /// and the crate item it becomes when picked up.
    /// </summary>
    internal static class CrateSetup
    {
        public const string CratePrefabName = "IM_CargoCrate";
        public const string CrateItemName = "IM_CargoCrateItem";
        public const string CrateDisplayName = "Cargo crate";
        // The floating crate a broken ship leaves behind.
        public const string VanillaCrate = "CargoCrate";
        private const string ItemBase = "Wood";
        private const string FallbackIconPiece = "piece_chest_wood";

        public static GameObject CratePrefab { get; private set; }

        private static CustomItem _item;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            CargoConfig.CratesEnabled.SettingChanged += (_, __) => ApplyRecipeEnabled();
            SynchronizationManager.OnConfigurationSynchronized += (_, __) => ApplyRecipeEnabled();
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            CratePrefab = CreateCratePrefab();
            if (CratePrefab != null)
            {
                CreateItem();
            }
        }

        private static GameObject CreateCratePrefab()
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(CratePrefabName, VanillaCrate);
            if (prefab == null)
            {
                Plugin.Log.LogError($"Vanilla prefab {VanillaCrate} not found; cargo crates are disabled.");
                return null;
            }
            Plugin.Log.LogInfo($"{VanillaCrate} components: {string.Join(", ", prefab.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct())}");
            Plugin.Log.LogInfo($"{VanillaCrate} collider layers: {string.Join(", ", prefab.GetComponentsInChildren<Collider>(true).Select(c => $"{c.name}={LayerMask.LayerToName(c.gameObject.layer)}{(c.isTrigger ? " (trigger)" : "")}"))}");

            // The vanilla crate bobs in water. Ours stays exactly where it's put: no floating, no physics sync.
            foreach (Floating floating in prefab.GetComponentsInChildren<Floating>(true))
            {
                Object.DestroyImmediate(floating);
            }
            foreach (ZSyncTransform sync in prefab.GetComponentsInChildren<ZSyncTransform>(true))
            {
                Object.DestroyImmediate(sync);
            }
            // Unbreakable: a stray hit shouldn't burst open a crate holding half a base.
            foreach (Destructible destructible in prefab.GetComponentsInChildren<Destructible>(true))
            {
                Object.DestroyImmediate(destructible);
            }
            foreach (Rigidbody body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            ZNetView nview = prefab.GetComponent<ZNetView>();
            nview.m_persistent = true;

            Container container = prefab.GetComponentInChildren<Container>(true);
            container.m_name = CrateDisplayName;
            container.m_width = CargoConfig.CrateWidth.Value;
            container.m_height = CargoConfig.CrateHeight.Value;
            container.m_autoDestroyEmpty = false;
            container.m_privacy = Container.PrivacySetting.Public;
            container.m_defaultItems = new DropTable();
            container.m_destroyedLootPrefab = null;

            UseMovingMaterials(prefab);
            prefab.AddComponent<ShipPassenger>();
            prefab.AddComponent<CrateCarry>();
            prefab.AddComponent<CargoCrate>();
            CrateShape.Measure(prefab);
            PrefabManager.Instance.AddPrefab(prefab);
            return prefab;
        }

        // The vanilla material shades with world-position noise and world-space texture projection, which shimmer on
        // anything that moves (like a crate riding a ship). The game's build ghosts turn both off the same way
        // (Player.CleanupGhostMaterials). Copies, so the vanilla floating crate keeps its own material.
        private static void UseMovingMaterials(GameObject prefab)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                    {
                        continue;
                    }
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + " (IM moving)" };
                        if (copy.HasProperty("_ValueNoise"))
                        {
                            copy.SetFloat("_ValueNoise", 0f);
                        }
                        if (copy.HasProperty("_TriplanarLocalPos"))
                        {
                            copy.SetFloat("_TriplanarLocalPos", 1f);
                        }
                        copies[source] = copy;
                        Plugin.Log.LogInfo($"Crate material {source.name} / {source.shader.name}: value noise {(source.HasProperty("_ValueNoise") ? "off" : "n/a")}, local triplanar {(source.HasProperty("_TriplanarLocalPos") ? "on" : "n/a")}");
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static void CreateItem()
        {
            var config = new ItemConfig
            {
                Name = CrateDisplayName,
                Description = "A sturdy crate for moving house. Use it to set it down on the ground or on a ship's deck, "
                    + "where it rides along without sliding. Pick it up again with everything inside.",
                CraftingStation = CargoConfig.CrateStation.Value,
                Requirements = ParseRecipe(CargoConfig.CrateRecipe.Value),
                Weight = CargoConfig.CrateWeight.Value,
            };
            Sprite icon = CreateIcon();
            if (icon != null)
            {
                config.Icon = icon;
            }
            _item = new CustomItem(CrateItemName, ItemBase, config);
            ItemDrop.ItemData.SharedData shared = _item.ItemDrop.m_itemData.m_shared;
            shared.m_maxStackSize = 1;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_teleportable = true;
            ItemManager.Instance.AddItem(_item);
            ApplyRecipeEnabled();
        }

        private static Sprite CreateIcon()
        {
            try
            {
                Sprite rendered = RenderManager.Instance.Render(CratePrefab, RenderManager.IsometricRotation);
                if (rendered != null)
                {
                    return rendered;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't render the crate icon: {e.Message}");
            }
            GameObject chest = PrefabManager.Cache.GetPrefab<GameObject>(FallbackIconPiece);
            Piece piece = chest != null ? chest.GetComponent<Piece>() : null;
            return piece != null ? piece.m_icon : null;
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

        private static void ApplyRecipeEnabled()
        {
            Recipe recipe = _item?.Recipe?.Recipe;
            if (recipe != null)
            {
                recipe.m_enabled = CargoConfig.CratesEnabled.Value;
            }
        }
    }
}
