namespace ImmersiveMapper.Cartographer
{
    /// <summary>A map case taken out: its sheets and journal read once, and written back to the item after every change.</summary>
    internal sealed class CaseSession
    {
        public readonly ItemDrop.ItemData Item;
        public readonly Sheet[] Drafts;
        public readonly Journal Journal;
        public int Page;

        public CaseSession(ItemDrop.ItemData item)
        {
            Item = item;
            Drafts = new Sheet[KitConfig.DraftSheets.Value];
            for (int i = 0; i < Drafts.Length; i++)
            {
                Drafts[i] = MapCaseItem.LoadDraft(item, i);
            }
            Journal = MapCaseItem.LoadJournal(item);
            Page = UnityEngine.Mathf.Clamp(MapCaseItem.LoadPage(item), 0, Drafts.Length - 1);
        }

        public Sheet Current => Drafts[Page];

        public string PageTitle(int page)
        {
            return $"Sheet {page + 1} of {Drafts.Length}";
        }

        public void Flip()
        {
            ShowPage((Page + 1) % Drafts.Length);
        }

        public void ShowPage(int page)
        {
            Page = page;
            MapCaseItem.SavePage(Item, page);
        }

        public void SaveDraft(int page)
        {
            MapCaseItem.SaveDraft(Item, page, Drafts[page]);
        }

        public void SaveJournal()
        {
            MapCaseItem.SaveJournal(Item, Journal);
        }
    }
}
