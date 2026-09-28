using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>
    /// One trader's signal, shown locally at the camp: a smoke column, plus optional firework volleys at night.
    /// Nothing here is networked; each client draws its own.
    /// </summary>
    internal sealed class BeaconSignal : MonoBehaviour
    {
        public enum Mode
        {
            Auto,
            Smoke,
            Fireworks,
        }

        // Keep the object around after stopping so the last smoke puffs can fade out.
        private const float FadeOutSeconds = 60f;
        private const float LaunchSpread = 10f;

        private Mode _mode;
        private Vector3 _ground;
        private float _expiresAt;
        private bool _stopped;
        private SmokeColumn _smoke;
        private float _nextVolleyAt;
        private float _volleyEndsAt = -1f;
        private float _nextShotAt;

        /// <param name="lifetime">Seconds until the signal stops by itself; 0 = until told to stop.</param>
        public static BeaconSignal Create(string name, Vector3 campPosition, Mode mode, float lifetime)
        {
            var go = new GameObject("TraderBeacon_" + name);
            BeaconSignal signal = go.AddComponent<BeaconSignal>();
            signal._mode = mode;
            signal._expiresAt = lifetime > 0f ? Time.time + lifetime : 0f;
            signal._nextVolleyAt = Time.time;
            signal.Retarget(campPosition);
            return signal;
        }

        public void Retarget(Vector3 campPosition)
        {
            _ground = GroundAt(campPosition);
            transform.position = _ground;
        }

        public void Stop()
        {
            if (_stopped)
            {
                return;
            }
            _stopped = true;
            _smoke?.SetEmitting(false);
            Destroy(gameObject, FadeOutSeconds);
        }

        public static Vector3 CameraPosition()
        {
            if (GameCamera.instance != null)
            {
                return GameCamera.instance.transform.position;
            }
            return Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
        }

        /// <summary>Ground height works even where the zone isn't loaded, because the world generator is deterministic.</summary>
        public static Vector3 GroundAt(Vector3 p)
        {
            float height = p.y;
            if (WorldGenerator.instance != null)
            {
                height = Mathf.Max(height, WorldGenerator.instance.GetHeight(p.x, p.z));
            }
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            return new Vector3(p.x, Mathf.Max(height, water), p.z);
        }

        private void Update()
        {
            if (_stopped)
            {
                return;
            }
            if (_expiresAt > 0f && Time.time > _expiresAt)
            {
                Stop();
                return;
            }
            // Smoke day and night. Fireworks are optional (off by default) and replace the smoke after dark.
            bool fireworks = BeaconConfig.FireworksEnabled.Value && (_mode == Mode.Fireworks || (_mode == Mode.Auto && IsDark()));
            bool smoke = BeaconConfig.SmokeEnabled.Value && (_mode == Mode.Smoke || (_mode == Mode.Auto && !fireworks));
            UpdateSmoke(smoke);
            UpdateFireworks(fireworks);
        }

        // The game's fog would hide the smoke at signal range, so beyond FogAsIfMeters its fog stops thickening.
        private void LateUpdate()
        {
            if (_smoke == null)
            {
                return;
            }
            float distance = Vector3.Distance(CameraPosition(), _ground);
            _smoke.SetFogScale(Mathf.Min(1f, BeaconConfig.FogAsIf.Value / Mathf.Max(distance, 1f)));
        }

        private static bool IsDark()
        {
            return EnvMan.instance != null && (EnvMan.IsNight() || !EnvMan.IsDaylight());
        }

        private void UpdateSmoke(bool wanted)
        {
            if (wanted && _smoke == null)
            {
                _smoke = new SmokeColumn(transform);
            }
            if (_smoke != null)
            {
                _smoke.SetEmitting(wanted);
                _smoke.Tick(Time.deltaTime);
            }
        }

        private void UpdateFireworks(bool wanted)
        {
            float now = Time.time;
            if (_volleyEndsAt > 0f)
            {
                if (!wanted || now >= _volleyEndsAt)
                {
                    _volleyEndsAt = -1f;
                    _nextVolleyAt = now + BeaconConfig.VolleyInterval.Value;
                }
                else if (now >= _nextShotAt)
                {
                    FireworkFx.Launch(LaunchPoint());
                    _nextShotAt = now + BeaconConfig.ShotSpacing.Value * Random.Range(0.6f, 1.4f);
                }
                return;
            }
            if (wanted && now >= _nextVolleyAt)
            {
                _volleyEndsAt = now + BeaconConfig.VolleyDuration.Value;
                _nextShotAt = now;
            }
        }

        private Vector3 LaunchPoint()
        {
            Vector2 spread = Random.insideUnitCircle * LaunchSpread;
            return _ground + new Vector3(spread.x, BeaconConfig.FireworkHeight.Value, spread.y);
        }
    }
}
