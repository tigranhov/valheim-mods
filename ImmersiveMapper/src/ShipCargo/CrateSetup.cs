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
    /// Creates the forms of a cargo crate: the placed crate (a copy of the vanilla shipwreck crate, made static and
    /// built with the hammer), the same crate come loose (afloat, or tumbled off a broken stack), and the crate item
    /// it becomes when packed.
    /// </summary>
    internal static class CrateSetup
    {
        public const string CratePrefabName = "IM_CargoCrate";
        public const string FloatingPrefabName = "IM_CargoCrateAfloat";
        public const string CrateItemName = "IM_CargoCrateItem";
        public const string CrateDisplayName = "Cargo crate";
        // The floating crate a broken ship leaves behind.
        public const string VanillaCrate = "CargoCrate";
        private const string ItemBase = "Wood";
        // Lends the crate its build sound, and its icon if rendering one fails.
        private const string ChestPiece = "piece_chest_wood";
        private const string Description = "A sturdy crate for moving house. Shift+E lifts it with everything inside; set it "
            + "down on the ground or on a ship's deck, where it rides along without sliding.";

        public static GameObject CratePrefab { get; private set; }

        /// <summary>The crate come loose: the vanilla floating crate's physics (floats in water, tumbles on land), our crate's contents.</summary>
        public static GameObject FloatingPrefab { get; private set; }

        /// <summary>The crate item's prefab; every crate item's m_dropPrefab points at it (ObjectDB holds this same object).</summary>
        public static GameObject CrateItemPrefab { get; private set; }

        private static CustomPiece _piece;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            CargoConfig.CratesEnabled.SettingChanged += (_, __) => ApplyEnabled();
            SynchronizationManager.OnConfigurationSynchronized += (_, __) => ApplyEnabled();
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            CratePrefab = CreateCratePrefab();
            if (CratePrefab == null)
            {
                return;
            }
            FloatingPrefab = CreateFloatingPrefab();
            Sprite icon = CreateIcon();
            CreatePiece(icon);
            CreateItem(icon);
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
            foreach (Rigidbody body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            MakeCargoCrate(prefab);
            MakeBuildPiece(prefab);
            UseMovingMaterials(prefab);
            prefab.AddComponent<ShipPassenger>();
            prefab.AddComponent<CrateCarry>();
            prefab.AddComponent<CargoCrate>();
            CrateShape.Measure(prefab);
            // Registered as a hammer piece in CreatePiece, which also adds it to the game's prefabs.
            return prefab;
        }

        // Keeps the vanilla crate's floating and its physics sync, so it drifts and bobs like the shipwreck crates.
        private static GameObject CreateFloatingPrefab()
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(FloatingPrefabName, VanillaCrate);
            MakeCargoCrate(prefab);
            Destructible destructible = prefab.GetComponent<Destructible>();
            if (destructible != null)
            {
                // Breakable like the shipwreck crates, but never by itself; its contents spill out when it breaks
                // (Container drops them when destroyed).
                destructible.m_ttl = 0f;
                if (CargoConfig.CrateHealth.Value > 0f)
                {
                    destructible.m_health = CargoConfig.CrateHealth.Value;
                }
            }
            UseMovingMaterials(prefab);
            prefab.AddComponent<CargoCrate>();
            PrefabManager.Instance.AddPrefab(prefab);
            return prefab;
        }

        // Saved with the world, holds a crate's worth of slots, and stays when emptied (it is the crate).
        private static void MakeCargoCrate(GameObject prefab)
        {
            prefab.GetComponent<ZNetView>().m_persistent = true;
            Container container = prefab.GetComponentInChildren<Container>(true);
            container.m_name = CrateDisplayName;
            container.m_width = CargoConfig.CrateWidth.Value;
            container.m_height = CargoConfig.CrateHeight.Value;
            container.m_autoDestroyEmpty = false;
            container.m_privacy = Container.PrivacySetting.Public;
            container.m_defaultItems = new DropTable();
            container.m_destroyedLootPrefab = null;
        }

        /// <summary>
        /// A building piece like a chest: built and repaired with the hammer, dismantled for its materials, and a
        /// broken one drops them. The vanilla crate's breakable part is swapped for the building kind, keeping its
        /// sounds and toughness. Unlike other buildings it needs no support, doesn't rot in the rain, and ash and
        /// lava don't hurt it (riding a ship it's part of the ship, see CrateDamagePatch). Nothing can be built on it.
        /// </summary>
        private static void MakeBuildPiece(GameObject prefab)
        {
            Destructible destructible = prefab.GetComponent<Destructible>();
            var wear = prefab.AddComponent<WearNTear>();
            wear.m_health = CargoConfig.CrateHealth.Value > 0f ? CargoConfig.CrateHealth.Value : destructible != null ? destructible.m_health : 100f;
            wear.m_materialType = WearNTear.MaterialType.Wood;
            wear.m_supports = false;
            wear.m_noRoofWear = false;
            wear.m_noSupportWear = false;
            wear.m_ashDamageImmune = true;
            wear.m_triggerPrivateArea = true;
            wear.m_autoCreateFragments = false;
            if (destructible != null)
            {
                Plugin.Log.LogInfo($"{VanillaCrate} health {destructible.m_health}; cargo crates have {wear.m_health}");
                wear.m_damages = destructible.m_damages;
                wear.m_hitEffect = destructible.m_hitEffect;
                wear.m_destroyedEffect = destructible.m_destroyedEffect;
                wear.m_hitNoise = destructible.m_hitNoise;
                wear.m_destroyNoise = destructible.m_destroyNoise;
                // Both would answer the same damage call.
                Object.DestroyImmediate(destructible);
            }
            Piece piece = prefab.AddComponent<Piece>();
            GameObject chest = PrefabManager.Cache.GetPrefab<GameObject>(ChestPiece);
            Piece chestPiece = chest != null ? chest.GetComponent<Piece>() : null;
            if (chestPiece != null)
            {
                piece.m_placeEffect = chestPiece.m_placeEffect;
            }
        }

        private static void CreatePiece(Sprite icon)
        {
            var config = new PieceConfig
            {
                Name = CrateDisplayName,
                Description = Description,
                PieceTable = "Hammer",
                Category = "Misc",
                CraftingStation = CargoConfig.CrateStation.Value,
                Requirements = ParseRecipe(CargoConfig.CrateRecipe.Value),
            };
            if (icon != null)
            {
                config.Icon = icon;
            }
            _piece = new CustomPiece(CratePrefab, false, config);
            PieceManager.Instance.AddPiece(_piece);
            ApplyEnabled();
        }

        // Only what a crate becomes when packed (PickUpMode Inventory): it has no recipe, crates are built.
        private static void CreateItem(Sprite icon)
        {
            var config = new ItemConfig
            {
                Name = CrateDisplayName,
                Description = Description,
                Weight = CargoConfig.CrateWeight.Value,
            };
            if (icon != null)
            {
                config.Icon = icon;
            }
            var item = new CustomItem(CrateItemName, ItemBase, config);
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_maxStackSize = 1;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_teleportable = true;
            CrateItemPrefab = item.ItemPrefab;
            ItemManager.Instance.AddItem(item);
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
            GameObject chest = PrefabManager.Cache.GetPrefab<GameObject>(ChestPiece);
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

        // Turning crates off only takes them out of the hammer's menu; crates already built keep working.
        private static void ApplyEnabled()
        {
            if (_piece?.Piece != null)
            {
                _piece.Piece.m_enabled = CargoConfig.CratesEnabled.Value;
            }
        }
    }
}
