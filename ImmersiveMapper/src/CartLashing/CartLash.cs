using ImmersiveMapper.Shared;
using UnityEngine;

namespace ImmersiveMapper.CartLashing
{
    /// <summary>
    /// Lashes a cart to the ship whose deck it stands on. A lashed cart rides the ship like cargo
    /// (<see cref="ShipPassenger"/>): the cart and its wheels are frozen, the game's own position sync for it is
    /// paused, it can't be pulled, and hits on it go to the ship (it's part of the ship). Its storage still opens.
    /// Untied, or when its ship is destroyed, it's an ordinary cart again. Added to every cart, meaning anything
    /// with the vanilla cart script (<see cref="Vagon"/>).
    /// </summary>
    internal sealed class CartLash : MonoBehaviour
    {
        private const string LashRpc = "IM_LashCart";
        private const string UntieRpc = "IM_UntieCart";
        private const string DeniedRpc = "IM_LashCartDenied";
        // From the cart's middle down to its deck, generously.
        private const float DeckProbe = 3f;
        // A lash or untie within this long counts as your own, for the message when it happens.
        private const float RequestSeconds = 5f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static int _mask;

        private ZNetView _nview;
        private Vagon _vagon;
        private ShipPassenger _passenger;
        private ZSyncTransform _sync;
        private Rigidbody[] _bodies;
        private bool[] _wasKinematic;
        private bool _frozen;
        private float _requestedAt = float.MinValue;

        public bool IsLashed => _passenger != null && _passenger.IsAttached;

        private static int Mask => _mask != 0 ? _mask
            : _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _vagon = GetComponent<Vagon>();
            _passenger = GetComponent<ShipPassenger>();
            _sync = GetComponent<ZSyncTransform>();
            _bodies = GetComponentsInChildren<Rigidbody>();
            _wasKinematic = new bool[_bodies.Length];
            _nview.Register(LashRpc, RPC_Lash);
            _nview.Register(UntieRpc, RPC_Untie);
            _nview.Register<string>(DeniedRpc, RPC_Denied);
        }

        // Not in Awake: the game's position sync notes in its own Awake whether the cart's body is frozen, and must
        // see it as it really is.
        private void Start()
        {
            _passenger.RidingChanged += OnRidingChanged;
            SetFrozen(IsLashed);
        }

        private void OnDestroy()
        {
            if (_passenger != null)
            {
                _passenger.RidingChanged -= OnRidingChanged;
            }
        }

        public string HoverSuffix()
        {
            string key = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] ";
            if (IsLashed)
            {
                return Localization.instance.Localize(key + "Untie\n<color=#9ab>Lashed to the ship</color>");
            }
            return LashConfig.Enabled.Value && ShipBelow() != null ? Localization.instance.Localize(key + "Lash to the ship") : "";
        }

        /// <summary>Shift+E on the cart: lash it to the ship it stands on, or untie it.</summary>
        public void Toggle(Humanoid character)
        {
            if (!(character is Player player) || player != Player.m_localPlayer || !_nview.IsValid())
            {
                return;
            }
            if (IsLashed)
            {
                _requestedAt = Time.time;
                _nview.InvokeRPC(UntieRpc);
                return;
            }
            if (!LashConfig.Enabled.Value)
            {
                return;
            }
            string reason = WhyNotLash();
            if (reason != null)
            {
                player.Message(MessageHud.MessageType.Center, reason);
                return;
            }
            _requestedAt = Time.time;
            _nview.InvokeRPC(LashRpc);
        }

        // Checked by whoever asks and again by the cart's owner, who does the lashing.
        private string WhyNotLash()
        {
            if (_vagon.IsAttached())
            {
                return "Unhitch the cart first";
            }
            return ShipBelow() == null ? "Stand the cart on a ship's deck to lash it" : null;
        }

        private void RPC_Lash(long sender)
        {
            if (!_nview.IsOwner() || IsLashed)
            {
                return;
            }
            string reason = WhyNotLash();
            if (reason != null)
            {
                _nview.InvokeRPC(sender, DeniedRpc, reason);
                return;
            }
            _passenger.AttachTo(ShipBelow());
        }

        private void RPC_Untie(long sender)
        {
            if (_nview.IsOwner())
            {
                _passenger.StopRiding();
            }
        }

        private void RPC_Denied(long sender, string reason)
        {
            if (Player.m_localPlayer != null && Time.time - _requestedAt < RequestSeconds)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, reason);
            }
        }

        private void OnRidingChanged(bool riding)
        {
            SetFrozen(riding);
            if (Player.m_localPlayer != null && Time.time - _requestedAt < RequestSeconds)
            {
                _requestedAt = float.MinValue;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, riding ? "Cart lashed to the ship" : "Cart untied");
            }
        }

        // Lashed, the cart and its wheels stop being physics bodies and the game's sync stops moving it: every
        // player's copy just follows its copy of the ship. Untied, all of it is as it was.
        private void SetFrozen(bool frozen)
        {
            if (frozen == _frozen)
            {
                return;
            }
            _frozen = frozen;
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];
                if (body == null)
                {
                    continue;
                }
                if (frozen)
                {
                    _wasKinematic[i] = body.isKinematic;
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.isKinematic = true;
                }
                else
                {
                    body.isKinematic = _wasKinematic[i];
                    body.WakeUp();
                }
            }
            if (_sync != null)
            {
                _sync.enabled = !frozen;
            }
        }

        /// <summary>The ship whose deck the cart stands on: the first thing straight below its middle that isn't the cart.</summary>
        public Ship ShipBelow()
        {
            Rigidbody main = _bodies.Length > 0 ? _bodies[0] : null;
            Vector3 from = main != null ? main.worldCenterOfMass : transform.position + Vector3.up;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, Hits, DeckProbe, Mask, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.distance < best && !hit.collider.transform.IsChildOf(transform))
                {
                    best = hit.distance;
                    nearest = hit.collider;
                }
            }
            return ShipPassenger.ShipOf(nearest);
        }
    }
}
