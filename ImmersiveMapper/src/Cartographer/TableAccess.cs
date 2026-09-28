using HarmonyLib;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Takes over the vanilla cartography table (in worlds without the map, by default): both of its action spots open the
    /// table's master map instead of reading or writing the vanilla map. The master is saved on the table under its own
    /// key; the vanilla map data stays untouched.
    /// </summary>
    [HarmonyPatch(typeof(MapTable), "Start")]
    internal static class TableAccess
    {
        private const string MasterKey = "IM_Map_Master";

        public static bool TakesOver
        {
            get
            {
                switch (KitConfig.Table.Value)
                {
                    case TableMode.Always:
                        return true;
                    case TableMode.Never:
                        return false;
                    default:
                        return Game.m_noMap;
                }
            }
        }

        public static byte[] ReadMaster(MapTable table)
        {
            return table.m_nview.GetZDO().GetByteArray(MasterKey);
        }

        /// <summary>Saves the master on the table; only its owner (the one working there) may.</summary>
        public static void WriteMaster(MapTable table, byte[] bytes)
        {
            ZNetView nview = table.m_nview;
            if (nview == null || !nview.IsValid())
            {
                return;
            }
            if (!nview.IsOwner())
            {
                nview.ClaimOwnership();
            }
            nview.GetZDO().Set(MasterKey, bytes);
        }

        private static void Postfix(MapTable __instance)
        {
            if (__instance.m_nview == null || !__instance.m_nview.IsValid())
            {
                return;
            }
            TableLock.Register(__instance);
            MapTable table = __instance;
            // Replaces the callbacks vanilla combined in; they're called again when the table stays vanilla.
            table.m_readSwitch.m_onUse = (caller, user, item) => Use(table, true, caller, user, item);
            table.m_readSwitch.m_onHover = () => Hover(table, true);
            table.m_writeSwitch.m_onUse = (caller, user, item) => Use(table, false, caller, user, item);
            table.m_writeSwitch.m_onHover = () => Hover(table, false);
        }

        private static bool Use(MapTable table, bool read, Switch caller, Humanoid user, ItemDrop.ItemData item)
        {
            if (!TakesOver)
            {
                return read ? table.OnRead(caller, user, item) : table.OnWrite(caller, user, item);
            }
            if (item != null || user != Player.m_localPlayer)
            {
                return false;
            }
            if (!PrivateArea.CheckAccess(table.transform.position))
            {
                return true;
            }
            if (!TableScreen.IsOpen)
            {
                TableLock.Request(table);
            }
            return true;
        }

        private static string Hover(MapTable table, bool read)
        {
            if (!TakesOver)
            {
                return read ? table.GetReadHoverText() : table.GetWriteHoverText();
            }
            if (!PrivateArea.CheckAccess(table.transform.position, 0f, flash: false))
            {
                return Localization.instance.Localize(table.m_name + "\n$piece_noaccess");
            }
            string busy = TableLock.InUseByOther(table) ? "\n<color=#9a9a9a>Someone is working at the table</color>" : "";
            return Localization.instance.Localize(table.m_name + "\n[<color=yellow><b>$KEY_Use</b></color>] Work at the master map") + busy;
        }
    }
}
