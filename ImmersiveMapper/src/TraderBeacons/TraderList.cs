using System;
using System.Collections.Generic;

namespace ImmersiveMapper.TraderBeacons
{
    internal readonly struct TraderDef
    {
        public readonly string Prefab;
        public readonly string Name;

        public TraderDef(string prefab, string name)
        {
            Prefab = prefab;
            Name = name;
        }
    }

    /// <summary>The trader camps that signal, parsed from the Traders config entry.</summary>
    internal static class TraderList
    {
        private static string _parsedFrom;
        private static readonly List<TraderDef> Defs = new List<TraderDef>();

        public static IReadOnlyList<TraderDef> All
        {
            get
            {
                string raw = BeaconConfig.Traders.Value ?? "";
                if (raw != _parsedFrom)
                {
                    Parse(raw);
                }
                return Defs;
            }
        }

        public static string NameOf(string prefab)
        {
            foreach (TraderDef def in All)
            {
                if (def.Prefab == prefab)
                {
                    return def.Name;
                }
            }
            return prefab;
        }

        private static void Parse(string raw)
        {
            _parsedFrom = raw;
            Defs.Clear();
            foreach (string entry in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split('=');
                string prefab = parts[0].Trim();
                if (prefab.Length == 0)
                {
                    continue;
                }
                string name = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : prefab;
                Defs.Add(new TraderDef(prefab, name));
            }
        }
    }
}
