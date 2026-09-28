using System.Globalization;

namespace Cartographer
{
    /// <summary>The tally's running count for the current leg.</summary>
    internal struct TallyState
    {
        public float Meters;
        public int Steps;
        /// <summary>This leg's stride error in Distance mode: 1 is exact, 0 means not chosen yet.</summary>
        public float Stride;
    }

    /// <summary>
    /// Everything a map case holds lives in its item's custom data, one key per part, so saving one sheet doesn't rewrite
    /// the others. The case travels with all of it: into a chest, onto a tombstone, into a friend's hands.
    /// </summary>
    internal static class MapCaseItem
    {
        private const string DraftKey = "IM_Map_Draft";
        private const string JournalKey = "IM_Map_Journal";
        private const string TallyKey = "IM_Map_Tally";
        private const string PageKey = "IM_Map_Page";
        private const string MasterKey = "IM_Map_Master";
        private const string UpgradesKey = "IM_Map_Upgrades";
        private const string SheetUpgradePrefix = "sheet_";

        public static bool IsCase(ItemDrop.ItemData item)
        {
            // By name, not shared-data reference: a spawned item carries its own copy of the shared data. The display name
            // is a plain field and runs every frame; the prefab name (a Unity call that allocates) only confirms a match.
            if (item == null || item.m_shared.m_name != MapCaseSetup.DisplayName)
            {
                return false;
            }
            return item.m_dropPrefab == null || ReferenceEquals(item.m_dropPrefab, MapCaseSetup.Prefab) || item.m_dropPrefab.name == MapCaseSetup.ItemName;
        }

        public static ItemDrop.ItemData Held(Player player)
        {
            ItemDrop.ItemData right = player != null ? player.GetRightItem() : null;
            return IsCase(right) ? right : null;
        }

        /// <summary>The case that counts paces: the one in hand, else the first one in the inventory.</summary>
        public static ItemDrop.ItemData Active(Player player)
        {
            ItemDrop.ItemData held = Held(player);
            if (held != null || player == null)
            {
                return held;
            }
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (IsCase(item))
                {
                    return item;
                }
            }
            return null;
        }

        public static Sheet LoadDraft(ItemDrop.ItemData item, int index)
        {
            item.m_customData.TryGetValue(DraftKey + index, out string text);
            return Sheet.Decode(text);
        }

        public static void SaveDraft(ItemDrop.ItemData item, int index, Sheet sheet)
        {
            if (!sheet.Unreadable)
            {
                item.m_customData[DraftKey + index] = sheet.Encode();
            }
        }

        /// <summary>Draft sheets in this case: the base count plus one per sheet part fitted.</summary>
        public static int SheetCount(ItemDrop.ItemData item)
        {
            int count = KitConfig.DraftSheets.Value;
            foreach (string upgrade in Upgrades(item))
            {
                if (upgrade.StartsWith(SheetUpgradePrefix))
                {
                    count++;
                }
            }
            return count;
        }

        public static bool HasUpgrade(ItemDrop.ItemData item, string key)
        {
            return System.Array.IndexOf(Upgrades(item), key) >= 0;
        }

        public static void AddUpgrade(ItemDrop.ItemData item, string key)
        {
            if (!HasUpgrade(item, key))
            {
                item.m_customData.TryGetValue(UpgradesKey, out string list);
                item.m_customData[UpgradesKey] = string.IsNullOrEmpty(list) ? key : list + "," + key;
            }
        }

        private static string[] Upgrades(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(UpgradesKey, out string list) && !string.IsNullOrEmpty(list)
                ? list.Split(',') : System.Array.Empty<string>();
        }

        public static void WipeDraft(ItemDrop.ItemData item, int index)
        {
            item.m_customData.Remove(DraftKey + index);
        }

        /// <summary>The copy of a table's master the case carries, or null when it has none yet.</summary>
        public static Sheet LoadMaster(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(MasterKey, out string text) && !string.IsNullOrEmpty(text) ? Sheet.Decode(text) : null;
        }

        public static byte[] LoadMasterBytes(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(MasterKey, out string text) && !string.IsNullOrEmpty(text) ? System.Convert.FromBase64String(text) : null;
        }

        public static void SaveMasterBytes(ItemDrop.ItemData item, byte[] bytes)
        {
            item.m_customData[MasterKey] = System.Convert.ToBase64String(bytes);
        }

        public static Journal LoadJournal(ItemDrop.ItemData item)
        {
            item.m_customData.TryGetValue(JournalKey, out string text);
            return Journal.Decode(text);
        }

        public static void SaveJournal(ItemDrop.ItemData item, Journal journal)
        {
            if (!journal.Unreadable)
            {
                item.m_customData[JournalKey] = journal.Encode();
            }
        }

        /// <summary>The sheet that was showing when the case was last put away.</summary>
        public static int LoadPage(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(PageKey, out string text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int page) ? page : 0;
        }

        public static void SavePage(ItemDrop.ItemData item, int page)
        {
            item.m_customData[PageKey] = page.ToString(CultureInfo.InvariantCulture);
        }

        public static TallyState LoadTally(ItemDrop.ItemData item)
        {
            var state = new TallyState();
            if (item.m_customData.TryGetValue(TallyKey, out string text))
            {
                string[] parts = text.Split('|');
                if (parts.Length == 3)
                {
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out state.Meters);
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out state.Steps);
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out state.Stride);
                }
            }
            return state;
        }

        public static void SaveTally(ItemDrop.ItemData item, TallyState state)
        {
            item.m_customData[TallyKey] = string.Join("|",
                state.Meters.ToString("0.##", CultureInfo.InvariantCulture),
                state.Steps.ToString(CultureInfo.InvariantCulture),
                state.Stride.ToString("0.####", CultureInfo.InvariantCulture));
        }
    }
}
