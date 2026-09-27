using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// A crate carried in a player's arms. The crate stays a world object, contents and all; every client holds it at
    /// the same pose relative to its copy of the carrier, the way <see cref="ShipPassenger"/> rides a ship, moving with
    /// the carrier's torso as it walks. The carrier is stored as the player's ZDO id, which is only valid within a
    /// session: after a reload, or if the carrier leaves, the crate's owner sets it down where it is.
    /// </summary>
    internal sealed class CrateCarry : MonoBehaviour
    {
        private static readonly int CarrierUserKey = "IM_CarrierUser".GetStableHashCode();
        private static readonly int CarrierIdKey = "IM_CarrierId".GetStableHashCode();
        private const float CarrierMissingSeconds = 3f;
        // Every update sends the crate's whole contents to each player nearby; others hold it by its carrier anyway,
        // so the ZDO position only has to keep up roughly (to be in the right zone, and set down there after a reload).
        private const float ZdoSyncSeconds = 0.5f;
        private const float SettleProbe = 20f;
        // How quickly (per second) the torso's resting place is re-learned, e.g. when crouching or on a slope.
        private const float BodyRestFollow = 2f;

        // Who carries what, kept as carriers come and go: asked several times a frame (encumbrance, running, IK).
        private static readonly Dictionary<Player, CrateCarry> ByCarrier = new Dictionary<Player, CrateCarry>();

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
        // Whose weapon was put away for the crate; the carrier reference is gone by the time it's set down.
        private Player _handsHiddenOn;
        private Vector3 _baseScale = Vector3.one;
        // The torso's walk motion, in the carrier's facing frame: where it rests on average, and how far it is from that.
        private Vector3 _bodyRest;
        private Vector3 _bodyMotion;
        private bool _bodyKnown;
        private int _bodyFrame = -1;

        public bool IsCarried => !_carrierId.IsNone();

        public Player Carrier => _carrier;

        /// <summary>The crate itself plus its contents.</summary>
        public float Weight => _passenger != null ? _passenger.Weight : CargoConfig.CrateWeight.Value;

        /// <summary>The crate <paramref name="player"/> is carrying, if any.</summary>
        public static CrateCarry CarriedBy(Player player)
        {
            return player != null && ByCarrier.TryGetValue(player, out CrateCarry crate) ? crate : null;
        }

        public static bool IsCarrying(Player player)
        {
            return CarriedBy(player) != null;
        }

        /// <summary>Where the crate's center is and how it's turned, walk bob included (see "5 - Carry tuning").</summary>
        public void GetPose(out Vector3 center, out Quaternion rotation)
        {
            Transform t = _carrier.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            // Positive tilt leans the top back, against the chest.
            rotation = facing * Quaternion.Euler(-CargoConfig.FrontTilt.Value, 0f, 0f);
            float halfDepth = CrateShape.Size.z * 0.5f * CargoConfig.FrontScale.Value;
            center = t.position + Vector3.up * CargoConfig.FrontHeight.Value + forward * (CargoConfig.FrontDistance.Value + halfDepth)
                + facing * (_bodyMotion * CargoConfig.BodyFollow.Value);
        }

        /// <summary>Where the hands grip the crate's sides, and how they're turned (palms in, fingers forward).</summary>
        public void GetHands(out Vector3 left, out Quaternion leftRotation, out Vector3 right, out Quaternion rightRotation)
        {
            GetPose(out Vector3 center, out Quaternion rotation);
            Vector3 grip = center + rotation * new Vector3(0f, CargoConfig.HandHeight.Value, CargoConfig.HandForward.Value);
            Vector3 side = rotation * Vector3.right * (CrateShape.Size.x * 0.5f * CargoConfig.FrontScale.Value - CargoConfig.HandInset.Value);
            left = grip - side;
            right = grip + side;
            // Humanoid hand goals are relative to the T-pose (fingers out to the sides, palms down): turn the fingers
            // forward and the palms in toward the crate, then the tuning offsets (mirrored for the left hand).
            float pitch = CargoConfig.HandPitch.Value;
            float yaw = CargoConfig.HandYaw.Value;
            float roll = CargoConfig.HandRoll.Value;
            rightRotation = rotation * Quaternion.Euler(pitch, -yaw, -roll) * Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, -90f, 0f);
            leftRotation = rotation * Quaternion.Euler(pitch, yaw, roll) * Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 90f, 0f);
        }

        /// <summary>Where the elbows should point: out to the sides, a little below and behind the hands.</summary>
        public void GetElbows(out Vector3 left, out Vector3 right)
        {
            GetHands(out Vector3 leftHand, out _, out Vector3 rightHand, out _);
            GetPose(out _, out Quaternion rotation);
            Vector3 outward = rotation * Vector3.right * CargoConfig.ElbowOut.Value;
            Vector3 offset = Vector3.down * CargoConfig.ElbowDown.Value - rotation * Vector3.forward * 0.3f;
            left = leftHand - outward + offset;
            right = rightHand + outward + offset;
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
            _baseScale = transform.localScale;
            Refresh();
        }

        private void OnDestroy()
        {
            SetCarrier(null);
            ShowHands();
        }

        /// <summary>Takes the crate off the ground (or deck) into the player's arms. Owner only.</summary>
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
            SetCarrier(player);
            CrateGhost.BeginCarried(player, this);
            player.Message(MessageHud.MessageType.TopLeft, $"Carrying a crate ({Weight:0} weight)");
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

        /// <summary>
        /// Follows the carrier's torso through the walk cycle, from the animator's body position for this frame (read
        /// in the IK pass, before the crate is placed in LateUpdate). Moving the crate and hands with the torso keeps
        /// the shoulder-to-hand distance steady, so the elbows don't pump with every step.
        /// </summary>
        public void TrackBody(Vector3 bodyPosition)
        {
            if (_carrier == null || Time.frameCount == _bodyFrame)
            {
                return;
            }
            _bodyFrame = Time.frameCount;
            Transform t = _carrier.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)) * (bodyPosition - t.position);
            if (!_bodyKnown)
            {
                _bodyRest = local;
                _bodyKnown = true;
            }
            _bodyRest = Vector3.Lerp(_bodyRest, local, Mathf.Clamp01(BodyRestFollow * Time.deltaTime));
            _bodyMotion = local - _bodyRest;
        }

        private void SetCarrier(Player player)
        {
            // By reference: a carrier that was just destroyed still has its entry to remove.
            if (!ReferenceEquals(_carrier, null) && ByCarrier.TryGetValue(_carrier, out CrateCarry carried) && carried == this)
            {
                ByCarrier.Remove(_carrier);
            }
            _carrier = player;
            if (player != null)
            {
                ByCarrier[player] = this;
            }
            _bodyKnown = false;
            _bodyMotion = Vector3.zero;
        }

        private void Refresh()
        {
            ZDO zdo = _nview.GetZDO();
            _revision = zdo.DataRevision;
            var id = new ZDOID(zdo.GetLong(CarrierUserKey, 0L), (uint)zdo.GetInt(CarrierIdKey, 0));
            if (id != _carrierId)
            {
                _carrierId = id;
                SetCarrier(null);
                _missingFor = 0f;
            }
            SetCollidersEnabled(!IsCarried);
            if (!IsCarried)
            {
                transform.localScale = _baseScale;
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
                SetCarrier(found != null ? found.GetComponent<Player>() : null);
                if (_carrier == null)
                {
                    WaitForCarrier();
                    return;
                }
            }
            // Drawn smaller while carried (colliders are off, so only the look changes); full size again when set down.
            float scale = CargoConfig.FrontScale.Value;
            GetPose(out Vector3 center, out Quaternion rotation);
            transform.localScale = _baseScale * scale;
            transform.SetPositionAndRotation(center - rotation * (CrateShape.Center * scale), rotation);
            HideHands();
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
                _crate.MakeLoose(false);
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

        // Straight down onto whatever is below, fitting or not, so a crate is never left hanging in the air; into the
        // water if that's what is below.
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
            _crate.MakeLoose(false);
        }

        // The crate goes where the weapon was.
        private void HideHands()
        {
            if (_carrier == Player.m_localPlayer && _handsHiddenOn == null && _carrier.HideHandItems())
            {
                _handsHiddenOn = _carrier;
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
