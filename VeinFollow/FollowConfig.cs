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
        public static ConfigEntry<float> Reach;

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("1 - General", "Enabled", true,
                "Mining a rock carries on through its chunks: a swing that breaks a chunk passes its leftover damage on to the "
                + "nearest remaining chunk, and swinging at the ground over buried chunks mines them. Off: the base game.");
            Key = cfg.Bind("1 - General", "Key", new KeyboardShortcut(KeyCode.LeftAlt),
                "Held while swinging; what it does depends on KeyMode.");
            Mode = cfg.Bind("1 - General", "KeyMode", KeyMode.HoldToDisable,
                "HoldToDisable: following is on, hold the key to mine the base-game way (to dig near a rock, say). "
                + "HoldToEnable: following is off, hold the key to follow.");
            Loot = cfg.Bind("2 - Mining", "Loot", LootPlace.InFront,
                "InFront: loot from broken chunks drops in front of you. AtChunk: where the chunk was, as in the base game (buried "
                + "chunks leave it in the ground).");
            DigGround = cfg.Bind("2 - Mining", "DigGround", false,
                "When a chunk below the ground breaks, dig out the ground above it, down to where the chunk was, like a pickaxe "
                + "does. Not inside wards or places where the game doesn't allow digging.");
            Reach = cfg.Bind("2 - Mining", "Reach", 2f,
                new ConfigDescription("Metres from where your pickaxe hits the ground within which a buried chunk is mined instead "
                    + "of digging the ground.", new AcceptableValueRange<float>(0.5f, 5f)));
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
