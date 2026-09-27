using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Keeps an object riding on a ship. Every client places it at the same fixed offset from its own copy of the
    /// ship each frame, so it looks rigid for everyone and never slides or falls off. It is deliberately not
    /// parented to the ship: a networked child is destroyed along with its parent when the ship's area unloads,
    /// which leaves ZNetScene holding a dead instance.
    ///
    /// The passenger stores no ship id. ZDO ids change when a world loads, and the game's saved links
    /// (ZDO connections) keep only one link per target, so several crates on one ship would lose all but one.
    /// Instead it stores its offset on the deck, and finds its ship again by that offset: the loaded ship whose
    /// deck has this passenger sitting exactly there. The owner keeps the passenger's ZDO position close behind
    /// the ship so that match holds after a reload.
    /// </summary>
    internal sealed class ShipPassenger : MonoBehaviour
    {
        private const string ShipLostRpc = "IM_ShipLost";
        // How far off the stored deck offset a ship may be and still be recognized as this passenger's ship.
        private const float MatchTolerance = 3f;
        private const float MatchRetrySeconds = 0.5f;
        // Give up on a ship that hasn't shown up for this long (it's gone, e.g. destroyed while unloaded).
        private const float LostAfterSeconds = 60f;
        // The owner writes the ZDO position when the passenger has moved this far, at most every ZdoSyncSeconds.
        private const float ZdoSyncDistance = 0.25f;
        private const float ZdoSyncAngle = 2f;
        private const float ZdoSyncSeconds = 0.1f;

        private static readonly int OnShipKey = "IM_OnShip".GetStableHashCode();
        private static readonly int LocalPosKey = "IM_ShipLocalPos".GetStableHashCode();
        private static readonly int LocalRotKey = "IM_ShipLocalRot".GetStableHashCode();

        private static readonly List<ShipPassenger> Instances = new List<ShipPassenger>();
        private static readonly HashSet<Ship> LoadedShips = new HashSet<Ship>();

        /// <summary>Raised on the passenger's owner when its ship sank or was destroyed. It no longer rides.</summary>
        public event Action ShipLost;

        private ZNetView _nview;
        private Collider[] _colliders;
        private bool _onShip;
        private Vector3 _localPos;
        private Quaternion _localRot = Quaternion.identity;
        private Ship _ship;
        private Rigidbody _shipBody;
        private readonly List<Collider> _ignoring = new List<Collider>();
        private uint _revision = uint.MaxValue;
        private float _matchTimer;
        private float _missingFor;
        private float _syncTimer;
        private Vector3 _syncedPos;
        private Quaternion _syncedRot;

        public bool IsAttached => _onShip;

        /// <summary>The ship this rides on, if it's loaded here.</summary>
        public Ship CurrentShip => _ship;

        /// <summary>The riding ship's physics body: standing on this passenger counts as standing on it.</summary>
        public Rigidbody ShipBody => _ship != null ? _shipBody : null;

        /// <summary>How hard this presses on its ship (see <see cref="CargoWeight"/>), kept up to date by its owner component.</summary>
        public float Weight { get; set; }

        public static IReadOnlyList<ShipPassenger> All => Instances;

        public static void ShipLoaded(Ship ship)
        {
            LoadedShips.Add(ship);
        }

        public static void ShipUnloaded(Ship ship)
        {
            LoadedShips.Remove(ship);
        }

        /// <summary>The ship is really being destroyed (not just unloaded): its passengers go into the water.</summary>
        public static void NotifyShipDestroyed(Ship ship)
        {
            foreach (ShipPassenger passenger in Instances.ToArray())
            {
                if (passenger._ship == ship)
                {
                    passenger._nview.InvokeRPC(ShipLostRpc);
                }
            }
        }

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            _colliders = GetComponentsInChildren<Collider>(true);
            _nview.Register(ShipLostRpc, RPC_ShipLost);
            Instances.Add(this);
            Refresh();
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
        }

        /// <summary>Starts riding <paramref name="ship"/> at the current pose. Owner only.</summary>
        public void AttachTo(Ship ship)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            Transform t = ship.transform;
            ZDO zdo = _nview.GetZDO();
            zdo.Set(LocalPosKey, t.InverseTransformPoint(transform.position));
            zdo.Set(LocalRotKey, Quaternion.Inverse(t.rotation) * transform.rotation);
            zdo.Set(OnShipKey, true);
            SyncZdoPose(zdo);
            Refresh();
            SetShip(ship);
        }

        /// <summary>Stops riding (it was lifted off the deck). Owner only.</summary>
        public void StopRiding()
        {
            if (_nview.IsOwner() && _onShip)
            {
                Detach();
            }
        }

        private void Detach()
        {
            _nview.GetZDO().Set(OnShipKey, false);
            Refresh();
        }

        private void RPC_ShipLost(long sender)
        {
            if (!_nview.IsOwner() || !_onShip)
            {
                return;
            }
            Detach();
            ShipLost?.Invoke();
        }

        private void Refresh()
        {
            ZDO zdo = _nview.GetZDO();
            _revision = zdo.DataRevision;
            // Crates placed by the first build have the offset but no flag.
            bool onShip = zdo.GetBool(OnShipKey, zdo.GetVec3(LocalPosKey, out _));
            _localPos = zdo.GetVec3(LocalPosKey, Vector3.zero);
            _localRot = zdo.GetQuaternion(LocalRotKey, Quaternion.identity);
            if (onShip != _onShip)
            {
                _onShip = onShip;
                _missingFor = 0f;
                SetShip(null);
            }
        }

        // Before each physics step: sit at the ship's physics pose, so players standing on this have solid footing.
        // (LateUpdate then moves it to the ship's rendered pose, which may be interpolated.)
        private void FixedUpdate()
        {
            if (_onShip && _ship != null && _shipBody != null)
            {
                transform.SetPositionAndRotation(_shipBody.position + _shipBody.rotation * _localPos, _shipBody.rotation * _localRot);
            }
        }

        private void LateUpdate()
        {
            ZDO zdo = _nview.GetZDO();
            if (zdo == null)
            {
                return;
            }
            if (zdo.DataRevision != _revision)
            {
                Refresh();
            }
            if (!_onShip)
            {
                return;
            }
            if (_ship == null && !FindShip(zdo))
            {
                return;
            }
            Transform ship = _ship.transform;
            transform.SetPositionAndRotation(ship.TransformPoint(_localPos), ship.rotation * _localRot);
            if (_nview.IsOwner())
            {
                _syncTimer += Time.deltaTime;
                if (_syncTimer >= ZdoSyncSeconds
                    && ((transform.position - _syncedPos).sqrMagnitude > ZdoSyncDistance * ZdoSyncDistance
                        || Quaternion.Angle(transform.rotation, _syncedRot) > ZdoSyncAngle))
                {
                    SyncZdoPose(zdo);
                }
            }
        }

        private void SyncZdoPose(ZDO zdo)
        {
            _syncTimer = 0f;
            _syncedPos = transform.position;
            _syncedRot = transform.rotation;
            zdo.SetPosition(_syncedPos);
            zdo.SetRotation(_syncedRot);
        }

        // Look for the loaded ship whose deck has this passenger at its stored offset. Compares saved (ZDO) poses
        // on both sides, which were written together when the world was saved.
        private bool FindShip(ZDO zdo)
        {
            _matchTimer -= Time.deltaTime;
            if (_matchTimer > 0f)
            {
                return false;
            }
            _matchTimer = MatchRetrySeconds;
            Vector3 position = zdo.GetPosition();
            Ship best = null;
            float bestError = MatchTolerance;
            foreach (Ship ship in LoadedShips)
            {
                ZNetView view = ship != null ? ship.GetComponent<ZNetView>() : null;
                ZDO shipZdo = view != null ? view.GetZDO() : null;
                if (shipZdo == null)
                {
                    continue;
                }
                Vector3 offset = Quaternion.Inverse(shipZdo.GetRotation()) * (position - shipZdo.GetPosition());
                float error = Vector3.Distance(offset, _localPos);
                if (error < bestError)
                {
                    bestError = error;
                    best = ship;
                }
            }
            if (best != null)
            {
                _missingFor = 0f;
                SetShip(best);
                return true;
            }
            _missingFor += MatchRetrySeconds;
            if (_missingFor >= LostAfterSeconds && _nview.IsOwner())
            {
                GiveUpOnShip();
            }
            return false;
        }

        // The ship never showed up. Stop riding and stay put, so the crate can simply be picked up. Cargo only
        // spills when a ship is seen being destroyed, never on a guess.
        private void GiveUpOnShip()
        {
            Plugin.Log.LogInfo($"{name}: no matching ship within {LostAfterSeconds:0} s; it no longer rides a ship.");
            Detach();
        }

        // Riding on a ship means overlapping its colliders: without this the ship would be shoved by its own cargo.
        private void SetShip(Ship ship)
        {
            foreach (Collider shipCollider in _ignoring)
            {
                SetIgnored(shipCollider, false);
            }
            _ignoring.Clear();
            _ship = ship;
            _shipBody = ship != null ? ship.GetComponent<Rigidbody>() : null;
            if (ship == null)
            {
                return;
            }
            foreach (Collider shipCollider in ship.GetComponentsInChildren<Collider>())
            {
                _ignoring.Add(shipCollider);
                SetIgnored(shipCollider, true);
            }
        }

        private void SetIgnored(Collider shipCollider, bool ignore)
        {
            if (shipCollider == null)
            {
                return;
            }
            foreach (Collider own in _colliders)
            {
                if (own != null)
                {
                    Physics.IgnoreCollision(own, shipCollider, ignore);
                }
            }
        }
    }
}
