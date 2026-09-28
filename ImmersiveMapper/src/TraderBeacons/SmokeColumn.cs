using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>A tall campfire-style smoke column that leans with the wind. Built from code with a vanilla smoke material.</summary>
    internal sealed class SmokeColumn
    {
        private const float Lifetime = 40f;
        private const float WindUpdateSeconds = 2f;
        // Sideways acceleration per unit of wind: 0.08 m/s² over 40 s drifts the top of the column ~60 m in full wind.
        private const float WindDrift = 0.08f;

        // Unity's built-in fog uniform. A per-renderer override thins the fog for this column only.
        private static readonly int FogParamsId = Shader.PropertyToID("unity_FogParams");

        private readonly ParticleSystem _ps;
        private readonly ParticleSystemRenderer _renderer;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private float _windTimer;
        // What the fog override was last built from; it's only re-applied when one of these changes noticeably.
        private float _appliedScale = float.NaN;
        private float _appliedDensity;
        private float _appliedStart;
        private float _appliedEnd;

        public SmokeColumn(Transform parent)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            // Unity's cone shape emits along +Z; point it up.
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float height = BeaconConfig.SmokeHeight.Value;
            float width = BeaconConfig.SmokeWidth.Value;
            float speed = height / Lifetime;

            ParticleSystem.MainModule main = _ps.main;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime * 0.8f, Lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.6f, width);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.45f, 0.44f, 0.42f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = _ps.emission;
            emission.rateOverTime = 6f;

            ParticleSystem.ShapeModule shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = width * 0.3f;

            ParticleSystem.SizeOverLifetimeModule size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f));

            ParticleSystem.ColorOverLifetimeModule color = _ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.08f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.ForceOverLifetimeModule force = _ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;

            _renderer = go.GetComponent<ParticleSystemRenderer>();
            _renderer.renderMode = ParticleSystemRenderMode.Billboard;
            _renderer.maxParticleSize = 5f;
            Material material = SmokeMaterial.Get();
            if (material != null)
            {
                _renderer.sharedMaterial = material;
            }

            UpdateWind();
            _ps.Play();
        }

        /// <summary>Fog for this column as if it were <paramref name="scale"/> times as far away; 1 = normal fog.</summary>
        public void SetFogScale(float scale)
        {
            scale = Mathf.Min(scale, 1f);
            float density = RenderSettings.fogDensity;
            float start = RenderSettings.fogStartDistance;
            float end = RenderSettings.fogEndDistance;
            // The game's fog drifts a little every frame with time of day, so compare with a 1% tolerance.
            if (Near(scale, _appliedScale) && Near(density, _appliedDensity) && Near(start, _appliedStart) && Near(end, _appliedEnd))
            {
                return;
            }
            _appliedScale = scale;
            _appliedDensity = density;
            _appliedStart = start;
            _appliedEnd = end;
            if (scale >= 1f)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }
            // Same layout Unity uses: x = density/sqrt(ln2) (exp2), y = density/ln2 (exp), z = -1/(end-start), w = end/(end-start) (linear).
            float scaled = density * scale;
            float range = Mathf.Max(end - start, 0.001f);
            _block.SetVector(FogParamsId, new Vector4(scaled / 0.8325546f, scaled / 0.6931472f, -scale / range, end / range));
            _renderer.SetPropertyBlock(_block);
        }

        private static bool Near(float value, float applied)
        {
            return Mathf.Abs(value - applied) <= Mathf.Abs(applied) * 0.01f;
        }

        public void SetEmitting(bool on)
        {
            if (on && !_ps.isEmitting)
            {
                _ps.Play();
            }
            else if (!on && _ps.isEmitting)
            {
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void Tick(float dt)
        {
            _windTimer += dt;
            if (_windTimer >= WindUpdateSeconds)
            {
                _windTimer = 0f;
                UpdateWind();
            }
        }

        private void UpdateWind()
        {
            Vector3 wind = EnvMan.instance != null ? EnvMan.instance.GetWindForce() : Vector3.zero;
            ParticleSystem.ForceOverLifetimeModule force = _ps.forceOverLifetime;
            force.x = wind.x * WindDrift;
            force.y = 0f;
            force.z = wind.z * WindDrift;
        }
    }

    /// <summary>
    /// A copy of a vanilla fire's smoke material, so the column matches the game's look. It's a copy so our
    /// changes never touch the game's own fires.
    /// </summary>
    internal static class SmokeMaterial
    {
        // Lux Lit Particles camera fade. Vanilla smoke is (2, 150, 25, 0): it fades out ~150 m from the camera.
        private const string CamFadeProperty = "_CamFadeDistance";
        private const float NoFarFade = 100000f;
        private static readonly string[] FirePrefabs = { "fire_pit", "bonfire", "hearth", "fire_pit_iron" };
        private static Material _material;
        private static bool _searched;

        public static Material Get()
        {
            if (!_searched)
            {
                _searched = true;
                Material source = FindVanilla();
                _material = source != null ? new Material(source) { name = "TraderBeacons_Smoke" } : Fallback();
                Plugin.Log.LogInfo($"Smoke material: {(source != null ? source.name + " / " + source.shader.name : "fallback")}");
                LogShaderProperties(_material);
                DisableFarFade(_material);
            }
            return _material;
        }

        private static void DisableFarFade(Material material)
        {
            if (material == null || !material.HasProperty(CamFadeProperty))
            {
                return;
            }
            Vector4 fade = material.GetVector(CamFadeProperty);
            fade.y = NoFarFade;
            material.SetVector(CamFadeProperty, fade);
            Plugin.Log.LogInfo($"Smoke camera fade set to {fade}");
        }

        // Diagnostics for tuning: which knobs the smoke shader has (e.g. any distance fade).
        private static void LogShaderProperties(Material material)
        {
            if (material == null)
            {
                return;
            }
            Shader shader = material.shader;
            var parts = new List<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string prop = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        parts.Add($"{prop}={material.GetFloat(prop):0.###}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        parts.Add($"{prop}={material.GetColor(prop)}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        parts.Add($"{prop}={material.GetVector(prop)}");
                        break;
                    default:
                        parts.Add(prop);
                        break;
                }
            }
            Plugin.Log.LogInfo($"Smoke shader properties: {string.Join(", ", parts)}; keywords: {string.Join(" ", material.shaderKeywords)}");
        }

        private static Material FindVanilla()
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }
            Material puffMaterial = null;
            var seen = new List<string>();
            foreach (string prefabName in FirePrefabs)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab == null)
                {
                    continue;
                }
                foreach (ParticleSystemRenderer r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    Material m = r.sharedMaterial;
                    if (m == null)
                    {
                        continue;
                    }
                    seen.Add(m.name);
                    if (m.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return m;
                    }
                }
                foreach (SmokeSpawner spawner in prefab.GetComponentsInChildren<SmokeSpawner>(true))
                {
                    Renderer r = spawner.m_smokePrefab != null ? spawner.m_smokePrefab.GetComponentInChildren<Renderer>(true) : null;
                    if (puffMaterial == null && r != null && r.sharedMaterial != null)
                    {
                        puffMaterial = r.sharedMaterial;
                    }
                }
            }
            Plugin.Log.LogInfo($"No particle smoke material found; fire particle materials seen: {string.Join(", ", seen)}");
            return puffMaterial;
        }

        private static Material Fallback()
        {
            Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) { name = "TraderBeacons_SmokeFallback" } : null;
        }
    }
}
