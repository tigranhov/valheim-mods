using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimTweaks
{
    /// <summary>
    /// Changes how often and how much of an item mining drops.
    /// Mined objects (MineRock, MineRock5, DropOnDestroyed) call <see cref="DropTable.GetDropList()"/> once per
    /// destroyed chunk. Vanilla rolls it as: with probability m_dropChance the table drops anything at all, then
    /// each of m_dropMin..m_dropMax picks chooses an entry by weight and adds its stack as one list entry per item.
    /// The objects spawn every entry. Chests and creature drops use other code paths and are unaffected.
    /// </summary>
    internal static class MiningDrops
    {
        private const string Section = "Mining";

        // Vanilla logs a warning on every roll for entries with weight 0, so "never" uses a negligible weight instead.
        private const float MinWeight = 1e-6f;

        private sealed class ItemSettings
        {
            public ConfigEntry<float> Amount;
            public ConfigEntry<float> Chance;
        }

        // Item prefab name -> settings. Values are read on every drop, so edits in
        // Configuration Manager apply immediately.
        private static readonly Dictionary<string, ItemSettings> Items = new Dictionary<string, ItemSettings>();

        public static void BindConfig(ConfigFile config)
        {
            Items["IronScrap"] = new ItemSettings
            {
                Amount = config.Bind(Section, "Iron drop multiplier", 1f,
                    new ConfigDescription(
                        "Multiplies how much scrap iron drops each time muddy scrap piles or Mistlands ancient swords/armor drop it.\n" +
                        "1 = vanilla, 2 = double, 0 = none. Fractional results round up or down at random (e.g. 1.5x of 1 drops 1 or 2).\n" +
                        "Stacks with the world's Resources modifier.",
                        new AcceptableValueRange<float>(0f, 10f),
                        new ConfigurationManagerAttributes { Order = 100 })),

                Chance = config.Bind(Section, "Iron drop chance multiplier", 1f,
                    new ConfigDescription(
                        "Multiplies the chance that a mined chunk drops scrap iron. Other drops (leather scraps, withered bones) keep their vanilla chance.\n" +
                        "Vanilla muddy scrap piles drop iron from about 14% of chunks in Sunken Crypts and 30% of chunks when buried in the swamp; 2 = twice as often.\n" +
                        "Capped at whatever chance the other drops leave (about 94% for crypt piles). Can't raise sources that already always drop iron.",
                        new AcceptableValueRange<float>(0f, 10f),
                        new ConfigurationManagerAttributes { Order = 90 })),
            };
        }

        private static int Scale(int count, float multiplier)
        {
            float scaled = count * multiplier;
            int whole = Mathf.FloorToInt(scaled);
            return UnityEngine.Random.value < scaled - whole ? whole + 1 : whole;
        }

        /// <summary>
        /// Gate chance and entry weight that make one pick drop the entry <paramref name="multiplier"/> times as often
        /// while every other entry keeps its vanilla chance.
        /// </summary>
        internal static void ScaleChance(float dropChance, float totalWeight, float weight, float multiplier,
            out float newDropChance, out float newWeight)
        {
            float otherWeight = totalWeight - weight;
            float otherChance = dropChance * otherWeight / totalWeight;
            float target = Math.Min(dropChance * weight / totalWeight * multiplier, 1f - otherChance);

            // A pick yields the entry with newDropChance * newWeight / (otherWeight + newWeight) and anything else with
            // newDropChance * otherWeight / (otherWeight + newWeight); solve for those being target and otherChance.
            newDropChance = otherChance + target;
            newWeight = otherWeight > 0f ? Math.Max(otherWeight * target / otherChance, MinWeight) : weight;
        }

        private sealed class TableOverride
        {
            public float DropChance;
            public int Index;
            public DropTable.DropData Drop;
        }

        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        private static class DropTable_GetDropList_Patch
        {
            // Reweights the table for this roll only; Finalizer restores it.
            private static void Prefix(DropTable __instance, out TableOverride __state)
            {
                __state = null;
                List<DropTable.DropData> drops = __instance.m_drops;
                if (drops == null || __instance.m_dropChance <= 0f)
                    return;

                // Only the first configured entry is adjusted; vanilla mining tables have at most one per item.
                float totalWeight = 0f;
                int index = -1;
                float multiplier = 1f;
                for (int i = 0; i < drops.Count; i++)
                {
                    totalWeight += drops[i].m_weight;
                    if (index < 0 && drops[i].m_item != null && Items.TryGetValue(drops[i].m_item.name, out ItemSettings settings) && !Mathf.Approximately(settings.Chance.Value, 1f))
                    {
                        index = i;
                        multiplier = settings.Chance.Value;
                    }
                }

                if (index < 0 || drops[index].m_weight <= 0f)
                    return;

                DropTable.DropData drop = drops[index];
                ScaleChance(__instance.m_dropChance, totalWeight, drop.m_weight, multiplier, out float dropChance, out float weight);

                __state = new TableOverride { DropChance = __instance.m_dropChance, Index = index, Drop = drop };
                __instance.m_dropChance = dropChance;
                drop.m_weight = weight;
                drops[index] = drop;
            }

            private static void Postfix(List<GameObject> __result)
            {
                if (__result == null || __result.Count == 0)
                    return;

                // Pull out the entries we scale; most drops (stone, wood) have none, so avoid allocating until needed.
                Dictionary<GameObject, int> counts = null;
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    GameObject item = __result[i];
                    if (item == null || !Items.TryGetValue(item.name, out ItemSettings settings) || Mathf.Approximately(settings.Amount.Value, 1f))
                        continue;

                    counts ??= new Dictionary<GameObject, int>();
                    counts.TryGetValue(item, out int count);
                    counts[item] = count + 1;
                    __result.RemoveAt(i);
                }

                if (counts == null)
                    return;

                foreach (KeyValuePair<GameObject, int> entry in counts)
                {
                    int amount = Scale(entry.Value, Items[entry.Key.name].Amount.Value);
                    for (int i = 0; i < amount; i++)
                        __result.Add(entry.Key);
                }
            }

            private static void Finalizer(DropTable __instance, TableOverride __state)
            {
                if (__state == null)
                    return;

                __instance.m_dropChance = __state.DropChance;
                __instance.m_drops[__state.Index] = __state.Drop;
            }
        }
    }
}
