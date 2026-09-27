using HarmonyLib;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    internal static class Rpc
    {
        public const string Signal = "TraderBeacons_Signal";
        public const string MetTrader = "TraderBeacons_MetTrader";
        public const string CampsRequest = "TraderBeacons_CampsRequest";
        public const string CampsReply = "TraderBeacons_CampsReply";
        public const string ResetFound = "TraderBeacons_ResetFound";

        public static void Register()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            rpc.Register<string, Vector3, bool>(Signal, BeaconClient.RPC_Signal);
            rpc.Register<string>(CampsReply, DebugCommands.RPC_CampsReply);
            if (ZNet.instance.IsServer())
            {
                rpc.Register<Vector3>(MetTrader, BeaconServer.RPC_MetTrader);
                rpc.Register(CampsRequest, BeaconServer.RPC_CampsRequest);
                rpc.Register(ResetFound, BeaconServer.RPC_ResetFound);
            }
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class GameStartPatch
    {
        private static void Postfix()
        {
            Rpc.Register();
        }
    }

    [HarmonyPatch(typeof(Game), "OnDestroy")]
    internal static class GameDestroyPatch
    {
        private static void Prefix()
        {
            BeaconClient.Clear();
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                BeaconServer.Reset();
            }
        }
    }

    // Talking to a trader counts as finding them. Trader.Interact always returns false, so check `hold` instead.
    [HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
    internal static class TraderInteractPatch
    {
        private static void Postfix(Trader __instance, bool hold)
        {
            if (!hold && ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(Rpc.MetTrader, __instance.transform.position);
            }
        }
    }
}
