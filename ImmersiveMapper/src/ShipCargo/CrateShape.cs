using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>The crate's box in its own local space, measured once from the prefab's colliders.</summary>
    internal static class CrateShape
    {
        public static Vector3 Center { get; private set; } = new Vector3(0f, 0.4f, 0f);
        public static Vector3 Size { get; private set; } = new Vector3(1f, 0.8f, 1f);

        /// <summary>Middle of the bottom face, local space. Placement puts this on the surface.</summary>
        public static Vector3 BottomCenter => Center - new Vector3(0f, Size.y * 0.5f, 0f);

        public static void Measure(GameObject prefab)
        {
            Transform root = prefab.transform;
            var bounds = new Bounds();
            bool any = false;
            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                    Vector3 world = box.transform.TransformPoint(box.center + Vector3.Scale(box.size, corner));
                    Vector3 local = root.InverseTransformPoint(world);
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
            if (!any)
            {
                Plugin.Log.LogWarning("Crate has no box collider; using a default 1 x 0.8 x 1 m box for placement.");
                return;
            }
            Center = bounds.center;
            Size = bounds.size;
            Plugin.Log.LogInfo($"Crate box: size {Size}, center {Center}");
        }
    }
}
