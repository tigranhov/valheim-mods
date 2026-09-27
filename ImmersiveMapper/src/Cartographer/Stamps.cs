using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// The stamps a player can press onto a sheet: the game's own icons in their own colours, found by name (an item,
    /// a piece, or a location that has a map icon, like the traders' camps), the vanilla map's pin icons (white art,
    /// shown in red ochre with a dark outline), and a few red markers drawn in code.
    /// A stamp is saved by that name, so any player sees it even if their palette doesn't list it; the palette itself
    /// is each player's own list in the config.
    /// </summary>
    internal static class Stamps
    {
        public const string DefaultPalette =
            "mark_x, mark_circle, mark_dot, pin_home, pin_fire, pin_hammer, pin_dot, pin_rune, pin_death, pin_bed, pin_boss, "
            + "bed, fire_pit, piece_workbench, Karve, "
            + "Vendor_BlackForest, Hildir_camp, BogWitch_Camp, "
            + "TrophyEikthyr, TrophyTheElder, TrophyBonemass, TrophyDragonQueen, TrophyGoblinKing, TrophySeekerQueen, TrophyFader, "
            + "TrophySkeleton, TrophyDraugr, TrophyFrostTroll, TrophyGoblin, TrophyCultist, TrophyWolf, TrophySerpent, TrophyDeathsquito, "
            + "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, Obsidian, Tar, Crystal, "
            + "Raspberry, Blueberries, Cloudberry, MushroomYellow, Thistle, Flax, Barley, FineWood, ElderBark, YggdrasilWood, "
            + "Coins, SurtlingCore";

        /// <summary>The default before the map pins were added; a palette still set to it gets the new default.</summary>
        public const string FirstDefaultPalette =
            "mark_x, mark_circle, mark_dot, bed, fire_pit, piece_workbench, Karve, "
            + "Vendor_BlackForest, Hildir_camp, BogWitch_Camp, "
            + "TrophyEikthyr, TrophyTheElder, TrophyBonemass, TrophyDragonQueen, TrophyGoblinKing, TrophySeekerQueen, TrophyFader, "
            + "TrophySkeleton, TrophyDraugr, TrophyFrostTroll, TrophyGoblin, TrophyCultist, TrophyWolf, TrophySerpent, TrophyDeathsquito, "
            + "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, Obsidian, Tar, Crystal, "
            + "Raspberry, Blueberries, Cloudberry, MushroomYellow, Thistle, Flax, Barley, FineWood, ElderBark, YggdrasilWood, "
            + "Coins, SurtlingCore";

        /// <summary>The vanilla map's pin icons, by stamp id.</summary>
        private static readonly Dictionary<string, (Minimap.PinType Type, string Name)> Pins = new Dictionary<string, (Minimap.PinType, string)>
        {
            { "pin_home", (Minimap.PinType.Icon1, "House") },
            { "pin_fire", (Minimap.PinType.Icon0, "Fire") },
            { "pin_hammer", (Minimap.PinType.Icon2, "Hammer") },
            { "pin_dot", (Minimap.PinType.Icon3, "Point") },
            { "pin_rune", (Minimap.PinType.Icon4, "Rune") },
            { "pin_death", (Minimap.PinType.Death, "Skull") },
            { "pin_bed", (Minimap.PinType.Bed, "Bed") },
            { "pin_boss", (Minimap.PinType.Boss, "Boss") },
        };

        public sealed class Kind
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Sprite Sprite;
            /// <summary>Colour the art is shown in: the game's icons are dimmed a little so they sit on the parchment.</summary>
            public readonly Color Tint;
            /// <summary>Drawn with a thin dark outline (the map pins, so white art reads on parchment).</summary>
            public readonly bool Outlined;

            public Kind(string id, string name, Sprite sprite, Color tint, bool outlined = false)
            {
                Id = id;
                Name = name;
                Sprite = sprite;
                Tint = tint;
                Outlined = outlined;
            }
        }

        public static readonly Color OutlineColor = new Color(0.16f, 0.1f, 0.06f, 0.85f);
        private static readonly Color GameIconTint = new Color(0.9f, 0.87f, 0.82f, 1f);

        // The first test builds saved these ids; they now show the matching map pin or game icon.
        private static readonly Dictionary<string, string> OldIds = new Dictionary<string, string>
        {
            { "home", "pin_home" },
            { "camp", "pin_fire" },
            { "point", "pin_dot" },
            { "work", "pin_hammer" },
            { "rune", "pin_rune" },
            { "danger", "pin_death" },
            { "boss", "pin_boss" },
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
                    return new Kind(id, "Cross", Marker(DrawCross), Color.white);
                case "mark_circle":
                    return new Kind(id, "Circle", Marker(DrawRing), Color.white);
                case "mark_dot":
                    return new Kind(id, "Dot", Marker(DrawDot), Color.white);
            }
            if (Pins.TryGetValue(id, out var pin))
            {
                Sprite art = Minimap.instance != null ? Minimap.instance.GetSprite(pin.Type) : null;
                return art != null ? new Kind(id, pin.Name, art, MarkerColor, true) : null;
            }
            if (Minimap.instance != null)
            {
                foreach (Minimap.LocationSpriteData icon in Minimap.instance.m_locationIcons)
                {
                    if (icon.m_name == id && icon.m_icon != null)
                    {
                        return new Kind(id, PlaceNames.TryGetValue(id, out string place) ? place : id, icon.m_icon, GameIconTint);
                    }
                }
            }
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(id) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (drop != null && drop.m_itemData.GetIcon() != null)
            {
                return new Kind(id, Localized(drop.m_itemData.m_shared.m_name, id), drop.m_itemData.GetIcon(), GameIconTint);
            }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(id) : null;
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (piece != null && piece.m_icon != null)
            {
                return new Kind(id, Localized(piece.m_name, id), piece.m_icon, GameIconTint);
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
