using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>
    /// Server-side view of trader camps. ZoneSystem keeps every candidate spot for a camp until a player comes
    /// within zone-generation range (~300 m) of one; vanilla then places that one and deletes the other candidates.
    /// Signals only ever come from placed camps, so they never give away a candidate spot.
    /// </summary>
    internal static class CampLocator
    {
        /// <summary>Fills <paramref name="into"/> with every placed camp and candidate spot per trader prefab.</summary>
        public static void Collect(Dictionary<string, List<ZoneSystem.LocationInstance>> into, IReadOnlyList<TraderDef> defs)
        {
            into.Clear();
            foreach (TraderDef def in defs)
            {
                into[def.Prefab] = new List<ZoneSystem.LocationInstance>();
            }
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
            {
                string name = PrefabName(instance.m_location);
                if (name != null && into.TryGetValue(name, out List<ZoneSystem.LocationInstance> list))
                {
                    list.Add(instance);
                }
            }
        }

        /// <param name="placedOnly">Only camps the game has actually generated. Candidate spots are never real camps yet.</param>
        public static bool TryGetNearest(List<ZoneSystem.LocationInstance> camps, Vector3 from, bool placedOnly, out ZoneSystem.LocationInstance nearest, out float distance)
        {
            nearest = default;
            distance = float.MaxValue;
            foreach (ZoneSystem.LocationInstance camp in camps)
            {
                if (placedOnly && !camp.m_placed)
                {
                    continue;
                }
                float d = Utils.DistanceXZ(camp.m_position, from);
                if (d < distance)
                {
                    distance = d;
                    nearest = camp;
                }
            }
            return distance < float.MaxValue;
        }

        public static string PrefabName(ZoneSystem.ZoneLocation location)
        {
            if (location == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(location.m_prefabName))
            {
                return location.m_prefabName;
            }
            return location.m_prefab.IsValid ? location.m_prefab.Name : null;
        }
    }
}
