using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>What the key does while you mine.</summary>
    public enum KeyMode
    {
        /// <summary>Following is on; hold the key to mine the base-game way.</summary>
        HoldToDisable,
        /// <summary>Following is off; hold the key to follow.</summary>
        HoldToEnable,
    }

    /// <summary>Where loot from broken chunks lands.</summary>
    public enum LootPlace
    {
        /// <summary>In front of you, so buried chunks don't leave their loot inside the ground.</summary>
        InFront,
        /// <summary>Where the chunk was, as in the base game.</summary>
        AtChunk,
    }

    /// <summary>
    /// Every setting is the player's own: the rock you mine is handled by your game, nothing is synced, and other
    /// players don't need the mod.
    /// </summary>
    public static class FollowConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> Key;
        public static ConfigEntry<KeyMode> Mode;
        public static ConfigEntry<LootPlace> Loot;
        public static ConfigEntry<bool> DigGround;

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("1 - General", "Enabled", true,
                "Mining a rock from one spot: the chunk you hit stays and each swing damages another chunk of the rock, the one "
                + "farthest from you first; a swing that breaks a chunk passes its spare damage on to the next, and the chunk you "
                + "hit goes last. Off: the base game.");
            Key = cfg.Bind("1 - General", "Key", new KeyboardShortcut(KeyCode.LeftAlt),
                "Held while swinging; what it does depends on KeyMode.");
            Mode = cfg.Bind("1 - General", "KeyMode", KeyMode.HoldToDisable,
                "HoldToDisable: following is on, hold the key to break the chunk you hit, as in the base game. "
                + "HoldToEnable: following is off, hold the key to follow.");
            Loot = cfg.Bind("2 - Mining", "Loot", LootPlace.InFront,
                "InFront: loot from broken chunks drops in front of you. AtChunk: where the chunk was, as in the base game (buried "
                + "chunks leave it in the ground).");
            DigGround = cfg.Bind("2 - Mining", "DigGround", false,
                "When a chunk below the ground breaks, dig out the ground above it, down to where the chunk was, like a pickaxe "
                + "does. Not inside wards or places where the game doesn't allow digging.");
        }

        /// <summary>Following is on for this swing: enabled, and the key held or not as KeyMode asks.</summary>
        public static bool Active()
        {
            if (!Enabled.Value)
            {
                return false;
            }
            bool held = KeyHeld(Key.Value);
            return Mode.Value == KeyMode.HoldToDisable ? !held : held;
        }

        // Read through the game's input (it runs on Unity's new input system).
        private static bool KeyHeld(KeyboardShortcut key)
        {
            return key.MainKey != KeyCode.None && Held(key.MainKey) && key.Modifiers.All(Held);
        }

        private static bool Held(KeyCode key)
        {
            return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 ? ZInput.GetMouseButton(key - KeyCode.Mouse0) : ZInput.GetKey(key, false);
        }
    }
}
