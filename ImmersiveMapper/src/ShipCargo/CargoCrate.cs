using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// The placed crate. Shift+E picks it up with everything inside. Picking up follows the vanilla container flow:
    /// ask the owner, who hands over ownership only if nobody has the crate open.
    /// </summary>
    internal sealed class CargoCrate : MonoBehaviour
    {
        private const string RequestPickupRpc = "IM_RequestPickup";
        private const string PickupResponseRpc = "IM_PickupResponse";
        // Guards against a loop if some item can't be moved into the floating crates.
        private const int MaxSpillCrates = 20;

        private ZNetView _nview;
        private Container _container;
        private ShipPassenger _passenger;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _container = GetComponentInChildren<Container>();
            _passenger = GetComponent<ShipPassenger>();
            if (_nview == null || _nview.GetZDO() == null)
            {
                return;
            }
            _nview.Register<long>(RequestPickupRpc, RPC_RequestPickup);
            _nview.Register<bool>(PickupResponseRpc, RPC_PickupResponse);
            if (_passenger != null)
            {
                _passenger.ShipLost += SpillIntoWater;
            }
        }

        public string HoverSuffix()
        {
            string text = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Pick up crate";
            if (_passenger != null && _passenger.IsAttached)
            {
                text += "\n<color=#9ab>Riding on the ship</color>";
            }
            return Localization.instance.Localize(text);
        }

        public void RequestPickup(Humanoid character)
        {
            if (!(character is Player player) || player != Player.m_localPlayer || _nview.GetZDO() == null)
            {
                return;
            }
            if (!player.GetInventory().HaveEmptySlot())
            {
                player.Message(MessageHud.MessageType.Center, "$inventory_full");
                return;
            }
            _nview.InvokeRPC(RequestPickupRpc, player.GetPlayerID());
        }

        private void RPC_RequestPickup(long sender, long playerId)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            if (_container.IsInUse())
            {
                _nview.InvokeRPC(sender, PickupResponseRpc, false);
                return;
            }
            ZDOMan.instance.ForceSendZDO(sender, _nview.GetZDO().m_uid);
            _nview.GetZDO().SetOwner(sender);
            _nview.InvokeRPC(sender, PickupResponseRpc, true);
        }

        private void RPC_PickupResponse(long sender, bool granted)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            if (!_nview.IsOwner())
            {
                return;
            }
            byte[] contents = _nview.GetZDO().GetByteArray(ZDOVars.s_items);
            Inventory inventory = LoadContents(contents);
            ItemDrop.ItemData item = CrateItem.Create(contents, inventory);
            if (!player.GetInventory().AddItem(item))
            {
                player.Message(MessageHud.MessageType.Center, "$inventory_full");
                return;
            }
            player.Message(MessageHud.MessageType.TopLeft, "Picked up " + item.m_shared.m_name, 1, item.GetIcon());
            _nview.Destroy();
        }

        private Inventory LoadContents(byte[] contents)
        {
            var inventory = new Inventory("crate", null, _container.m_width, _container.m_height);
            if (contents != null)
            {
                inventory.Load(new ZPackage(contents));
            }
            return inventory;
        }

        // The ship this crate rode on was destroyed: like the ship's own hold, the cargo ends up in floating crates.
        // The empty crate goes in too, so it isn't lost.
        private void SpillIntoWater()
        {
            GameObject floating = ZNetScene.instance.GetPrefab(CrateSetup.VanillaCrate);
            if (floating == null)
            {
                Plugin.Log.LogWarning("No floating crate prefab; leaving the cargo crate where it is.");
                return;
            }
            Inventory cargo = _container.GetInventory();
            Container last = null;
            for (int i = 0; i < MaxSpillCrates && (cargo.NrOfItems() > 0 || last == null); i++)
            {
                last = Instantiate(floating, transform.position + Random.insideUnitSphere, Random.rotation).GetComponent<Container>();
                last.GetInventory().MoveAll(cargo);
            }
            if (!last.GetInventory().AddItem(CrateItem.Create(null, null)))
            {
                Instantiate(floating, transform.position + Random.insideUnitSphere, Random.rotation)
                    .GetComponent<Container>().GetInventory().AddItem(CrateItem.Create(null, null));
            }
            _nview.Destroy();
        }
    }
}
