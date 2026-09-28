using BepInEx.Configuration;

namespace ImmersiveMapper.CartLashing
{
    /// <summary>All settings are admin-only and synced from the server, so every player's carts behave the same.</summary>
    internal static class LashConfig
    {
        public static ConfigEntry<bool> Enabled;

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("1 - Cart lashing", "Enabled", true,
                new ConfigDescription("A cart standing on a ship's deck can be lashed to it (Shift+E): it rides the ship without "
                    + "moving, can't be pulled, and hits on it go to the ship. Turning this off only stops new lashing: lashed "
                    + "carts stay lashed until untied.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
    }
}
