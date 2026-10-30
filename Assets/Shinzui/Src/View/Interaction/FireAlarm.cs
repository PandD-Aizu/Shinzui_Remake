using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Rendering;
using Shinzui.Infrastructure.Lighting;

namespace Shinzui.View.Interaction
{
    /// <summary>接近で警報を開始し、インタラクトで停止する火災報知器</summary>
    [DisallowMultipleComponent]
    public sealed class FireAlarm : InteractableComponent
    {
        /// <summary>外観ごとの表示ルートと表示灯の参照</summary>
        [System.Serializable]
        private sealed class ModelVariant
        {
            public GameObject root;
            public Transform lightOrigin;
            public Renderer lampRenderer;
            public int lampMaterialIndex;
        }

        [Header("Models")]
        [SerializeField] private ModelVariant[] modelVariants;
        [SerializeField] private int lampMaterialIndex = -1;

        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 30f;
        [SerializeField] private Transform player;

        [Header("Alarm")]
        [SerializeField] private EventReference alarmEvent;
        [Tooltip("パトランプがある場合はその発光位置を指定")]
        [SerializeField] private Transform lightOrigin;
        [SerializeField] private Light alarmLight;
        [SerializeField] private Renderer lampRenderer;
        [SerializeField, Min(0f)] private float lightIntensity = 4f;
        [SerializeField, Min(0.1f)] private float lightRange = 8f;
        [SerializeField, Min(0.1f)] private float flashInterval = 0.6f;

        private EventInstance _alarmInstance;
        private MaterialPropertyBlock _lampProperties;
        private float _nextActivationTime;
        private float _nextPlayerSearchTime;
        private float _activatedAt;
        private float _nextNoiseTime;
        /// <summary>手置きと動的生成の報知器の鳴動を通知</summary>
        public static event System.Action<FireAlarm, Vector3> Rang;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissiveColor");

        public bool IsAlarmActive { get; private set; }
        public override bool RequiresHold => false;
        public override string DisplayName => "[E] 警報を止める";
        public override string InteractableId => "fire_alarm";
        public override string InteractMessage => "火災報知器の警報を止めた";

        /// <summary>指定位置に赤色ライトを準備して待機状態にする</summary>
        private void Awake()
        {
            // 生成時に一度だけ外観を選び、対応する表示灯へ接続する
            if (modelVariants != null && modelVariants.Length > 0)
            {
                int selected = Random.Range(0, modelVariants.Length);
                for (int i = 0; i < modelVariants.Length; i++)
                    modelVariants[i].root.SetActive(i == selected);

                var variant = modelVariants[selected];
                lightOrigin = variant.lightOrigin;
                lampRenderer = variant.lampRenderer;
                lampMaterialIndex = variant.lampMaterialIndex;
                if (alarmLight != null) alarmLight.transform.position = lightOrigin.position;
            }

            // 既存モデルにも発光位置の指定だけでライトを追加できる
            if (lightOrigin == null) lightOrigin = transform;
            if (alarmLight == null)
            {
                var lightObject = new GameObject("Alarm Light");
                lightObject.transform.SetParent(lightOrigin, false);
                alarmLight = lightObject.AddComponent<Light>();
            }

            alarmLight.type = LightType.Point;
            alarmLight.color = Color.red;
            alarmLight.range = lightRange;
            /* 旧点光源の強度はカンデラとして保持しHDRPデータを初期化する */
            var data = HdrpLightUtility.SetIntensity(alarmLight, lightIntensity, LightUnit.Candela);
            data.affectsVolumetric = true;
            data.volumetricDimmer = .5f;
            _lampProperties = new MaterialPropertyBlock();
            CanInteract = false;
            SetLight(0f);
        }

