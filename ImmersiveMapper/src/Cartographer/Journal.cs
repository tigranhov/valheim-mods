using System;
using System.Collections.Generic;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>One stretch of route: how many paces the tally counted, and the player's own label.</summary>
    internal sealed class Leg
    {
        public int Number;
        public int Paces;
        public string Label;
        public int Day;
    }

    /// <summary>The legs noted in a map case, oldest first. At the table they become measuring strings.</summary>
    internal sealed class Journal
    {
        public const int MaxLegs = 200;

        private const int Version = 1;

        public readonly List<Leg> Legs = new List<Leg>();

        public bool Unreadable;

        public int NextNumber => Legs.Count > 0 ? Legs[Legs.Count - 1].Number + 1 : 1;

        public void Add(Leg leg)
        {
            Legs.Add(leg);
            // A full journal forgets its oldest leg rather than refusing new ones out in the field.
            while (Legs.Count > MaxLegs)
            {
                Legs.RemoveAt(0);
            }
        }

        public string Encode()
        {
            var pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(Legs.Count);
            foreach (Leg leg in Legs)
            {
                pkg.Write(leg.Number);
                pkg.Write(leg.Paces);
                pkg.Write(leg.Label ?? "");
                pkg.Write(leg.Day);
            }
            return Convert.ToBase64String(Utils.Compress(pkg.GetArray()));
        }

        public static Journal Decode(string text)
        {
            var journal = new Journal();
            if (string.IsNullOrEmpty(text))
            {
                return journal;
            }
            try
            {
                var pkg = new ZPackage(Utils.Decompress(Convert.FromBase64String(text)));
                if (pkg.ReadInt() > Version)
                {
                    return new Journal { Unreadable = true };
                }
                int count = pkg.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    journal.Legs.Add(new Leg { Number = pkg.ReadInt(), Paces = pkg.ReadInt(), Label = pkg.ReadString(), Day = pkg.ReadInt() });
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read a journal: {e.Message}");
                return new Journal { Unreadable = true };
            }
            return journal;
        }
    }
}
