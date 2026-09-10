using LitMotion.Extensions;
using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.Src.Title
{
    public class FadeController : MonoBehaviour
    {
        [SerializeField] private List<Image> fadeInObjects;
        [SerializeField] private List<TextMeshProUGUI> fadeInTexts;

        [Header("共通設定")]
        [SerializeField] private float fadeDuration = 3.0f;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        [Header("Image スライド設定")]
        [SerializeField] private float imageSlideOffsetX = 130f; // 左(マイナス方向)から入ってくる距離
        [SerializeField] private float imageStartDelay = 0.5f; // 画像全体の開始ディレイ

        [Header("Text スライド & ディレイ設定")]
        [SerializeField] private float textSlideOffsetX = 80f;
        [SerializeField] private float textStartDelay = 0.2f; // 最初のテキスト開始ディレイ
        [SerializeField] private float textStagger = 0.15f; // テキスト間の追加ディレイ

        [Header("Layout 対策 / オプション")]
        [SerializeField] private bool disableLayoutGroupDuringAnimation = true; // trueなら開始時にLayoutGroupを無効化
        [SerializeField] private LayoutGroup targetLayoutGroup; // 手動で割り当て。nullなら自動探索を試みる
        [SerializeField] private bool sequentialTextMode = false; // true=テキストを完全に一つずつ順番に再生 / false=スタッガー
        [SerializeField] private bool debugLogDelays = false; // ディレイ確認用デバッグログ

        private readonly List<MotionHandle> runningMotions = new();

        private void Start()
        {
            Play();
        }

        public void Play()
        {
            KillAll();
            PrepareLayoutGroup();
            AnimateImages();
            AnimateTexts();
        }

        public void KillAll()
        {
            if (runningMotions.Count == 0) return;

            for (int i = 0; i < runningMotions.Count; i++)
            {
                var motion = runningMotions[i];

                if (motion.IsActive())
                {
                    motion.Cancel();
                }
            }

            runningMotions.Clear();
        }

        private void PrepareLayoutGroup()
        {
            if (!disableLayoutGroupDuringAnimation) return;

            if (targetLayoutGroup == null)
            {
                // 自動で同階層か親から探す
                targetLayoutGroup = GetComponent<LayoutGroup>();

                if (targetLayoutGroup == null)
                {
                    targetLayoutGroup = GetComponentInParent<LayoutGroup>();
                }
            }

            if (targetLayoutGroup != null)
            {
                // 先にレイアウト確定
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    targetLayoutGroup.GetComponent<RectTransform>()
                );

                // 無効化してこれ以降Motionで位置を動かせるようにする
                targetLayoutGroup.enabled = false;
            }
        }

        private void AnimateImages()
        {
            if (fadeInObjects == null) return;

            for (int i = 0; i < fadeInObjects.Count; i++)
            {
                var img = fadeInObjects[i];

                if (img == null) continue;

                var rt = img.rectTransform;

                // 目標座標を保持
                Vector2 targetPos = rt.anchoredPosition;

                // 開始位置を左にオフセット
                rt.anchoredPosition =
                    targetPos + new Vector2(-Mathf.Abs(imageSlideOffsetX), 0f);

                // アルファ初期化
                var col = img.color;
                col.a = 0f;
                img.color = col;

                // LitMotion Sequence
                MotionSequenceBuilder sequence = LSequence.Create();

                if (imageStartDelay > 0f)
                {
                    sequence.AppendInterval(imageStartDelay);
                }

                // アルファと位置を同時開始
                sequence.Join(
                    LMotion.Create(0f, 1f, fadeDuration)
                        .WithEase(fadeEase)
                        .Bind(
                            img,
                            (a, target) =>
                            {
                                var c = target.color;
                                c.a = a;
                                target.color = c;
                            }
                        )
                );

                sequence.Join(
                    LMotion.Create(rt.anchoredPosition, targetPos, fadeDuration)
                        .WithEase(fadeEase)
                        .BindToAnchoredPosition(rt)
                );

                runningMotions.Add(sequence.Run());
            }
        }

        private void AnimateTexts()
        {
            if (fadeInTexts == null || fadeInTexts.Count == 0) return;

            if (sequentialTextMode)
            {
                // 各テキストを開始時間にInsertする
                MotionSequenceBuilder master = LSequence.Create();

                for (int i = 0; i < fadeInTexts.Count; i++)
                {
                    var txt = fadeInTexts[i];

                    if (txt == null) continue;

                    var rt = txt.rectTransform;

                    Vector2 targetPos = rt.anchoredPosition;

                    rt.anchoredPosition =
                        targetPos + new Vector2(-Mathf.Abs(textSlideOffsetX), 0f);

                    var col = txt.color;
                    col.a = 0f;
                    txt.color = col;

                    // 内部では即開始させ、外側で開始時刻を制御
                    MotionSequenceBuilder one = LSequence.Create();

                    one.Join(
                        LMotion.Create(0f, 1f, fadeDuration)
                            .WithEase(fadeEase)
                            .Bind(
                                txt,
                                (a, target) =>
                                {
                                    var c = target.color;
                                    c.a = a;
                                    target.color = c;
                                }
                            )
                    );

                    one.Join(
                        LMotion.Create(rt.anchoredPosition, targetPos, fadeDuration)
                            .WithEase(fadeEase)
                            .BindToAnchoredPosition(rt)
                    );

                    float startTime = textStartDelay + textStagger * i;

                    MotionHandle oneHandle = one.Run();
                    master.Insert(startTime, oneHandle);

                    if (debugLogDelays)
                    {
                        Debug.Log(
                            $"[FadeController] Sequential(Overlap) Text index={i} startTime={startTime:F2}"
                        );
                    }
                }

                runningMotions.Add(master.Run());
            }
            else
            {
                // 個別ディレイ
                for (int i = 0; i < fadeInTexts.Count; i++)
                {
                    var txt = fadeInTexts[i];

                    if (txt == null) continue;

                    var rt = txt.rectTransform;

                    Vector2 targetPos = rt.anchoredPosition;

                    rt.anchoredPosition =
                        targetPos + new Vector2(-Mathf.Abs(textSlideOffsetX), 0f);

                    var col = txt.color;
                    col.a = 0f;
                    txt.color = col;

                    float delay = textStartDelay + textStagger * i;

                    MotionSequenceBuilder sequence = LSequence.Create();

                    if (delay > 0f)
                    {
                        sequence.AppendInterval(delay);
                    }

                    sequence.Join(
                        LMotion.Create(0f, 1f, fadeDuration)
                            .WithEase(fadeEase)
                            .Bind(
                                txt,
                                (a, target) =>
                                {
                                    var c = target.color;
                                    c.a = a;
                                    target.color = c;
                                }
                            )
                    );

                    sequence.Join(
                        LMotion.Create(rt.anchoredPosition, targetPos, fadeDuration)
                            .WithEase(fadeEase)
                            .BindToAnchoredPosition(rt)
                    );

                    if (debugLogDelays)
                    {
                        Debug.Log(
                            $"[FadeController] Stagger Text index={i} delay={delay:F2}"
                        );
                    }

                    runningMotions.Add(sequence.Run());
                }
            }
        }

        private void OnDisable()
        {
            KillAll();
        }
    }
}