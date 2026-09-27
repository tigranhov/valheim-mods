using BepInEx.Configuration;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>What Shift+E on a placed crate does.</summary>
    internal enum CarryMode
    {
        /// <summary>Pack it, contents and all, into one inventory item.</summary>
        Inventory,
        /// <summary>Lift it onto your back.</summary>
        Back,
        /// <summary>Hold it in front of you with both hands.</summary>
        Front,
    }

    /// <summary>All settings are admin-only and synced from the server, so every player's crates behave the same.</summary>
    internal static class CargoConfig
    {
        public static ConfigEntry<bool> CratesEnabled;
        public static ConfigEntry<int> CrateWidth;
        public static ConfigEntry<int> CrateHeight;
        public static ConfigEntry<float> CrateWeight;
        public static ConfigEntry<string> CrateRecipe;
        public static ConfigEntry<string> CrateStation;

        public static ConfigEntry<bool> StackLimitsEnabled;
        public static ConfigEntry<string> StackLimits;

        public static ConfigEntry<bool> WeightEnabled;
        public static ConfigEntry<string> ShipCapacities;
        public static ConfigEntry<float> WeightStrength;

        public static ConfigEntry<CarryMode> PickUpMode;
        public static ConfigEntry<bool> CarryEncumbers;

        public static void Bind(ConfigFile cfg)
        {
            CratesEnabled = cfg.Bind("1 - Cargo crates", "Enabled", true,
                Synced("Cargo crates can be crafted. Turning this off only hides the recipe: crates already placed or carried keep working, so nothing is lost."));
            CrateWidth = cfg.Bind("1 - Cargo crates", "Width", 5,
                Synced("Slots per row inside a crate. Applies after a restart.", new AcceptableValueRange<int>(1, 8)));
            CrateHeight = cfg.Bind("1 - Cargo crates", "Height", 2,
                Synced("Rows of slots inside a crate. Applies after a restart.", new AcceptableValueRange<int>(1, 6)));
            CrateWeight = cfg.Bind("1 - Cargo crates", "EmptyWeight", 10f,
                Synced("Weight of the crate itself. A packed crate weighs this plus everything inside. Applies after a restart.", new AcceptableValueRange<float>(0f, 100f)));
            CrateRecipe = cfg.Bind("1 - Cargo crates", "Recipe", "Wood:10,BronzeNails:4",
                Synced("Ingredients as Item:Amount pairs separated by commas. Applies after a restart."));
            CrateStation = cfg.Bind("1 - Cargo crates", "CraftingStation", "piece_workbench",
                Synced("Where crates are crafted (piece_workbench, forge, ...). Applies after a restart."));

            StackLimitsEnabled = cfg.Bind("2 - Stacking", "Enabled", true,
                Synced("Limit how many crates high you can stack."));
            StackLimits = cfg.Bind("2 - Stacking", "Levels", "Raft=1, Karve=1, VikingShip=2, VikingShip_Ashlands=3, OtherShips=2, Ground=0",
                Synced("Crates-high limit per ship type (prefab name: Karve, VikingShip = longship, VikingShip_Ashlands = drakkar). "
                    + "OtherShips covers modded ships, Ground is on land. 0 = no limit."));

            WeightEnabled = cfg.Bind("3 - Cargo weight", "Enabled", true,
                Synced("Loaded crates press their ship down where they stand: pile them at the bow and the bow dips, so balance the load."));
            ShipCapacities = cfg.Bind("3 - Cargo weight", "Capacity", "Raft=300, Karve=1200, VikingShip=3000, VikingShip_Ashlands=5000, OtherShips=2500",
                Synced("Cargo weight each ship type is built for. At full capacity the crates press down with a tenth of the ship's own weight: "
                    + "spread evenly the ship just sits a little lower, piled at one end it tips toward that end."));
            WeightStrength = cfg.Bind("3 - Cargo weight", "Strength", 1f,
                Synced("Multiplier for how hard cargo weight pushes on the ship.", new AcceptableValueRange<float>(0f, 5f)));

            PickUpMode = cfg.Bind("4 - Carrying", "PickUpMode", CarryMode.Front,
                Synced("What Shift+E on a placed crate does. Inventory: pack it into one item. Back / Front: carry the crate itself, "
                    + "one at a time, on your back or in your arms; click to set it down, right-click to put it down beside you."));
            CarryEncumbers = cfg.Bind("4 - Carrying", "Encumbers", true,
                Synced("Carrying a crate counts as being encumbered: slow walk, stamina drain, no running. Off: it only looks that way."));
        }

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null)
        {
            return new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
