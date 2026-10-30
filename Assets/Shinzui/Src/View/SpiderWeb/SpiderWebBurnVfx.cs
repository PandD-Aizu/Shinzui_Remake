using System.Collections.Generic;
using Shinzui.Infrastructure.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View
{
    /// <summary>
    /// 蜘蛛の巣の燃焼演出
    /// 着火点から広がる燃焼前線が糸に届いた位置ごとに、小さな炎・火の粉・煙を発生させ、
    /// 前線に追従する揺らぐ光源で周囲を照らす
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiderWebBurnVfx : MonoBehaviour
    {
        [Tooltip("着火から煙が消えるまでの全体の秒数")]
        [SerializeField, Min(0.5f)] private float duration = 3.4f;

        [Header("Particles")]
        [Tooltip("前線上で炎を出す糸の間隔")]
        [SerializeField, Min(0.01f)] private float sampleSpacing = 0.03f;
        [SerializeField, Min(0.0f)] private float flameNits = 1400.0f;
        [SerializeField, Min(0.0f)] private float emberNits = 3000.0f;
        [SerializeField, Min(0.0f)] private float smokeNits = 2.5f;
        [SerializeField, Range(0.0f, 1.0f)] private float emberChance = 0.04f;
        [SerializeField, Range(0.0f, 1.0f)] private float smokeChance = 0.06f;

        [Header("Light")]
        [SerializeField, Min(0.0f)] private float lightLumen = 160.0f;
        [SerializeField, Min(0.1f)] private float lightRange = 5.0f;
        [SerializeField] private Color lightColor = new(1.0f, 0.52f, 0.18f);

        private readonly List<Material> _runtimeMaterials = new();
        private readonly List<Texture2D> _runtimeTextures = new();
        private readonly List<Vector3> _samples = new();
        private readonly List<float> _sampleDistances = new();
        private ParticleSystem _flames;
        private ParticleSystem _embers;
        private ParticleSystem _smoke;
        private Light _light;
        private Vector3 _lightTarget;
        private float _heat;
        private int _nextSample;
        private bool _played;

        public float Duration => duration;
        public float SampleSpacing => sampleSpacing;

        /// <summary>
        /// 巣の中央から燃え広がる演出を開始する
        /// </summary>
        /// <param name="width">巣の幅</param>
        /// <param name="height">巣の高さ</param>
        public void Play(float width, float height)
        {
            // 糸の情報が無い場合は巣の矩形を格子状に燃やす
            var samples = new List<Vector3>();
            for (float y = -height * 0.5f; y <= height * 0.5f; y += sampleSpacing * 4.0f)
            {
                for (float x = -width * 0.5f; x <= width * 0.5f; x += sampleSpacing * 4.0f)
                {
                    samples.Add(new Vector3(x, y, 0.0f));
                }
            }
            Play(Vector3.zero, samples, 0.0f);
        }

        /// <summary>
        /// 着火点と糸上の標本点を受け取り、燃焼演出を開始する
        /// </summary>
        /// <param name="localOrigin">巣のローカル座標での着火点</param>
        /// <param name="localSamples">巣のローカル座標での糸上の点</param>
        /// <param name="jitter">前線の不規則さ。シェーダーと同じ値</param>
        public void Play(Vector3 localOrigin, IReadOnlyList<Vector3> localSamples, float jitter)
        {
            if (_played)
            {
                return;
            }

            _played = true;

            // 前線の到達順に並べ、毎フレーム先頭から着火させる
            var order = new List<(float Distance, Vector3 Sample)>(localSamples.Count);
            foreach (Vector3 sample in localSamples)
            {
                float distance = Vector3.Distance(sample, localOrigin) + SpiderWeb.BurnJitter(sample) * jitter;
                order.Add((distance, sample));
            }
            order.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            foreach ((float distance, Vector3 sample) in order)
            {
                _sampleDistances.Add(distance);
                _samples.Add(sample);
            }

            _flames = CreateFlameSystem();
            _embers = CreateEmberSystem();
            _smoke = CreateSmokeSystem();
            CreateLight(transform.TransformPoint(localOrigin));
            EmitIgnitionFlare(transform.TransformPoint(localOrigin));
        }

        /// <summary>
        /// 燃焼前線の半径を進め、新たに届いた糸から炎を出す
        /// </summary>
        /// <param name="radius">着火点からの前線の距離</param>
        public void UpdateFront(float radius)
        {
            if (!_played)
            {
                return;
            }

            // 今フレームに着火した点の重心へ光源を寄せる
            Vector3 ignitedSum = Vector3.zero;
            int ignitedCount = 0;
            while (_nextSample < _samples.Count && _sampleDistances[_nextSample] <= radius)
            {
                Vector3 worldPosition = transform.TransformPoint(_samples[_nextSample]);
                IgniteSample(worldPosition);
                ignitedSum += worldPosition;
                ignitedCount++;
                _nextSample++;
            }

            if (ignitedCount > 0)
            {
                _lightTarget = ignitedSum / ignitedCount;
                _heat = Mathf.Min(1.0f, _heat + ignitedCount * 0.02f);
            }
        }

        private void Update()
        {
            if (_light == null)
            {
                return;
            }

            // 炎の量に合わせて明るさを変え、不規則に揺らして炎の明滅を作る
            _heat = Mathf.MoveTowards(_heat, 0.0f, Time.deltaTime * 1.6f);
            float flicker = 0.72f + 0.28f * Mathf.PerlinNoise(Time.time * 14.0f, 0.37f);
            HdrpLightUtility.SetIntensity(_light, lightLumen * _heat * flicker, LightUnit.Lumen);
            _light.transform.position = Vector3.Lerp(
                _light.transform.position,
                _lightTarget + Vector3.up * 0.05f,
                1.0f - Mathf.Exp(-10.0f * Time.deltaTime));
            _light.enabled = _heat > 0.001f;
        }

        /// <summary>
        /// 前線が届いた糸の一点から炎と、確率で火の粉・煙を出す
        /// </summary>
        /// <param name="position">ワールド座標</param>
        private void IgniteSample(Vector3 position)
        {
            // 糸は細いので炎は小さく短命で、上昇気流に乗ってすぐ消える
            var flame = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(0.12f, 0.35f), Random.Range(-0.03f, 0.03f)),
                startSize = Random.Range(0.01f, 0.026f),
                startLifetime = Random.Range(0.1f, 0.26f),
                rotation = Random.Range(-12.0f, 12.0f),
                applyShapeToPosition = false
            };
            _flames.Emit(flame, 1);

            // 溶けた糸のしずくが光りながら垂れ、一部は熱で舞い上がる
            if (Random.value < emberChance)
            {
                bool rising = Random.value < 0.35f;
                var ember = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = rising
                        ? new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 1.0f), Random.Range(-0.2f, 0.2f))
                        : new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.15f, 0.1f), Random.Range(-0.08f, 0.08f)),
                    startSize = Random.Range(0.0015f, 0.004f),
                    startLifetime = Random.Range(0.5f, 1.4f),
                    applyShapeToPosition = false
                };
                _embers.Emit(ember, 1);
            }

            // 焦げた糸から細い煙が立ち上る
            if (Random.value < smokeChance)
            {
                var smoke = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.03f,
                    velocity = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0.12f, 0.3f), Random.Range(-0.04f, 0.04f)),
                    startSize = Random.Range(0.06f, 0.14f),
                    startLifetime = Random.Range(1.3f, 2.4f),
                    rotation = Random.Range(0.0f, 360.0f),
                    applyShapeToPosition = false
                };
                _smoke.Emit(smoke, 1);
            }
        }

        /// <summary>
        /// 着火した瞬間の小さな閃光と火の粉を出す
        /// </summary>
        /// <param name="position">ワールド座標</param>
        private void EmitIgnitionFlare(Vector3 position)
        {
            _heat = 0.6f;
            _lightTarget = position;
            for (int i = 0; i < 6; i++)
            {
                _flames.Emit(new ParticleSystem.EmitParams
                {
                    position = position + Random.insideUnitSphere * 0.02f,
                    velocity = Vector3.up * Random.Range(0.2f, 0.5f),
                    startSize = Random.Range(0.025f, 0.045f),
                    startLifetime = Random.Range(0.2f, 0.35f),
                    applyShapeToPosition = false
                }, 1);
            }
            for (int i = 0; i < 5; i++)
            {
                _embers.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = Random.insideUnitSphere * 0.9f + Vector3.up * 0.4f,
                    startSize = Random.Range(0.0015f, 0.0035f),
                    startLifetime = Random.Range(0.3f, 0.9f),
                    applyShapeToPosition = false
                }, 1);
            }
        }

        /// <summary>
        /// 糸に沿って燃える小さな炎の粒子を作る
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateFlameSystem()
        {
            ParticleSystem particles = CreateParticleSystem("Web Flames", 900);
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = -0.04f;
            main.startColor = Color.white;

            // 芯は白っぽい黄色、外へ向かって橙から暗い赤へ冷え、上端で消える
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.7f, 0.32f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.36f, 0.06f), 0.35f),
                    new GradientColorKey(new Color(0.55f, 0.08f, 0.01f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.1f),
                    new GradientAlphaKey(0.6f, 0.55f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }));

            // 着火直後に膨らみ、燃え尽きながら細くなる
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.4f),
                new Keyframe(0.2f, 1.0f),
                new Keyframe(1.0f, 0.25f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 3.5f;
            noise.scrollSpeed = 1.5f;

            Texture2D texture = FireParticleFactory.CreateFlameTexture();
            _runtimeTextures.Add(texture);
            ConfigureRenderer(particles, FireParticleFactory.CreateMaterial(texture, flameNits, true));
            return particles;
        }

        /// <summary>
        /// 光りながら垂れ落ちる糸のしずくと、舞い上がる火の粉を作る
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateEmberSystem()
        {
            ParticleSystem particles = CreateParticleSystem("Web Embers", 400);
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = 0.18f;
            main.startColor = Color.white;

            // 落ちながら冷えて暗い赤になり、最後に消える
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.8f, 0.45f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.35f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.4f, 0.04f, 0.0f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.6f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 1.8f;

            Texture2D texture = FireParticleFactory.CreateGlowTexture();
            _runtimeTextures.Add(texture);
            ConfigureRenderer(particles, FireParticleFactory.CreateMaterial(texture, emberNits, true));

            // 速度方向へ伸ばし、動きの残像を短い光の筋にする
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.015f;
            renderer.lengthScale = 1.0f;
            return particles;
        }

        /// <summary>
        /// 焦げた糸から立ち上る細い煙を作る
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateSmokeSystem()
        {
            ParticleSystem particles = CreateParticleSystem("Web Smoke", 200);
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new Color(0.42f, 0.38f, 0.34f, 1.0f);

            // 発生直後は薄く、広がりながら最も濃くなり、上昇しつつ薄れる
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.45f, 0.38f), 0.0f),
                    new GradientColorKey(new Color(0.32f, 0.31f, 0.3f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.22f, 0.2f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }));

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.5f),
                new Keyframe(1.0f, 3.2f)));

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.3f;

            Texture2D texture = FireParticleFactory.CreateSmokeTexture();
            _runtimeTextures.Add(texture);
            ConfigureRenderer(particles, FireParticleFactory.CreateMaterial(texture, smokeNits, false));
            return particles;
        }

        /// <summary>
        /// 自動発生しない、Emitで駆動するワールド空間の粒子を作る
        /// </summary>
        /// <param name="objectName">オブジェクト名</param>
        /// <param name="maxParticles">同時に存在できる粒子数</param>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateParticleSystem(string objectName, int maxParticles)
        {
            var particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = Vector3.zero;
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

            // AddComponent直後は既定設定で再生が始まるため、設定値を書き換える前に
            // 完全停止・既定パーティクル消去を行う
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 巣が消えても粒子が残るよう、ワールド空間で上昇させる
            ParticleSystem.MainModule main = particles.main;
            main.duration = duration;
            // 放出はEmitだけで行い、着火が続く間は停止させない
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            particles.Play();
            return particles;
        }

        /// <summary>
        /// 粒子の描画方法とマテリアルを設定する
        /// </summary>
        /// <param name="particles">対象の粒子</param>
        /// <param name="material">描画マテリアル</param>
        private void ConfigureRenderer(ParticleSystem particles, Material material)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = material;
            if (material != null)
            {
                _runtimeMaterials.Add(material);
            }
        }

        /// <summary>
        /// 炎の位置に追従する影なしの点光源を作る
        /// </summary>
        /// <param name="position">着火点</param>
        private void CreateLight(Vector3 position)
        {
            var lightObject = new GameObject("Web Fire Light");
            lightObject.transform.SetParent(transform, true);
            lightObject.transform.position = position;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = lightColor;
            _light.range = lightRange;
            _light.shadows = LightShadows.None;
            HdrpLightUtility.SetIntensity(_light, 0.0f, LightUnit.Lumen);
        }

        /// <summary>
        /// 色と透明度のキーからグラデーションを作る
        /// </summary>
        /// <param name="colors">色のキー</param>
        /// <param name="alphas">透明度のキー</param>
        /// <returns>生成したグラデーション</returns>
        private static Gradient CreateGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
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

            foreach (Texture2D texture in _runtimeTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            _runtimeTextures.Clear();
        }
    }
}
