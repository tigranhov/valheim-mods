using System.Collections.Generic;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>
    /// A rock made of chunks, of either kind the game has: MineRock5 (ore veins, big rock formations) keeps each chunk's
    /// health in one list, MineRock (smaller multi-part rocks) keeps it per chunk in the rock's saved data.
    /// </summary>
    internal abstract class Rock
    {
        public abstract ZNetView View { get; }
        public abstract HitData.DamageModifiers Modifiers { get; }
        public abstract int MinToolTier { get; }
        public abstract int Count { get; }
        public abstract Collider Collider(int area);
        public abstract float Health(int area);

        /// <summary>Hits one chunk through the rock's own handling (damage, loot, effects, stats), on the owner's game.</summary>
        public abstract void Hit(HitData hit, int area);

        public abstract int AreaOf(Collider collider);

        public bool IsValid => View != null && View.IsValid();

        public bool Alive(int area)
        {
            return area >= 0 && area < Count && Health(area) > 0f && Collider(area) != null;
        }

        public Vector3 Center(int area)
        {
            return Collider(area).bounds.center;
        }

        /// <summary>The chunk left nearest to a point, or -1 when none is.</summary>
        public int Nearest(Vector3 point)
        {
            int best = -1;
            float bestEdge = float.MaxValue;
            float bestCenter = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (!Alive(i))
                {
                    continue;
                }
                Bounds bounds = Collider(i).bounds;
                float edge = bounds.SqrDistance(point);
                float center = (bounds.center - point).sqrMagnitude;
                if (edge < bestEdge || (Mathf.Approximately(edge, bestEdge) && center < bestCenter))
                {
                    best = i;
                    bestEdge = edge;
                    bestCenter = center;
                }
            }
            return best;
        }

        /// <summary>Where each chunk left is, kept before a hit because a broken chunk's collider is switched off.</summary>
        public Dictionary<int, Bounds> AliveBounds()
        {
            var bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < Count; i++)
            {
                if (Alive(i))
                {
                    bounds[i] = Collider(i).bounds;
                }
            }
            return bounds;
        }

        public static Rock Of(Component component)
        {
            if (component == null)
            {
                return null;
            }
            MineRock5 vein = component.GetComponentInParent<MineRock5>();
            if (vein != null)
            {
                return vein.m_hitAreas != null ? new Vein(vein) : null;
            }
            MineRock rock = component.GetComponentInParent<MineRock>();
            return rock != null && rock.m_hitAreas != null ? new PartRock(rock) : null;
        }

        private sealed class Vein : Rock
        {
            private readonly MineRock5 _rock;

            public Vein(MineRock5 rock)
            {
                _rock = rock;
                if (IsValid)
                {
                    rock.LoadHealth();
                }
            }

            public override ZNetView View => _rock != null ? _rock.m_nview : null;
            public override HitData.DamageModifiers Modifiers => _rock.m_damageModifiers;
            public override int MinToolTier => _rock.m_minToolTier;
            public override int Count => _rock.m_hitAreas.Count;
            public override Collider Collider(int area) => _rock.m_hitAreas[area].m_collider;
            public override float Health(int area) => _rock.m_hitAreas[area].m_health;
            public override int AreaOf(Collider collider) => _rock.GetAreaIndex(collider);

            public override void Hit(HitData hit, int area)
            {
                _rock.m_nview.InvokeRPC("RPC_Damage", hit, area);
            }
        }

        private sealed class PartRock : Rock
        {
            private readonly MineRock _rock;

            public PartRock(MineRock rock)
            {
                _rock = rock;
            }

            public override ZNetView View => _rock != null ? _rock.m_nview : null;
            public override HitData.DamageModifiers Modifiers => _rock.m_damageModifiers;
            public override int MinToolTier => _rock.m_minToolTier;
            public override int Count => _rock.m_hitAreas.Length;
            public override Collider Collider(int area) => _rock.m_hitAreas[area];
            public override int AreaOf(Collider collider) => _rock.GetAreaIndex(collider);

            public override float Health(int area)
            {
                ZDO zdo = IsValid ? _rock.m_nview.GetZDO() : null;
                return zdo != null ? zdo.GetFloat("Health" + area, _rock.GetHealth()) : 0f;
            }

            public override void Hit(HitData hit, int area)
            {
                _rock.m_nview.InvokeRPC("Hit", hit, area);
            }
        }
    }
}
