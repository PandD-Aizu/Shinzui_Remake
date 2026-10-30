using System;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.View
{
    /// <summary>実移動に歩行周期を同期し、脚の二関節IKで足元の接地を補正</summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class HumanEnemyLocomotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Leg
        {
            public Transform thigh;
            public Transform shin;
            public Transform foot;
        }

        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Leg leftLeg;
        [SerializeField] private Leg rightLeg;
        [SerializeField] private LayerMask groundLayers = 1 << 8;
        [SerializeField, Min(.01f)] private float soleHeight = .17f;
        [SerializeField, Min(.01f)] private float authoredWalkSpeed = .75f;
        [Header("Ceiling Ambush")]
        [SerializeField, Min(2f)] private float minCeilingHeight = 4.2f;
        [SerializeField, Min(.5f)] private float ambushDropRadius = 4.5f;
        [SerializeField, Min(0f)] private float ceilingCreepSpeed = .45f;
        [SerializeField, Min(0f)] private float ceilingCreepRange = 8f;
        [SerializeField, Min(1f)] private float ambushGiveUpTime = 50f;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int StrideRate = Animator.StringToHash("StrideRate");
        private const float CrawlSpineAngle = 105f;
        private const float CrawlHipHeight = .32f;
        private Transform _hips, _spine;
        private float _introPose;
        private bool _poseApplied;
        private Transform _head;
        private Camera _introCamera;
        private Leg[] _crawlLimbs;
        private Transform[] _crawlBones;
        private Vector3[] _bonePositions;
        private Quaternion[] _boneRotations;
        private readonly Vector3[] _contacts = new Vector3[4];
        private readonly Vector3[] _stepStarts = new Vector3[4];
        private readonly Vector3[] _stepEnds = new Vector3[4];
        private readonly bool[] _swinging = new bool[4];
        private Vector3 _previousRoot;
        private float _crawlCycle;
        private bool _crawlInitialized;
        private Vector3 _crawlUp;
        private float _lungeProgress;
        private Vector3 _lungeStartFace;
        private bool _chasing;
        private Vector3 _chaseTarget;
        private float _chaseWeight;
        private float _runCycle;

        /// <summary>待ち伏せに使う大きいトンネルの最低天井高</summary>
        public float MinCeilingHeight => minCeilingHeight;
        /// <summary>真下へ落ちてくるプレイヤーとの水平距離</summary>
        public float AmbushDropRadius => ambushDropRadius;
        /// <summary>天井を這う速度</summary>
        public float CeilingCreepSpeed => ceilingCreepSpeed;
        /// <summary>登った地点から這って離れられる距離</summary>
        public float CeilingCreepRange => ceilingCreepRange;
        /// <summary>空振りした待ち伏せを諦めるまでの秒数</summary>
        public float AmbushGiveUpTime => ambushGiveUpTime;

        /// <summary>追跡状態と両手を伸ばす位置を設定</summary>
        /// <param name="chasing">プレイヤーを追跡中か</param>
        /// <param name="target">プレイヤーの上半身の位置</param>
        public void SetChasePose(bool chasing, Vector3 target)
        {
            _chasing = chasing;
            _chaseTarget = target;
        }

        /// <summary>登場時に屈み込む骨格を取得</summary>
        private void Awake()
        {
            foreach (var bone in GetComponentsInChildren<Transform>())
            {
                if (bone.name == "Hips") _hips = bone;
                if (bone.name == "Spine") _spine = bone;
                if (bone.name == "Head") _head = bone;
            }
            _crawlBones = _hips.GetComponentsInChildren<Transform>();
            _bonePositions = new Vector3[_crawlBones.Length];
            _boneRotations = new Quaternion[_crawlBones.Length];
            _crawlLimbs = new[] { FindArm("L"), FindArm("R"), leftLeg, rightLeg };
        }

        /// <summary>四つん這いでカメラを覗き込む姿勢を設定</summary>
        /// <param name="weight">屈み姿勢の割合</param>
        /// <param name="camera">顔を向けるカメラ</param>
        public void SetIntroPose(float weight, Camera camera = null)
        {
            _introPose = Mathf.Clamp01(weight);
            _introCamera = camera;
            if (_introPose <= 0f) _lungeProgress = 0f;
        }

        /// <summary>飛びつき開始時の顔の位置と対象カメラを保存</summary>
        /// <param name="camera">飛びつく対象のカメラ</param>
        public void BeginLunge(Camera camera)
        {
            _introCamera = camera;
            _lungeStartFace = _head.position + _head.up * .12f + _head.forward * .08f;
            _lungeProgress = 0f;
        }

        /// <summary>飛びつきの進行率を設定</summary>
        /// <param name="progress">飛びつき開始から衝突までの割合</param>
        public void SetLungeProgress(float progress) => _lungeProgress = Mathf.Clamp01(progress);

        /// <summary>接地した屈み姿勢の終点から顔の位置を予測</summary>
        /// <param name="position">接近終了時の足元の位置</param>
        /// <param name="rotation">接近中の身体の向き</param>
        /// <param name="cameraPosition">顔を向けるカメラの位置</param>
        /// <returns>屈み切った時点の顔の中心</returns>
        public Vector3 PredictIntroFacePosition(Vector3 position, Quaternion rotation, Vector3 cameraPosition)
        {
            Quaternion turn = rotation * Quaternion.Inverse(transform.rotation);
            Vector3 bentHead = _spine.position + Quaternion.AngleAxis(CrawlSpineAngle, transform.right) * (_head.position - _spine.position);
            float drop = _hips.position.y - transform.position.y - CrawlHipHeight;
            Vector3 head = position + turn * (bentHead - transform.position - Vector3.up * drop);
            Quaternion look = Quaternion.LookRotation(cameraPosition - head, Vector3.up);
            return head + look * new Vector3(0f, .12f, .08f);
        }

        /// <summary>実際に描画した顔が画面中央付近へ達したかを判定</summary>
        /// <param name="camera">転倒中のカメラ</param>
        /// <returns>顔が中央の狭い範囲に入った場合はtrue</returns>
        public bool IsFaceAtCenter(Camera camera)
        {
            Vector3 face = _head.position + _head.up * .12f + _head.forward * .08f;
            Vector3 viewport = camera.WorldToViewportPoint(face);
            return viewport.z > 0f && Mathf.Abs(viewport.x - .5f) < .035f && Mathf.Abs(viewport.y - .5f) < .06f;
        }

        /// <summary>実際の速度に合わせて待機・歩行と歩幅を切り替える</summary>
        private void Update()
        {
            // 前フレームの手足と首の補正を戻し、アニメーションへ累積させない
            if (_poseApplied)
            {
                for (int i = 0; i < _crawlBones.Length; i++)
                {
                    _crawlBones[i].localPosition = _bonePositions[i];
                    _crawlBones[i].localRotation = _boneRotations[i];
                }
                _poseApplied = false;
            }

            if (animator == null || agent == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            _chaseWeight = Mathf.MoveTowards(_chaseWeight, _chasing && _introPose <= 0f ? 1f : 0f, Time.deltaTime * 5f);
            _runCycle += speed * Time.deltaTime / 1.4f;
            animator.SetFloat(Speed, _introPose > 0f ? 0f : speed, .15f, Time.deltaTime);
            // 後ずさり中は歩行周期も反転し、足の運びと移動方向を揃える
            float direction = speed > .01f && Vector3.Dot(agent.velocity, transform.forward) < -.01f ? -1f : 1f;
            animator.SetFloat(StrideRate, Mathf.Clamp(speed / authoredWalkSpeed, .3f, 2.5f) * direction);
        }

        /// <summary>アニメーション評価後に左右の脚を床へ合わせる</summary>
        private void LateUpdate()
        {
            if (animator == null || !animator.enabled || leftLeg?.foot == null || rightLeg?.foot == null) return;
            Vector3 leftTarget = leftLeg.foot.position;
            Vector3 rightTarget = rightLeg.foot.position;

            // 導入中は手足を接地した四つん這い、通常時は歩行の接地補正
            if (_introPose > 0f)
            {
                ApplyCrawlPose();
                return;
            }
            _crawlInitialized = false;
            SavePose();
            ApplyUprightPose();
            leftTarget = leftLeg.foot.position;
            rightTarget = rightLeg.foot.position;
            PlaceFoot(leftLeg, leftTarget);
            PlaceFoot(rightLeg, rightTarget);
        }

        /// <summary>腕の三つの骨を取得して接地処理へ渡す</summary>
        /// <param name="side">左右を表す骨名の末尾</param>
        /// <returns>上腕、前腕、手首の組</returns>
        private Leg FindArm(string side)
        {
            var arm = new Leg();
            foreach (var bone in _crawlBones)
            {
                if (bone.name == "UpperArm." + side) arm.thigh = bone;
                if (bone.name == "Forearm." + side) arm.shin = bone;
                if (bone.name == "Hand." + side) arm.foot = bone;
            }
            return arm;
        }

        /// <summary>補正前の姿勢を保存し、次のフレームへの累積を防ぐ</summary>
        private void SavePose()
        {
            // 補正対象の骨を保存し、起立や通常追跡へ滑らかに戻せるようにする
            for (int i = 0; i < _crawlBones.Length; i++)
            {
                _bonePositions[i] = _crawlBones[i].localPosition;
                _boneRotations[i] = _crawlBones[i].localRotation;
            }
            _poseApplied = true;
        }

        /// <summary>通常の猫背と、両腕を対象へ伸ばす走行姿勢を合成</summary>
        private void ApplyUprightPose()
        {
            // 通常時は背中と首を曲げ、追跡時は顔を上げる
            _spine.rotation = Quaternion.AngleAxis(Mathf.Lerp(32f, 15f, _chaseWeight), transform.right) * _spine.rotation;
            _head.rotation = Quaternion.AngleAxis(22f * (1f - _chaseWeight), transform.right) * _head.rotation;
            if (_chaseWeight <= 0f) return;

            Vector3 look = _chaseTarget - _head.position;
            if (look.sqrMagnitude > .001f)
                _head.rotation = Quaternion.Slerp(_head.rotation, Quaternion.LookRotation(look, Vector3.up), _chaseWeight);

            // 左右の腕を肩幅で伸ばし、曲がり角でも実際の対象へ向ける
            for (int i = 0; i < 2; i++)
            {
                Leg arm = _crawlLimbs[i];
                Vector3 side = transform.right * (i == 0 ? -1f : 1f);
                Vector3 direction = (_chaseTarget + side * .18f - arm.thigh.position).normalized;
                float reach = Vector3.Distance(arm.thigh.position, arm.shin.position)
                    + Vector3.Distance(arm.shin.position, arm.foot.position);
                PlaceCrawlLimb(arm, arm.thigh.position + direction * reach * .96f,
                    Vector3.down + side * .35f, false, _chaseWeight);
                arm.foot.rotation = Quaternion.Slerp(arm.foot.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), _chaseWeight);
            }

            // 実移動に同期した大きな歩幅と足の蹴り上げで走行を表現
            float moving = agent != null && agent.enabled && agent.isOnNavMesh
                ? Mathf.Clamp01(agent.velocity.magnitude / .4f) : 0f;
            for (int i = 0; i < 2; i++)
            {
                Leg leg = i == 0 ? leftLeg : rightLeg;
                float phase = (_runCycle + i * .5f) * Mathf.PI * 2f;
                Vector3 target = transform.TransformPoint(new Vector3(i == 0 ? -.16f : .16f,
                    soleHeight + Mathf.Max(0f, Mathf.Sin(phase)) * .32f, Mathf.Cos(phase) * .42f));
                PlaceCrawlLimb(leg, target, transform.forward, false, _chaseWeight * moving);
            }
        }

        /// <summary>腰を下げ、対角の手足を交互に運びながら顔をカメラへ向ける</summary>
        private void ApplyCrawlPose()
        {
            SavePose();

            // 天井に逆さまで張り付く場合も身体の上方向を床の法線として扱う
            Vector3 up = transform.up;
            Vector3 floor = GroundPoint(transform.position);
            Vector3 hips = _hips.position;
            hips += up * (CrawlHipHeight - Vector3.Dot(hips - floor, up));
            _hips.position = Vector3.Lerp(_hips.position, hips, _introPose);
            _spine.rotation = Quaternion.AngleAxis(CrawlSpineAngle * _introPose, transform.right) * _spine.rotation;

            // 歩幅は実移動に同期し、接地中の手足をワールド座標に固定
            // 天井へ跳び移る回転中は接地点を身体へ追従させる
            if (!_crawlInitialized || Vector3.Dot(up, _crawlUp) < .999f)
            {
                _crawlUp = up;
                _crawlCycle = 0f;
                for (int i = 0; i < 4; i++)
                {
                    _contacts[i] = CrawlContact(i);
                    _swinging[i] = false;
                }
                _previousRoot = transform.position;
                _crawlInitialized = true;
            }
            float distance = Vector3.Distance(transform.position, _previousRoot);
            _previousRoot = transform.position;
            _crawlCycle += distance / .55f;
            for (int i = 0; i < 4; i++)
            {
                float phase = Mathf.Repeat(_crawlCycle + (i == 0 || i == 3 ? 0f : .5f), 1f);
                bool swing = phase < .4f && distance > .00001f;
                if (swing && !_swinging[i])
                {
                    _stepStarts[i] = _contacts[i];
                    _stepEnds[i] = GroundPoint(CrawlContact(i) + transform.forward * .16f) + up * (i < 2 ? .14f : soleHeight);
                }
                if (swing)
                {
                    float t = phase / .4f;
                    _contacts[i] = Vector3.Lerp(_stepStarts[i], _stepEnds[i], Mathf.SmoothStep(0f, 1f, t))
                        + up * (Mathf.Sin(t * Mathf.PI) * .07f);
                }
                else if (_swinging[i]) _contacts[i] = _stepEnds[i];
                _swinging[i] = swing;
                Vector3 side = transform.right * (i % 2 == 0 ? -1f : 1f);
                Vector3 pole = i < 2 ? side + transform.forward * .25f : -up + side * .15f + transform.forward * .15f;
                PlaceCrawlLimb(_crawlLimbs[i], _contacts[i], pole, i < 2);
            }

            // 胴体の前傾から首を独立させ、床ではなくプレイヤーの顔を見る
            if (_introCamera != null)
            {
                Quaternion look = Quaternion.LookRotation(_introCamera.transform.position - _head.position, up);
                _head.rotation = Quaternion.Slerp(_head.rotation, look, _introPose);
            }

            // 手足を床から離し、身体全体を弧を描いてカメラ直前へ飛び込ませる
            if (_lungeProgress > 0f && _introCamera != null)
            {
                float travel = _lungeProgress * _lungeProgress;
                var camera = _introCamera.transform;

                // 横を向いた視線に顔の正面を合わせ、画面中央を顔で埋める
                _head.rotation = Quaternion.Slerp(_head.rotation, Quaternion.LookRotation(-camera.forward, camera.up), travel);
                Vector3 face = _head.position + _head.up * .12f + _head.forward * .08f;
                Vector3 target = Vector3.Lerp(_lungeStartFace, camera.position + camera.forward * .22f, travel);
                _hips.position += target - face + Vector3.up * (Mathf.Sin(travel * Mathf.PI) * .18f);
            }
        }

        /// <summary>四つん這いの手足の基準接地点を求める</summary>
        /// <param name="index">左手、右手、左足、右足の順の番号</param>
        /// <returns>床の高さを反映した接地点</returns>
        private Vector3 CrawlContact(int index)
        {
            float side = index % 2 == 0 ? -1f : 1f;
            Vector3 point = transform.TransformPoint(new Vector3(side * (index < 2 ? .36f : .28f), 0f, index < 2 ? .65f : -.65f));
            return GroundPoint(point) + transform.up * (index < 2 ? .14f : soleHeight);
        }

        /// <summary>骨格補正で使う床面の位置を取得</summary>
        /// <param name="point">床を探す水平位置</param>
        /// <returns>床面上の位置</returns>
        private Vector3 GroundPoint(Vector3 point)
        {
            // 天井では身体の上方向から接地面を探す
            Vector3 up = transform.up;
            if (Physics.Raycast(point + up, -up, out var hit, 2f, groundLayers, QueryTriggerInteraction.Ignore)) return hit.point;
            return point + up * Vector3.Dot(transform.position - point, up);
        }

        /// <summary>手首または足首を接地点へ合わせ、肘や膝を外側へ曲げる</summary>
        /// <param name="limb">補正する三つの骨</param>
        /// <param name="contact">接地点</param>
        /// <param name="pole">関節を曲げる方向</param>
        /// <param name="hand">手のひらを床へ向けるか</param>
        /// <param name="weight">補正割合、省略時は導入姿勢の割合</param>
        private void PlaceCrawlLimb(Leg limb, Vector3 contact, Vector3 pole, bool hand, float weight = -1f)
        {
            if (weight < 0f) weight = _introPose;
            Vector3 target = Vector3.Lerp(limb.foot.position, contact, weight);
            Quaternion endRotation = limb.foot.rotation;
            Vector3 root = limb.thigh.position;
            float upper = Vector3.Distance(root, limb.shin.position);
            float lower = Vector3.Distance(limb.shin.position, limb.foot.position);
            Vector3 direction = (target - root).normalized;
            float length = Mathf.Clamp(Vector3.Distance(root, target), Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            float along = (upper * upper - lower * lower + length * length) / (2f * length);
            Vector3 joint = root + direction * along + Vector3.ProjectOnPlane(pole, direction).normalized
                * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            limb.thigh.rotation = Quaternion.FromToRotation(limb.shin.position - root, joint - root) * limb.thigh.rotation;
            limb.shin.rotation = Quaternion.FromToRotation(limb.foot.position - limb.shin.position, target - limb.shin.position) * limb.shin.rotation;
            limb.foot.rotation = hand ? Quaternion.Slerp(endRotation, Quaternion.LookRotation(-transform.up, transform.forward), _introPose) : endRotation;
        }

        /// <summary>膝の曲がる方向を保ち、届く範囲内で足を地面へ合わせる</summary>
        /// <param name="leg">補正する脚の骨格</param>
        /// <param name="animatedFoot">屈む前の足の位置</param>
        private void PlaceFoot(Leg leg, Vector3 animatedFoot)
        {
            if (leg == null || leg.thigh == null || leg.shin == null || leg.foot == null) return;
            Vector3 footPosition = leg.foot.position;
            if (!Physics.Raycast(animatedFoot + Vector3.up * .45f, Vector3.down, out var hit,
                    .9f, groundLayers, QueryTriggerInteraction.Ignore)) return;

            // 振り上げた足は空中に残し、接地高さの差だけを加算
            float lift = Mathf.Max(0f, animatedFoot.y - transform.position.y - soleHeight);
            Vector3 target = new Vector3(animatedFoot.x, hit.point.y + soleHeight + lift, animatedFoot.z);
            if (_introPose <= 0f && Mathf.Abs(target.y - footPosition.y) > .3f) return;
            Quaternion rotation = leg.foot.rotation;
            Vector3 hip = leg.thigh.position;
            float upper = Vector3.Distance(hip, leg.shin.position);
            float lower = Vector3.Distance(leg.shin.position, footPosition);
            Vector3 direction = (target - hip).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, hip), Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 pole = Vector3.ProjectOnPlane(leg.shin.position - hip, direction);
            if (pole.sqrMagnitude < .00001f) pole = Vector3.ProjectOnPlane(transform.forward, direction);
            Vector3 knee = hip + direction * along + pole.normalized * bend;

            leg.thigh.rotation = Quaternion.FromToRotation(leg.shin.position - hip, knee - hip) * leg.thigh.rotation;
            leg.shin.rotation = Quaternion.FromToRotation(leg.foot.position - leg.shin.position, target - leg.shin.position) * leg.shin.rotation;
            leg.foot.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;
        }
    }
}
