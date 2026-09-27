using Jotunn.Managers;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Opens and closes the table view for the local player: the game's input is blocked while working at the table, and
    /// leaving (Esc with nothing to stop, Done, dying, the table gone) saves and frees the table.
    /// </summary>
    internal static class TableScreen
    {
        private static TableView _view;
        private static bool _close;
        private static bool _promptWasOpen;

        public static bool IsOpen => _view != null && _view.Alive && _view.IsOpen;

        public static void Open(MapTable table)
        {
            Player player = Player.m_localPlayer;
            GameObject canvas = GUIManager.CustomGUIBack;
            if (player == null || canvas == null || IsOpen)
            {
                return;
            }
            if (_view == null || !_view.Alive)
            {
                _view = new TableView(canvas.transform) { OnDone = () => _close = true };
            }
            CaseInHand.PutDownQuill();
            ItemDrop.ItemData item = MapCaseItem.Active(player);
            player.m_autoRun = false;
            _view.Open(table, item != null ? new CaseSession(item) : null);
            GUIManager.BlockInput(true);
            Ui.HideHud(true);
        }

        public static void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            MapTable table = _view.Table;
            if (player == null || player.IsDead() || table == null || table.m_nview == null || !table.m_nview.IsValid())
            {
                Close();
                return;
            }
            _view.Update();
            bool prompt = TextPrompt.Open || _promptWasOpen;
            if (prompt)
            {
                return;
            }
            // Esc first stops placing a draft or laying strings; with nothing to stop, it leaves the table.
            if (ZInput.GetKeyDown(KeyCode.Escape) && !_view.Cancel())
            {
                _close = true;
            }
        }

        public static void LateUpdate()
        {
            // After every Update ran, so the Esc that closes the table can't also open the game menu this frame.
            if (_close)
            {
                _close = false;
                Close();
            }
            _promptWasOpen = TextPrompt.Open;
        }

        private static void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            _view.Close();
            GUIManager.BlockInput(false);
            Ui.HideHud(false);
            CaseInHand.Reload();
        }
    }
}
