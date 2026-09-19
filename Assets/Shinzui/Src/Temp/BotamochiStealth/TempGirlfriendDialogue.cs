using System.Collections;
using UnityEngine;
using Shinzui.View;
using Shinzui.View.Interaction;

namespace Shinzui.Temp.BotamochiStealth
{
    /// <summary>
    /// 彼女との対話と現代トンネルへの帰還ワープを処理するシンプルなスクリプト
    /// </summary>
    public class TempGirlfriendDialogue : InteractableComponent
    {
        [Header("Dialogue Content")]
        [SerializeField, TextArea(2, 4)]
        private string[] messages = new string[]
        {
            "彼女『後藤健子です』",
            "彼女『帰んな』"
        };

        [Header("Return Position")]
        [Tooltip("会話終了後にワープする現代トンネルの座標（TempPastTunnelDirectorが無い場合のフォールバック）")]
        [SerializeField] private Vector3 modernTunnelPosition = Vector3.zero;

        [Header("Optional References")]
        [SerializeField] private InteractionMessageView messageView;
        [SerializeField] private TempPatrolChaseEnemy chasingEnemy;

        [Header("Interact Gate")]
        [Tooltip("自前入力で会話を開始する距離(m)")]
        [SerializeField] private float interactDistance = 3.5f;
        [Tooltip("この内積以上プレイヤーがこちらを向いている時だけ会話開始（誤爆防止）")]
        [SerializeField, Range(-1f, 1f)] private float facingDotThreshold = 0.3f;

        private TempPastTunnelDirector _director;
        private int _currentIndex = 0;
        private bool _isTalking = false;
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
            _isTalking = false;
            _finished = false;
            _currentIndex = 0;
            CanInteract = true;
        }

        private void Reset()
        {
            // Inspectorでのプロンプト表示設定
            var field = typeof(InteractableComponent).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(this, "[E] 話しかける");
        }

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();
            if (_isTalking || _finished) return;

            StartDialogue();
        }

        private void StartDialogue()
        {
            _isTalking = true;
            _currentIndex = 0;

            if (chasingEnemy == null) chasingEnemy = FindAnyObjectByType<TempPatrolChaseEnemy>();
            if (chasingEnemy != null) chasingEnemy.PauseEnemy();

            ShowCurrentLine();
        }

        private void Update()
        {
            if (!_isTalking)
            {
                // 話しかけ前：プレイヤーが近くでこちらを向いて[E]キー/クリック等を押したら会話開始
                if (!_finished && IsInteractKeyPressedNearPlayer())
                {
                    StartDialogue();
                }
                return;
            }

            // 会話中：メッセージ送り
            if (IsAdvanceKeyPressed())
            {
                _currentIndex++;
                if (_currentIndex < messages.Length)
                {
                    ShowCurrentLine();
                }
                else
                {
                    EndDialogueAndReturn();
                }
            }
        }

        private bool IsInteractKeyPressedNearPlayer()
        {
            var player = FindAnyObjectByType<PlayerView>();
            if (player == null) return false;

            Vector3 toGirl = transform.position - player.transform.position;
            if (toGirl.sqrMagnitude > interactDistance * interactDistance) return false;

            // プレイヤー（カメラ）がこちらを向いているか
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 flatDir = toGirl;
                flatDir.y = 0.0f;
                Vector3 flatFwd = cam.transform.forward;
                flatFwd.y = 0.0f;
                if (flatDir.sqrMagnitude > 0.001f && flatFwd.sqrMagnitude > 0.001f &&
                    Vector3.Dot(flatDir.normalized, flatFwd.normalized) < facingDotThreshold)
                {
                    return false;
                }
            }

            return IsAdvanceKeyPressed();
        }

        private bool IsAdvanceKeyPressed()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (UnityEngine.InputSystem.Gamepad.current != null &&
                (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        private void ShowCurrentLine()
        {
            if (messageView == null) messageView = FindAnyObjectByType<InteractionMessageView>();
            if (messageView != null && _currentIndex < messages.Length)
            {
                messageView.ShowMessage(messages[_currentIndex]);
            }
        }

        private void EndDialogueAndReturn()
        {
            _isTalking = false;
            _finished = true;
            CanInteract = false;

            if (messageView != null)
            {
                messageView.ShowMessage("現代トンネルへ戻される……");
            }

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
