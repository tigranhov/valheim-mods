using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// A crate carried by a player, on the back or in front (see <see cref="CarryMode"/>). The crate stays a world
    /// object, contents and all; every client holds it at the same pose relative to its copy of the carrier, the way
    /// <see cref="ShipPassenger"/> rides a ship. The carrier is stored as the player's ZDO id, which is only valid
    /// within a session: after a reload, or if the carrier leaves, the crate's owner sets it down where it is.
    /// </summary>
    internal sealed class CrateCarry : MonoBehaviour
    {
        private static readonly int CarrierUserKey = "IM_CarrierUser".GetStableHashCode();
        private static readonly int CarrierIdKey = "IM_CarrierId".GetStableHashCode();
        private const float CarrierMissingSeconds = 3f;
        private const float ZdoSyncSeconds = 0.25f;
        private const float SettleProbe = 20f;

        // Where the crate's center sits relative to the carrier's feet (meters up, meters forward).
        private const float FrontHeight = 1.0f;
        private const float FrontGap = 0.25f;
        private const float BackHeight = 1.3f;
        private const float BackGap = 0.15f;
        // Hands grip the sides slightly inside the box and a little below its middle.
        private const float HandInset = 0.02f;
        private const float HandDrop = 0.05f;

        private static readonly List<CrateCarry> Instances = new List<CrateCarry>();
        private static Player _cachedPlayer;
        private static int _cachedFrame = -1;
        private static CrateCarry _cachedCrate;

        private ZNetView _nview;
        private CargoCrate _crate;
        private ShipPassenger _passenger;
        private Collider[] _colliders;
        private ZDOID _carrierId = ZDOID.None;
        private Player _carrier;
        private uint _revision = uint.MaxValue;
        private float _missingFor;
        private float _syncTimer;
        private bool _collidersOff;
        // Whose weapon was put away for a crate held in front; the carrier reference is gone by the time it's set down.
        private Player _handsHiddenOn;

        public bool IsCarried => !_carrierId.IsNone();

        public Player Carrier => _carrier;

        /// <summary>The crate <paramref name="player"/> is carrying, if any. Cached per frame: this is asked often.</summary>
        public static CrateCarry CarriedBy(Player player)
        {
            if (player == null)
            {
                return null;
            }
            if (player == _cachedPlayer && Time.frameCount == _cachedFrame)
            {
                return _cachedCrate;
            }
            _cachedPlayer = player;
            _cachedFrame = Time.frameCount;
            _cachedCrate = null;
            foreach (CrateCarry crate in Instances)
            {
                if (crate._carrier == player && crate.IsCarried)
                {
                    _cachedCrate = crate;
                    break;
                }
            }
            return _cachedCrate;
        }

        public static bool IsCarrying(Player player)
        {
            return CarriedBy(player) != null;
        }

        public static CarryMode Style => CargoConfig.PickUpMode.Value == CarryMode.Back ? CarryMode.Back : CarryMode.Front;

        /// <summary>Where a carried crate's center is, and how it's turned, for this carrier.</summary>
        public static void CarryPose(Player carrier, out Vector3 center, out Quaternion rotation)
        {
            Transform t = carrier.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            float halfDepth = CrateShape.Size.z * 0.5f;
            center = Style == CarryMode.Back
                ? t.position + Vector3.up * BackHeight - forward * (BackGap + halfDepth)
                : t.position + Vector3.up * FrontHeight + forward * (FrontGap + halfDepth);
        }

        /// <summary>Hand targets on the sides of a crate carried in front.</summary>
        public static void HandTargets(Player carrier, out Vector3 left, out Vector3 right)
        {
            CarryPose(carrier, out Vector3 center, out Quaternion rotation);
            Vector3 side = rotation * Vector3.right * (CrateShape.Size.x * 0.5f - HandInset);
            Vector3 down = Vector3.down * HandDrop;
            left = center - side + down;
            right = center + side + down;
        }

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            _crate = GetComponent<CargoCrate>();
            _passenger = GetComponent<ShipPassenger>();
            _colliders = GetComponentsInChildren<Collider>(true);
            Instances.Add(this);
            Refresh();
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            ShowHands();
        }

        /// <summary>Takes the crate off the ground (or deck) into the player's hands. Owner only.</summary>
        public void Lift(Player player)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            _passenger.StopRiding();
            ZDOID id = player.GetZDOID();
            ZDO zdo = _nview.GetZDO();
            zdo.Set(CarrierUserKey, id.UserID);
            zdo.Set(CarrierIdKey, (int)id.ID);
            Refresh();
            _carrier = player;
            CrateGhost.BeginCarried(player, this);
        }

        /// <summary>Sets the crate down at this pose, riding <paramref name="ship"/> if given. Owner only.</summary>
        public void PutDown(Vector3 position, Quaternion rotation, Ship ship)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            ZDO zdo = _nview.GetZDO();
            zdo.Set(CarrierUserKey, 0L);
            zdo.Set(CarrierIdKey, 0);
            transform.SetPositionAndRotation(position, rotation);
            zdo.SetPosition(position);
            zdo.SetRotation(rotation);
            Refresh();
            if (ship != null)
            {
                _passenger.AttachTo(ship);
            }
        }

        /// <summary>Puts the crate down at the nearest free spot; failing that, right where the carrier stands.</summary>
        public void PutDownNearby()
        {
            if (_carrier != null && CratePlacement.FindDropSpot(_carrier, out Vector3 position, out Quaternion rotation, out Ship ship))
            {
                PutDown(position, rotation, ship);
                return;
            }
            Settle();
        }

        private void Refresh()
        {
            ZDO zdo = _nview.GetZDO();
            _revision = zdo.DataRevision;
            var id = new ZDOID(zdo.GetLong(CarrierUserKey, 0L), (uint)zdo.GetInt(CarrierIdKey, 0));
            if (id != _carrierId)
            {
                _carrierId = id;
                _carrier = null;
                _missingFor = 0f;
            }
            SetCollidersEnabled(!IsCarried);
            if (!IsCarried)
            {
                ShowHands();
            }
        }

        private void LateUpdate()
        {
            ZDO zdo = _nview.GetZDO();
            if (zdo == null || ZNetScene.instance == null)
            {
                return;
            }
            if (zdo.DataRevision != _revision)
            {
                Refresh();
            }
            if (!IsCarried)
            {
                return;
            }
            if (_carrier == null)
            {
                GameObject found = ZNetScene.instance.FindInstance(_carrierId);
                _carrier = found != null ? found.GetComponent<Player>() : null;
                if (_carrier == null)
                {
                    WaitForCarrier();
                    return;
                }
            }
            CarryPose(_carrier, out Vector3 center, out Quaternion rotation);
            transform.SetPositionAndRotation(center - rotation * CrateShape.Center, rotation);
            UpdateHands();
            if (!_nview.IsOwner())
            {
                return;
            }
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= ZdoSyncSeconds)
            {
                _syncTimer = 0f;
                zdo.SetPosition(transform.position);
                zdo.SetRotation(transform.rotation);
            }
            if (_carrier == Player.m_localPlayer)
            {
                CheckMustPutDown();
            }
        }

        // Things you can't do with a crate in your arms. Swimming drops it in the water, where it floats away.
        private void CheckMustPutDown()
        {
            Player p = _carrier;
            if (p.IsSwimming())
            {
                p.Message(MessageHud.MessageType.Center, "You dropped the crate in the water");
                _crate.SpillIntoWater();
                return;
            }
            if (p.IsDead() || p.IsTeleporting() || p.IsAttached() || p.InPlaceMode())
            {
                PutDownNearby();
            }
        }

        // The carrier isn't here (left the game, or the world was reloaded): the owner sets the crate down.
        private void WaitForCarrier()
        {
            _missingFor += Time.deltaTime;
            if (_missingFor >= CarrierMissingSeconds && _nview.IsOwner())
            {
                Settle();
            }
        }

        // Straight down onto whatever is below, fitting or not, so a crate is never left hanging in the air.
        private void Settle()
        {
            Vector3 center = transform.TransformPoint(CrateShape.Center);
            if (Physics.Raycast(center, Vector3.down, out RaycastHit hit, SettleProbe, CrateFit.Mask, QueryTriggerInteraction.Ignore))
            {
                float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
                Ship ship = CratePlacement.ShipOf(hit.collider);
                if (ship != null || hit.point.y >= water)
                {
                    float yaw = ship != null ? 0f : transform.rotation.eulerAngles.y;
                    CrateFit.RestOn(hit, yaw, out Vector3 position, out Quaternion rotation, out ship);
                    PutDown(position, rotation, ship);
                    return;
                }
            }
            _crate.SpillIntoWater();
        }

        // A crate held in front goes where the weapon was.
        private void UpdateHands()
        {
            bool hide = Style == CarryMode.Front && _carrier == Player.m_localPlayer;
            if (hide && _handsHiddenOn == null)
            {
                if (_carrier.HideHandItems())
                {
                    _handsHiddenOn = _carrier;
                }
            }
            else if (!hide)
            {
                ShowHands();
            }
        }

        private void ShowHands()
        {
            if (_handsHiddenOn != null)
            {
                _handsHiddenOn.ShowHandItems();
            }
            _handsHiddenOn = null;
        }

        // While carried, the crate touches nothing: it can't shove its carrier or catch on doorways.
        private void SetCollidersEnabled(bool enabled)
        {
            if (_collidersOff == !enabled)
            {
                return;
            }
            _collidersOff = !enabled;
            foreach (Collider c in _colliders)
            {
                if (c != null)
                {
                    c.enabled = enabled;
                }
            }
        }
    }
}
