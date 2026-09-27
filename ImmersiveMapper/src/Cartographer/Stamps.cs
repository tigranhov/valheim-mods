using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// The stamps a player can press onto a sheet, borrowed from the game's own art: the map's pin icons (white, so they
    /// take the ink colour) and a few item and piece icons (in their own colours). Saved by id, so the list can grow.
    /// </summary>
    internal static class Stamps
    {
        public sealed class Kind
        {
            public readonly string Id;
            public readonly string Name;
            /// <summary>White art, tinted like ink; otherwise shown in its own colours.</summary>
            public readonly bool Tinted;
            private readonly Func<Sprite> _find;
            private Sprite _sprite;

            public Kind(string id, string name, bool tinted, Func<Sprite> find)
            {
                Id = id;
                Name = name;
                Tinted = tinted;
                _find = find;
            }

            public Sprite Sprite
            {
                get
                {
                    if (_sprite == null)
                    {
                        try
                        {
                            _sprite = _find();
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogWarning($"No art for stamp {Id}: {e.Message}");
                        }
                    }
                    return _sprite;
                }
            }
        }

        public static readonly Kind[] All =
        {
            new Kind("home", "Home", true, () => PinIcon(Minimap.PinType.Icon1)),
            new Kind("camp", "Camp", true, () => PinIcon(Minimap.PinType.Icon0)),
            new Kind("point", "Point", true, () => PinIcon(Minimap.PinType.Icon3)),
            new Kind("work", "Work", true, () => PinIcon(Minimap.PinType.Icon2)),
            new Kind("rune", "Runestone", true, () => PinIcon(Minimap.PinType.Icon4)),
            new Kind("danger", "Danger", true, () => PinIcon(Minimap.PinType.Death)),
            new Kind("boss", "Boss", true, () => PinIcon(Minimap.PinType.Boss)),
            new Kind("trader", "Trader", false, () => LocationIcon("Vendor_BlackForest")),
            new Kind("hildir", "Hildir", false, () => LocationIcon("Hildir_camp")),
            new Kind("bogwitch", "Bog Witch", false, () => LocationIcon("BogWitch_Camp")),
            new Kind("dungeon", "Dungeon", false, () => ItemIcon("TrophySkeleton")),
            new Kind("copper", "Copper", false, () => ItemIcon("CopperOre")),
            new Kind("tin", "Tin", false, () => ItemIcon("TinOre")),
            new Kind("silver", "Silver", false, () => ItemIcon("SilverOre")),
            new Kind("ship", "Harbour", false, () => PieceIcon("Karve")),
            new Kind("tree", "Great tree", false, () => ItemIcon("FineWood")),
        };

        private static Dictionary<string, Kind> _byId;

        public static Kind Get(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, Kind>();
                foreach (Kind kind in All)
                {
                    _byId[kind.Id] = kind;
                }
            }
            return id != null && _byId.TryGetValue(id, out Kind found) ? found : null;
        }

        private static Sprite PinIcon(Minimap.PinType type)
        {
            return Minimap.instance != null ? Minimap.instance.GetSprite(type) : null;
        }

        private static Sprite LocationIcon(string name)
        {
            if (Minimap.instance == null)
            {
                return null;
            }
            foreach (Minimap.LocationSpriteData icon in Minimap.instance.m_locationIcons)
            {
                if (icon.m_name == name)
                {
                    return icon.m_icon;
                }
            }
            return null;
        }

        private static Sprite ItemIcon(string name)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.GetIcon() : null;
        }

        private static Sprite PieceIcon(string name)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            return piece != null ? piece.m_icon : null;
        }
    }
}
