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

        // Each station type's box in its own local space, measured once from its colliders.
        private static readonly Dictionary<string, Bounds> StationBounds = new Dictionary<string, Bounds>();

        public static bool AppearsAtStation => CargoConfig.PickUpMode.Value != CarryMode.Inventory;

        public static bool IsCrateRecipe(Recipe recipe)
        {
            return recipe != null && recipe.m_item != null && recipe.m_item.name == CrateSetup.CrateItemName;
        }

        /// <summary>A free spot for a new crate at the station, or why there is none.</summary>
        public static bool FindSpot(CraftingStation station, Player player, out Vector3 position, out Quaternion rotation, out Ship ship, out string reason)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            ship = null;
            Bounds bounds = BoundsOf(station);
            Transform t = station.transform;
            if (CountWaiting(t, bounds) >= CargoConfig.CratesAtStation.Value)
            {
                reason = "Carry the crates at the workbench away first";
                return false;
            }
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
            // The middle first, then either side: on top of the station, then on the ground on the crafter's side.
            foreach (Vector3 rowCenter in new[] { top, beside })
            {
                foreach (float slot in new[] { 0f, -1f, 1f })
                {
                    Vector3 probe = t.TransformPoint(rowCenter + rowAxis * (slot * spacing));
                    if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, ProbeAbove + ProbeDepth + bounds.size.y, CrateFit.Mask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    ship = null;
                    CrateFit.RestOn(hit, yaw, out position, out rotation, out ship);
                    if (CrateFit.Fits(position, rotation, ref ship, out _))
                    {
                        reason = null;
                        return true;
                    }
                }
            }
            reason = "No room for a crate at the workbench";
            return false;
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
}
