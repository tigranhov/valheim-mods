using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// A map case in use: its drafts, its copy of a table's master and its journal, read once and written back to the
    /// item after every change. Page -1 is the master copy (when the case has one), 0 and up are the drafts.
    /// </summary>
    internal sealed class CaseSession
    {
        public const int MasterPage = -1;

        public readonly ItemDrop.ItemData Item;
        public readonly Sheet[] Drafts;
        public readonly Sheet Master;
        public readonly Journal Journal;
        public int Page;

        /// <summary>Where the master copy was last looked at, so reading on the move shows the same part.</summary>
        public float MasterZoom = 1f;
        public Vector2 MasterCenter = new Vector2(Sheet.Aspect * 0.5f, 0.5f);
        public bool MasterViewSet;

        public CaseSession(ItemDrop.ItemData item)
        {
            Item = item;
            Drafts = new Sheet[KitConfig.DraftSheets.Value];
            for (int i = 0; i < Drafts.Length; i++)
            {
                Drafts[i] = MapCaseItem.LoadDraft(item, i);
            }
            Master = MapCaseItem.LoadMaster(item);
            Journal = MapCaseItem.LoadJournal(item);
            Page = Mathf.Clamp(MapCaseItem.LoadPage(item), HasMaster ? MasterPage : 0, Drafts.Length - 1);
        }

        public bool HasMaster => Master != null;

        public bool OnMaster => Page == MasterPage;

        public Sheet Current => OnMaster ? Master : Drafts[Page];

        public string PageTitle(int page)
        {
            return page == MasterPage ? "Master copy" : $"Sheet {page + 1} of {Drafts.Length}";
        }

        public void Flip()
        {
            int next = Page + 1;
            if (next >= Drafts.Length)
            {
                next = HasMaster ? MasterPage : 0;
            }
            ShowPage(next);
        }

        public void ShowPage(int page)
        {
            Page = page;
            MapCaseItem.SavePage(Item, page);
        }

        public void SaveDraft(int page)
        {
            if (page >= 0 && page < Drafts.Length)
            {
                MapCaseItem.SaveDraft(Item, page, Drafts[page]);
            }
        }

        public void WipeDraft(int page)
        {
            Drafts[page] = new Sheet();
            MapCaseItem.WipeDraft(Item, page);
        }

        public void SaveJournal()
        {
            MapCaseItem.SaveJournal(Item, Journal);
        }
    }
}
