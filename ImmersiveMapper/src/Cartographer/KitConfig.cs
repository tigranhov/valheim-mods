using BepInEx.Configuration;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>How fast you can move while the map case is out.</summary>
    internal enum ReadingPace
    {
        /// <summary>The slow walk: reading a map is slow going.</summary>
        Walk,
        /// <summary>The normal pace, but no sprinting.</summary>
        Jog,
        /// <summary>No limit.</summary>
        Any,
    }

    /// <summary>What you can draw away from a cartography table.</summary>
    internal enum FieldDrawing
    {
        /// <summary>Every tool, ink included.</summary>
        Everything,
        /// <summary>Charcoal, stamps and notes: rough field notes.</summary>
        Sketch,
        /// <summary>Stamps and notes only.</summary>
        Stamps,
        /// <summary>Only reading.</summary>
        Nothing,
    }

    /// <summary>When the cartography table becomes the kit's drawing table.</summary>
    internal enum TableMode
    {
        /// <summary>In worlds without the map, where the vanilla table has nothing to do.</summary>
        NoMapWorlds,
        Always,
        /// <summary>The table stays vanilla.</summary>
        Never,
    }

    /// <summary>What the tally counts.</summary>
    internal enum TallyMode
    {
        /// <summary>Ground covered on foot, shown as paces of a set length (with a little error per leg).</summary>
        Distance,
        /// <summary>Every footfall. Running strides are longer than walking ones, so a leg is only steady if you walk it.</summary>
        Footsteps,
    }

    /// <summary>
    /// Gameplay settings are admin-only and synced from the server, so every player's kit works the same.
    /// Screen layout settings are each player's own.
    /// </summary>
    internal static class KitConfig
    {
        public static ConfigEntry<string> CaseRecipe;
        public static ConfigEntry<string> CaseStation;
        public static ConfigEntry<int> DraftSheets;
        public static ConfigEntry<ReadingPace> Pace;
        public static ConfigEntry<FieldDrawing> Drawing;
        public static ConfigEntry<bool> MapKeyTakesOut;

        public static ConfigEntry<TallyMode> Tally;
        public static ConfigEntry<float> PaceLength;
        public static ConfigEntry<float> TallyError;

        public static ConfigEntry<int> MaxPointsPerSheet;
        public static ConfigEntry<int> MaxMarksPerSheet;

        public static ConfigEntry<TableMode> Table;
        public static ConfigEntry<int> MaxMasterPoints;
        public static ConfigEntry<int> MaxMasterMarks;

        public static ConfigEntry<string> StampPalette;

        public static ConfigEntry<string> LogLineRecipe;
        public static ConfigEntry<string> LogLineStation;
        public static ConfigEntry<float> LogLineError;
        public static ConfigEntry<float> ReelSide;
        public static ConfigEntry<float> ReelUp;
        public static ConfigEntry<float> ReelBack;

        public static ConfigEntry<float> ReadingSize;
        public static ConfigEntry<float> ReadingLift;
        public static ConfigEntry<float> DrawingSize;

        public static ConfigEntry<float> HoldX;
        public static ConfigEntry<float> HoldY;
        public static ConfigEntry<float> HoldZ;
        public static ConfigEntry<float> HoldPitch;
        public static ConfigEntry<float> HoldYaw;
        public static ConfigEntry<float> HoldRoll;

        public static void Bind(ConfigFile cfg)
        {
            const string mapCase = "1 - Map case";
            CaseRecipe = cfg.Bind(mapCase, "Recipe", "DeerHide:2,LeatherScraps:4",
                Synced("Ingredients as Item:Amount pairs separated by commas. Applies after a restart."));
            CaseStation = cfg.Bind(mapCase, "CraftingStation", "piece_workbench",
                Synced("Where the map case is crafted (piece_workbench, forge, ...). Applies after a restart."));
            DraftSheets = cfg.Bind(mapCase, "DraftSheets", 3,
                Synced("Draft sheets in a map case. Lowering it never deletes a drawing: hidden sheets come back if you raise it again.",
                    new AcceptableValueRange<int>(1, 8)));
            Pace = cfg.Bind(mapCase, "ReadingPace", ReadingPace.Jog,
                Synced("How fast you can move with the map case out. Walk: the slow walk. Jog: the normal pace, no sprinting (default, "
                    + "user's pick 2026-09-27). Any: no limit."));
            Drawing = cfg.Bind(mapCase, "FieldDrawing", FieldDrawing.Sketch,
                Synced("What you can draw away from a cartography table. Everything: every tool, ink included. Sketch: charcoal, stamps and "
                    + "notes. Stamps: stamps and notes only. Nothing: reading only."));
            MapKeyTakesOut = cfg.Bind(mapCase, "MapKeyTakesOut", true,
                Synced("In worlds without the map, the map key (M) takes the map case out and puts it away."));

            const string tally = "2 - Tally";
            Tally = cfg.Bind(tally, "Mode", TallyMode.Distance,
                Synced("Distance: counts ground covered on foot, shown as paces of PaceLength, with a little error per leg. "
                    + "Footsteps: counts every footfall; running strides are longer than walking ones, so a leg is only steady if you walk it."));
            PaceLength = cfg.Bind(tally, "PaceLength", 0.8f,
                Synced("Meters per pace in Distance mode.", new AcceptableValueRange<float>(0.3f, 3f)));
            TallyError = cfg.Bind(tally, "ErrorPercent", 5f,
                Synced("Distance mode: each leg is counted with a stride that is off by up to this many percent, like a real pace count.",
                    new AcceptableValueRange<float>(0f, 30f)));

            const string sheets = "3 - Sheets";
            MaxPointsPerSheet = cfg.Bind(sheets, "MaxLinePoints", 6000,
                Synced("Most line points one sheet holds; keeps sheets small to save and send.", new AcceptableValueRange<int>(500, 20000)));
            MaxMarksPerSheet = cfg.Bind(sheets, "MaxMarks", 200,
                Synced("Most stamps plus notes one sheet holds.", new AcceptableValueRange<int>(10, 1000)));

            const string table = "6 - Cartography table";
            Table = cfg.Bind(table, "TakeOver", TableMode.NoMapWorlds,
                Synced("When the cartography table becomes the drawing table for master maps. NoMapWorlds: in worlds without the map, "
                    + "where the vanilla table has nothing to do. Always. Never: the table stays vanilla. Master maps are kept either way."));
            MaxMasterPoints = cfg.Bind(table, "MaxLinePoints", 150000,
                Synced("Most line points one table's master map holds (about 2.5 bytes each, sent to players near the table).",
                    new AcceptableValueRange<int>(10000, 1000000)));
            MaxMasterMarks = cfg.Bind(table, "MaxMarks", 3000,
                Synced("Most stamps plus labels one master map holds.", new AcceptableValueRange<int>(100, 20000)));

            StampPalette = cfg.Bind("7 - Stamps", "Palette", Stamps.DefaultPalette,
                "Your stamps, in order: names of items, pieces or locations with a map icon (like Vendor_BlackForest), or mark_x, "
                + "mark_circle, mark_dot. Any vanilla or modded prefab name works. Only changes your own palette: stamps already on a "
                + "sheet show for everyone.");
            // A palette nobody edited follows the default as it grows.
            if (StampPalette.Value == Stamps.FirstDefaultPalette)
            {
                StampPalette.Value = Stamps.DefaultPalette;
            }

            const string log = "8 - Log line";
            LogLineRecipe = cfg.Bind(log, "Recipe", "Iron:1,LeatherScraps:6",
                Synced("Ingredients as Item:Amount pairs separated by commas (default: Swamp iron, for the Karve era). Applies after a restart."));
            LogLineStation = cfg.Bind(log, "CraftingStation", "forge",
                Synced("Where the log line is crafted. Applies after a restart."));
            LogLineError = cfg.Bind(log, "ErrorPercent", 5f,
                Synced("Each run is counted off by up to this many percent, like a real log line.", new AcceptableValueRange<float>(0f, 30f)));
            // Where the reel sits, from the ship's steering spot (the log trails behind the stern).
            ReelSide = cfg.Bind(log, "ReelSide", 0.6f,
                Synced("Meters to the other side of the ship from the steering spot.", new AcceptableValueRange<float>(-3f, 3f)));
            ReelUp = cfg.Bind(log, "ReelUp", 0.2f,
                Synced("Meters above the steering spot.", new AcceptableValueRange<float>(-2f, 3f)));
            ReelBack = cfg.Bind(log, "ReelBack", 0.3f,
                Synced("Meters behind the steering spot.", new AcceptableValueRange<float>(-3f, 5f)));

            const string screen = "4 - Screen";
            ReadingSize = cfg.Bind(screen, "ReadingSize", 0.42f,
                new ConfigDescription("Height of the sheet while you read on the move, as a share of the screen height.",
                    new AcceptableValueRange<float>(0.15f, 0.9f)));
            ReadingLift = cfg.Bind(screen, "ReadingLift", 0.02f,
                new ConfigDescription("Gap between the bottom of the screen and the sheet while reading, as a share of the screen height.",
                    new AcceptableValueRange<float>(0f, 0.5f)));
            DrawingSize = cfg.Bind(screen, "DrawingSize", 0.8f,
                new ConfigDescription("Height of the sheet while drawing, as a share of the screen height.",
                    new AcceptableValueRange<float>(0.4f, 0.95f)));

            // Where the case sits in the right hand. Synced so every player sees the same pose; applies when it's taken out.
            const string tuning = "9 - Hold tuning";
            HoldX = cfg.Bind(tuning, "OffsetX", 0f, Synced("Meters along the hand's X axis.", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            HoldY = cfg.Bind(tuning, "OffsetY", 0.05f, Synced("Meters along the hand's Y axis.", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            HoldZ = cfg.Bind(tuning, "OffsetZ", 0f, Synced("Meters along the hand's Z axis.", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            HoldPitch = cfg.Bind(tuning, "Pitch", 0f, Synced("Degrees around the hand's X axis.", new AcceptableValueRange<float>(-180f, 180f)));
            HoldYaw = cfg.Bind(tuning, "Yaw", 0f, Synced("Degrees around the hand's Y axis.", new AcceptableValueRange<float>(-180f, 180f)));
            HoldRoll = cfg.Bind(tuning, "Roll", 0f, Synced("Degrees around the hand's Z axis.", new AcceptableValueRange<float>(-180f, 180f)));
        }

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null)
        {
            return new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
