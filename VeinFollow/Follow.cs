using System.Collections.Generic;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>
    /// A swing that follows the rock: it hits the chunk you struck (or, once that one is gone, the chunk left nearest to where
    /// you struck), and a chunk that breaks passes the damage it didn't need on to the next nearest, until the swing is spent.
    /// Each chunk still goes through the rock's own handling, so tool tier, loot, effects and stats are the game's.
    /// </summary>
    internal static class Follow
    {
        private static readonly Collider[] Overlaps = new Collider[64];
        private static int _rockMask;

        /// <summary>For a hit on a rock. True when this mod mined it (the game's own handling is then skipped).</summary>
        public static bool TryMine(Rock rock, HitData hit)
        {
            if (rock == null || !rock.IsValid || !IsOwnSwing(hit.GetAttacker(), hit.m_skill) || hit.m_hitCollider == null || hit.m_radius > 0f)
            {
                return false;
            }
            int area = rock.AreaOf(hit.m_hitCollider);
            if (!rock.Alive(area))
            {
                area = rock.Nearest(hit.m_point);
            }
            if (area < 0)
            {
                return false;
            }
            Mine(rock, hit, area);
            return true;
        }

        /// <summary>
        /// For a pickaxe hitting the ground: a chunk within reach, buried or not, is mined instead of digging. True when one
        /// was (the dig is then skipped).
        /// </summary>
        public static bool TryMineBelow(Vector3 point, Character character, ItemDrop.ItemData weapon)
        {
            if (weapon == null || !IsOwnSwing(character, weapon.m_shared.m_skillType) || !(character is Humanoid humanoid)
                || humanoid.m_currentAttack == null)
            {
                return false;
            }
            if (!FindChunk(point, FollowConfig.Reach.Value, out Rock rock, out int area, out Collider collider))
            {
                return false;
            }
            Attack attack = humanoid.m_currentAttack;
            Vector3 target = collider.bounds.center;
            Mine(rock, SwingAt(attack, character, weapon, target, collider), area);
            // The game raises the skill for a rock hit, not for a ground hit.
            character.RaiseSkill(weapon.m_shared.m_skillType, attack.m_raiseSkillAmount);
            return true;
        }

        private static bool IsOwnSwing(Character attacker, Skills.SkillType skill)
        {
            Player player = Player.m_localPlayer;
            return player != null && attacker == player && skill == Skills.SkillType.Pickaxes && FollowConfig.Active();
        }

        private static void Mine(Rock rock, HitData hit, int area)
        {
            // Your game handles the rock (breaks, loot, digging) with your settings, whoever handled it before.
            rock.View.ClaimOwnership();
            Vector3 origin = hit.m_point;
            HitData current = hit;
            using (LootDrop.For(Player.m_localPlayer))
            {
                for (int step = 0; step < rock.Count && area >= 0 && rock.IsValid; step++)
                {
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
                    area = rock.Nearest(origin);
                    if (area < 0)
                    {
                        break;
                    }
                    current = current.Clone();
                    current.m_damage.Modify((damage - health) / damage);
                    current.m_point = rock.Center(area);
                    current.m_hitCollider = rock.Collider(area);
                }
            }
        }

        // What the rock takes from a hit, after its resistances (the hit itself is left as it is).
        private static float DamageOn(Rock rock, HitData hit)
        {
            HitData copy = hit.Clone();
            copy.ApplyResistance(rock.Modifiers, out _);
            return copy.GetTotalDamage();
        }

        // The chunk left nearest to a point on the ground, within reach.
        private static bool FindChunk(Vector3 point, float reach, out Rock rock, out int area, out Collider collider)
        {
            rock = null;
            area = -1;
            collider = null;
            if (_rockMask == 0)
            {
                _rockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
            }
            float best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(point, reach, Overlaps, _rockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rock candidate = Rock.Of(Overlaps[i]);
                if (candidate == null || !candidate.IsValid)
                {
                    continue;
                }
                int index = candidate.AreaOf(Overlaps[i]);
                float distance = Overlaps[i].bounds.SqrDistance(point);
                if (candidate.Alive(index) && distance < best)
                {
                    best = distance;
                    rock = candidate;
                    area = index;
                    collider = Overlaps[i];
                }
            }
            return rock != null;
        }

        // The hit the swing would have made on the chunk, built as the game builds a melee hit.
        private static HitData SwingAt(Attack attack, Character character, ItemDrop.ItemData weapon, Vector3 point, Collider collider)
        {
            Skills.SkillType skill = weapon.m_shared.m_skillType;
            float factor = character.GetRandomSkillFactor(skill);
            var hit = new HitData
            {
                m_toolTier = (short)weapon.m_shared.m_toolTier,
                m_skillLevel = character.GetSkillLevel(skill),
                m_itemLevel = (short)weapon.m_quality,
                m_itemWorldLevel = (byte)weapon.m_worldLevel,
                m_pushForce = weapon.m_shared.m_attackForce * factor * attack.m_forceMultiplier,
                m_backstabBonus = weapon.m_shared.m_backstabBonus,
                m_staggerMultiplier = attack.m_staggerMultiplier,
                m_skill = skill,
                m_skillRaiseAmount = attack.m_raiseSkillAmount,
                m_damage = weapon.GetDamage(),
                m_point = point,
                m_dir = (point - character.transform.position).normalized,
                m_hitCollider = collider,
                m_hitType = HitData.HitType.PlayerHit,
            };
            hit.SetAttacker(character);
            attack.ModifyDamage(hit, factor);
            character.GetSEMan().ModifyAttack(skill, ref hit);
            return hit;
        }
    }
}
