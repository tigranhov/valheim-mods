using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// When crates are carried (not packed into the inventory), a freshly crafted crate appears at the crafting
    /// station: side by side on its top, or on the ground beside it on the crafter's side if the top is taken,
    /// with at most <see cref="CargoConfig.CratesAtStation"/> crates waiting there.
    /// </summary>
    internal static class CrateCrafting
    {
        private const float Gap = 0.04f;
        private const float GroundGap = 0.15f;
        private const float ProbeAbove = 1f;
        private const float ProbeDepth = 4f;
        private const float WaitingRadius = 1.5f;
        private const float RoomCacheSeconds = 0.25f;
        private static readonly float[] Slots = { 0f, -1f, 1f };

        // Each station type's box in its own local space, measured once from its colliders.
        private static readonly Dictionary<string, Bounds> StationBounds = new Dictionary<string, Bounds>();
        private static CraftingStation _roomStation;
        private static float _roomUntil;
        private static int _room;

        public static bool AppearsAtStation => CargoConfig.PickUpMode.Value != CarryMode.Inventory;

        // Asked every frame while a recipe is shown: the item's name field, not Object.name (which allocates).
        public static bool IsCrateRecipe(Recipe recipe)
        {
            return recipe != null && recipe.m_item != null && recipe.m_item.m_itemData.m_shared.m_name == CrateSetup.CrateDisplayName;
        }

        /// <summary>A free spot for a new crate at the station, or why there is none.</summary>
        public static bool FindSpot(CraftingStation station, Player player, out Vector3 position, out Quaternion rotation, out Ship ship, out string reason)
        {
            Bounds bounds = BoundsOf(station);
            if (CountWaiting(station.transform, bounds) >= CargoConfig.CratesAtStation.Value)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                ship = null;
                reason = "Carry the crates at the workbench away first";
                return false;
            }
            if (CountSpots(station, player, bounds, 1, out position, out rotation, out ship) > 0)
            {
                reason = null;
                return true;
            }
            reason = "No room for a crate at the workbench";
            return false;
        }

        /// <summary>
        /// How many new crates there's room for at the station, within <see cref="CargoConfig.CratesAtStation"/>.
        /// Cached briefly unless <paramref name="fresh"/>: the recipe panel asks every frame.
        /// </summary>
        public static int RoomFor(CraftingStation station, Player player, bool fresh = false)
        {
            if (!fresh && station == _roomStation && Time.time < _roomUntil)
            {
                return _room;
            }
            Bounds bounds = BoundsOf(station);
            int max = CargoConfig.CratesAtStation.Value - CountWaiting(station.transform, bounds);
            _room = max > 0 ? CountSpots(station, player, bounds, max, out _, out _, out _) : 0;
            _roomStation = station;
            _roomUntil = Time.time + RoomCacheSeconds;
            return _room;
        }

        // Free spots for a new crate, up to max, and where the first one is. The middle first, then either side: on
        // top of the station, then on the ground on the crafter's side. The spots are a crate's width apart, so each
        // one that's free now is still free once crates fill the others.
        private static int CountSpots(CraftingStation station, Player player, Bounds bounds, int max, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            ship = null;
            Transform t = station.transform;
            // Rows run along the station's long side; crates face across it.
            bool alongX = bounds.size.x >= bounds.size.z;
            Vector3 rowAxis = alongX ? Vector3.right : Vector3.forward;
            Vector3 sideAxis = alongX ? Vector3.forward : Vector3.right;
            float yaw = t.eulerAngles.y + (alongX ? 0f : 90f);
            float spacing = CrateShape.Size.x + Gap;
            float sideSign = Vector3.Dot(player.transform.position - t.position, t.TransformDirection(sideAxis)) >= 0f ? 1f : -1f;
            float sideReach = Vector3.Scale(bounds.extents, sideAxis).magnitude + CrateShape.Size.z * 0.5f + GroundGap;
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y + ProbeAbove, bounds.center.z);
            Vector3 beside = top + sideAxis * (sideSign * sideReach);
            int count = 0;
            for (int row = 0; row < 2; row++)
            {
                Vector3 rowCenter = row == 0 ? top : beside;
                foreach (float slot in Slots)
                {
                    Vector3 probe = t.TransformPoint(rowCenter + rowAxis * (slot * spacing));
                    if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, ProbeAbove + ProbeDepth + bounds.size.y, CrateFit.Mask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    CrateFit.RestOn(hit, yaw, out Vector3 spot, out Quaternion turn, out Ship on);
                    if (!CrateFit.Fits(spot, turn, ref on, out _))
                    {
                        continue;
                    }
                    if (count == 0)
                    {
                        position = spot;
                        rotation = turn;
                        ship = on;
                    }
                    if (++count >= max)
                    {
                        return count;
                    }
                }
            }
            return count;
        }

        private static int CountWaiting(Transform station, Bounds bounds)
        {
            float radius = bounds.extents.magnitude + WaitingRadius;
            int count = 0;
            foreach (CargoCrate crate in CargoCrate.All)
            {
                if (!crate.IsCarried && Vector3.Distance(crate.transform.position, station.position) < radius)
                {
                    count++;
                }
            }
            return count;
        }

        private static Bounds BoundsOf(CraftingStation station)
        {
            string type = Utils.GetPrefabName(station.gameObject);
            if (StationBounds.TryGetValue(type, out Bounds cached))
            {
                return cached;
            }
            Transform root = station.transform;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            bool any = false;
            foreach (Collider c in station.GetComponentsInChildren<Collider>())
            {
                // Each collider's own local box, not its world-aligned bounds, so a station placed at an angle measures the same.
                if (c.isTrigger || !LocalBox(c, out Bounds box))
                {
                    continue;
                }
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
                    Vector3 local = root.InverseTransformPoint(c.transform.TransformPoint(corner));
                    if (any)
                    {
                        bounds.Encapsulate(local);
                    }
                    else
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                }
            }
            Plugin.Log.LogInfo($"{type} box for crates: size {bounds.size}, center {bounds.center}");
            StationBounds[type] = bounds;
            return bounds;
        }

        private static bool LocalBox(Collider collider, out Bounds box)
        {
            switch (collider)
            {
                case BoxCollider b:
                    box = new Bounds(b.center, b.size);
                    return true;
                case MeshCollider m when m.sharedMesh != null:
                    box = m.sharedMesh.bounds;
                    return true;
                case SphereCollider s:
                    box = new Bounds(s.center, Vector3.one * (s.radius * 2f));
                    return true;
                case CapsuleCollider cap:
                    box = new Bounds(cap.center, Vector3.one * Mathf.Max(cap.radius * 2f, cap.height));
                    return true;
                default:
                    box = default;
                    return false;
            }
        }
    }

    // Crafting a crate while crates are carried: refuse before anything is used up if the station has no room, and
    // move the new crate from the inventory onto the station after the game has crafted it.
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class CrateCraftingPatch
    {
        private static bool Prefix(InventoryGui __instance, Player player, out List<ItemDrop.ItemData> __state)
        {
            __state = null;
            CrateMultiCraft.Apply(__instance, __instance.m_craftRecipe, fresh: true);
            if (!CrateCrafting.AppearsAtStation || !CrateCrafting.IsCrateRecipe(__instance.m_craftRecipe))
            {
                return true;
            }
            CraftingStation station = player.GetCurrentCraftingStation();
            if (station == null)
            {
                return true;
            }
            if (!CrateCrafting.FindSpot(station, player, out _, out _, out _, out string reason))
            {
                player.Message(MessageHud.MessageType.Center, reason);
                return false;
            }
            __state = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (CrateItem.IsCrate(item))
                {
                    __state.Add(item);
                }
            }
            return true;
        }

        private static void Postfix(Player player, List<ItemDrop.ItemData> __state)
        {
            CraftingStation station = player != null ? player.GetCurrentCraftingStation() : null;
            if (__state == null || station == null)
            {
                return;
            }
            Inventory inventory = player.GetInventory();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems().ToArray())
            {
                if (!CrateItem.IsCrate(item) || __state.Contains(item))
                {
                    continue;
                }
                // Crafted several at once: whatever doesn't fit at the station stays in the inventory.
                if (!CrateCrafting.FindSpot(station, player, out Vector3 position, out Quaternion rotation, out Ship ship, out _))
                {
                    break;
                }
                CratePlacement.Spawn(player, inventory, item, position, rotation, ship);
            }
        }
    }

    /// <summary>
    /// Shift crafts several of an item at once. For crates that appear at the station, only as many as there's room
    /// for there (at least one; the station refuses when it's full). The game reads the amount from one field when it
    /// shows the recipe, checks the inventory, and crafts, so it's set before each of those.
    /// </summary>
    internal static class CrateMultiCraft
    {
        private static bool _limited;
        private static int _usual;

        public static void Apply(InventoryGui gui, Recipe recipe, bool fresh = false)
        {
            Player player = Player.m_localPlayer;
            CraftingStation station = player != null ? player.GetCurrentCraftingStation() : null;
            if (station == null || !CrateCrafting.AppearsAtStation || !CrateCrafting.IsCrateRecipe(recipe))
            {
                if (_limited)
                {
                    gui.m_multiCraftAmount = _usual;
                    _limited = false;
                }
                return;
            }
            if (!_limited)
            {
                _usual = gui.m_multiCraftAmount;
                _limited = true;
            }
            gui.m_multiCraftAmount = Mathf.Clamp(CrateCrafting.RoomFor(station, player, fresh), 1, _usual);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    internal static class CrateMultiCraftShowPatch
    {
        private static void Prefix(InventoryGui __instance)
        {
            CrateMultiCraft.Apply(__instance, __instance.m_selectedRecipe.Recipe);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
    internal static class CrateMultiCraftPressPatch
    {
        private static void Prefix(InventoryGui __instance)
        {
            CrateMultiCraft.Apply(__instance, __instance.m_selectedRecipe.Recipe, fresh: true);
        }
    }
}
