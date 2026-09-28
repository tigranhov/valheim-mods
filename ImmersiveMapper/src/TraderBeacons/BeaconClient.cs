using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>Client side: keeps one <see cref="BeaconSignal"/> per trader the server says is signalling to us.</summary>
    internal static class BeaconClient
    {
        private static readonly Dictionary<string, BeaconSignal> Signals = new Dictionary<string, BeaconSignal>();

        public static void RPC_Signal(long sender, string trader, Vector3 campPosition, bool on)
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }
            Signals.TryGetValue(trader, out BeaconSignal signal);
            if (on)
            {
                if (signal != null)
                {
                    signal.Retarget(campPosition);
                }
                else
                {
                    Signals[trader] = BeaconSignal.Create(trader, campPosition, BeaconSignal.Mode.Auto, 0f);
                }
            }
            else
            {
                if (signal != null)
                {
                    signal.Stop();
                }
                Signals.Remove(trader);
            }
        }

        public static void Clear()
        {
            foreach (BeaconSignal signal in Signals.Values)
            {
                if (signal != null)
                {
                    signal.Stop();
                }
            }
            Signals.Clear();
        }
    }
}
