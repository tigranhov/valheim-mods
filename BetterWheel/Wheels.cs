using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Valheim.UI;
using Object = UnityEngine.Object;

namespace BetterWheel
{
    /// <summary>
    /// The extra wheels: each is an item list of chosen types behind its own key. A wheel key opens the game's wheel the
    /// usual way (so its setup runs as for G) and asks for that list; the same key closes it again, another wheel's key
    /// switches to that wheel.
    /// </summary>
    public static class Wheels
    {
        private static readonly Func<Player, bool> TakeInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        private static int _requested = -1;
        private static int _open = -1;
        private static int _openedFrame;
        private static float _pressedAt;

        public static void Update()
        {
            Player player = Player.m_localPlayer;
            Hud hud = Hud.instance;
            if (player == null || hud == null || hud.m_radialMenu == null || !Hud.InRadial())
            {
                _open = -1;
            }
            // While a wheel is open, its keys are read by the wheel's own close check (see WheelControlsPatch).
            if (player == null || hud == null || hud.m_radialMenu == null || Hud.InRadial())
            {
                return;
            }
            int pressed = PressedWheel();
            if (pressed < 0 || Create(pressed) == null || !CanOpen(player))
            {
                return;
            }
            _requested = pressed;
            _pressedAt = Time.time;
            RadialBase radial = hud.m_radialMenu;
            radial.CanOpen = true;
            radial.Open(hud.m_config);
            _requested = -1;
        }

        /// <summary>For the wheel's opening: the list a wheel key asked for, if one did.</summary>
        public static bool TryTakeRequest(out ItemGroupConfig config)
        {
            config = null;
            if (_requested < 0)
            {
                return false;
            }
            config = Create(_requested);
            _open = _requested;
            _openedFrame = Time.frameCount;
            _requested = -1;
            return config != null;
        }

        /// <summary>All your items: the game's own "all items" list, the one it spirals through.</summary>
        public static ItemGroupConfig AllItems()
        {
            ItemGroupConfig config = Object.Instantiate(RadialData.SO.ItemGroupConfig);
            config.GroupName = "allitems";
            return config;
        }

        /// <summary>For the wheel's close check: its key again closes it; another wheel's key switches to that wheel.</summary>
        public static bool CloseRequested(RadialBase radial)
        {
            if (_open < 0 || Time.frameCount <= _openedFrame)
            {
                return false;
            }
            int pressed = PressedWheel();
            if (pressed < 0)
            {
                return false;
            }
            if (pressed == _open)
            {
                return true;
            }
            ItemGroupConfig next = Create(pressed);
            if (next != null)
            {
                _open = pressed;
                _openedFrame = Time.frameCount;
                _pressedAt = Time.time;
                radial.QueuedOpen(next);
            }
            return false;
        }

        /// <summary>The game's Release to use setting, for wheel keys: hold the key, point, let go to use.</summary>
        public static bool ReleasedToUse()
        {
            if (_open < 0 || !RadialData.SO.EnableReleaseToUseMode)
            {
                return false;
            }
            return Time.time - _pressedAt > RadialData.SO.HoldCloseDelay && KeyUp(WheelConfig.Keys[_open].Value);
        }

        private static ItemGroupConfig Create(int slot)
        {
            ItemDrop.ItemData.ItemType[] types = ParseTypes(WheelConfig.Items[slot].Value);
            if (types.Length == 0)
            {
                return null;
            }
            ItemGroupConfig config = Object.Instantiate(RadialData.SO.ItemGroupConfig);
            string name = WheelConfig.Names[slot].Value;
            // A name the game doesn't know is shown as it is (like the game's own hover menus).
            config.GroupName = string.IsNullOrWhiteSpace(name) ? $"Wheel {slot + 1}" : name.Trim();
            config.ItemTypes = types;
            return config;
        }

        private static ItemDrop.ItemData.ItemType[] ParseTypes(string list)
        {
            var types = new List<ItemDrop.ItemData.ItemType>();
            foreach (string part in (list ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(part.Trim(), true, out ItemDrop.ItemData.ItemType type) && type != ItemDrop.ItemData.ItemType.None && !types.Contains(type))
                {
                    types.Add(type);
                }
            }
            return types.ToArray();
        }

        private static bool CanOpen(Player player)
        {
            return !player.IsDead() && TakeInput(player) && !player.InPlaceMode() && !Hud.IsPieceSelectionVisible();
        }

        private static int PressedWheel()
        {
            for (int i = 0; i < WheelConfig.WheelSlots; i++)
            {
                if (KeyDown(WheelConfig.Keys[i].Value))
                {
                    return i;
                }
            }
            return -1;
        }

        // Read through the game's input (it runs on Unity's new input system), with side mouse buttons as mouse buttons.
        private static bool KeyDown(KeyboardShortcut key)
        {
            return key.MainKey != KeyCode.None && Down(key.MainKey) && key.Modifiers.All(Held);
        }

        private static bool KeyUp(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None)
            {
                return false;
            }
            return IsMouse(key.MainKey, out int button) ? ZInput.GetMouseButtonUp(button) : ZInput.GetKeyUp(key.MainKey, false);
        }

        private static bool Down(KeyCode key)
        {
            return IsMouse(key, out int button) ? ZInput.GetMouseButtonDown(button) : ZInput.GetKeyDown(key, false);
        }

        private static bool Held(KeyCode key)
        {
            return IsMouse(key, out int button) ? ZInput.GetMouseButton(button) : ZInput.GetKey(key, false);
        }

        private static bool IsMouse(KeyCode key, out int button)
        {
            button = key - KeyCode.Mouse0;
            return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;
        }
    }
}
