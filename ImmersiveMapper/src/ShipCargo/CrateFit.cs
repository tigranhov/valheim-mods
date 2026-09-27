using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>Where a crate may stand. Shared by the placement ghost and dropping a crate from the inventory.</summary>
    internal static class CrateFit
    {
        public const string DoesntFit = "The crate doesn't fit there";

        // Boxes may touch; they only clip when they overlap by more than this share of the crate's size.
        private const float OverlapShrink = 0.9f;
        // Hull planks are convex shapes that bulge a little into the deck space, so allow this much overlap (m) with
        // the ship's own hull. The game's build check allows 0.2 m (Player.TestGhostClipping).
        private const float HullTolerance = 0.15f;
        private const float SupportProbe = 0.35f;
        private const float WaterMargin = 0.1f;
        private const int MaxStackScan = 16;
        // Out of the way: the measuring box is only ever used with explicit poses.
        private static readonly Vector3 ProbeParking = new Vector3(0f, -10000f, 0f);

        private static readonly Collider[] Overlaps = new Collider[32];
        private static int _mask;
        private static int _hullLayer;
        private static BoxCollider _probe;

        public static int Mask
        {
            get
            {
                if (_mask == 0)
                {
                    _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");
                    _hullLayer = LayerMask.NameToLayer("vehicle");
                }
                return _mask;
            }
        }

        /// <summary>Rests the crate's bottom on the hit point: aligned with the deck on a ship, turned yawDegrees on land.</summary>
        public static void RestOn(RaycastHit hit, float yawDegrees, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            ship = CratePlacement.ShipOf(hit.collider);
            Quaternion baseRotation = ship != null ? ship.transform.rotation : Quaternion.identity;
            rotation = baseRotation * Quaternion.Euler(0f, yawDegrees, 0f);
            position = hit.point - rotation * CrateShape.BottomCenter;
        }

        /// <summary>
        /// True if a crate fits at this pose: not clipping into anything, something under it, not sinking, not stacked
        /// higher than allowed. <paramref name="ship"/> is filled in from what it stands on if not known yet.
        /// </summary>
        public static bool Fits(Vector3 position, Quaternion rotation, ref Ship ship, out string reason)
        {
            reason = DoesntFit;
            Vector3 up = rotation * Vector3.up;
            // Slightly shrunk and lifted, so resting on a surface or touching a neighbor doesn't count as clipping.
            Vector3 center = position + rotation * CrateShape.Center + up * (CrateShape.Size.y * 0.03f);
            int count = Physics.OverlapBoxNonAlloc(center, CrateShape.Size * (0.5f * OverlapShrink), Overlaps, rotation, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsHarmlessOverlap(Overlaps[i], position, rotation))
                {
                    return false;
                }
            }
            Vector3 bottom = position + rotation * CrateShape.BottomCenter;
            if (!Physics.Raycast(bottom + up * 0.1f, -up, out RaycastHit support, 0.1f + SupportProbe, Mask, QueryTriggerInteraction.Ignore))
            {
                reason = "Nothing to stand the crate on";
                return false;
            }
            CargoCrate under = support.collider.GetComponentInParent<CargoCrate>();
            if (under != null && under.IsLoose)
            {
                reason = "Not on a loose crate";
                return false;
            }
            if (ship == null)
            {
                ship = CratePlacement.ShipOf(support.collider);
            }
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (ship == null && support.point.y < water - WaterMargin)
            {
                reason = "The crate would sink here";
                return false;
            }
            int limit = ShipRules.StackLimit(ship);
            if (limit > 0 && StackLevel(bottom, up) > limit)
            {
                reason = $"Crates stack only {limit} high {ShipRules.Where(ship)}";
                return false;
            }
            return true;
        }

        private static bool IsHarmlessOverlap(Collider other, Vector3 position, Quaternion rotation)
        {
            Ship owner = other.GetComponentInParent<Ship>();
            if (owner == null)
            {
                return false;
            }
            // A ship's buoyancy box encloses its whole hull.
            if (other == owner.m_floatCollider)
            {
                return true;
            }
            // Hull planks may bulge in a little; the mast, seats, rudder and built-in chest may not be clipped at all.
            return other.gameObject.layer == _hullLayer
                && (!Physics.ComputePenetration(Probe(), position, rotation, other, other.transform.position, other.transform.rotation, out _, out float depth)
                    || depth <= HullTolerance);
        }

        // 1 for a crate on the floor, 2 on top of one crate, and so on: counts the crates underneath.
        private static int StackLevel(Vector3 bottom, Vector3 up)
        {
            int level = 1;
            for (int i = 0; i < MaxStackScan; i++)
            {
                // Rays ignore the collider they start in, so from just inside a crate's bottom this finds what's below it.
                if (!Physics.Raycast(bottom + up * 0.05f, -up, out RaycastHit hit, 0.2f, Mask, QueryTriggerInteraction.Ignore))
                {
                    break;
                }
                CargoCrate below = hit.collider.GetComponentInParent<CargoCrate>();
                if (below == null)
                {
                    break;
                }
                level++;
                bottom = below.transform.TransformPoint(CrateShape.BottomCenter);
                up = below.transform.up;
            }
            return level;
        }

        // A crate-sized box for measuring overlap depth (Physics.ComputePenetration). A trigger on the ghost layer,
        // like the game's build ghosts: it touches nothing.
        private static BoxCollider Probe()
        {
            if (_probe != null)
            {
                return _probe;
            }
            var go = new GameObject("IM_CrateProbe");
            Object.DontDestroyOnLoad(go);
            go.layer = LayerMask.NameToLayer("ghost");
            go.transform.position = ProbeParking;
            _probe = go.AddComponent<BoxCollider>();
            _probe.isTrigger = true;
            _probe.center = CrateShape.Center;
            _probe.size = CrateShape.Size;
            return _probe;
        }
    }
}
