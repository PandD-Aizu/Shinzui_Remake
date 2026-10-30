using System.Collections;
using System.Collections.Generic;
using Shinzui.Infrastructure.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View
{
    /// <summary>
    /// 右手でマッチを擦って火をつけ、蜘蛛の巣へ近づけて着火する主観演出
    /// 右腕の姿勢はPlayerBodyViewの取得姿勢を借りて動かす
    /// </summary>
    [DefaultExecutionOrder(10001)]
    [DisallowMultipleComponent]
    public sealed class MatchIgnitionView : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float raiseDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float strikeDuration = 0.22f;
        [SerializeField, Min(0.05f)] private float steadyDuration = 0.5f;
        [SerializeField, Min(0.05f)] private float reachDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float touchDuration = 0.4f;
        [SerializeField, Min(0.05f)] private float retractDuration = 0.6f;

        [Header("Hand")]
        [Tooltip("構えた時の掌の位置。カメラ基準の右・上・前")]
        [SerializeField] private Vector3 holdOffset = new(0.1f, -0.2f, 0.32f);
        [Tooltip("掌に対するマッチの根元の位置。掌基準の右・上・前")]
        [SerializeField] private Vector3 gripOffset = new(0.0f, -0.015f, 0.02f);
        [SerializeField, Range(0.0f, 1.0f)] private float pinchGrip = 0.55f;

        [Header("Match")]
        [SerializeField, Min(0.01f)] private float matchLength = 0.048f;
        [SerializeField, Min(0.0f)] private float flameNits = 9000.0f;
        [SerializeField, Min(0.0f)] private float sparkNits = 16000.0f;
        [SerializeField, Min(0.0f)] private float smokeNits = 2.5f;
        [SerializeField, Min(0.0f)] private float lightLumen = 12.0f;
        [SerializeField, Min(0.0f)] private float flareLumen = 70.0f;
        [SerializeField] private Color lightColor = new(1.0f, 0.58f, 0.26f);

        private readonly List<Object> _runtimeObjects = new();
        private PlayerView _player;
        private PlayerBodyView _body;
        private Transform _match;
        private Transform _tip;
        private Material _headMaterial;
        private ParticleSystem _flame;
        private ParticleSystem _sparks;
        private ParticleSystem _smoke;
        private Light _light;
        private float _flameStrength;
        private float _flare;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// マッチの着火から蜘蛛の巣への点火までを再生する
        /// </summary>
        /// <param name="web">燃やす蜘蛛の巣</param>
        /// <returns>演出を開始できたか。falseの場合は呼び出し側で直接燃やす</returns>
        public bool Play(SpiderWeb web)
        {
            if (IsPlaying || web == null)
            {
                return false;
            }

            if (_player == null)
            {
                _player = GetComponent<PlayerView>();
            }
            if (_body == null)
            {
                _body = GetComponentInChildren<PlayerBodyView>();
            }
            if (_player == null || _body == null || !_body.isActiveAndEnabled)
            {
                return false;
            }

            StartCoroutine(PlayRoutine(web));
            return true;
        }

        private IEnumerator PlayRoutine(SpiderWeb web)
        {
            IsPlaying = true;
            CreateMatch();

            // 構え: 胸の前へ手を上げ、マッチを指先でつまむ
            yield return Animate(raiseDuration, t => SetPose(HoldPosition(), HoldRotation(), Ease(t)));

            // 擦る: 手首を素早く払うように動かし、振り抜く直前に発火させる
            bool ignited = false;
            yield return Animate(strikeDuration, t =>
            {
                float swipe = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
                Vector3 offset = _player.CameraRight * Mathf.Lerp(0.06f, -0.05f, swipe) +
                                 Vector3.up * (0.02f * Mathf.Sin(t * Mathf.PI));
                Quaternion flick = Quaternion.AngleAxis(Mathf.Lerp(18.0f, -10.0f, swipe), Vector3.up);
                SetPose(HoldPosition() + offset, flick * HoldRotation(), 1.0f);
                if (!ignited && t >= 0.6f)
                {
                    ignited = true;
                    Ignite();
                }
            });

            // 落ち着かせる: 硫黄の閃光が収まり、炎が安定するまで見せる
            Vector3 strikeEnd = HoldPosition() + _player.CameraRight * -0.05f;
            yield return Animate(steadyDuration, t =>
                SetPose(Vector3.Lerp(strikeEnd, HoldPosition(), Ease(t)), HoldRotation(), 1.0f));

            // 近づける: 見ている位置に最も近い糸へ、炎の先端が触れるまで手を伸ばす
            Vector3 target = GetAimThreadPoint(web);
            yield return Animate(reachDuration, t =>
            {
                Quaternion reachRotation = ReachRotation(target);
                Vector3 contact = target - reachRotation * (gripOffset + Vector3.forward * matchLength);
                float eased = Ease(t);
                SetPose(Vector3.Lerp(HoldPosition(), contact, eased),
                    Quaternion.Slerp(HoldRotation(), reachRotation, eased), 1.0f);
            });

            // 着火: 先端に最も近い糸から燃え広がらせ、炎を当てたまま少し保持する
            if (web != null)
            {
                Vector3 tip = _tip != null ? _tip.position : target;
                web.Burn(web.GetClosestThreadPoint(tip));
            }
            yield return Animate(touchDuration, t =>
            {
                Quaternion reachRotation = ReachRotation(target);
                Vector3 contact = target - reachRotation * (gripOffset + Vector3.forward * matchLength);
                Vector3 recoil = (_player.CameraPosition - target).normalized * (0.04f * Ease(t));
                SetPose(contact + recoil, reachRotation, 1.0f);
            });

            // 戻す: 手を引きながら手首を振って火を消し、細い煙を残す
            Vector3 released = _match != null ? _match.position : target;
            yield return Animate(retractDuration, t =>
            {
                float shake = Mathf.Sin(t * Mathf.PI * 6.0f) * (1.0f - t);
                Vector3 position = Vector3.Lerp(released, HoldPosition(), Ease(Mathf.Clamp01(t * 1.6f))) +
                                   _player.CameraRight * (shake * 0.03f);
                SetPose(position, HoldRotation(), 1.0f - Ease(Mathf.Clamp01((t - 0.55f) / 0.45f)));
                if (t > 0.15f && _flameStrength > 0.0f)
                {
                    Extinguish();
                }
            });

            // 燃えさしを指から離して落とし、煙が漂い終わってから片付ける
            _body.SetPickupPose(Vector3.zero, Quaternion.identity, 0.0f, 0.0f);
            DropMatch();
            DestroyMatch(3.0f);
            IsPlaying = false;
        }

        private void LateUpdate()
        {
            if (_match == null)
            {
                return;
            }

            // 骨格の姿勢確定後に、つまんだ指の位置へマッチを追従させる
            if (_body != null && !_match.TryGetComponent(out Rigidbody _) &&
                _body.TryGetRightPalmPose(out Pose palm))
            {
                _match.SetPositionAndRotation(palm.position + palm.rotation * gripOffset, palm.rotation);
            }

            // 炎の大きさと光を、点火直後の閃光から安定した小さな炎へ減衰させる
            _flare = Mathf.MoveTowards(_flare, 0.0f, Time.deltaTime * 3.0f);
            float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 17.0f, 0.71f);
            float size = 0.016f * _flameStrength * (1.0f + _flare * 1.8f);
            ParticleSystem.MainModule main = _flame.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.2f);
            ParticleSystem.EmissionModule emission = _flame.emission;
            emission.rateOverTimeMultiplier = _flameStrength > 0.0f ? 70.0f : 0.0f;
            emission.rateOverDistanceMultiplier = _flameStrength > 0.0f ? 600.0f : 0.0f;
            HdrpLightUtility.SetIntensity(_light,
                (lightLumen * _flameStrength + flareLumen * _flare) * flicker, LightUnit.Lumen);
            _light.enabled = _flameStrength > 0.0f || _flare > 0.0f;
        }

        private void OnDisable()
        {
            // 途中で無効化された場合も腕の取得姿勢を解除する
            if (IsPlaying && _body != null)
            {
                _body.SetPickupPose(Vector3.zero, Quaternion.identity, 0.0f, 0.0f);
            }
            StopAllCoroutines();
            DestroyMatch();
            IsPlaying = false;
        }

        /// <summary>
        /// 指定秒数のあいだ毎フレーム進捗を渡す
        /// </summary>
        /// <param name="seconds">所要時間</param>
        /// <param name="step">0から1の進捗を受け取る処理</param>
        /// <returns>コルーチン</returns>
        private static IEnumerator Animate(float seconds, System.Action<float> step)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                step(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
        }

        /// <summary>
        /// 加速して減速する補間値を求める
        /// </summary>
        /// <param name="t">0から1の進捗</param>
        /// <returns>補間値</returns>
        private static float Ease(float t)
        {
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>
        /// 右手の目標姿勢を指でつまむ握りで設定する
        /// </summary>
        /// <param name="position">掌の位置</param>
        /// <param name="rotation">指先を前とした向き</param>
        /// <param name="weight">通常姿勢からの割合</param>
        private void SetPose(Vector3 position, Quaternion rotation, float weight)
        {
            _body.SetPickupPose(position, rotation, weight, pinchGrip);
        }

        /// <summary>
        /// 胸の前で構える掌の位置を求める
        /// </summary>
        /// <returns>ワールド座標</returns>
        private Vector3 HoldPosition()
        {
            Vector3 up = Vector3.Cross(_player.CameraForward, _player.CameraRight);
            return _player.CameraPosition +
                   _player.CameraRight * holdOffset.x +
                   up * holdOffset.y +
                   _player.CameraForward * holdOffset.z;
        }

        /// <summary>
        /// 炎が視界に入るよう、マッチの先をやや上の内側へ向けた手の向きを求める
        /// </summary>
        /// <returns>指先を前とした向き</returns>
        private Quaternion HoldRotation()
        {
            Vector3 forward = Quaternion.AngleAxis(-25.0f, Vector3.up) *
                              Quaternion.AngleAxis(-15.0f, _player.CameraRight) *
                              _player.CameraForward;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// 着火点へマッチの先を向ける手の向きを求める
        /// </summary>
        /// <param name="target">着火点</param>
        /// <returns>指先を前とした向き</returns>
        private Quaternion ReachRotation(Vector3 target)
        {
            Vector3 forward = target - (_player.CameraPosition + _player.CameraRight * 0.15f - Vector3.up * 0.25f);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = _player.CameraForward;
            }
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>
        /// 視線と巣の面の交点に最も近い糸の点を求める
        /// </summary>
        /// <param name="web">対象の巣</param>
        /// <returns>ワールド座標</returns>
        private Vector3 GetAimThreadPoint(SpiderWeb web)
        {
            var plane = new Plane(web.transform.forward, web.transform.position);
            var ray = new Ray(_player.CameraPosition, _player.CameraForward);
            Vector3 aim = plane.Raycast(ray, out float distance) && distance > 0.0f
                ? ray.GetPoint(distance)
                : plane.ClosestPointOnPlane(_player.CameraPosition);
            return web.GetClosestThreadPoint(aim);
        }

        /// <summary>
        /// 軸木と頭薬、炎・火花・煙・光源を持つマッチを生成する
        /// </summary>
        private void CreateMatch()
        {
            _match = new GameObject("Held Match").transform;
            _runtimeObjects.Add(_match.gameObject);
            if (_body.TryGetRightPalmPose(out Pose palm))
            {
                _match.SetPositionAndRotation(palm.position, palm.rotation);
            }

            // 軸木は細い角材、頭薬は先端の楕円体で表す
            CreatePart(PrimitiveType.Cube, "Stick",
                new Vector3(0.0f, 0.0f, matchLength * 0.5f),
                new Vector3(0.0024f, 0.0024f, matchLength),
                CreateLitMaterial(new Color(0.72f, 0.55f, 0.34f), 0.25f));
            _headMaterial = CreateLitMaterial(new Color(0.42f, 0.06f, 0.04f), 0.35f);
            CreatePart(PrimitiveType.Sphere, "Head",
                new Vector3(0.0f, 0.0f, matchLength),
                new Vector3(0.0042f, 0.0042f, 0.0065f),
                _headMaterial);

            _tip = new GameObject("Tip").transform;
            _tip.SetParent(_match, false);
            _tip.localPosition = new Vector3(0.0f, 0.0f, matchLength + 0.002f);

            _flame = CreateFlame();
            _sparks = CreateSparks();
            _smoke = CreateSmoke();

            var lightObject = new GameObject("Match Light");
            lightObject.transform.SetParent(_tip, false);
            lightObject.transform.localPosition = Vector3.up * 0.015f;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = lightColor;
            _light.range = 3.0f;
            _light.shadows = LightShadows.None;
            HdrpLightUtility.SetIntensity(_light, 0.0f, LightUnit.Lumen);
            _light.enabled = false;
        }

        /// <summary>
        /// コライダーと影を持たないマッチの部品を作る
        /// </summary>
        /// <param name="type">プリミティブの種類</param>
        /// <param name="partName">オブジェクト名</param>
        /// <param name="localPosition">マッチ基準の位置</param>
        /// <param name="localScale">寸法</param>
        /// <param name="material">マテリアル</param>
        private void CreatePart(PrimitiveType type, string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(_match, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// マッチの部品用のHDRP Litマテリアルを作る
        /// </summary>
        /// <param name="color">基本色</param>
        /// <param name="smoothness">滑らかさ</param>
        /// <returns>生成したマテリアル。シェーダーが無い場合はnull</returns>
        private Material CreateLitMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "Match Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            HDMaterial.ValidateMaterial(material);
            _runtimeObjects.Add(material);
            return material;
        }

        /// <summary>
        /// 先端で揺らぐ小さな炎を作る。ワールド空間で放出し、動かすと炎がたなびく
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateFlame()
        {
            ParticleSystem particles = CreateParticles("Match Flame", 120,
                FireParticleFactory.CreateFlameTexture(), flameNits, true);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startSize = 0.0f;
            main.startSpeed = 0.0f;
            main.startColor = Color.white;

            // 根元は青白く、上へ行くほど黄色から橙に変わって消える
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.65f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.88f, 0.55f), 0.25f),
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.1f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            // 熱で上昇する気流をワールドの上方向へ与える
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 6.0f;
            noise.scrollSpeed = 2.0f;

            // 放出量は点火状態に合わせて倍率で切り替え、素早く動かした時も炎の尾を途切れさせない
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 1.0f;
            emission.rateOverTimeMultiplier = 0.0f;
            emission.rateOverDistance = 1.0f;
            emission.rateOverDistanceMultiplier = 0.0f;
            return particles;
        }

        /// <summary>
        /// 擦った瞬間に飛ぶ火花を作る
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateSparks()
        {
            ParticleSystem particles = CreateParticles("Match Sparks", 60,
                FireParticleFactory.CreateGlowTexture(), sparkNits, true);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.005f);
            main.startColor = new Color(1.0f, 0.7f, 0.35f);
            main.gravityModifier = 0.5f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.003f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.0f;
            return particles;
        }

        /// <summary>
        /// 点火時と消火後に立つ硫黄の煙を作る
        /// </summary>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateSmoke()
        {
            ParticleSystem particles = CreateParticles("Match Smoke", 40,
                FireParticleFactory.CreateSmokeTexture(), smokeNits, false);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = new Color(0.62f, 0.6f, 0.56f);
            main.gravityModifier = -0.02f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.3f, 0.15f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.6f),
                new Keyframe(1.0f, 3.0f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 2.0f;
            return particles;
        }

        /// <summary>
        /// マッチの先端に置く粒子の共通設定を作る
        /// </summary>
        /// <param name="objectName">オブジェクト名</param>
        /// <param name="maxParticles">同時に存在できる粒子数</param>
        /// <param name="texture">粒子テクスチャ</param>
        /// <param name="nits">白い粒子の輝度</param>
        /// <param name="additive">加算合成にするか</param>
        /// <returns>生成した粒子</returns>
        private ParticleSystem CreateParticles(string objectName, int maxParticles, Texture2D texture, float nits, bool additive)
        {
            var particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(_tip, false);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _runtimeObjects.Add(texture);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material material = FireParticleFactory.CreateMaterial(texture, nits, additive);
            if (material != null)
            {
                renderer.material = material;
                _runtimeObjects.Add(material);
            }
            particles.Play();
            return particles;
        }

        /// <summary>
        /// 頭薬を発火させ、閃光・火花・硫黄の煙を出して炎を灯す
        /// </summary>
        private void Ignite()
        {
            _flameStrength = 1.0f;
            _flare = 1.0f;
            _sparks.Emit(18);
            _smoke.Emit(4);

            // 燃えた頭薬は黒く焦げる
            if (_headMaterial != null)
            {
                _headMaterial.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.035f));
            }
        }

        /// <summary>
        /// 手首を振って炎を消し、細い煙を残す
        /// </summary>
        private void Extinguish()
        {
            _flameStrength = 0.0f;
            _flare = 0.0f;
            _smoke.Emit(6);
        }

        /// <summary>
        /// 燃えさしを物理で落下させる。プレイヤーとは衝突させない
        /// </summary>
        private void DropMatch()
        {
            if (_match == null)
            {
                return;
            }

            var collider = _match.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.0f, 0.0f, matchLength * 0.5f);
            collider.size = new Vector3(0.003f, 0.003f, matchLength);
            if (_player.PlayerCollider != null)
            {
                Physics.IgnoreCollision(collider, _player.PlayerCollider);
            }

            var body = _match.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.001f;
            body.linearDamping = 0.6f;
            body.angularDamping = 0.2f;
            body.linearVelocity = _player.CameraRight * 0.2f - Vector3.up * 0.1f;
            body.angularVelocity = Random.insideUnitSphere * 8.0f;
        }

        /// <summary>
        /// 生成したマッチ一式を破棄する
        /// </summary>
        /// <param name="delay">破棄までの秒数</param>
        private void DestroyMatch(float delay = 0.0f)
        {
            foreach (Object runtimeObject in _runtimeObjects)
            {
                if (runtimeObject != null)
                {
                    Destroy(runtimeObject, delay);
                }
            }
            _runtimeObjects.Clear();
            _match = null;
            _tip = null;
            _flameStrength = 0.0f;
            _flare = 0.0f;
        }
    }
}
