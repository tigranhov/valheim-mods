using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cartographer
{
    /// <summary>
    /// Simple models built in code from Unity's cylinder and the game's own materials (so the mod ships no assets),
    /// and the shared bits of making the kit's items: swapping a cloned item's model, icons, recipes.
    /// </summary>
    internal static class Models
    {
        private static Mesh _cylinder;

        public static Mesh Cylinder
        {
            get
            {
                if (_cylinder == null)
                {
                    GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    _cylinder = primitive.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(primitive);
                }
                return _cylinder;
            }
        }

        /// <summary>The material of a vanilla item's model (leather scraps, wood, ...), or a plain one.</summary>
        public static Material MaterialOf(string item, Color fallback)
        {
            GameObject prefab = PrefabManager.Cache.GetPrefab<GameObject>(item);
            Renderer renderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
            if (renderer != null && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial;
            }
            Plugin.Log.LogWarning($"No material on {item}; using a plain one.");
            return new Material(Shader.Find("Standard")) { color = fallback };
        }

        public static Material Darker(Material material, float factor)
        {
            var copy = new Material(material) { name = material.name + " (IM dark)" };
            if (copy.HasProperty("_Color"))
            {
                copy.color = copy.color * factor;
            }
            return copy;
        }

        /// <param name="scale">Unity's cylinder is 1 wide and 2 tall; this scales it.</param>
        public static Transform Part(Transform parent, string name, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var part = new GameObject(name);
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = Cylinder;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        /// <summary>
        /// Takes the look off a cloned item (its meshes) and puts a model built by <paramref name="build"/> in its place,
        /// once lying on the ground and once for the hand (the "attach" child the game shows when it's equipped).
        /// </summary>
        public static void ReplaceLook(GameObject prefab, string visualName, Action<Transform> build, Quaternion droppedRotation, Vector3 droppedOffset)
        {
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
            Transform dropped = Visual(prefab.transform, visualName, build);
            dropped.localRotation = droppedRotation;
            dropped.localPosition = droppedOffset;

            Transform attach = prefab.transform.Find("attach");
            if (attach == null)
            {
                attach = new GameObject("attach").transform;
                attach.SetParent(prefab.transform, false);
            }
            // Only the copy made for the hand is shown (VisEquipment activates it), never the one inside the item.
            attach.gameObject.SetActive(false);
            Visual(attach, visualName, build);
        }

        public static Transform Visual(Transform parent, string name, Action<Transform> build)
        {
            var root = new GameObject(name).transform;
            root.gameObject.layer = parent.gameObject.layer;
            root.SetParent(parent, false);
            build(root);
            return root;
        }

        public static Sprite Icon(GameObject prefab, string what)
        {
            try
            {
                return RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 30f, 45f));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't render the {what} icon: {e.Message}");
                return null;
            }
        }

        /// <summary>"Item:Amount" pairs separated by commas.</summary>
        public static RequirementConfig[] Recipe(string recipe)
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
