using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Shinzui.View.Inventory
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ItemGetUnitView : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private UnityEngine.VFX.VisualEffect appearVfx;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private AsyncOperationHandle<Sprite> _iconLoadHandle;
        private MotionHandle _appearMotion;
        private MotionHandle _disappearMotion;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Initialize(string itemName, string iconAddress, float displayTime, Action<ItemGetUnitView> onComplete)
        {
            // モーションのキャンセル
            CancelMotions();

            if (itemNameText != null)
            {
                itemNameText.text = itemName;
            }

            // Addressables によるスプライトのロード
            if (_iconLoadHandle.IsValid())
            {
                Addressables.Release(_iconLoadHandle);
            }

            if (!string.IsNullOrEmpty(iconAddress) && iconImage != null)
            {
                _iconLoadHandle = Addressables.LoadAssetAsync<Sprite>(iconAddress);
                _iconLoadHandle.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        iconImage.sprite = handle.Result;
                        iconImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        iconImage.gameObject.SetActive(false);
                    }
                };
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
                iconImage.sprite = null;
            }

            // 初期化状態の設定
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            if (_rectTransform != null)
            {
                // ピボットを左端中央 (0, 0.5) に強制設定して、左側への半分はみ出しを防止
                _rectTransform.pivot = new Vector2(0f, 0.5f);
                
                Vector2 pos = _rectTransform.anchoredPosition;
                pos.x = -300f; // 左側に配置
                _rectTransform.anchoredPosition = pos;
            }

            // アニメーションの開始
            PlayAppearAnimation(displayTime, onComplete);
        }

        private void PlayAppearAnimation(float displayTime, Action<ItemGetUnitView> onComplete)
        {
            // パーティクル再生 (VFXGraph)
            if (appearVfx != null)
            {
                appearVfx.Play();
            }

            // スライドイン＆フェードイン
            if (_rectTransform != null)
            {
                LMotion.Create(_rectTransform.anchoredPosition.x, 0f, 0.4f)
                    .WithEase(Ease.OutQuad)
                    .BindToAnchoredPositionX(_rectTransform);
            }

            if (_canvasGroup != null)
            {
                LMotion.Create(0f, 1f, 0.4f)
                    .WithEase(Ease.OutQuad)
                    .BindToAlpha(_canvasGroup);
            }

            // 指定時間後にフェードアウトアニメーションを実行
            _appearMotion = LMotion.Create(0f, 1f, displayTime)
                .WithOnComplete(() => PlayDisappearAnimation(onComplete))
                .RunWithoutBinding();
        }

        private void PlayDisappearAnimation(Action<ItemGetUnitView> onComplete)
        {
            if (_canvasGroup != null)
            {
                // フェードアウト
                _disappearMotion = LMotion.Create(1f, 0f, 0.3f)
                    .WithEase(Ease.InQuad)
                    .WithOnComplete(() =>
                    {
                        onComplete?.Invoke(this);
                    })
                    .BindToAlpha(_canvasGroup);
            }
            else
            {
                onComplete?.Invoke(this);
            }
        }

        private void CancelMotions()
        {
            if (_appearMotion.IsActive()) _appearMotion.Cancel();
            if (_disappearMotion.IsActive()) _disappearMotion.Cancel();
        }

        private void OnDisable()
        {
            CancelMotions();
        }

        private void OnDestroy()
        {
            // メモリリーク防止
            if (_iconLoadHandle.IsValid())
            {
                Addressables.Release(_iconLoadHandle);
            }
        }
    }
}
