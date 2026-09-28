using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace Cartographer
{
    /// <summary>
    /// The map case's own keys (each player's choice, not synced) and the key hints shown while it's held.
    /// The small map uses the attack button (left mouse), drawing the block button (right mouse) and putting the case
    /// away the map button, all vanilla bindings.
    /// </summary>
    internal static class KitKeys
    {
        public const string Draw = "Block";
        public const string Look = "Attack";
        public const string Map = "Map";

        public static ButtonConfig FlipSheet;
        public static ButtonConfig EndLeg;
        public static ButtonConfig ResetTally;

        public static void Register(ConfigFile cfg)
        {
            ConfigEntry<KeyCode> flip = cfg.Bind("5 - Keys", "FlipSheet", KeyCode.L, "With the map case out: show the next sheet.");
            // The first builds used F, which is also the game's guardian power key.
            if (flip.Value == KeyCode.F)
            {
                flip.Value = KeyCode.L;
            }
            FlipSheet = new ButtonConfig
            {
                Name = "IM_MapFlipSheet",
                Config = flip,
                Hint = "Next sheet",
            };
            EndLeg = new ButtonConfig
            {
                Name = "IM_MapEndLeg",
                Config = cfg.Bind("5 - Keys", "EndLeg", KeyCode.J, "With the map case out: note the tally in the journal and start a new leg."),
                Hint = "End leg",
            };
            ResetTally = new ButtonConfig
            {
                Name = "IM_MapResetTally",
                Config = cfg.Bind("5 - Keys", "ResetTally", KeyCode.K, "With the map case out: set the tally back to 0 without noting a leg."),
                Hint = "Reset tally",
            };
            InputManager.Instance.AddButton(Plugin.PluginGuid, FlipSheet);
            InputManager.Instance.AddButton(Plugin.PluginGuid, EndLeg);
            InputManager.Instance.AddButton(Plugin.PluginGuid, ResetTally);

            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig
            {
                Item = MapCaseSetup.ItemName,
                ButtonConfigs = new[]
                {
                    new ButtonConfig { Name = Look, Hint = "Small map" },
                    new ButtonConfig { Name = Draw, Hint = "Draw" },
                    FlipSheet,
                    EndLeg,
                    ResetTally,
                    new ButtonConfig { Name = Map, Hint = "Put away" },
                },
            });
        }
    }
}