        /// <summary>接近と再作動待ち時間を監視し、作動中の音と点滅を更新</summary>
        private void Update()
        {
            if (Time.timeScale == 0f) return;

            // 遅延生成されるプレイヤーも一定間隔で取得する
            if (player == null && Time.time >= _nextPlayerSearchTime)
            {
                var view = FindAnyObjectByType<PlayerView>();
                if (view != null) player = view.transform;
                _nextPlayerSearchTime = Time.time + 1f;
            }

            if (!IsAlarmActive)
            {
                if (player != null && player.gameObject.activeInHierarchy && Time.time >= _nextActivationTime
                    && (player.position - transform.position).sqrMagnitude <= detectionRadius * detectionRadius)
                    ActivateAlarm();
                return;
            }

            // 非ループのFMODイベントでも停止操作まで繰り返し再生する
            if (_alarmInstance.isValid())
            {
                _alarmInstance.set3DAttributes(RuntimeUtils.To3DAttributes(lightOrigin.position));
                _alarmInstance.getPlaybackState(out var state);
                if (state == PLAYBACK_STATE.STOPPED) _alarmInstance.start();
            }

            // 鳴動中も一定間隔で聴覚刺激を発行する
            if (Time.time >= _nextNoiseTime) EmitNoise();

            float phase = (Time.time - _activatedAt) / Mathf.Max(0.1f, flashInterval);
            SetLight(Mathf.Repeat(phase, 1f) < 0.5f ? 1f : 0.15f);
        }

        /// <summary>警報音と赤色灯を開始して停止操作を有効にする</summary>
        private void ActivateAlarm()
        {
            IsAlarmActive = true;
            CanInteract = true;
            _activatedAt = Time.time;
            EmitNoise();
            SetLight(1f);

            // 各報知器が自分のインスタンスを所有し、他の警報を停止しない
            _alarmInstance = alarmEvent.IsNull
                ? RuntimeManager.CreateInstance("event:/SE/FireAlarm")
                : RuntimeManager.CreateInstance(alarmEvent);
            _alarmInstance.set3DAttributes(RuntimeUtils.To3DAttributes(lightOrigin.position));
            _alarmInstance.start();
        }

        /// <summary>現在の音源位置を通知して次の発行時刻を設定</summary>
        private void EmitNoise()
        {
            Rang?.Invoke(this, lightOrigin.position);
            _nextNoiseTime = Time.time + 1f;
        }

        /// <summary>警報を停止し、この時点からクールダウンを開始</summary>
        public override void ExecuteInteractEffect()
        {
            if (!IsAlarmActive) return;
            StopAlarm();
        }

        /// <summary>音と赤色灯を停止し、再作動可能時刻を設定</summary>
        private void StopAlarm()
        {
            if (IsAlarmActive) _nextActivationTime = Time.time + cooldownSeconds;
            IsAlarmActive = false;
            CanInteract = false;
            SetLight(0f);

            // シーン破棄や非表示でも音源を残さない
            if (!_alarmInstance.isValid()) return;
            _alarmInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _alarmInstance.release();
            _alarmInstance.clearHandle();
        }

        /// <summary>ライトとランプ表面の発光を同時に変更</summary>
        /// <param name="brightness">最大発光に対する明るさ</param>
        private void SetLight(float brightness)
        {
            if (alarmLight != null)
            {
                alarmLight.enabled = brightness > 0f;
                alarmLight.intensity = lightIntensity * brightness;
            }

            if (lampRenderer == null || _lampProperties == null) return;
            if (lampMaterialIndex >= 0) lampRenderer.GetPropertyBlock(_lampProperties, lampMaterialIndex);
            else lampRenderer.GetPropertyBlock(_lampProperties);
            _lampProperties.SetColor(EmissionColor, Color.red * (brightness * lightIntensity));
            _lampProperties.SetColor("emissiveFactor", Color.red * (brightness * lightIntensity));
            if (lampMaterialIndex >= 0) lampRenderer.SetPropertyBlock(_lampProperties, lampMaterialIndex);
            else lampRenderer.SetPropertyBlock(_lampProperties);
        }

        /// <summary>非表示やシーン終了時に警報を解放</summary>
        private void OnDisable()
        {
            StopAlarm();
        }

        /// <summary>選択中の報知器の検知範囲を表示</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
