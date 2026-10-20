using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Shinzui.View;
using Shinzui.View.Interaction;

namespace Shinzui.Temp.BotamochiStealth
{
    /// <summary>
    /// 話しかけられるキャラの共通部分。セリフ表示・選択肢・入力を持つ
    /// キャラごとの処理はこれを継承して、BuildScriptで台本を返す
    /// </summary>
    public abstract class TempTalkCharacter : InteractableComponent
    {
        [Header("Talk")]
        [SerializeField] private InteractionMessageView messageView;
        [Tooltip("自前入力で会話を開始する距離(m)")]
        [SerializeField] private float interactDistance = 3.5f;
        [Tooltip("この内積以上プレイヤーがこちらを向いている時だけ会話開始（誤爆防止）")]
        [SerializeField, Range(-1f, 1f)] private float facingDotThreshold = 0.3f;

        private const float RefreshInterval = 1.0f;

        private Coroutine _talk;
        private bool _ended = false;
        private string _currentText = string.Empty;
        private float _sinceShown = 0.0f;

        public bool IsTalking { get; private set; }
        public bool IsEnded => _ended;

        /// <summary>
        /// 話しかけられたときに流す台本を返す
        /// </summary>
        protected abstract TempTalkStep[] BuildScript();

        /// <summary>
        /// falseを返すと話しかけられない（1回しか話せないキャラなど）
        /// </summary>
        protected virtual bool CanStartTalk() => true;

        /// <summary>
        /// 会話が始まった直後に呼ばれる
        /// </summary>
        protected virtual void OnTalkStarted() { }

        /// <summary>
        /// 会話が終わったときに呼ばれる（End()でも台本の最後まで行ったときでも）
        /// </summary>
        protected virtual void OnTalkEnded() { }

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();
            if (IsTalking || !CanStartTalk()) return;

            StartTalk();
        }

        protected virtual void Update()
        {
            if (IsTalking) return;

            // 話しかけ前：プレイヤーが近くでこちらを向いて[E]キー/クリック等を押したら会話開始
            if (CanStartTalk() && IsInteractKeyPressedNearPlayer())
            {
                StartTalk();
            }
        }

        private void StartTalk()
        {
            IsTalking = true;
            _ended = false;
            OnTalkStarted();
            _talk = StartCoroutine(RunTalk(BuildScript()));
        }

        private IEnumerator RunTalk(TempTalkStep[] steps)
        {
            yield return TempTalk.RunAll(steps, this);
            _talk = null;
            // End()を書き忘れても抜けられるように
            EndTalk();
        }

        /// <summary>
        /// 会話を途中で止めて、また話しかけられる状態に戻す。OnTalkEndedは呼ばれない
        /// </summary>
        protected void StopTalk()
        {
            if (_talk != null)
            {
                StopCoroutine(_talk);
                _talk = null;
            }
            IsTalking = false;
            _ended = false;
        }

        /// <summary>
        /// 会話を終わらせてOnTalkEndedを呼ぶ。台本のEnd()から呼ばれる
        /// </summary>
        public void EndTalk()
        {
            if (_ended) return;
            _ended = true;
            IsTalking = false;
            OnTalkEnded();
        }

        /// <summary>
        /// セリフを出して、決定キーか秒数経過まで待つ。台本のSayから呼ばれる
        /// </summary>
        public IEnumerator ShowLine(string text, float autoAdvanceSeconds)
        {
            ShowText(text);
            float elapsed = 0.0f;
            while (true)
            {
                // 前の入力（話しかけたEキーなど）で飛ばないように1フレーム待ってから見る
                yield return null;
                elapsed += Time.deltaTime;
                KeepTextVisible();

                if (autoAdvanceSeconds > 0.0f && elapsed >= autoAdvanceSeconds) yield break;
                if (IsAdvanceKeyPressed()) yield break;
            }
        }

        /// <summary>
        /// 選択肢を出して、数字キーで選ばれるまで待つ。選んだ番号（0始まり）をonPickedで返す
        /// </summary>
        public IEnumerator ShowChoices(IReadOnlyList<string> options, Action<int> onPicked)
        {
            var labels = new List<string>();
            for (int i = 0; i < options.Count; i++)
            {
                labels.Add($"[{i + 1}] {options[i]}");
            }
            ShowText(TempMessages.Talk.ChoicePrompt + "\n" + string.Join("　　", labels));

            while (true)
            {
                yield return null;
                KeepTextVisible();

                int index = ReadChoiceIndex(options.Count);
                if (index >= 0)
                {
                    onPicked(index);
                    yield break;
                }
            }
        }

        /// <summary>
        /// 会話の外で一言出したいとき用（数秒で消える）
        /// </summary>
        protected void ShowMessage(string text)
        {
            if (messageView == null) messageView = FindAnyObjectByType<InteractionMessageView>();
            if (messageView != null)
            {
                messageView.ShowMessage(text);
            }
        }

        private void ShowText(string text)
        {
            _currentText = text;
            _sinceShown = 0.0f;
            ShowMessage(_currentText);
        }

        // InteractionMessageViewは数秒で勝手に消えるので、会話中は出し直して消えないようにしてる
        private void KeepTextVisible()
        {
            _sinceShown += Time.deltaTime;
            if (_sinceShown >= RefreshInterval)
            {
                ShowText(_currentText);
            }
        }

        // 選択肢が1個しか無いときはEとかでも決定できるようにしてる
        private int ReadChoiceIndex(int count)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < count && i < 9; i++)
                {
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    {
                        return i;
                    }
                }
            }

            if (count == 1 && IsAdvanceKeyPressed())
            {
                return 0;
            }
            return -1;
        }

        private bool IsInteractKeyPressedNearPlayer()
        {
            var player = FindAnyObjectByType<PlayerView>();
            if (player == null) return false;

            Vector3 toTarget = transform.position - player.transform.position;
            if (toTarget.sqrMagnitude > interactDistance * interactDistance) return false;

            // プレイヤー（カメラ）がこちらを向いているか
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 flatDir = toTarget;
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

        private static bool IsAdvanceKeyPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null &&
                (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }
    }
}
