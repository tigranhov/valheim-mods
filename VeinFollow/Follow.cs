using System.Collections.Generic;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>
    /// A swing that follows the rock: the chunk you hit stays and the swing damages another chunk of the same rock, the one
    /// farthest from you first, so the whole rock comes out while you keep hitting one spot. A chunk that breaks passes the
    /// damage it didn't need on to the next farthest; the chunk you hit goes last. Each chunk still goes through the rock's
    /// own handling, so tool tier, loot, effects and stats are the game's.
    /// </summary>
    internal static class Follow
    {
        /// <summary>For a hit on a rock. True when this mod mined it (the game's own handling is then skipped).</summary>
        public static bool TryMine(Rock rock, HitData hit)
        {
            Player player = Player.m_localPlayer;
            if (rock == null || !rock.IsValid || player == null || hit.GetAttacker() != player
                || hit.m_skill != Skills.SkillType.Pickaxes || hit.m_hitCollider == null || hit.m_radius > 0f || !FollowConfig.Active())
            {
                return false;
            }
            int struck = rock.AreaOf(hit.m_hitCollider);
            if (!rock.Alive(struck))
            {
                return false;
            }
            Mine(rock, hit, struck, player.transform.position);
            return true;
        }

        private static void Mine(Rock rock, HitData hit, int struck, Vector3 miner)
        {
            // Your game handles the rock (breaks, loot, digging) with your settings, whoever handled it before.
            rock.View.ClaimOwnership();
            HitData current = hit;
            int area = Next(rock, struck, miner);
            using (LootDrop.For(Player.m_localPlayer))
            {
                for (int step = 0; step < rock.Count && area >= 0 && rock.IsValid; step++)
                {
                    current = At(rock, current, area);
                    float health = rock.Health(area);
                    float damage = DamageOn(rock, current);
                    Dictionary<int, Bounds> before = FollowConfig.DigGround.Value ? rock.AliveBounds() : null;
                    rock.Hit(current, area);
                    if (before != null)
                    {
                        Ground.DigBroken(rock, before);
                    }
                    if (!current.CheckToolTier(rock.MinToolTier) || damage <= health || !rock.IsValid)
                    {
                        break;
                    }
                    area = Next(rock, struck, miner);
                    current = current.Clone();
                    current.m_damage.Modify((damage - health) / damage);
                }
            }
        }

        // The chunk to damage: the one farthest from you, the chunk you hit only once it's the last one left.
        private static int Next(Rock rock, int struck, Vector3 miner)
        {
            int farthest = rock.Farthest(miner, struck);
            return farthest >= 0 ? farthest : rock.Alive(struck) ? struck : -1;
        }

        // The hit, aimed at a chunk (where its effects and damage number show).
        private static HitData At(Rock rock, HitData hit, int area)
        {
            if (hit.m_hitCollider == rock.Collider(area))
            {
                return hit;
            }
            HitData aimed = hit.Clone();
            aimed.m_point = rock.Center(area);
            aimed.m_hitCollider = rock.Collider(area);
            return aimed;
        }

        // What the rock takes from a hit, after its resistances (the hit itself is left as it is).
        private static float DamageOn(Rock rock, HitData hit)
        {
            HitData copy = hit.Clone();
            copy.ApplyResistance(rock.Modifiers, out _);
            return copy.GetTotalDamage();
        }
    }
}
