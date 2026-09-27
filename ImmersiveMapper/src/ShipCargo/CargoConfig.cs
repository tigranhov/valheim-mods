using BepInEx.Configuration;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>What Shift+E on a placed crate does.</summary>
    internal enum CarryMode
    {
        /// <summary>Pack it, contents and all, into one inventory item.</summary>
        Inventory,
        /// <summary>Carry it in front of you with both hands.</summary>
        Front,
    }

    /// <summary>When carrying a crate makes you encumbered.</summary>
    internal enum CarryEncumbrance
    {
        /// <summary>When your inventory plus the crate and its contents weigh more than you can carry.</summary>
        Weight,
        /// <summary>Always, even for an empty crate.</summary>
        Always,
        /// <summary>Never; only the inventory counts, as usual.</summary>
        Never,
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
        public static ConfigEntry<CarryEncumbrance> Encumbrance;

        public static ConfigEntry<float> FrontScale;
        public static ConfigEntry<float> FrontHeight;
        public static ConfigEntry<float> FrontDistance;
        public static ConfigEntry<float> FrontTilt;
        public static ConfigEntry<float> BodyFollow;
        public static ConfigEntry<float> HandInset;
        public static ConfigEntry<float> HandHeight;
        public static ConfigEntry<float> HandForward;
        public static ConfigEntry<float> HandRotationWeight;
        public static ConfigEntry<float> HandPitch;
        public static ConfigEntry<float> HandYaw;
        public static ConfigEntry<float> HandRoll;
        public static ConfigEntry<float> ElbowWeight;
        public static ConfigEntry<float> ElbowOut;
        public static ConfigEntry<float> ElbowDown;

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
                Synced("What Shift+E on a placed crate does. Inventory: pack it into one item. Front: carry the crate itself in your "
                    + "arms, one at a time; click to set it down, right-click to put it down beside you."));
            Encumbrance = cfg.Bind("4 - Carrying", "Encumbrance", CarryEncumbrance.Weight,
                Synced("When carrying a crate makes you encumbered (the heavy walk, slow, stamina drain). Weight: when your inventory "
                    + "plus the crate and its contents weigh more than you can carry. Always: even for an empty crate. Never: only your "
                    + "inventory counts, as usual. Otherwise you walk normally, but you can never sprint with a crate."));

            // Live tuning for how a carried crate sits and moves; all read every frame. Defaults tuned in game 2026-09-27.
            const string tuning = "5 - Carry tuning";
            FrontScale = cfg.Bind(tuning, "FrontScale", 0.6f,
                Synced("Size of a carried crate, as a share of its full size (it's full size again when set down).", new AcceptableValueRange<float>(0.2f, 1f)));
            FrontHeight = cfg.Bind(tuning, "FrontHeight", 1.11f,
                Synced("Meters from the feet up to the middle of a carried crate.", new AcceptableValueRange<float>(0.2f, 2f)));
            FrontDistance = cfg.Bind(tuning, "FrontDistance", 0.127f,
                Synced("Meters between the body and the near side of a carried crate.", new AcceptableValueRange<float>(-0.5f, 1.5f)));
            FrontTilt = cfg.Bind(tuning, "FrontTilt", 4.2f,
                Synced("Degrees a carried crate leans back toward the chest (negative: away).", new AcceptableValueRange<float>(-45f, 45f)));
            BodyFollow = cfg.Bind(tuning, "BodyFollow", 1f,
                Synced("How much the crate and hands move with the torso as you walk. 1: fully, so the elbows stay steady; "
                    + "0: the crate stays level and the elbows bend with each step.", new AcceptableValueRange<float>(0f, 1.5f)));
            HandInset = cfg.Bind(tuning, "HandInset", -0.017f,
                Synced("Meters the hands reach in from the crate's sides (negative: hands further out).", new AcceptableValueRange<float>(-0.3f, 0.3f)));
            HandHeight = cfg.Bind(tuning, "HandHeight", -0.09f,
                Synced("Meters above (or, negative, below) the crate's middle that the hands grip.", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            HandForward = cfg.Bind(tuning, "HandForward", -0.209f,
                Synced("Meters toward the far side (or, negative, the near side) of the crate that the hands grip.", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            HandRotationWeight = cfg.Bind(tuning, "HandRotationWeight", 1f,
                Synced("How firmly the wrists are held to the grip angle below (0: the walk animation turns them freely).", new AcceptableValueRange<float>(0f, 1f)));
            HandPitch = cfg.Bind(tuning, "HandPitch", 154.5f,
                Synced("Degrees to tip the hands' grip up or down.", new AcceptableValueRange<float>(-180f, 180f)));
            HandYaw = cfg.Bind(tuning, "HandYaw", -13f,
                Synced("Degrees to turn the fingers inward or outward.", new AcceptableValueRange<float>(-180f, 180f)));
            HandRoll = cfg.Bind(tuning, "HandRoll", -9.1f,
                Synced("Degrees to roll the palms (toward the crate is the starting point).", new AcceptableValueRange<float>(-180f, 180f)));
            ElbowWeight = cfg.Bind(tuning, "ElbowWeight", 1f,
                Synced("How firmly the elbows are held out and down (0: the walk animation moves them freely).", new AcceptableValueRange<float>(0f, 1f)));
            ElbowOut = cfg.Bind(tuning, "ElbowOut", 0.25f,
                Synced("Meters the elbows point out past the hands.", new AcceptableValueRange<float>(-0.3f, 0.8f)));
            ElbowDown = cfg.Bind(tuning, "ElbowDown", 0.25f,
                Synced("Meters the elbows point down below the hands.", new AcceptableValueRange<float>(-0.3f, 0.8f)));
        }

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null)
        {
            return new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
