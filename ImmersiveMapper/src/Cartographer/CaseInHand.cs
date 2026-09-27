using Jotunn.Managers;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Runs the map case for the local player each frame: the tally, taking the case out and putting it away with the map
    /// key, the reading view while it's held, and the drawing view on right-click.
    /// </summary>
    internal static class CaseInHand
    {
        private static CaseSession _session;
        private static ReadingView _reading;
        private static FieldView _drawing;
        private static bool _closeDrawing;
        private static bool _promptWasOpen;

        public static bool IsDrawing => _drawing != null && _drawing.Alive && _drawing.IsOpen;

        public static void Update()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Switch(null);
                return;
            }
            Tally.Update(player);

            ItemDrop.ItemData held = MapCaseItem.Held(player);
            if (held != _session?.Item)
            {
                Switch(held);
            }
            // At the table the case isn't read on the move; the table view has it.
            if (TableScreen.IsOpen)
            {
                if (_reading != null && _reading.Alive)
                {
                    _reading.SetVisible(false);
                }
                return;
            }
            if (_session == null)
            {
                if (MapKeyPressed() && player.TakeInput())
                {
                    ItemDrop.ItemData item = MapCaseItem.Active(player);
                    if (item != null)
                    {
                        player.UseItem(null, item, true);
                    }
                }
                return;
            }
            if (!EnsureViews())
            {
                return;
            }

            if (IsDrawing)
            {
                _drawing.Update();
                // Esc or right-click closes, but not the keypress that just closed a text prompt.
                bool prompt = TextPrompt.Open || _promptWasOpen;
                if (!prompt && (ZInput.GetKeyDown(KeyCode.Escape) || DrawPressed() || MapKeyPressed()))
                {
                    _closeDrawing = true;
                }
                return;
            }

            _reading.SetVisible(true);
            _reading.Update(Tally.Paces);
            if (!player.TakeInput())
            {
                return;
            }
            if (MapKeyPressed())
            {
                player.UseItem(null, _session.Item, true);
            }
            else if (DrawPressed())
            {
                OpenDrawing(player);
            }
            else if (ZInput.GetButtonDown(KitKeys.FlipSheet.Name))
            {
                _session.Flip();
                _reading.Show(_session);
            }
            else if (ZInput.GetButtonDown(KitKeys.EndLeg.Name))
            {
                AskEndLeg();
            }
        }

        public static void LateUpdate()
        {
            // Closed after every Update ran, so the Esc that closed the drawing can't also open the game menu this frame.
            if (_closeDrawing)
            {
                _closeDrawing = false;
                CloseDrawing();
            }
            _promptWasOpen = TextPrompt.Open;
        }

        /// <summary>Closes the field drawing view, if it's open (the table is about to take over).</summary>
        public static void PutDownQuill()
        {
            CloseDrawing();
        }

        /// <summary>Reads the held case again, after something else (the table) changed it.</summary>
        public static void Reload()
        {
            if (_session != null)
            {
                Switch(_session.Item);
            }
        }

        private static void Switch(ItemDrop.ItemData held)
        {
            CloseDrawing();
            _session = held != null ? new CaseSession(held) : null;
            if (_reading != null && _reading.Alive)
            {
                if (_session != null)
                {
                    _reading.Show(_session);
                }
                _reading.SetVisible(false);
            }
        }

        private static bool EnsureViews()
        {
            GameObject canvas = GUIManager.CustomGUIBack;
            if (canvas == null)
            {
                return false;
            }
            if (_reading == null || !_reading.Alive)
            {
                _reading = new ReadingView(canvas.transform);
                _reading.Show(_session);
            }
            if (_drawing == null || !_drawing.Alive)
            {
                _drawing = new FieldView(canvas.transform);
                _drawing.OnEndLeg = AskEndLeg;
                _drawing.OnDone = () => _closeDrawing = true;
            }
            return true;
        }

        private static void OpenDrawing(Player player)
        {
            player.m_autoRun = false;
            _reading.SetVisible(false);
            _drawing.Open(_session);
            GUIManager.BlockInput(true);
            Ui.HideHud(true);
        }

        private static void CloseDrawing()
        {
            if (!IsDrawing)
            {
                return;
            }
            _drawing.Close();
            GUIManager.BlockInput(false);
            Ui.HideHud(false);
            if (_session != null && _reading != null && _reading.Alive)
            {
                _reading.Show(_session);
            }
        }

        private static void AskEndLeg()
        {
            CaseSession session = _session;
            if (session == null)
            {
                return;
            }
            TextPrompt.Ask($"End leg {session.Journal.NextNumber}: {Tally.Paces} paces. Label it:", "", 60, label =>
            {
                if (_session != session)
                {
                    return;
                }
                var leg = new Leg
                {
                    Number = session.Journal.NextNumber,
                    Paces = Tally.TakeLeg(),
                    Label = label,
                    Day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0,
                };
                session.Journal.Add(leg);
                session.SaveJournal();
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Leg {leg.Number} noted: {leg.Paces} paces");
                if (IsDrawing)
                {
                    _drawing.RefreshJournal();
                }
            });
        }

        private static bool MapKeyPressed()
        {
            return KitConfig.MapKeyTakesOut.Value && Game.m_noMap && (ZInput.GetButtonDown(KitKeys.Map) || ZInput.GetButtonDown("JoyMap"));
        }

        private static bool DrawPressed()
        {
            return ZInput.GetButtonDown(KitKeys.Draw) || ZInput.GetButtonDown("JoyBlock");
        }
    }
}
