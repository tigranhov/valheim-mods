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
    /// The ship link is a ZDO "SyncTransform" connection, the same one used for players standing on ships: the game
    /// saves it as a hash pair and re-links it on world load (ZDOMan.ConnectSyncTransforms), since ZDO ids change.
    /// </summary>
    internal sealed class ShipPassenger : MonoBehaviour
    {
        private const string ShipLostRpc = "IM_ShipLost";
        private const float ZdoSyncSeconds = 1f;
        private const float ShipCheckSeconds = 5f;
        private static readonly int LocalPosKey = "IM_ShipLocalPos".GetStableHashCode();
        private static readonly int LocalRotKey = "IM_ShipLocalRot".GetStableHashCode();

        private static readonly List<ShipPassenger> Instances = new List<ShipPassenger>();

        /// <summary>Raised on the passenger's owner once its ship is gone for good. The link is already cleared.</summary>
        public event Action ShipLost;

        private ZNetView _nview;
        private Collider[] _colliders;
        private ZDOID _shipId = ZDOID.None;
        private Vector3 _localPos;
        private Quaternion _localRot = Quaternion.identity;
        private Transform _ship;
        private readonly List<Collider> _ignoring = new List<Collider>();
        private uint _revision = uint.MaxValue;
        private float _zdoTimer;
        private float _checkTimer;

        public bool IsAttached => !_shipId.IsNone();

        /// <summary>The ship this rides on, if it's loaded here.</summary>
        public Ship CurrentShip => _ship != null ? _ship.GetComponent<Ship>() : null;

        public static void NotifyShipDestroyed(ZDOID shipId)
        {
            foreach (ShipPassenger passenger in Instances.ToArray())
            {
                if (passenger._shipId == shipId)
                {
                    passenger.RequestShipLost();
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
            ZNetView shipView = ship.GetComponent<ZNetView>();
            if (!_nview.IsOwner() || shipView == null || shipView.GetZDO() == null)
            {
                return;
            }
            Transform t = ship.transform;
            ZDO zdo = _nview.GetZDO();
            zdo.Set(LocalPosKey, t.InverseTransformPoint(transform.position));
            zdo.Set(LocalRotKey, Quaternion.Inverse(t.rotation) * transform.rotation);
            zdo.SetConnection(ZDOExtraData.ConnectionType.SyncTransform, shipView.GetZDO().m_uid);
            Refresh();
        }

        private void RequestShipLost()
        {
            // Goes to the owner.
            _nview.InvokeRPC(ShipLostRpc);
        }

        private void RPC_ShipLost(long sender)
        {
            if (!_nview.IsOwner() || !IsAttached)
            {
                return;
            }
            _nview.GetZDO().UpdateConnection(ZDOExtraData.ConnectionType.SyncTransform, ZDOID.None);
            Refresh();
            ShipLost?.Invoke();
        }

        private void Refresh()
        {
            ZDO zdo = _nview.GetZDO();
            _revision = zdo.DataRevision;
            _localPos = zdo.GetVec3(LocalPosKey, Vector3.zero);
            _localRot = zdo.GetQuaternion(LocalRotKey, Quaternion.identity);
            ZDOID ship = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform);
            if (ship != _shipId)
            {
                _shipId = ship;
                SetShip(null);
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
            if (!IsAttached)
            {
                return;
            }
            if (_ship == null)
            {
                GameObject found = ZNetScene.instance.FindInstance(_shipId);
                if (found == null)
                {
                    CheckShipGone();
                    return;
                }
                SetShip(found.transform);
            }
            transform.SetPositionAndRotation(_ship.TransformPoint(_localPos), _ship.rotation * _localRot);
            // The owner keeps the ZDO's position following the ship, so the passenger loads and unloads with it.
            if (_nview.IsOwner())
            {
                _zdoTimer += Time.deltaTime;
                if (_zdoTimer >= ZdoSyncSeconds)
                {
                    _zdoTimer = 0f;
                    zdo.SetPosition(transform.position);
                    zdo.SetRotation(transform.rotation);
                }
            }
        }

        // The server has every ZDO, so only it can tell "ship destroyed" apart from "ship not loaded here yet".
        private void CheckShipGone()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer < ShipCheckSeconds)
            {
                return;
            }
            _checkTimer = 0f;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance.GetZDO(_shipId) != null)
            {
                return;
            }
            if (!_nview.HasOwner())
            {
                _nview.ClaimOwnership();
            }
            RequestShipLost();
        }

        // Riding on a ship means overlapping its colliders: without this the ship would be shoved by its own cargo.
        private void SetShip(Transform ship)
        {
            foreach (Collider shipCollider in _ignoring)
            {
                SetIgnored(shipCollider, false);
            }
            _ignoring.Clear();
            _ship = ship;
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
