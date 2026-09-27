using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// The placed crate, or the same crate loose (afloat, or tumbled off a stack). Shift+E picks it up with everything inside: packed into an
    /// inventory item, or lifted to be carried (<see cref="CargoConfig.PickUpMode"/>). Picking up follows the vanilla
    /// container flow: ask the owner, who hands over ownership only if nobody has the crate open.
    /// </summary>
    internal sealed class CargoCrate : MonoBehaviour
    {
        private const string RequestPickupRpc = "IM_RequestPickup";
        private const string PickupResponseRpc = "IM_PickupResponse";
        private const string TumbleRpc = "IM_Tumble";
        // Guards the walk up a stack.
        private const int MaxStack = 16;
        // A small shove for crates tumbling off a stack, so they topple instead of dropping as a column.
        private const float TumbleSpin = 1.5f;
        private const float TumblePush = 0.6f;

        /// <summary>Placed crates loaded here (not the loose ones), for placement snapping.</summary>
        public static readonly List<CargoCrate> All = new List<CargoCrate>();

        private ZNetView _nview;
        private Container _container;
        private ShipPassenger _passenger;
        private CrateCarry _carry;
        private bool _loose;
        private bool _trackingWeight;

        public bool IsCarried => _carry != null && _carry.IsCarried;

        /// <summary>
        /// Loose (<see cref="CrateSetup.FloatingPrefab"/>): a physics crate that floats in water and tumbles on land,
        /// so nothing can be set on it.
        /// </summary>
        public bool IsLoose => _loose;

        /// <summary>Riding a ship it's part of the ship, so it can't be broken (see <see cref="CrateDamagePatch"/>).</summary>
        public bool IsRiding => _passenger != null && _passenger.IsAttached;

        /// <summary>The ship it rides, if that ship is loaded here.</summary>
        public Ship Ship => _passenger != null ? _passenger.CurrentShip : null;

        private static bool Carrying => CargoConfig.PickUpMode.Value != CarryMode.Inventory;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _container = GetComponentInChildren<Container>();
            _passenger = GetComponent<ShipPassenger>();
            _carry = GetComponent<CrateCarry>();
            _loose = GetComponent<Floating>() != null;
            if (_nview == null || _nview.GetZDO() == null)
            {
                return;
            }
            _nview.Register<long>(RequestPickupRpc, RPC_RequestPickup);
            _nview.Register<bool>(PickupResponseRpc, RPC_PickupResponse);
            _nview.Register(TumbleRpc, RPC_Tumble);
            Destructible destructible = GetComponent<Destructible>();
            if (destructible != null)
            {
                destructible.m_onDestroyed += OnBroken;
            }
            if (_passenger != null)
            {
                _passenger.ShipLost += () => MakeLoose(false);
            }
            if (!_loose)
            {
                All.Add(this);
            }
            TrackWeight();
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        // Again once every component is awake, in case the container (which makes its inventory in its own Awake) woke later.
        private void Start()
        {
            TrackWeight();
        }

        private void TrackWeight()
        {
            Inventory inventory = _container != null ? _container.GetInventory() : null;
            if (_trackingWeight || inventory == null || _passenger == null || _nview == null || _nview.GetZDO() == null)
            {
                return;
            }
            _trackingWeight = true;
            inventory.m_onChanged += UpdateWeight;
            UpdateWeight();
        }

        // The crate's weight, for its ship's trim and its carrier: the crate itself plus whatever is inside. The
        // inventory keeps its total up to date, so this only has to follow its changes.
        private void UpdateWeight()
        {
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
            // Swimming with a crate drops it straight back into the water.
            if (Carrying && player.IsSwimming())
            {
                player.Message(MessageHud.MessageType.Center, "You can't lift a crate while swimming");
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
                if (_loose)
                {
                    LiftLoose(player);
                }
                else
                {
                    _carry.Lift(player);
                }
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

        /// <summary>
        /// Moves items that lie outside the crate's grid (after the crate size was lowered in the config) into free
        /// slots, adding rows as needed. The game keeps such items but never shows them, so they'd be stuck.
        /// Returns true if anything moved.
        /// </summary>
        public static bool FitToGrid(Inventory inventory)
        {
            int width = inventory.GetWidth();
            List<ItemDrop.ItemData> outside = null;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_gridPos.x < 0 || item.m_gridPos.x >= width || item.m_gridPos.y < 0)
                {
                    (outside ??= new List<ItemDrop.ItemData>()).Add(item);
                }
            }
            if (outside == null)
            {
                return false;
            }
            int x = 0;
            int y = 0;
            int rows = inventory.GetHeight();
            foreach (ItemDrop.ItemData item in outside)
            {
                while (inventory.GetItemAt(x, y) != null)
                {
                    if (++x >= width)
                    {
                        x = 0;
                        y++;
                    }
                }
                item.m_gridPos = new Vector2i(x, y);
                rows = Mathf.Max(rows, y + 1);
            }
            inventory.SetHeight(rows);
            return true;
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

        // Broken (the owner runs this): everything stacked on it comes loose and tumbles down.
        private void OnBroken()
        {
            CargoCrate above = CratePlacement.CrateOnTop(this);
            for (int i = 0; above != null && i < MaxStack; i++)
            {
                CargoCrate next = CratePlacement.CrateOnTop(above);
                above._nview.InvokeRPC(TumbleRpc);
                above = next;
            }
        }

        private void RPC_Tumble(long sender)
        {
            if (_nview.IsOwner() && !_loose && !IsCarried && !IsRiding)
            {
                MakeLoose(true);
            }
        }

        /// <summary>
        /// The crate comes loose, contents and all: it floats in the water (its ship was destroyed, or its carrier
        /// went swimming) or tumbles off a broken stack, until someone picks it up. Owner only.
        /// </summary>
        public void MakeLoose(bool tumble)
        {
            if (CrateSetup.FloatingPrefab == null)
            {
                Plugin.Log.LogWarning("No loose crate prefab; leaving the cargo crate where it is.");
                return;
            }
            // Full size again (it's drawn smaller while carried), where the crate is now.
            GameObject loose = CratePlacement.SpawnWithContents(CrateSetup.FloatingPrefab, transform.position, transform.rotation, _nview.GetZDO().GetByteArray(ZDOVars.s_items));
            Rigidbody body = loose.GetComponent<Rigidbody>();
            if (tumble && body != null)
            {
                Vector3 push = Random.insideUnitSphere * TumblePush;
                push.y = 0f;
                body.linearVelocity = push;
                body.angularVelocity = Random.insideUnitSphere * TumbleSpin;
            }
            _nview.Destroy();
        }

        // Picked up again it's a placed crate, upright, and that one is carried.
        private void LiftLoose(Player player)
        {
            Quaternion upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            GameObject crate = CratePlacement.SpawnWithContents(CrateSetup.CratePrefab, transform.position, upright, _nview.GetZDO().GetByteArray(ZDOVars.s_items));
            _nview.Destroy();
            crate.GetComponent<CrateCarry>().Lift(player);
        }
    }
}
