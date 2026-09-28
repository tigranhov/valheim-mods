using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Where a crate goes when you aim at a spot: flush against the face of a crate you look at (beside or on top),
    /// beside the nearest crate when you aim at the floor next to one, or right where you aim when snapping is off.
    /// Shared by setting a crate down (<see cref="CrateGhost"/>) and building one with the hammer.
    /// </summary>
    internal static class CratePose
    {
        private const float MaxReach = 6f;
        private const float RayLength = 30f;
        // Aim this close (m) to the side of a crate on the floor and the crate snaps beside it.
        private const float SnapRange = 0.6f;

        /// <summary>Holding Shift (the game's alternative-placement key) turns snapping off, for fine placement.</summary>
        public static bool Snapping => !ZInput.GetButton("AltPlace") && !ZInput.GetButton("JoyAltPlace");

        /// <summary>The pose for where <paramref name="player"/> aims, turned yawDegrees when not snapped. False if out of reach.</summary>
        public static bool Find(Player player, float yawDegrees, bool snap, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            ship = null;
            // Crates riding a ship were just moved by transform; bring their colliders along before testing against them.
            Physics.SyncTransforms();
            Transform view = GameCamera.instance != null ? GameCamera.instance.transform : null;
            if (view == null
                || !Physics.Raycast(view.position, view.forward, out RaycastHit hit, RayLength, CrateFit.Mask, QueryTriggerInteraction.Ignore)
                || Vector3.Distance(hit.point, player.transform.position) > MaxReach)
            {
                return false;
            }
            CargoCrate looked = snap ? hit.collider.GetComponentInParent<CargoCrate>() : null;
            if (looked != null && !looked.IsLoose && SnapToFace(looked, hit.normal, out position, out rotation, out ship))
            {
                // Against the face of the crate being looked at.
            }
            else if (snap && FindNeighbor(hit.point, out CargoCrate beside, out Vector3 side))
            {
                SnapTo(beside, side, out position, out rotation, out ship);
            }
            else
            {
                CrateFit.RestOn(hit, yawDegrees, out position, out rotation, out ship);
            }
            return true;
        }

        // Against the face of `crate` that `normal` points out of: beside it, or on top. Not underneath.
        private static bool SnapToFace(CargoCrate crate, Vector3 normal, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            Vector3 local = Quaternion.Inverse(crate.transform.rotation) * normal;
            Vector3 side = MainAxis(local);
            if (side.y < 0f)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                ship = null;
                return false;
            }
            SnapTo(crate, side, out position, out rotation, out ship);
            return true;
        }

        private static void SnapTo(CargoCrate crate, Vector3 side, out Vector3 position, out Quaternion rotation, out Ship ship)
        {
            Transform t = crate.transform;
            rotation = t.rotation;
            position = t.position + rotation * Vector3.Scale(side, CrateShape.Size);
            ship = crate.Ship;
        }

        // The crate on the same level whose side the aim point is closest to, within SnapRange of that side.
        private static bool FindNeighbor(Vector3 point, out CargoCrate nearest, out Vector3 side)
        {
            nearest = null;
            side = Vector3.zero;
            float best = SnapRange;
            Vector3 half = CrateShape.Size * 0.5f;
            foreach (CargoCrate crate in CargoCrate.All)
            {
                if (crate.IsCarried)
                {
                    continue;
                }
                Transform t = crate.transform;
                Vector3 local = Quaternion.Inverse(t.rotation) * (point - t.position) - CrateShape.Center;
                if (Mathf.Abs(local.y) > CrateShape.Size.y)
                {
                    continue;
                }
                float gap = Mathf.Max(Mathf.Abs(local.x) - half.x, Mathf.Abs(local.z) - half.z);
                if (gap < best)
                {
                    best = gap;
                    nearest = crate;
                    // Which side: the axis the point is furthest out along, relative to the crate's size.
                    side = Mathf.Abs(local.x) / half.x > Mathf.Abs(local.z) / half.z
                        ? new Vector3(Mathf.Sign(local.x), 0f, 0f)
                        : new Vector3(0f, 0f, Mathf.Sign(local.z));
                }
            }
            return nearest != null;
        }

        private static Vector3 MainAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.y >= a.x && a.y >= a.z)
            {
                return new Vector3(0f, Mathf.Sign(v.y), 0f);
            }
            return a.x >= a.z ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }
    }
}
