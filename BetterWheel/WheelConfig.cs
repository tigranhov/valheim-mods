using BepInEx.Configuration;

namespace BetterWheel
{
    /// <summary>What the wheel key (G) opens.</summary>
    public enum MainWheel
    {
        /// <summary>Straight into all your items; hover menus (fermenter, item stand, ...) and the emote key work as before.</summary>
        AllItems,
        /// <summary>The game's category wheel.</summary>
        Categories,
    }

    /// <summary>What happens after you use something from the wheel.</summary>
    public enum KeepOpen
    {
        /// <summary>The wheel stays open for the next pick until you close it.</summary>
        UntilClosed,
        /// <summary>As the game does it: gear and food keep it open, other things close it.</summary>
        Vanilla,
    }

    /// <summary>
    /// Every setting is the player's own: the wheel is a menu on your screen, nothing is sent to other players.
    /// </summary>
    public static class WheelConfig
    {
        public const int WheelSlots = 4;

        /// <summary>Item types that count as gear: weapons, shields, tools, armour, utility and trinkets, ammo.</summary>
        public const string GearTypes =
            "OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Shield, Torch, Tool, Attach_Atgeir, Ammo, "
            + "Helmet, Chest, Legs, Shoulder, Hands, Utility, Trinket";

        public static ConfigEntry<MainWheel> Main;
        public static ConfigEntry<KeepOpen> AfterUse;
        public static readonly ConfigEntry<string>[] Names = new ConfigEntry<string>[WheelSlots];
        public static readonly ConfigEntry<KeyboardShortcut>[] Keys = new ConfigEntry<KeyboardShortcut>[WheelSlots];
        public static readonly ConfigEntry<string>[] Items = new ConfigEntry<string>[WheelSlots];

        public static void Bind(ConfigFile cfg)
        {
            Main = cfg.Bind("1 - General", "WheelKeyOpens", MainWheel.AllItems,
                "What the wheel key (G) opens. AllItems: straight into all your items, no category wheel; looking at a fermenter, "
                + "item stand and the like still opens the items it takes, and the emote key still opens emotes. Categories: the "
                + "game's category wheel.");
            AfterUse = cfg.Bind("1 - General", "AfterUse", KeepOpen.UntilClosed,
                "After you use something from a wheel. UntilClosed: the wheel stays open for the next pick until you close it "
                + "(its key again, Esc or right-click). Vanilla: gear and food keep it open, other things close it. The game's "
                + "Release to use setting always closes it.");

            // Extra wheels start without a key: bind the ones you want.
            string[] names = { "Food & gear", "Food", "Gear", "" };
            string[] items = { "Consumable, " + GearTypes, "Consumable", GearTypes, "" };
            for (int i = 0; i < WheelSlots; i++)
            {
                string section = $"{i + 2} - Wheel {i + 1}";
                Names[i] = cfg.Bind(section, "Name", names[i], "Shown in the middle of this wheel.");
                Keys[i] = cfg.Bind(section, "Key", KeyboardShortcut.Empty,
                    "Opens this wheel (press again to close). Any key, with modifiers if you like, or a side mouse button (Mouse3, Mouse4). "
                    + "Empty: no key, the wheel is off.");
                Items[i] = cfg.Bind(section, "ItemTypes", items[i],
                    "Item types on this wheel, separated by commas: Consumable (food, meads, potions), OneHandedWeapon, "
                    + "TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Shield, Torch, Tool, Attach_Atgeir, Ammo, Helmet, Chest, Legs, "
                    + "Shoulder, Hands, Utility, Trinket, Material, Trophy, Fish, Misc.");
            }
        }
    }
}
