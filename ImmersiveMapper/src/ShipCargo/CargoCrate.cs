using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// The placed crate. Shift+E picks it up with everything inside: packed into an inventory item, or lifted to be
    /// carried (<see cref="CargoConfig.PickUpMode"/>). Picking up follows the vanilla container flow: ask the owner,
    /// who hands over ownership only if nobody has the crate open.
    /// </summary>
    internal sealed class CargoCrate : MonoBehaviour
    {
        private const string RequestPickupRpc = "IM_RequestPickup";
        private const string PickupResponseRpc = "IM_PickupResponse";
        // Guards against a loop if some item can't be moved into the floating crates.
        private const int MaxSpillCrates = 20;
        private const float WeightUpdateSeconds = 0.5f;

        /// <summary>Crates loaded here, for placement snapping.</summary>
        public static readonly List<CargoCrate> All = new List<CargoCrate>();

        private ZNetView _nview;
        private Container _container;
        private ShipPassenger _passenger;
        private CrateCarry _carry;
        private float _weightTimer;

        public bool IsCarried => _carry != null && _carry.IsCarried;

        private static bool Carrying => CargoConfig.PickUpMode.Value != CarryMode.Inventory;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _container = GetComponentInChildren<Container>();
            _passenger = GetComponent<ShipPassenger>();
            _carry = GetComponent<CrateCarry>();
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
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        // The crate's weight for its ship's trim: the crate itself plus whatever is inside.
        private void Update()
        {
            _weightTimer -= Time.deltaTime;
            if (_weightTimer > 0f || _passenger == null || _container == null || _container.GetInventory() == null)
            {
                return;
            }
            _weightTimer = WeightUpdateSeconds;
            _passenger.Weight = CargoConfig.CrateWeight.Value + _container.GetInventory().GetTotalWeight();
        }

        public string HoverSuffix()
        {
            string text = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " + (Carrying ? "Lift crate" : "Pick up crate");
            if (_passenger != null && _passenger.IsAttached)
            {
                text += "\n<color=#9ab>Riding on the ship</color>";
            }
            return Localization.instance.Localize(text);
        }

        public void RequestPickup(Humanoid character)
        {
            if (!(character is Player player) || player != Player.m_localPlayer || _nview.GetZDO() == null || IsCarried)
            {
                return;
            }
            if (CratePlacement.HasCrateOnTop(this))
            {
                player.Message(MessageHud.MessageType.Center, "Take the crate on top off first");
                return;
            }
            if (Carrying && CrateCarry.IsCarrying(player))
            {
                player.Message(MessageHud.MessageType.Center, "You're already carrying a crate");
                return;
            }
            if (!Carrying && !player.GetInventory().HaveEmptySlot())
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
            if (_container.IsInUse() || IsCarried)
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
            if (Carrying)
            {
                _carry.Lift(player);
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

        // The ship this crate rode on was destroyed (or its carrier went swimming): like a ship's own hold, the cargo
        // ends up in floating crates. The empty crate goes in too, so it isn't lost. Owner only.
        public void SpillIntoWater()
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
