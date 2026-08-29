using System.Collections.Generic;
using DG.Tweening;
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
        [SerializeField] private float imageStartDelay = 0.5f;    // 画像全体の開始ディレイ

        [Header("Text スライド & ディレイ設定")] 
        [SerializeField] private float textSlideOffsetX = 80f;
        [SerializeField] private float textStartDelay = 0.2f;  // 最初のテキスト開始ディレイ
        [SerializeField] private float textStagger = 0.15f;    // テキスト間の追加ディレイ

        [Header("Layout 対策 / オプション")] 
        [SerializeField] private bool disableLayoutGroupDuringAnimation = true; // trueなら開始時にLayoutGroupを無効化
        [SerializeField] private LayoutGroup targetLayoutGroup;                 // 手動で割り当て。nullなら自動探索を試みる
        [SerializeField] private bool sequentialTextMode = false;               // true=テキストを完全に一つずつ順番に再生 / false=スタッガー
        [SerializeField] private bool debugLogDelays = false;                   // ディレイ確認用デバッグログ

        private readonly List<Tween> runningTweens = new();

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
            if (runningTweens.Count == 0) return;
            for (int i = 0; i < runningTweens.Count; i++)
            {
                var t = runningTweens[i];
                if (t != null && t.IsActive()) t.Kill();
            }
            runningTweens.Clear();
        }

        private void PrepareLayoutGroup()
        {
            if (!disableLayoutGroupDuringAnimation) return;
            if (targetLayoutGroup == null)
            {
                // 自動で同階層か親から探す
                targetLayoutGroup = GetComponent<LayoutGroup>();
                if (targetLayoutGroup == null)
                    targetLayoutGroup = GetComponentInParent<LayoutGroup>();
            }
            if (targetLayoutGroup != null)
            {
                // 先にレイアウト確定
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetLayoutGroup.GetComponent<RectTransform>());
                // 無効化してこれ以降Tweenで位置を動かせるようにする
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
                rt.anchoredPosition = targetPos + new Vector2(-Mathf.Abs(imageSlideOffsetX), 0f);

                // アルファ初期化
                var col = img.color; col.a = 0f; img.color = col;

                // Sequence内でTween生成
                Sequence seq = DOTween.Sequence();
                if (imageStartDelay > 0f)
                {
                    seq.AppendInterval(imageStartDelay);
                }

                // 同時開始
                seq.Join(DOTween.To(
                    () => img.color.a,
                    a => { var c = img.color; c.a = a; img.color = c; },
                    1f,
                    fadeDuration
                ).SetEase(fadeEase).SetTarget(img));

                seq.Join(DOTween.To(
                    () => rt.anchoredPosition,
                    p => rt.anchoredPosition = p,
                    targetPos,
                    fadeDuration
                ).SetEase(fadeEase).SetTarget(rt));

                runningTweens.Add(seq);
            }
        }

        private void AnimateTexts()
        {
            if (fadeInTexts == null || fadeInTexts.Count == 0) return;

            if (sequentialTextMode)
            {
                // 各テキストを開始時間にInsertする
                Sequence master = DOTween.Sequence();
                for (int i = 0; i < fadeInTexts.Count; i++)
                {
                    var txt = fadeInTexts[i];
                    if (txt == null) continue;
                    var rt = txt.rectTransform;

                    Vector2 targetPos = rt.anchoredPosition;
                    rt.anchoredPosition = targetPos + new Vector2(-Mathf.Abs(textSlideOffsetX), 0f);

                    var col = txt.color; col.a = 0f; txt.color = col;

                    // 内部では即開始させ、外側で開始時刻を制御
                    Sequence one = DOTween.Sequence();
                    one.Join(DOTween.To(
                        () => txt.color.a,
                        a => { var c = txt.color; c.a = a; txt.color = c; },
                        1f,
                        fadeDuration
                    ).SetEase(fadeEase).SetTarget(txt));

                    one.Join(DOTween.To(
                        () => rt.anchoredPosition,
                        p => rt.anchoredPosition = p,
                        targetPos,
                        fadeDuration
                    ).SetEase(fadeEase).SetTarget(rt));

                    float startTime = textStartDelay + textStagger * i;
                    master.Insert(startTime, one);

                    if (debugLogDelays)
                        Debug.Log($"[FadeController] Sequential(Overlap) Text index={i} startTime={startTime:F2}");
                }
                runningTweens.Add(master);
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
                    rt.anchoredPosition = targetPos + new Vector2(-Mathf.Abs(textSlideOffsetX), 0f);

                    var col = txt.color; col.a = 0f; txt.color = col;

                    float delay = textStartDelay + textStagger * i;
                    Sequence seq = DOTween.Sequence();
                    if (delay > 0f) seq.AppendInterval(delay);

                    seq.Join(DOTween.To(
                        () => txt.color.a,
                        a => { var c = txt.color; c.a = a; txt.color = c; },
                        1f,
                        fadeDuration
                    ).SetEase(fadeEase).SetTarget(txt));

                    seq.Join(DOTween.To(
                        () => rt.anchoredPosition,
                        p => rt.anchoredPosition = p,
                        targetPos,
                        fadeDuration
                    ).SetEase(fadeEase).SetTarget(rt));

                    if (debugLogDelays)
                        Debug.Log($"[FadeController] Stagger Text index={i} delay={delay:F2}");

                    runningTweens.Add(seq);
                }
            }
        }

        private void OnDisable()
        {
            KillAll();
        }
    }
}