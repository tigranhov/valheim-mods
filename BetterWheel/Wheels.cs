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
        // The wheel's own "use what's selected", as a click does.
        private static readonly Action<RadialBase> Use =
            AccessTools.MethodDelegate<Action<RadialBase>>(AccessTools.Method(typeof(RadialBase), "OnInteract"));

        private static int _requested = -1;
        private static bool _releaseOverridden;
        private static bool _releaseUses;
        private static string _openButton;
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
                RestoreRelease();
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

        /// <summary>
        /// Release to use is handled here, not by the game: the game only counts a release after a fixed hold of its own,
        /// so a quick press, point and let go did nothing. Here the hold is the ReleaseHoldTime setting. The game's own
        /// flag stays off while a wheel is open (so it can't fire as well) and is put back when the wheel closes.
        /// </summary>
        public static void ApplyRelease(bool fromWheelKey)
        {
            ReleaseToUse setting = fromWheelKey && _open >= 0 ? WheelConfig.Release[_open].Value : WheelConfig.MainRelease.Value;
            _releaseUses = setting == ReleaseToUse.Game ? GameRelease() : setting == ReleaseToUse.On;
            // G, or T for emotes, opened this one (a gamepad's wheel button counts too).
            _openButton = fromWheelKey ? null : ZInput.GetButton("OpenEmote") ? "OpenEmote" : "OpenRadial";
            RadialData.SO.EnableReleaseToUseMode = false;
            _releaseOverridden = true;
        }

        private static void RestoreRelease()
        {
            if (_releaseOverridden && RadialData.SO != null)
            {
                RadialData.SO.EnableReleaseToUseMode = GameRelease();
                _releaseOverridden = false;
            }
        }

        // The game's own setting, as it loads it.
        private static bool GameRelease()
        {
            return PlatformPrefs.GetInt("RadialReleaseToUse") != 0;
        }

        /// <summary>All your items: the game's own "all items" list, the one it spirals through.</summary>
        public static ItemGroupConfig AllItems()
        {
            ItemGroupConfig config = Object.Instantiate(RadialData.SO.ItemGroupConfig);
            config.GroupName = "allitems";
            return config;
        }

        /// <summary>
        /// For the wheel's close check, first thing each frame: the key that opened the wheel let go after a hold uses what's
        /// selected (release to use) or just closes. A quicker tap leaves the wheel open for clicking.
        /// </summary>
        public static bool Released(RadialBase radial)
        {
            if (!TryGetRelease(out float held) || held < WheelConfig.ReleaseHold.Value)
            {
                return false;
            }
            WheelLog.Write($"Let go after {held:0.00}s: {(_releaseUses ? "use" : "close")}, {WheelLog.State(radial)}");
            if (_releaseUses)
            {
                Use(radial);
            }
            return true;
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
                ApplyRelease(true);
                radial.QueuedOpen(next);
            }
            return false;
        }

        // The opening key let go this frame, and how long it was held.
        private static bool TryGetRelease(out float held)
        {
            held = 0f;
            if (_openButton != null)
            {
                if (ZInput.GetButtonUp(_openButton))
                {
                    held = ZInput.GetButtonLastPressedTimer(_openButton);
                    return true;
                }
                if (ZInput.GetButtonUp("JoyRadial"))
                {
                    held = ZInput.GetButtonLastPressedTimer("JoyRadial");
                    return true;
                }
                return false;
            }
            if (_open >= 0 && Time.frameCount > _openedFrame && KeyUp(WheelConfig.Keys[_open].Value))
            {
                held = Time.time - _pressedAt;
                return true;
            }
            return false;
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
