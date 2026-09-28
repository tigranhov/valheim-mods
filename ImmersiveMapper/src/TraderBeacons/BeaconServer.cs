using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>
    /// Runs on the server (dedicated or host). Only the server knows where trader camps are, so it checks player
    /// positions and tells each client when a trader should start or stop signalling to them.
    /// A camp signals once the game has generated it (a player came within ~300 m, like vanilla's map icon),
    /// to every player within SignalRadius who hasn't reached it yet.
    /// </summary>
    internal static class BeaconServer
    {
        private readonly struct Traveller
        {
            public readonly long PeerId;
            public readonly long PlayerId;
            public readonly Vector3 Position;

            public Traveller(long peerId, long playerId, Vector3 position)
            {
                PeerId = peerId;
                PlayerId = playerId;
                Position = position;
            }
        }

        // Once signalling, keep going until the player is this much beyond the signal radius, so it doesn't flicker at the edge.
        private const float StopHysteresis = 1.1f;
        // How close a trader NPC must be to a camp for talking to it to count as finding that camp.
        private const float MetTraderMatchRadius = 100f;

        private static float _timer;
        private static readonly Dictionary<long, HashSet<string>> Signalling = new Dictionary<long, HashSet<string>>();
        private static readonly Dictionary<string, List<ZoneSystem.LocationInstance>> Camps = new Dictionary<string, List<ZoneSystem.LocationInstance>>();
        private static readonly List<Traveller> Travellers = new List<Traveller>();
        private static readonly HashSet<long> Online = new HashSet<long>();

        public static void Tick(float dt)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || ZRoutedRpc.instance == null)
            {
                return;
            }
            _timer += dt;
            if (_timer < BeaconConfig.ScanInterval.Value)
            {
                return;
            }
            _timer = 0f;
            FoundStore.EnsureLoaded();
            if (BeaconConfig.Enabled.Value)
            {
                Scan();
            }
            else
            {
                StopAll();
            }
            FoundStore.SaveIfDirty();
        }

        public static void Reset()
        {
            Signalling.Clear();
            _timer = 0f;
            FoundStore.Unload();
        }

        private static void Scan()
        {
            CollectTravellers();
            if (Travellers.Count == 0)
            {
                // Nobody online: skip the location scan. Anyone who left is forgotten below.
                Signalling.Clear();
                return;
            }
            IReadOnlyList<TraderDef> defs = TraderList.All;
            CampLocator.Collect(Camps, defs);
            Online.Clear();
            foreach (Traveller t in Travellers)
            {
                Online.Add(t.PeerId);
                foreach (TraderDef def in defs)
                {
                    UpdateSignal(t, def);
                }
            }
            List<long> gone = null;
            foreach (long peerId in Signalling.Keys)
            {
                if (!Online.Contains(peerId))
                {
                    (gone ??= new List<long>()).Add(peerId);
                }
            }
            if (gone != null)
            {
                foreach (long peerId in gone)
                {
                    Signalling.Remove(peerId);
                }
            }
        }

        private static void UpdateSignal(Traveller t, TraderDef def)
        {
            bool active = IsSignalling(t.PeerId, def.Prefab);
            if (FoundStore.HasFound(t.PlayerId, def.Prefab)
                || !Camps.TryGetValue(def.Prefab, out List<ZoneSystem.LocationInstance> camps)
                || !CampLocator.TryGetNearest(camps, t.Position, true, out ZoneSystem.LocationInstance camp, out float distance))
            {
                if (active)
                {
                    SetSignal(t.PeerId, def, Vector3.zero, false);
                }
                return;
            }

            if (distance <= BeaconConfig.FoundRadius.Value)
            {
                FoundStore.MarkFound(t.PlayerId, def.Prefab);
                Plugin.Log.LogInfo($"Player {t.PlayerId} found {def.Name}.");
                if (active)
                {
                    SetSignal(t.PeerId, def, Vector3.zero, false);
                }
                return;
            }

            float radius = BeaconConfig.SignalRadius.Value;
            if (distance > (active ? radius * StopHysteresis : radius))
            {
                if (active)
                {
                    SetSignal(t.PeerId, def, Vector3.zero, false);
                }
                return;
            }

            if (!active)
            {
                SetSignal(t.PeerId, def, camp.m_position, true);
            }
        }

        private static bool IsSignalling(long peerId, string prefab)
        {
            return Signalling.TryGetValue(peerId, out HashSet<string> set) && set.Contains(prefab);
        }

        private static void SetSignal(long peerId, TraderDef def, Vector3 position, bool on)
        {
            if (on)
            {
                if (!Signalling.TryGetValue(peerId, out HashSet<string> set))
                {
                    Signalling[peerId] = set = new HashSet<string>();
                }
                set.Add(def.Prefab);
            }
            else if (Signalling.TryGetValue(peerId, out HashSet<string> set))
            {
                set.Remove(def.Prefab);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(peerId, Rpc.Signal, def.Prefab, position, on);
            if (BeaconConfig.DebugLogging.Value)
            {
                Plugin.Log.LogInfo($"{def.Name} signal {(on ? "started" : "stopped")} for peer {peerId}.");
            }
        }

        private static void StopAll()
        {
            foreach (KeyValuePair<long, HashSet<string>> kv in Signalling)
            {
                foreach (string prefab in kv.Value)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(kv.Key, Rpc.Signal, prefab, Vector3.zero, false);
                }
            }
            Signalling.Clear();
        }

        private static void CollectTravellers()
        {
            Travellers.Clear();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (!peer.IsReady() || peer.m_characterID.IsNone())
                {
                    continue;
                }
                ZDO zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
                long playerId = zdo != null ? zdo.GetLong(ZDOVars.s_playerID, 0L) : 0L;
                if (playerId != 0L)
                {
                    Travellers.Add(new Traveller(peer.m_uid, playerId, zdo.GetPosition()));
                }
            }
            Player local = Player.m_localPlayer;
            if (!ZNet.instance.IsDedicated() && local != null)
            {
                Travellers.Add(new Traveller(ZNet.GetUID(), local.GetPlayerID(), local.transform.position));
            }
        }

        private static bool TryGetTraveller(long peerId, out Traveller traveller)
        {
            CollectTravellers();
            foreach (Traveller t in Travellers)
            {
                if (t.PeerId == peerId)
                {
                    traveller = t;
                    return true;
                }
            }
            traveller = default;
            return false;
        }

        private static bool IsAdmin(long peerId)
        {
            if (peerId == ZNet.GetUID())
            {
                return true;
            }
            ZNetPeer peer = ZNet.instance.GetPeer(peerId);
            return peer != null && ZNet.instance.ListContainsId(ZNet.instance.m_adminList, peer.m_socket.GetHostName());
        }

        // Client talked to a trader NPC at traderPos: count the matching camp as found.
        public static void RPC_MetTrader(long sender, Vector3 traderPos)
        {
            if (!TryGetTraveller(sender, out Traveller t))
            {
                return;
            }
            FoundStore.EnsureLoaded();
            CampLocator.Collect(Camps, TraderList.All);
            foreach (TraderDef def in TraderList.All)
            {
                if (FoundStore.HasFound(t.PlayerId, def.Prefab)
                    || !Camps.TryGetValue(def.Prefab, out List<ZoneSystem.LocationInstance> camps)
                    || !CampLocator.TryGetNearest(camps, traderPos, true, out _, out float distance)
                    || distance > MetTraderMatchRadius)
                {
                    continue;
                }
                FoundStore.MarkFound(t.PlayerId, def.Prefab);
                Plugin.Log.LogInfo($"Player {t.PlayerId} met {def.Name}.");
                if (IsSignalling(sender, def.Prefab))
                {
                    SetSignal(sender, def, Vector3.zero, false);
                }
            }
            FoundStore.SaveIfDirty();
        }

        // Debug (spoiler): report every camp and candidate spot relative to the requesting player.
        public static void RPC_CampsRequest(long sender)
        {
            if (!IsAdmin(sender))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc.CampsReply, "TraderBeacons: only admins can list trader camps.");
                return;
            }
            if (!TryGetTraveller(sender, out Traveller t))
            {
                return;
            }
            FoundStore.EnsureLoaded();
            CampLocator.Collect(Camps, TraderList.All);
            var sb = new StringBuilder("Trader camps (distance and bearing from you, 0° = north):");
            foreach (TraderDef def in TraderList.All)
            {
                List<ZoneSystem.LocationInstance> camps = Camps[def.Prefab];
                string found = FoundStore.HasFound(t.PlayerId, def.Prefab) ? ", found" : "";
                sb.Append($"\n  {def.Name} [{def.Prefab}]: {camps.Count} spot(s){found}");
                camps.Sort((a, b) => Utils.DistanceXZ(a.m_position, t.Position).CompareTo(Utils.DistanceXZ(b.m_position, t.Position)));
                for (int i = 0; i < camps.Count && i < 5; i++)
                {
                    Vector3 delta = camps[i].m_position - t.Position;
                    float bearing = (Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + 360f) % 360f;
                    string state = camps[i].m_placed ? "placed" : "candidate";
                    sb.Append($"\n    {state}: {Utils.DistanceXZ(camps[i].m_position, t.Position):0} m at {bearing:0}°");
                }
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc.CampsReply, sb.ToString());
        }

        // Debug: forget which traders the requesting player has found, so signals start again.
        public static void RPC_ResetFound(long sender)
        {
            if (!IsAdmin(sender))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc.CampsReply, "TraderBeacons: only admins can reset found traders.");
                return;
            }
            if (!TryGetTraveller(sender, out Traveller t))
            {
                return;
            }
            FoundStore.EnsureLoaded();
            int removed = FoundStore.ResetPlayer(t.PlayerId);
            FoundStore.SaveIfDirty();
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc.CampsReply, $"TraderBeacons: cleared {removed} found trader(s).");
        }
    }
}
