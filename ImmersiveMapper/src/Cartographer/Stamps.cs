using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// The stamps a player can press onto a sheet: the game's own icons in their own colours, found by name (an item,
    /// a piece, or a location that has a map icon, like the traders' camps), plus a few red markers drawn in code.
    /// A stamp is saved by that name, so any player sees it even if their palette doesn't list it; the palette itself
    /// is each player's own list in the config.
    /// </summary>
    internal static class Stamps
    {
        public const string DefaultPalette =
            "mark_x, mark_circle, mark_dot, bed, fire_pit, piece_workbench, Karve, "
            + "Vendor_BlackForest, Hildir_camp, BogWitch_Camp, "
            + "TrophyEikthyr, TrophyTheElder, TrophyBonemass, TrophyDragonQueen, TrophyGoblinKing, TrophySeekerQueen, TrophyFader, "
            + "TrophySkeleton, TrophyDraugr, TrophyFrostTroll, TrophyGoblin, TrophyCultist, TrophyWolf, TrophySerpent, TrophyDeathsquito, "
            + "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, Obsidian, Tar, Crystal, "
            + "Raspberry, Blueberries, Cloudberry, MushroomYellow, Thistle, Flax, Barley, FineWood, ElderBark, YggdrasilWood, "
            + "Coins, SurtlingCore";

        public sealed class Kind
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Sprite Sprite;
            /// <summary>The game's icons are dimmed a little so they sit on the parchment; the drawn markers aren't.</summary>
            public readonly bool Dim;

            public Kind(string id, string name, Sprite sprite, bool dim)
            {
                Id = id;
                Name = name;
                Sprite = sprite;
                Dim = dim;
            }
        }

        // The first test builds saved these ids; they now show the matching game icon.
        private static readonly Dictionary<string, string> OldIds = new Dictionary<string, string>
        {
            { "home", "bed" },
            { "camp", "fire_pit" },
            { "point", "mark_dot" },
            { "work", "piece_workbench" },
            { "rune", "mark_circle" },
            { "danger", "mark_x" },
            { "boss", "TrophyEikthyr" },
            { "trader", "Vendor_BlackForest" },
            { "hildir", "Hildir_camp" },
            { "bogwitch", "BogWitch_Camp" },
            { "dungeon", "TrophySkeleton" },
            { "copper", "CopperOre" },
            { "tin", "TinOre" },
            { "silver", "SilverOre" },
            { "ship", "Karve" },
            { "tree", "FineWood" },
        };

        private static readonly Dictionary<string, string> PlaceNames = new Dictionary<string, string>
        {
            { "Vendor_BlackForest", "Haldor" },
            { "Hildir_camp", "Hildir" },
            { "BogWitch_Camp", "Bog Witch" },
        };

        private static readonly Color32 MarkerColor = new Color32(170, 45, 25, 255);

        private static readonly Dictionary<string, Kind> Found = new Dictionary<string, Kind>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static List<Kind> _palette;
        private static string _paletteSource;

        /// <summary>The player's palette, in their order; names with no art are left out (and logged once).</summary>
        public static List<Kind> Palette()
        {
            string source = KitConfig.StampPalette.Value ?? "";
            if (_palette != null && _paletteSource == source)
            {
                return _palette;
            }
            _paletteSource = source;
            _palette = new List<Kind>();
            foreach (string part in source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Kind kind = Get(part.Trim());
                if (kind != null && !_palette.Contains(kind))
                {
                    _palette.Add(kind);
                }
            }
            return _palette;
        }

        public static Kind Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            if (OldIds.TryGetValue(id, out string renamed))
            {
                id = renamed;
            }
            if (Found.TryGetValue(id, out Kind kind))
            {
                return kind;
            }
            if (Missing.Contains(id))
            {
                return null;
            }
            kind = Find(id);
            if (kind == null)
            {
                // Only remembered once the game's data is loaded, so a lookup from the main menu can try again later.
                if (ObjectDB.instance != null && ZNetScene.instance != null && Minimap.instance != null)
                {
                    Missing.Add(id);
                    Plugin.Log.LogWarning($"No icon found for stamp \"{id}\" (not an item, piece or location with a map icon).");
                }
                return null;
            }
            Found[id] = kind;
            return kind;
        }

        private static Kind Find(string id)
        {
            switch (id)
            {
                case "mark_x":
                    return new Kind(id, "Cross", Marker(DrawCross), false);
                case "mark_circle":
                    return new Kind(id, "Circle", Marker(DrawRing), false);
                case "mark_dot":
                    return new Kind(id, "Dot", Marker(DrawDot), false);
            }
            if (Minimap.instance != null)
            {
                foreach (Minimap.LocationSpriteData icon in Minimap.instance.m_locationIcons)
                {
                    if (icon.m_name == id && icon.m_icon != null)
                    {
                        return new Kind(id, PlaceNames.TryGetValue(id, out string place) ? place : id, icon.m_icon, true);
                    }
                }
            }
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(id) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (drop != null && drop.m_itemData.GetIcon() != null)
            {
                return new Kind(id, Localized(drop.m_itemData.m_shared.m_name, id), drop.m_itemData.GetIcon(), true);
            }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(id) : null;
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (piece != null && piece.m_icon != null)
            {
                return new Kind(id, Localized(piece.m_name, id), piece.m_icon, true);
            }
            return null;
        }

        private static string Localized(string token, string fallback)
        {
            string text = Localization.instance != null ? Localization.instance.Localize(token) : token;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        // ---- Markers drawn in code: red ochre, with soft edges ----

        private const int MarkerSize = 64;

        private static Sprite Marker(Func<float, float, float> coverage)
        {
            var texture = new Texture2D(MarkerSize, MarkerSize, TextureFormat.RGBA32, false) { name = "IM_Marker", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[MarkerSize * MarkerSize];
            for (int y = 0; y < MarkerSize; y++)
            {
                for (int x = 0; x < MarkerSize; x++)
                {
                    // Centred coordinates from -1 to 1.
                    float u = (x + 0.5f) / MarkerSize * 2f - 1f;
                    float v = (y + 0.5f) / MarkerSize * 2f - 1f;
                    Color32 color = MarkerColor;
                    color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage(u, v)) * 255f);
                    pixels[y * MarkerSize + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, MarkerSize, MarkerSize), new Vector2(0.5f, 0.5f), 100f);
        }

        // Coverage from a distance to an edge, about one pixel of soft edge.
        private static float Edge(float inside)
        {
            return Mathf.Clamp01(inside * MarkerSize * 0.5f + 0.5f);
        }

        private static float DrawCross(float u, float v)
        {
            const float half = 0.13f;
            float a = Mathf.Abs(u - v) / Mathf.Sqrt(2f);
            float b = Mathf.Abs(u + v) / Mathf.Sqrt(2f);
            float reach = 0.8f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            return Mathf.Min(Edge(half - Mathf.Min(a, b)), Edge(reach));
        }

        private static float DrawRing(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Edge(0.12f - Mathf.Abs(r - 0.68f));
        }

        private static float DrawDot(float u, float v)
        {
            return Edge(0.42f - Mathf.Sqrt(u * u + v * v));
        }
    }
}
