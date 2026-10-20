using UnityEngine;
using Shinzui.View;
using Shinzui.View.Interaction;

namespace Shinzui.Temp.BotamochiStealth
{
    /// <summary>
    /// 彼女。会話中は敵を止め、会話が終わったら現代トンネルへ戻して次の階へ進める
    /// 1階につき1回だけ話せる。会話の中身はTempGirlfriendScriptに書く
    /// </summary>
    public class TempGirlfriendDialogue : TempTalkCharacter
    {
        [Header("Return Position")]
        [Tooltip("会話終了後にワープする現代トンネルの座標（TempPastTunnelDirectorが無い場合のフォールバック）")]
        [SerializeField] private Vector3 modernTunnelPosition = Vector3.zero;

        [Header("Optional References")]
        [SerializeField] private TempPatrolChaseEnemy chasingEnemy;

        private TempPastTunnelDirector _director;
        private bool _finished = false;

        /// <summary>
        /// 過去トンネルディレクターを登録し、会話完了時の「次の階層へ」進行を委譲する
        /// </summary>
        public void ConfigureReturn(TempPastTunnelDirector director)
        {
            _director = director;
        }

        /// <summary>
        /// 次の階層で改めて会話できるように、会話状態をリセットする
        /// （in-place 再生成では同じインスタンスが使い回されるため必要）
        /// </summary>
        public void ResetForNextFloor()
        {
            StopTalk();
            _finished = false;
            CanInteract = true;
        }

        // デバッグ用
        public void ResetScenarioProgress()
        {
            TempTalk.ResetChosen();
        }

        private void Reset()
        {
            // Inspectorでのプロンプト表示設定
            var field = typeof(InteractableComponent).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(this, TempMessages.Girlfriend.Prompt);
        }

        private void Awake()
        {
            // シーンのInspectorが既定の「[E] 調べる」のままならTempMessagesの文言にする
            var field = typeof(InteractableComponent).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.GetValue(this) as string == "[E] 調べる")
            {
                field.SetValue(this, TempMessages.Girlfriend.Prompt);
            }
        }

        protected override bool CanStartTalk() => !_finished;

        protected override TempTalkStep[] BuildScript()
        {
            if (_director == null) _director = FindAnyObjectByType<TempPastTunnelDirector>();
            int floor = _director != null ? _director.FloorNumber : 1;
            return TempGirlfriendScript.Build(floor);
        }

        protected override void OnTalkStarted()
        {
            if (chasingEnemy == null) chasingEnemy = FindAnyObjectByType<TempPatrolChaseEnemy>();
            if (chasingEnemy != null) chasingEnemy.PauseEnemy();
        }

        protected override void OnTalkEnded()
        {
            _finished = true;
            CanInteract = false;

            ShowMessage(TempMessages.Girlfriend.ReturnToModern);

            // ドキュメントは次の階に持ち越さない
            TempDocumentCollector.ResetAll();
            TempTalk.ResetChosen();

            if (_director == null)
            {
                _director = FindAnyObjectByType<TempPastTunnelDirector>();
            }

            if (_director != null)
            {
                // 現代トンネルを再生成して次の階層へ
                _director.AdvanceToNextFloor();
                return;
            }

            // フォールバック：ディレクター不在なら固定座標へワープ
            var player = FindAnyObjectByType<PlayerView>();
            if (player != null)
            {
                Vector3 offset = modernTunnelPosition - player.transform.position;
                player.Warp(offset);
            }

            if (chasingEnemy != null)
            {
                chasingEnemy.ResumeEnemy();
            }
        }
    }
}
