using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View
{
    /// <summary>
    /// 蜘蛛の巣の燃焼演出。
    /// 炎・火の粉・煙のParticleSystemを重ねて巣全体へ広がる見た目を作る。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiderWebBurnVfx : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float duration = 2.4f;

        private readonly List<Material> _runtimeMaterials = new();
        private Texture2D _softParticleTexture;
        private bool _played;

        public float Duration => duration;

        public void Play(float width, float height)
        {
            if (_played)
            {
                return;
            }

            _played = true;
            CreateFlameSystem(width, height);
            CreateSparkSystem(width, height);
            CreateSmokeSystem(width, height);
        }

        private void CreateFlameSystem(float width, float height)
        {
            ParticleSystem particles = CreateParticleSystem("Web Flames");
            ParticleSystem.MainModule main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 280;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.18f, 0.01f, 0.9f),
                new Color(1.0f, 0.85f, 0.08f, 1.0f));
            main.gravityModifier = -0.05f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(95.0f, 145.0f);

            ConfigureWebPlaneShape(particles, width, height, 0.04f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.35f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(CreateFlameGradient());

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.25f),
                new Keyframe(0.18f, 1.0f),
                new Keyframe(1.0f, 0.0f)));

            ConfigureRenderer(particles, new Color(1.0f, 0.45f, 0.04f, 1.0f), 2);
            particles.Play();
        }

        private void CreateSparkSystem(float width, float height)
        {
            ParticleSystem particles = CreateParticleSystem("Web Sparks");
            ParticleSystem.MainModule main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 160;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.35f, 0.01f, 1.0f),
                new Color(1.0f, 0.95f, 0.35f, 1.0f));
            main.gravityModifier = 0.12f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 42.0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 24, 40),
                new ParticleSystem.Burst(0.65f, 12, 24)
            });

            ConfigureWebPlaneShape(particles, width, height, 0.08f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 1.2f;

            ParticleSystem.TrailModule trails = particles.trails;
            trails.enabled = true;
            trails.lifetime = 0.16f;
            trails.dieWithParticles = true;

            ConfigureRenderer(particles, new Color(1.0f, 0.65f, 0.08f, 1.0f), 3, true);
            particles.Play();
        }

        private void CreateSmokeSystem(float width, float height)
        {
            ParticleSystem particles = CreateParticleSystem("Web Smoke");
            ParticleSystem.MainModule main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 100;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.08f, 0.07f, 0.06f, 0.28f),
                new Color(0.22f, 0.16f, 0.11f, 0.4f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 28.0f;
            ConfigureWebPlaneShape(particles, width, height, 0.05f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.25f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.18f, 0.12f, 0.08f), 0.0f),
                    new GradientColorKey(new Color(0.03f, 0.03f, 0.03f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.35f, 0.18f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            color.color = new ParticleSystem.MinMaxGradient(smokeGradient);

            ConfigureRenderer(particles, Color.gray, 1);
            particles.Play();
        }

        private ParticleSystem CreateParticleSystem(string objectName)
        {
            var particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = Vector3.zero;
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

            // AddComponent直後は既定設定で再生が始まるため、設定値を書き換える前に
            // 完全停止・既定パーティクル消去を行う。
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static void ConfigureWebPlaneShape(
            ParticleSystem particles,
            float width,
            float height,
            float depth)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.1f, width * 0.92f),
                Mathf.Max(0.1f, height * 0.92f),
                Mathf.Max(0.01f, depth));
        }

        private void ConfigureRenderer(
            ParticleSystem particles,
            Color tint,
            int sortingOrder,
            bool trails = false)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            renderer.trailMaterial = trails ? CreateParticleMaterial(tint) : null;
            renderer.material = CreateParticleMaterial(tint);
        }

        private Material CreateParticleMaterial(Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "Spider Web Burn Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1.0f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0.0f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0.0f);
            }
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }
            Texture2D particleTexture = GetOrCreateSoftParticleTexture();
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", particleTexture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", particleTexture);
            }
            _runtimeMaterials.Add(material);
            return material;
        }

        private Texture2D GetOrCreateSoftParticleTexture()
        {
            if (_softParticleTexture != null)
            {
                return _softParticleTexture;
            }

            const int size = 32;
            _softParticleTexture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Spider Web Burn Soft Particle",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX = (x + 0.5f) / size * 2.0f - 1.0f;
                    float normalizedY = (y + 0.5f) / size * 2.0f - 1.0f;
                    float alpha = Mathf.Clamp01(1.0f - Mathf.Sqrt(
                        normalizedX * normalizedX + normalizedY * normalizedY));
                    alpha = alpha * alpha * (3.0f - 2.0f * alpha);
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }

            _softParticleTexture.SetPixels(pixels);
            _softParticleTexture.Apply(false, true);
            return _softParticleTexture;
        }

        private static Gradient CreateFlameGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.95f, 0.35f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.22f, 0.01f), 0.45f),
                    new GradientColorKey(new Color(0.18f, 0.02f, 0.0f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.08f),
                    new GradientAlphaKey(0.9f, 0.55f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            return gradient;
        }

        private void OnDestroy()
        {
            foreach (Material material in _runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _runtimeMaterials.Clear();

            if (_softParticleTexture != null)
            {
                Destroy(_softParticleTexture);
                _softParticleTexture = null;
            }
        }
    }
}
