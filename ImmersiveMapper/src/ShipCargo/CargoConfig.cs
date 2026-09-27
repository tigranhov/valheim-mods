using BepInEx.Configuration;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>All settings are admin-only and synced from the server, so every player's crates behave the same.</summary>
    internal static class CargoConfig
    {
        public static ConfigEntry<bool> CratesEnabled;
        public static ConfigEntry<int> CrateWidth;
        public static ConfigEntry<int> CrateHeight;
        public static ConfigEntry<float> CrateWeight;
        public static ConfigEntry<string> CrateRecipe;
        public static ConfigEntry<string> CrateStation;

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
        }

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null)
        {
            return new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
