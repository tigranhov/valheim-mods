using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Placement mode for a crate item. A ghost follows where you look; it snaps flush against the face of a crate
    /// you look at (beside or on top), or beside the nearest crate when you aim at the floor next to one. Holding
    /// Shift (the game's alternative-placement key) turns snapping off for fine placement. It turns red where
    /// <see cref="CrateFit"/> refuses the spot. Left click sets it down (or says why it can't), scroll turns it,
    /// right click cancels.
    /// </summary>
    internal sealed class CrateGhost : MonoBehaviour
    {
        private const float MaxReach = 6f;
        private const float RayLength = 30f;
        private const float RotationStep = 22.5f;
        private const float ScrollThreshold = 0.05f;
        // Aim this close (m) to the side of a crate on the floor and the ghost snaps beside it.
        private const float SnapRange = 0.6f;
        private const string Hint = "Click to set the crate down, hold Shift to place it freely, scroll to turn it, right-click to cancel";

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly HashSet<string> LoggedShips = new HashSet<string>();

        private static CrateGhost _instance;
        // After placing or cancelling with a mouse button, keep it from also attacking or blocking until released.
        private static bool _awaitRelease;

        /// <summary>Set while this class reads the scroll wheel, so the camera doesn't zoom at the same time.</summary>
        internal static bool ReadingScroll;

        public static bool Active => _instance != null && _instance._item != null;

        /// <summary>
        /// Attack and block input belongs to crate placement: while placing, and until the button that placed or
        /// cancelled is released. The game picks up a held button as a new attack on its next physics step, which at
        /// high frame rates comes after placement has already ended.
        /// </summary>
        public static bool OwnsMouseButtons
        {
            get
            {
                if (Active)
                {
                    return true;
                }
                if (_awaitRelease && !ZInput.GetButton("Attack") && !ZInput.GetButton("JoyPlace")
                    && !ZInput.GetButton("Block") && !ZInput.GetButton("JoyBlock"))
                {
                    _awaitRelease = false;
                }
                return _awaitRelease;
            }
        }

        private Player _player;
        private Inventory _inventory;
        private ItemDrop.ItemData _item;
        private GameObject _ghost;
        private string _reason = CrateFit.DoesntFit;
        private int _rotationSteps;
        private float _scroll;
        private Vector3 _position;
        private Quaternion _rotation;
        private Ship _ship;
        private bool _valid;
        private bool _tintedInvalid;

        public static void Begin(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (_instance == null)
            {
                var go = new GameObject("IM_CrateGhost");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CrateGhost>();
            }
            if (_instance._item == item)
            {
                // Using the same crate again puts it away.
                _instance.End();
                return;
            }
            if (player.InPlaceMode())
            {
                player.Message(MessageHud.MessageType.Center, "Put the building tool away first");
                return;
            }
            _instance.Setup(player, inventory, item);
        }

        private void Setup(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            End();
            if (CrateSetup.CratePrefab == null)
            {
                return;
            }
            _player = player;
            _inventory = inventory;
            _item = item;
            _ghost = CreateGhost(CrateSetup.CratePrefab);
            _tintedInvalid = false;
            player.Message(MessageHud.MessageType.Center, Hint);
        }

        private void End()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
            }
            _ghost = null;
            _item = null;
            _player = null;
            _inventory = null;
        }

        // LateUpdate: crates riding a ship have been moved for this frame by then, so snapping lines up with them.
        private void LateUpdate()
        {
            if (_item == null)
            {
                return;
            }
            if (_player == null || _player != Player.m_localPlayer || _player.IsDead() || _ghost == null
                || !_inventory.ContainsItem(_item) || _player.InPlaceMode())
            {
                End();
                return;
            }
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Console.IsVisible() || TextInput.IsVisible()
                || StoreGui.IsVisible() || Minimap.IsOpen() || (Chat.instance != null && Chat.instance.HasFocus()))
            {
                _ghost.SetActive(false);
                return;
            }
            ReadRotation();
            UpdatePose();
            if (ZInput.GetButtonDown("Block") || ZInput.GetButtonDown("JoyBlock"))
            {
                _awaitRelease = true;
                End();
                return;
            }
            if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyPlace"))
            {
                _awaitRelease = true;
                if (_ghost.activeSelf && _valid)
                {
                    CratePlacement.Spawn(_player, _inventory, _item, _position, _rotation, _ship);
                    End();
                }
                else
                {
                    _player.Message(MessageHud.MessageType.Center, _ghost.activeSelf ? _reason : CrateFit.DoesntFit);
                }
            }
        }

        private void ReadRotation()
        {
            ReadingScroll = true;
            _scroll += ZInput.GetMouseScrollWheel();
            ReadingScroll = false;
            if (_scroll > ScrollThreshold)
            {
                _scroll = 0f;
                _rotationSteps++;
            }
            else if (_scroll < -ScrollThreshold)
            {
                _scroll = 0f;
                _rotationSteps--;
            }
        }

        private void UpdatePose()
        {
            // Crates riding a ship were just moved by transform; bring their colliders along before testing against them.
            Physics.SyncTransforms();
            Transform view = GameCamera.instance != null ? GameCamera.instance.transform : null;
            if (view == null
                || !Physics.Raycast(view.position, view.forward, out RaycastHit hit, RayLength, CrateFit.Mask, QueryTriggerInteraction.Ignore)
                || Vector3.Distance(hit.point, _player.transform.position) > MaxReach)
            {
                _ghost.SetActive(false);
                _valid = false;
                return;
            }
            _ghost.SetActive(true);
            bool snap = !ZInput.GetButton("AltPlace") && !ZInput.GetButton("JoyAltPlace");
            CargoCrate looked = snap ? hit.collider.GetComponentInParent<CargoCrate>() : null;
            if (looked != null && SnapToFace(looked, hit.normal))
            {
                // Placed against the face of the crate being looked at.
            }
            else if (snap && FindNeighbor(hit.point, out CargoCrate beside, out Vector3 side))
            {
                SnapTo(beside, side);
            }
            else
            {
                CrateFit.RestOn(hit, _rotationSteps * RotationStep, out _position, out _rotation, out _ship);
            }
            _ghost.transform.SetPositionAndRotation(_position, _rotation);
            _valid = CrateFit.Fits(_position, _rotation, ref _ship, out _reason);
            LogShipColliders(_ship);
            if (_valid == _tintedInvalid)
            {
                SetTint(!_valid);
            }
        }

        // Against the face of `crate` that `normal` points out of: beside it, or on top. Not underneath.
        private bool SnapToFace(CargoCrate crate, Vector3 normal)
        {
            Vector3 local = Quaternion.Inverse(crate.transform.rotation) * normal;
            Vector3 side = MainAxis(local);
            if (side.y < 0f)
            {
                return false;
            }
            SnapTo(crate, side);
            return true;
        }

        private void SnapTo(CargoCrate crate, Vector3 side)
        {
            Transform t = crate.transform;
            _rotation = t.rotation;
            _position = t.position + _rotation * Vector3.Scale(side, CrateShape.Size);
            ShipPassenger passenger = crate.GetComponent<ShipPassenger>();
            _ship = passenger != null ? passenger.CurrentShip : null;
        }

        // The crate on the same level whose side the aim point is closest to, within SnapRange of that side.
        private static bool FindNeighbor(Vector3 point, out CargoCrate nearest, out Vector3 side)
        {
            nearest = null;
            side = Vector3.zero;
            float best = SnapRange;
            Vector3 half = CrateShape.Size * 0.5f;
            foreach (CargoCrate crate in CargoCrate.All)
            {
                Transform t = crate.transform;
                Vector3 local = Quaternion.Inverse(t.rotation) * (point - t.position) - CrateShape.Center;
                if (Mathf.Abs(local.y) > CrateShape.Size.y)
                {
                    continue;
                }
                float gap = Mathf.Max(Mathf.Abs(local.x) - half.x, Mathf.Abs(local.z) - half.z);
                if (gap < best)
                {
                    best = gap;
                    nearest = crate;
                    // Which side: the axis the point is furthest out along, relative to the crate's size.
                    side = Mathf.Abs(local.x) / half.x > Mathf.Abs(local.z) / half.z
                        ? new Vector3(Mathf.Sign(local.x), 0f, 0f)
                        : new Vector3(0f, 0f, Mathf.Sign(local.z));
                }
            }
            return nearest != null;
        }

        private static Vector3 MainAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.y >= a.x && a.y >= a.z)
            {
                return new Vector3(0f, Mathf.Sign(v.y), 0f);
            }
            return a.x >= a.z ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        // Diagnostics, once per ship type: which colliders the clipping check sees on it.
        private static void LogShipColliders(Ship ship)
        {
            if (ship == null)
            {
                return;
            }
            string type = Utils.GetPrefabName(ship.gameObject);
            if (!LoggedShips.Add(type))
            {
                return;
            }
            var parts = new List<string>();
            foreach (Collider c in ship.GetComponentsInChildren<Collider>())
            {
                string kind = c is MeshCollider mesh ? (mesh.convex ? "convex mesh" : "mesh") : c.GetType().Name;
                string role = c == ship.m_floatCollider ? ", float" : c.isTrigger ? ", trigger" : "";
                parts.Add($"{c.name} ({kind}, {LayerMask.LayerToName(c.gameObject.layer)}{role}, {c.bounds.size.x:0.0}x{c.bounds.size.y:0.0}x{c.bounds.size.z:0.0})");
            }
            Plugin.Log.LogInfo($"{type} colliders: {string.Join("; ", parts)}");
        }

        private void SetTint(bool invalid)
        {
            _tintedInvalid = invalid;
            if (MaterialMan.instance == null)
            {
                return;
            }
            if (invalid)
            {
                MaterialMan.instance.SetValue(_ghost, ColorId, Color.red);
                MaterialMan.instance.SetValue(_ghost, EmissionId, Color.red * 0.7f);
            }
            else
            {
                MaterialMan.instance.ResetValue(_ghost, ColorId);
                MaterialMan.instance.ResetValue(_ghost, EmissionId);
            }
        }

        // A visual-only copy of the crate, like the game's build ghosts: nothing networked, no colliders, "ghost" layer.
        private static GameObject CreateGhost(GameObject prefab)
        {
            var holder = new GameObject("IM_CrateGhostHolder");
            holder.SetActive(false);
            bool previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject ghost;
            try
            {
                ghost = Instantiate(prefab, holder.transform);
                StripAll<CargoCrate>(ghost);
                StripAll<ShipPassenger>(ghost);
                StripAll<Container>(ghost);
                StripAll<Rigidbody>(ghost);
                StripAll<Collider>(ghost);
                StripAll<ZNetView>(ghost);
                int layer = LayerMask.NameToLayer("ghost");
                foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = layer;
                }
                foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                ghost.name = "IM_CrateGhost";
                ghost.transform.SetParent(null, false);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previous;
                Destroy(holder);
            }
            return ghost;
        }

        private static void StripAll<T>(GameObject go) where T : Component
        {
            foreach (T component in go.GetComponentsInChildren<T>(true))
            {
                DestroyImmediate(component);
            }
        }
    }
}
