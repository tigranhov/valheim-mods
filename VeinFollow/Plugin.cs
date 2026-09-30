using BepInEx;
using HarmonyLib;

namespace VeinFollow
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nexuschip.VeinFollow";
        public const string PluginName = "Vein Follow";
        public const string PluginVersion = "0.1.0";

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        private void Awake()
        {
            FollowConfig.Bind(Config);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
