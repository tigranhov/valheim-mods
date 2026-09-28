using BepInEx;
using HarmonyLib;

namespace ValheimTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "thov88.ValheimTweaks";
        public const string PluginName = "Valheim Tweaks";
        public const string PluginVersion = "0.1.0";

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        private void Awake()
        {
            MiningDrops.BindConfig(Config);

            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
