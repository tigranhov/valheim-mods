using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>Per-ship-type settings from "Type=Value, ..." config strings, keyed by the ship's prefab name.</summary>
    internal static class ShipRules
    {
        private const string OtherShips = "OtherShips";
        private const string Ground = "Ground";

        private static readonly Dictionary<string, string> FriendlyNames = new Dictionary<string, string>
        {
            ["Raft"] = "a raft",
            ["Karve"] = "a karve",
            ["VikingShip"] = "a longship",
            ["VikingShip_Ashlands"] = "a drakkar",
        };

        private static readonly Dictionary<string, Dictionary<string, float>> Parsed = new Dictionary<string, Dictionary<string, float>>();

        /// <summary>How many crates high may stand here; 0 = no limit. <paramref name="ship"/> null = on land.</summary>
        public static int StackLimit(Ship ship)
        {
            return CargoConfig.StackLimitsEnabled.Value ? Mathf.Max(0, (int)Lookup(CargoConfig.StackLimits.Value, ship, 0f)) : 0;
        }

        public static float Capacity(Ship ship)
        {
            return Mathf.Max(1f, Lookup(CargoConfig.ShipCapacities.Value, ship, 2500f));
        }

        /// <summary>"on a karve", "on this ship", "on the ground".</summary>
        public static string Where(Ship ship)
        {
            if (ship == null)
            {
                return "on the ground";
            }
            return "on " + (FriendlyNames.TryGetValue(TypeOf(ship), out string name) ? name : "this ship");
        }

        private static string TypeOf(Ship ship)
        {
            return Utils.GetPrefabName(ship.gameObject);
        }

        private static float Lookup(string config, Ship ship, float fallback)
        {
            Dictionary<string, float> values = Parse(config);
            if (ship == null)
            {
                return values.TryGetValue(Ground, out float ground) ? ground : fallback;
            }
            if (values.TryGetValue(TypeOf(ship), out float value) || values.TryGetValue(OtherShips, out value))
            {
                return value;
            }
            return fallback;
        }

        private static Dictionary<string, float> Parse(string config)
        {
            config = config ?? "";
            if (Parsed.TryGetValue(config, out Dictionary<string, float> values))
            {
                return values;
            }
            values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in config.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split('=');
                if (pair.Length == 2 && float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                {
                    values[pair[0].Trim()] = number;
                }
            }
            Parsed[config] = values;
            return values;
        }
    }
}
