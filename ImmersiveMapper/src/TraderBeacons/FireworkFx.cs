using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>
    /// Launches vanilla fireworks (the effects a campfire plays when you throw fireworks in) at any position,
    /// locally and without networking.
    /// </summary>
    internal static class FireworkFx
    {
        private const float SpeedOfSound = 343f;
        private const float MaxSoundPlacement = 30f;
        private const float FarQuietDistance = 1500f;

        private sealed class Variant
        {
            public readonly List<GameObject> Visuals = new List<GameObject>();
            public readonly List<GameObject> Sounds = new List<GameObject>();
        }

        private static List<Variant> _variants;

        public static void Launch(Vector3 position)
        {
            if (!Resolve())
            {
                return;
            }
            Variant variant = _variants[UnityEngine.Random.Range(0, _variants.Count)];
            Quaternion rotation = Quaternion.Euler(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-5f, 5f));
            bool delaySound = BeaconConfig.FlashToBang.Value;
            float scale = BeaconConfig.FireworkScale.Value;
            foreach (GameObject prefab in variant.Visuals)
            {
                LocalFx.Spawn(prefab, position, rotation, go =>
                {
                    if (delaySound)
                    {
                        LocalFx.StripSound(go);
                    }
                    LocalFx.Scale(go, scale);
                });
            }
            foreach (GameObject prefab in variant.Sounds)
            {
                if (delaySound)
                {
                    LocalFx.Runner.StartCoroutine(PlayDelayedSound(prefab, position));
                }
                else
                {
                    LocalFx.Spawn(prefab, position, rotation, null);
                }
            }
        }

        // Light travels instantly, sound at 343 m/s: play the bang late and quieter, from the camp's direction.
        private static IEnumerator PlayDelayedSound(GameObject prefab, Vector3 source)
        {
            Vector3 listener = BeaconSignal.CameraPosition();
            float distance = Vector3.Distance(listener, source);
            yield return new WaitForSeconds(distance / SpeedOfSound);
            if (Player.m_localPlayer == null)
            {
                yield break;
            }
            listener = BeaconSignal.CameraPosition();
            Vector3 toSource = source - listener;
            Vector3 at = listener + toSource.normalized * Mathf.Min(toSource.magnitude, MaxSoundPlacement);
            float volume = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(distance / FarQuietDistance)) * BeaconConfig.SoundVolume.Value;
            LocalFx.Spawn(prefab, at, Quaternion.identity, go =>
            {
                foreach (ZSFX sfx in go.GetComponentsInChildren<ZSFX>(true))
                {
                    sfx.m_minVol *= volume;
                    sfx.m_maxVol *= volume;
                }
            });
        }

        private static bool Resolve()
        {
            if (_variants != null)
            {
                return _variants.Count > 0;
            }
            if (ZNetScene.instance == null)
            {
                return false;
            }
            _variants = new List<Variant>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Fireplace fireplace = prefab != null ? prefab.GetComponent<Fireplace>() : null;
                if (fireplace == null || fireplace.m_fireworkItemList == null || fireplace.m_fireworkItemList.Length == 0)
                {
                    continue;
                }
                foreach (Fireplace.FireworkItem item in fireplace.m_fireworkItemList)
                {
                    EffectList effects = item.m_fireworksEffects;
                    if (effects?.m_effectPrefabs == null)
                    {
                        continue;
                    }
                    var variant = new Variant();
                    foreach (EffectList.EffectData data in effects.m_effectPrefabs)
                    {
                        if (!data.m_enabled || data.m_prefab == null)
                        {
                            continue;
                        }
                        bool soundOnly = data.m_prefab.GetComponentInChildren<ZSFX>(true) != null
                            && data.m_prefab.GetComponentInChildren<ParticleSystem>(true) == null;
                        (soundOnly ? variant.Sounds : variant.Visuals).Add(data.m_prefab);
                    }
                    if (variant.Visuals.Count > 0)
                    {
                        _variants.Add(variant);
                        Plugin.Log.LogInfo($"Firework from {prefab.name}: visuals [{string.Join(", ", variant.Visuals.Select(p => p.name))}], sounds [{string.Join(", ", variant.Sounds.Select(p => p.name))}]");
                    }
                }
                break;
            }
            if (_variants.Count == 0)
            {
                Plugin.Log.LogWarning("No vanilla firework effects found; night signals will be invisible.");
            }
            return _variants.Count > 0;
        }
    }

    /// <summary>Spawns effect prefabs as plain local objects: no ZDO is created even if the prefab has a ZNetView.</summary>
    internal static class LocalFx
    {
        private const float MaxLifetime = 30f;

        // _root stays active and runs coroutines; _inactive is its disabled child where new effects wait before waking.
        private static GameObject _root;
        private static Transform _inactive;
        private static CoroutineRunner _runner;

        private sealed class CoroutineRunner : MonoBehaviour
        {
        }

        public static MonoBehaviour Runner
        {
            get
            {
                EnsureHolder();
                return _runner;
            }
        }

        /// <param name="configure">Runs before the object wakes up, so Awake/Start see the changes.</param>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> configure)
        {
            EnsureHolder();
            bool previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject go;
            try
            {
                // Instantiating under an inactive parent defers Awake until we move it out.
                go = Object.Instantiate(prefab, position, rotation, _inactive);
                configure?.Invoke(go);
                go.transform.SetParent(null, true);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previous;
            }
            Object.Destroy(go, MaxLifetime);
            return go;
        }

        public static void StripSound(GameObject go)
        {
            foreach (ZSFX sfx in go.GetComponentsInChildren<ZSFX>(true))
            {
                Object.DestroyImmediate(sfx);
            }
            foreach (AudioSource source in go.GetComponentsInChildren<AudioSource>(true))
            {
                Object.DestroyImmediate(source);
            }
        }

        /// <summary>Scales a particle effect uniformly, including trajectories (speed and gravity scale with size).</summary>
        public static void Scale(GameObject go, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                return;
            }
            go.transform.localScale *= scale;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startSpeedMultiplier *= scale;
                main.gravityModifierMultiplier *= scale;
            }
            foreach (Light light in go.GetComponentsInChildren<Light>(true))
            {
                light.range *= scale;
            }
        }

        private static void EnsureHolder()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("TraderBeacons_Fx");
            Object.DontDestroyOnLoad(_root);
            _runner = _root.AddComponent<CoroutineRunner>();
            var inactive = new GameObject("Inactive");
            inactive.transform.SetParent(_root.transform, false);
            inactive.SetActive(false);
            _inactive = inactive.transform;
        }
    }
}
