using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace Cartographer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "thov88.Cartographer";
        public const string PluginName = "Cartographer";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            KitConfig.Bind(Config);
            CaseUpgrades.Bind(Config);
            KitKeys.Register(Config);
            MapCaseSetup.Register();
            LogLineSetup.Register();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void Update()
        {
            CaseInHand.Update();
            TableScreen.Update();
        }

        private void LateUpdate()
        {
            CaseInHand.LateUpdate();
            TableScreen.LateUpdate();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
