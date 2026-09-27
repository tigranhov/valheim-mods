using BepInEx.Configuration;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>All settings are admin-only and synced from the server, so every player sees the same signals.</summary>
    internal static class BeaconConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Traders;
        public static ConfigEntry<bool> DebugLogging;

        public static ConfigEntry<float> SignalRadius;
        public static ConfigEntry<float> FoundRadius;
        public static ConfigEntry<float> ScanInterval;

        public static ConfigEntry<bool> SmokeEnabled;
        public static ConfigEntry<float> SmokeHeight;
        public static ConfigEntry<float> SmokeWidth;
        public static ConfigEntry<float> FogAsIf;

        public static ConfigEntry<bool> FireworksEnabled;
        public static ConfigEntry<float> VolleyDuration;
        public static ConfigEntry<float> VolleyInterval;
        public static ConfigEntry<float> ShotSpacing;
        public static ConfigEntry<float> FireworkHeight;
        public static ConfigEntry<float> FireworkScale;

        public static ConfigEntry<bool> FlashToBang;
        public static ConfigEntry<float> SoundVolume;

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("1 - General", "Enabled", true,
                Synced("Turn trader signals on or off."));
            Traders = cfg.Bind("1 - General", "Traders", "Vendor_BlackForest=Haldor, Hildir_camp=Hildir, BogWitch_Camp=Bog Witch",
                Synced("Trader camps that signal, as LocationPrefab=Name pairs separated by commas. Modded trader locations can be added here."));
            DebugLogging = cfg.Bind("1 - General", "DebugLogging", false,
                Synced("Log every signal start/stop to the BepInEx log (spoilers!)."));

            SignalRadius = cfg.Bind("2 - Detection", "SignalRadius", 500f,
                Synced("Meters. Farthest distance a trader's signal can be seen from. A camp only signals after the game has generated it (someone came within ~300 m, like vanilla's map icon), and keeps signalling until you reach it.", new AcceptableValueRange<float>(200f, 5000f)));
            FoundRadius = cfg.Bind("2 - Detection", "FoundRadius", 40f,
                Synced("Meters. Coming this close, or talking to the trader, counts as finding them. Signals then stop for that player.", new AcceptableValueRange<float>(10f, 300f)));
            ScanInterval = cfg.Bind("2 - Detection", "ScanIntervalSeconds", 5f,
                Synced("How often the server checks player positions.", new AcceptableValueRange<float>(1f, 60f)));

            SmokeEnabled = cfg.Bind("3 - Smoke", "Enabled", true,
                Synced("Show a tall smoke column above the camp, day and night."));
            SmokeHeight = cfg.Bind("3 - Smoke", "HeightMeters", 150f,
                Synced("Roughly how high the smoke column rises.", new AcceptableValueRange<float>(30f, 500f)));
            SmokeWidth = cfg.Bind("3 - Smoke", "WidthMeters", 10f,
                Synced("Size of each smoke puff at the base. The column widens as it rises.", new AcceptableValueRange<float>(2f, 50f)));
            FogAsIf = cfg.Bind("3 - Smoke", "FogAsIfMeters", 500f,
                Synced("The game's fog would hide the smoke long before signal range. Farther than this, the smoke gets only as much fog as something this far away; closer, normal fog. Lower = clearer.", new AcceptableValueRange<float>(20f, 2000f)));

            FireworksEnabled = cfg.Bind("4 - Fireworks (optional)", "Enabled", false,
                Synced("Off by default (less immersive). When on, firework volleys replace the smoke at night."));
            VolleyDuration = cfg.Bind("4 - Fireworks (optional)", "VolleySeconds", 60f,
                Synced("How long one volley lasts.", new AcceptableValueRange<float>(5f, 300f)));
            VolleyInterval = cfg.Bind("4 - Fireworks (optional)", "SecondsBetweenVolleys", 300f,
                Synced("Pause between volleys while a player stays in range at night.", new AcceptableValueRange<float>(30f, 3600f)));
            ShotSpacing = cfg.Bind("4 - Fireworks (optional)", "SecondsBetweenShots", 3f,
                Synced("Average time between rockets within a volley.", new AcceptableValueRange<float>(0.5f, 30f)));
            FireworkHeight = cfg.Bind("4 - Fireworks (optional)", "LaunchHeightMeters", 40f,
                Synced("Rockets launch this far above the camp so they clear the trees.", new AcceptableValueRange<float>(0f, 200f)));
            FireworkScale = cfg.Bind("4 - Fireworks (optional)", "Scale", 3f,
                Synced("Size multiplier for the bursts, so they read from far away.", new AcceptableValueRange<float>(1f, 10f)));
            FlashToBang = cfg.Bind("4 - Fireworks (optional)", "FlashToBang", true,
                Synced("Delay the firework sound by distance / 343 m/s, so you can count seconds to judge how far away the camp is."));
            SoundVolume = cfg.Bind("4 - Fireworks (optional)", "SoundVolume", 1f,
                Synced("Volume multiplier for distant firework sounds.", new AcceptableValueRange<float>(0f, 2f)));
        }

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null)
        {
            return new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
