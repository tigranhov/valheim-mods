using System;

namespace Cartographer
{
    /// <summary>
    /// One player at a time at a cartography table. The table's owner decides (like a chest being opened): it marks the
    /// table in use and answers yes or no. The one working there then takes ownership, so they can save the master, and
    /// keeps the mark fresh; a mark nobody refreshed for a while (a crash, a lost connection) no longer counts.
    /// </summary>
    internal static class TableLock
    {
        private const string UserKey = "IM_Map_InUse";
        private const string TimeKey = "IM_Map_InUseTime";
        private const string RequestRpc = "IM_MapTable_Request";
        private const string ResponseRpc = "IM_MapTable_Response";
        private static readonly long StaleTicks = TimeSpan.FromSeconds(15).Ticks;

        public static void Register(MapTable table)
        {
            ZNetView nview = table.m_nview;
            nview.Register<long>(RequestRpc, (sender, player) => OnRequest(table, sender, player));
            nview.Register<bool>(ResponseRpc, (sender, granted) => OnResponse(table, granted));
        }

        public static void Request(MapTable table)
        {
            table.m_nview.InvokeRPC(RequestRpc, Player.m_localPlayer.GetPlayerID());
        }

        public static bool InUseByOther(MapTable table)
        {
            ZDO zdo = table.m_nview != null && table.m_nview.IsValid() ? table.m_nview.GetZDO() : null;
            if (zdo == null || Player.m_localPlayer == null)
            {
                return false;
            }
            long user = zdo.GetLong(UserKey);
            return user != 0 && user != Player.m_localPlayer.GetPlayerID() && !IsStale(zdo);
        }

        /// <summary>While working at the table: keeps the in-use mark fresh.</summary>
        public static void Heartbeat(MapTable table)
        {
            ZNetView nview = table.m_nview;
            if (nview != null && nview.IsValid() && nview.IsOwner())
            {
                nview.GetZDO().Set(TimeKey, Now());
            }
        }

        public static void Release(MapTable table)
        {
            ZNetView nview = table != null ? table.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || Player.m_localPlayer == null)
            {
                return;
            }
            ZDO zdo = nview.GetZDO();
            if (zdo.GetLong(UserKey) == Player.m_localPlayer.GetPlayerID())
            {
                zdo.Set(UserKey, 0L);
            }
        }

        private static void OnRequest(MapTable table, long sender, long player)
        {
            ZNetView nview = table.m_nview;
            if (!nview.IsOwner())
            {
                return;
            }
            ZDO zdo = nview.GetZDO();
            long user = zdo.GetLong(UserKey);
            bool free = user == 0 || user == player || IsStale(zdo);
            if (free)
            {
                zdo.Set(UserKey, player);
                zdo.Set(TimeKey, Now());
            }
            nview.InvokeRPC(sender, ResponseRpc, free);
        }

        private static void OnResponse(MapTable table, bool granted)
        {
            if (table == null || Player.m_localPlayer == null)
            {
                return;
            }
            if (!granted)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Someone is working at the table");
                return;
            }
            table.m_nview.ClaimOwnership();
            TableScreen.Open(table);
        }

        private static bool IsStale(ZDO zdo)
        {
            return Now() - zdo.GetLong(TimeKey) > StaleTicks;
        }

        private static long Now()
        {
            return ZNet.instance != null ? ZNet.instance.GetTime().Ticks : DateTime.UtcNow.Ticks;
        }
    }
}
