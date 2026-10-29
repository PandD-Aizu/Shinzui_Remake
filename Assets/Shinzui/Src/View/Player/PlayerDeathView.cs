using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Shinzui.View
{
    public class PlayerDeathView : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private float jumpscareDelay = 0.8f;
        [SerializeField] private bool pauseGameOnGameOver = true;

        [Header("Game Over UI")]
        [SerializeField] private CanvasGroup gameOverCanvasGroup;

        [Header("Game Over Dissolve")]
        [SerializeField] private GameOverDissolveView gameOverDissolveView;
        [SerializeField] private bool waitForDissolveBeforeGameOverUi = true;

        [Header("Hooks")]
        [SerializeField] private UnityEvent onJumpscareRequested;
        [SerializeField] private UnityEvent onGameOverShown;

        private bool _isSequencePlaying;
        private EnemyView _captureEnemy;
        private PlayerView _capturePlayer;

        /// <summary>死亡確定時に再生する人型の捕獲演出を予約または解除</summary>
        /// <param name="enemy">捕獲した敵、解除時はnull</param>
        /// <param name="player">捕獲されたプレイヤー</param>
        public void PrepareHumanCapture(EnemyView enemy, PlayerView player)
        {
            _captureEnemy = enemy;
            _capturePlayer = player;
        }

        private void Awake()
        {
            EnsureGameOverCanvas();
            SetGameOverVisible(false);
        }

        private void OnDestroy()
        {
            if (pauseGameOnGameOver)
            {
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// 死亡シーケンスを開始する
        /// </summary>
        public void PlayDeathSequence()
        {
            if (_isSequencePlaying)
            {
                return;
            }

            DeathSequenceAsync().Forget();
        }

        /// <summary>
        /// 死亡シーケンスを実行する
        /// </summary>
        private async UniTaskVoid DeathSequenceAsync()
        {
            _isSequencePlaying = true;
            bool playedHumanCapture = false;
            // 人型に捕まった場合は振り返りと顔アップを終えてからゲームオーバーへ進む
            if (_captureEnemy != null)
            {
                var capture = _captureEnemy.GetComponent<HumanEnemyCaptureView>();
                if (capture == null) capture = _captureEnemy.gameObject.AddComponent<HumanEnemyCaptureView>();
                if (capture.Begin(_capturePlayer))
                {
                    playedHumanCapture = true;
                    await UniTask.WaitUntil(() => capture == null || !capture.IsPlaying,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                }
            }
            if (!playedHumanCapture) onJumpscareRequested?.Invoke();

            if (!playedHumanCapture && jumpscareDelay > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(jumpscareDelay),
                    DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
            }

            if (gameOverDissolveView != null)
            {
                if (waitForDissolveBeforeGameOverUi)
                {
                    await gameOverDissolveView.PlayAsync();
                }
                else
                {
                    gameOverDissolveView.Play();
                }
            }

            SetGameOverVisible(true);
            onGameOverShown?.Invoke();

            if (pauseGameOnGameOver)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// ゲームオーバー用のCanvasの表示/非表示を切り替える
        /// </summary>
        /// <param name="visible">表示するかどうか</param>
        private void SetGameOverVisible(bool visible)
        {
            if (gameOverCanvasGroup == null) return;

            gameOverCanvasGroup.alpha = visible ? 1f : 0f;
            gameOverCanvasGroup.interactable = visible;
            gameOverCanvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// ゲームオーバー用のCanvasを生成する
        /// フォールバックです
        /// </summary>
        private void EnsureGameOverCanvas()
        {
            if (gameOverCanvasGroup != null) return;

            GameObject canvasObject = new GameObject(
                "GameOverCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            gameOverCanvasGroup = canvasObject.GetComponent<CanvasGroup>();

            GameObject backgroundObject = new GameObject("Background", typeof(Image));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.92f);

            GameObject textObject = new GameObject("GameOverText", typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(900f, 180f);

            Text text = textObject.GetComponent<Text>();
            text.text = "GAME OVER";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 96;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
