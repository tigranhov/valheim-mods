using BepInEx;
using HarmonyLib;

namespace BetterWheel
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nexuschip.BetterWheel";
        public const string PluginName = "Better Wheel";
        public const string PluginVersion = "1.0.1";

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        private void Awake()
        {
            WheelConfig.Bind(Config);
            WheelLog.Bind(Config, Logger);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void Update()
        {
            Wheels.Update();
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
