using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ImmersiveMapper.TraderBeacons";
        public const string PluginName = "TraderBeacons";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            BeaconConfig.Bind(Config);
            DebugCommands.Register();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void Update()
        {
            BeaconServer.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
