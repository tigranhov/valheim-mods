using System;

namespace ImmersiveMapper.Shared
{
    /// <summary>
    /// A ship's own id for its cargo, kept in the ship's ZDO. ZDO ids change when a world loads, but a ZDO's data is
    /// saved with it, so this key stays the same: a crate that remembers it finds its ship again exactly after a reload.
    /// Only the ship's owner may write it, so anyone else asks the owner to. Shared by the mods whose things ride
    /// ships: they all use the same key, and whichever loads first answers the requests.
    /// </summary>
    internal static class ShipKey
    {
        private const string ClaimRpc = "IM_ClaimShipKey";
        private static readonly int Key = "IM_ShipKey".GetStableHashCode();

        /// <summary>Lets this ship's owner accept a key from other players. Called once per ship instance.</summary>
        public static void Register(Ship ship)
        {
            ZNetView view = ship.m_nview;
            if (view == null || view.GetZDO() == null || view.m_functions.ContainsKey(ClaimRpc.GetStableHashCode()))
            {
                return;
            }
            view.Register<long>(ClaimRpc, (sender, key) =>
            {
                ZDO zdo = view.GetZDO();
                if (zdo != null && view.IsOwner() && key != 0L && zdo.GetLong(Key, 0L) == 0L)
                {
                    zdo.Set(Key, key);
                }
            });
        }

        /// <summary>The ship's key, or 0 if it has none (yet).</summary>
        public static long Get(ZDO shipZdo)
        {
            return shipZdo != null ? shipZdo.GetLong(Key, 0L) : 0L;
        }

        public static long Get(Ship ship)
        {
            ZNetView view = ship != null ? ship.m_nview : null;
            return Get(view != null ? view.GetZDO() : null);
        }

        /// <summary>
        /// The ship's key. If it has none yet, gives it <paramref name="proposal"/> (or a new key) and returns that; when
        /// someone else owns the ship this only asks, and another player's proposal may win (callers check again later).
        /// </summary>
        public static long Claim(Ship ship, long proposal = 0L)
        {
            ZNetView view = ship != null ? ship.m_nview : null;
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (zdo == null)
            {
                return 0L;
            }
            long key = zdo.GetLong(Key, 0L);
            if (key != 0L)
            {
                return key;
            }
            key = proposal != 0L ? proposal : NewKey();
            if (view.IsOwner())
            {
                zdo.Set(Key, key);
            }
            else
            {
                view.InvokeRPC(ClaimRpc, key);
            }
            return key;
        }

        private static long NewKey()
        {
            long key;
            do
            {
                key = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            }
            while (key == 0L);
            return key;
        }
    }
}
