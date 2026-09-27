using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Counts the local player's paces while a map case is in the inventory, on foot only (not swimming, riding, sitting or
    /// steering). Both measures are kept, ground covered and footfalls; the config picks which one the tally shows.
    /// </summary>
    internal static class Tally
    {
        private const float SaveInterval = 1f;
        // More than this in one frame is a teleport or a respawn, not a step.
        private const float MaxFrameMeters = 2f;

        private static ItemDrop.ItemData _case;
        private static TallyState _state;
        private static bool _dirty;
        private static float _sinceSave;

        private static bool _tracking;
        private static Rigidbody _lastBody;
        private static Vector3 _lastLocal;

        public static int Paces => _case != null ? ToPaces(_state) : 0;

        public static void Update(Player player)
        {
            ItemDrop.ItemData active = MapCaseItem.Active(player);
            if (active != _case)
            {
                Flush();
                _case = active;
                _state = _case != null ? MapCaseItem.LoadTally(_case) : default;
                _tracking = false;
                if (_case != null && _state.Stride <= 0f)
                {
                    _state.Stride = NewStride();
                    _dirty = true;
                }
            }
            if (_case == null)
            {
                return;
            }
            TrackDistance(player);
            _sinceSave += Time.deltaTime;
            if (_dirty && _sinceSave >= SaveInterval)
            {
                Flush();
            }
        }

        public static void OnFootstep(Character character)
        {
            Player player = Player.m_localPlayer;
            if (_case == null || player == null || character != player || !OnFoot(player))
            {
                return;
            }
            _state.Steps++;
            _dirty = true;
        }

        /// <summary>Ends the current leg: returns its paces and starts counting from zero with a new stride.</summary>
        public static int TakeLeg()
        {
            int paces = Paces;
            _state = new TallyState { Stride = NewStride() };
            _dirty = true;
            Flush();
            return paces;
        }

        public static void Flush()
        {
            if (_case != null && _dirty)
            {
                MapCaseItem.SaveTally(_case, _state);
            }
            _dirty = false;
            _sinceSave = 0f;
        }

        // Measured against whatever the player stands on, so walking a ship's deck counts and being carried by it doesn't.
        private static void TrackDistance(Player player)
        {
            bool counts = OnFoot(player) && player.GetMoveDir().sqrMagnitude > 0.01f;
            Rigidbody body = player.m_lastGroundBody;
            Vector3 position = player.transform.position;
            Vector3 local = body != null ? body.transform.InverseTransformPoint(position) : position;
            if (counts && _tracking && body == _lastBody)
            {
                float meters = Vector3.Distance(local, _lastLocal);
                if (meters < MaxFrameMeters)
                {
                    _state.Meters += meters;
                    _dirty = true;
                }
            }
            _tracking = counts;
            _lastBody = body;
            _lastLocal = local;
        }

        private static bool OnFoot(Player player)
        {
            return player.IsOnGround() && !player.IsSwimming() && !player.IsAttached() && !player.InDebugFlyMode()
                && !player.IsDead() && !player.IsTeleporting();
        }

        private static int ToPaces(TallyState state)
        {
            if (KitConfig.Tally.Value == TallyMode.Footsteps)
            {
                return state.Steps;
            }
            return Mathf.RoundToInt(state.Meters * state.Stride / KitConfig.PaceLength.Value);
        }

        private static float NewStride()
        {
            float error = KitConfig.TallyError.Value / 100f;
            return 1f + Random.Range(-error, error);
        }
    }
}
