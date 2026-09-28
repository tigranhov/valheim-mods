using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// A ship's log line, on every ship (added when a ship wakes). Once fitted, a reel sits at the stern with a line
    /// trailing in the water, and the ship's owner (whoever is simulating it) counts the ground the ship covers while
    /// it moves. The count is saved on the ship, so it survives reloads and everyone aboard reads the same number.
    /// Changes go through the ship's owner, like vanilla ship controls.
    /// </summary>
    internal sealed class LogLine : MonoBehaviour
    {
        private const string FittedKey = "IM_Log_Fitted";
        private const string RunKey = "IM_Log_Run";
        private const string StrideKey = "IM_Log_Stride";
        private const string FitRpc = "IM_Log_Fit";
        private const string FitResultRpc = "IM_Log_FitResult";
        private const string HaulRpc = "IM_Log_Haul";
        private const string RemoveRpc = "IM_Log_Remove";
        private const string RemovedRpc = "IM_Log_Removed";

        // Slower than this is drifting at anchor, not sailing.
        private const float MinSpeed = 0.3f;
        // More than this in one frame is a teleport or a reload, not sailing.
        private const float MaxFrameMeters = 30f;
        private const float FlushEvery = 1f;
        private const int RopePoints = 10;

        private Ship _ship;
        private ZNetView _nview;
        private Transform _reel;
        private LineRenderer _rope;
        private Vector3 _last;
        private bool _tracking;
        private float _pending;
        private float _sinceFlush;

        private bool Valid => _nview != null && _nview.IsValid();

        public bool Fitted => Valid && _nview.GetZDO().GetBool(FittedKey);

        /// <summary>The run so far, in paces (the tally's unit, so it lays out at a table like a walked leg).</summary>
        public int Paces
        {
            get
            {
                if (!Valid)
                {
                    return 0;
                }
                ZDO zdo = _nview.GetZDO();
                float meters = zdo.GetFloat(RunKey) + (_nview.IsOwner() ? _pending : 0f);
                return Mathf.RoundToInt(meters * zdo.GetFloat(StrideKey, 1f) / KitConfig.PaceLength.Value);
            }
        }

        /// <summary>The ship under a player: the deck they stand on, or the one they sit or steer on.</summary>
        public static LogLine OnShipUnder(Player player)
        {
            Ship ship = player.GetStandingOnShip();
            if (ship == null && player.IsAttachedToShip() && player.m_attachPoint != null)
            {
                ship = player.m_attachPoint.GetComponentInParent<Ship>();
            }
            return ship != null ? ship.GetComponent<LogLine>() : null;
        }

        /// <summary>Using the log line item: fits it to the ship you stand on. True when the use was handled.</summary>
        public static bool TryFit(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            LogLine log = OnShipUnder(player);
            if (log == null || !log.Valid)
            {
                player.Message(MessageHud.MessageType.Center, "Stand on a ship's deck to fit the log line");
                return true;
            }
            if (log.Fitted)
            {
                player.Message(MessageHud.MessageType.Center, "This ship already has a log line");
                return true;
            }
            // Taken now and given back if the owner says another one got there first.
            inventory.RemoveOneItem(item);
            log._nview.InvokeRPC(FitRpc);
            player.Message(MessageHud.MessageType.Center, "Log line fitted at the stern");
            return true;
        }

        public void Haul()
        {
            if (Valid)
            {
                _nview.InvokeRPC(HaulRpc);
            }
        }

        public void TakeOff()
        {
            if (Valid)
            {
                _nview.InvokeRPC(RemoveRpc);
            }
        }

        /// <summary>The ship is being destroyed (its owner calls this): the log line floats free as an item.</summary>
        public void DropFree()
        {
            if (Fitted && LogLineSetup.Prefab != null)
            {
                Instantiate(LogLineSetup.Prefab, transform.position + Vector3.up, Quaternion.identity);
            }
        }

        private void Awake()
        {
            _ship = GetComponent<Ship>();
            _nview = GetComponent<ZNetView>();
            if (!Valid)
            {
                enabled = false;
                return;
            }
            _nview.Register(FitRpc, RPC_Fit);
            _nview.Register<bool>(FitResultRpc, RPC_FitResult);
            _nview.Register(HaulRpc, RPC_Haul);
            _nview.Register(RemoveRpc, RPC_Remove);
            _nview.Register(RemovedRpc, RPC_Removed);
        }

        private void Update()
        {
            if (!Valid)
            {
                return;
            }
            bool fitted = Fitted;
            if (fitted != (_reel != null))
            {
                if (fitted)
                {
                    BuildReel();
                }
                else
                {
                    Destroy(_reel.gameObject);
                    _reel = null;
                    _rope = null;
                }
            }
            if (fitted && _nview.IsOwner())
            {
                Count();
            }
            else
            {
                _tracking = false;
                _pending = 0f;
            }
        }

        private void LateUpdate()
        {
            if (_rope != null)
            {
                LayRope();
            }
        }

        private void Count()
        {
            Vector3 position = transform.position;
            float dt = Time.deltaTime;
            if (_tracking && dt > 0f)
            {
                Vector3 step = position - _last;
                step.y = 0f;
                float meters = step.magnitude;
                if (meters < MaxFrameMeters && meters / dt > MinSpeed)
                {
                    _pending += meters;
                }
            }
            _last = position;
            _tracking = true;
            _sinceFlush += dt;
            if (_pending > 0f && _sinceFlush >= FlushEvery)
            {
                ZDO zdo = _nview.GetZDO();
                zdo.Set(RunKey, zdo.GetFloat(RunKey) + _pending);
                _pending = 0f;
                _sinceFlush = 0f;
            }
        }

        // The reel sits at the stern, on the other side from the steering spot.
        private void BuildReel()
        {
            Transform helm = _ship.m_shipControlls != null ? _ship.m_shipControlls.transform : transform;
            Vector3 local = transform.InverseTransformPoint(helm.position);
            float across = local.x > 0.05f ? -1f : 1f;
            _reel = Models.Visual(transform, "IM_LogReel", LogLineSetup.BuildReel);
            _reel.localPosition = new Vector3(local.x + across * KitConfig.ReelSide.Value, local.y + KitConfig.ReelUp.Value, local.z - KitConfig.ReelBack.Value);
            _reel.localRotation = Quaternion.identity;

            // A trigger on a non-solid layer: something to point at and press E on, that nothing bumps into.
            int layer = LayerMask.NameToLayer("piece_nonsolid");
            _reel.gameObject.layer = layer;
            var collider = _reel.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.3f, 0.3f, 0.3f);
            _reel.gameObject.AddComponent<LogLineReel>().Log = this;

            var rope = new GameObject("rope");
            rope.transform.SetParent(_reel, false);
            _rope = rope.AddComponent<LineRenderer>();
            _rope.positionCount = RopePoints;
            _rope.useWorldSpace = true;
            _rope.widthMultiplier = 0.02f;
            _rope.textureMode = LineTextureMode.Tile;
            _rope.generateLightingData = true;
            _rope.shadowCastingMode = ShadowCastingMode.Off;
            _rope.sharedMaterial = LogLineSetup.LineMaterial;
        }

        // Hangs near the stern at rest and streams out behind as the ship picks up speed, ending just under the water.
        private void LayRope()
        {
            Vector3 start = _reel.position;
            float speed = _ship.m_body != null ? _ship.m_body.linearVelocity.magnitude : 0f;
            float trail = Mathf.Lerp(1.5f, 10f, Mathf.Clamp01(speed / 6f));
            Vector3 back = -transform.forward;
            back.y = 0f;
            back.Normalize();
            Vector3 end = start + back * trail;
            end.y = Floating.GetLiquidLevel(end) - 0.3f;
            Vector3 middle = (start + end) * 0.5f + Vector3.down * Mathf.Lerp(0.6f, 0.15f, Mathf.Clamp01(speed / 6f));
            for (int i = 0; i < RopePoints; i++)
            {
                float t = i / (RopePoints - 1f);
                // A simple sag: a curve through the middle point.
                Vector3 a = Vector3.Lerp(start, middle, t);
                Vector3 b = Vector3.Lerp(middle, end, t);
                _rope.SetPosition(i, Vector3.Lerp(a, b, t));
            }
        }

        private static float NewStride()
        {
            float error = KitConfig.LogLineError.Value / 100f;
            return 1f + Random.Range(-error, error);
        }

        private void RPC_Fit(long sender)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            ZDO zdo = _nview.GetZDO();
            if (zdo.GetBool(FittedKey))
            {
                _nview.InvokeRPC(sender, FitResultRpc, false);
                return;
            }
            zdo.Set(FittedKey, true);
            zdo.Set(RunKey, 0f);
            zdo.Set(StrideKey, NewStride());
            _pending = 0f;
            _nview.InvokeRPC(sender, FitResultRpc, true);
        }

        private void RPC_FitResult(long sender, bool fitted)
        {
            if (!fitted)
            {
                GiveBack("This ship already has a log line");
            }
        }

        private void RPC_Haul(long sender)
        {
            if (!_nview.IsOwner())
            {
                return;
            }
            ZDO zdo = _nview.GetZDO();
            zdo.Set(RunKey, 0f);
            zdo.Set(StrideKey, NewStride());
            _pending = 0f;
        }

        private void RPC_Remove(long sender)
        {
            if (!_nview.IsOwner() || !_nview.GetZDO().GetBool(FittedKey))
            {
                return;
            }
            ZDO zdo = _nview.GetZDO();
            zdo.Set(FittedKey, false);
            zdo.Set(RunKey, 0f);
            _pending = 0f;
            _nview.InvokeRPC(sender, RemovedRpc);
        }

        private void RPC_Removed(long sender)
        {
            GiveBack("Log line taken off");
        }

        // Into the inventory, or at your feet if it's full.
        private static void GiveBack(string message)
        {
            Player player = Player.m_localPlayer;
            GameObject prefab = LogLineSetup.Prefab;
            if (player == null || prefab == null)
            {
                return;
            }
            ItemDrop.ItemData item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            if (!player.GetInventory().AddItem(item))
            {
                ItemDrop.DropItem(item, 1, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
            }
            player.Message(MessageHud.MessageType.Center, message);
        }
    }

    /// <summary>The reel on the stern: shows the run, E hauls in, Shift+E takes the log line off.</summary>
    internal sealed class LogLineReel : MonoBehaviour, Hoverable, Interactable
    {
        public LogLine Log;

        public string GetHoverText()
        {
            if (Log == null)
            {
                return "";
            }
            return Localization.instance.Localize($"{LogLineSetup.DisplayName}\n{Log.Paces} paces run"
                + "\n[<color=yellow><b>$KEY_Use</b></color>] Haul in (count from 0)"
                + "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Take it off"
                + "\n<color=#b0b0b0>With your map case out, End leg notes the run as a sea leg</color>");
        }

        public string GetHoverName()
        {
            return LogLineSetup.DisplayName;
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || Log == null || user != Player.m_localPlayer)
            {
                return false;
            }
            if (alt)
            {
                Log.TakeOff();
            }
            else
            {
                Log.Haul();
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Log hauled in: counting from 0");
            }
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}
