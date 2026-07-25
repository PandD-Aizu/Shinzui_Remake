using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Shinzui.View
{
    /**
     * PlayerのHUDの右下にあるQuickAccessItemSlotのView
     */
    public class PlayerQuickItemSlotView : MonoBehaviour
    {
        [Header("装備中の特殊アイテム")]
        [SerializeField] public Image specialItemSlot;
        [SerializeField] public Image specialItemIcon;

        [Header("装備中の通常アイテム")]
        [SerializeField] public Image normalItemSlot;
        [SerializeField] public Image normalItemIcon;

        private AsyncOperationHandle<Sprite> _specialIconHandle;
        private AsyncOperationHandle<Sprite> _normalIconHandle;

        private void Awake()
        {
            ResolveImagesIfNeeded();
            ClearSpecialItem();
            ClearNormalItem();
        }

        /// <summary>
        /// 設定
        /// </summary>
        /// <param name="normalSlot"></param>
        /// <param name="normalIcon"></param>
        /// <param name="specialSlot"></param>
        /// <param name="specialIcon"></param>
        public void Configure(Image normalSlot, Image normalIcon, Image specialSlot, Image specialIcon)
        {
            normalItemSlot = normalSlot;
            normalItemIcon = normalIcon;
            specialItemSlot = specialSlot;
            specialItemIcon = specialIcon;

            ClearSpecialItem();
            ClearNormalItem();
        }

        /// <summary>
        /// 通常アイテム表示を設定する
        /// </summary>
        /// <param name="iconAddress">アイコンのアドレス</param>
        public void SetNormalItem(string iconAddress)
        {
            SetIcon(ref _normalIconHandle, normalItemSlot, normalItemIcon, iconAddress);
        }

        /// <summary>
        /// 通常アイテムの表示をクリアする
        /// </summary>
        public void ClearNormalItem()
        {
            ClearIcon(ref _normalIconHandle, normalItemSlot, normalItemIcon);
        }

        /// <summary>
        /// 特殊アイテム表示を設定する
        /// </summary>
        /// <param name="iconAddress">アイコンのアドレス</param>
        public void SetSpecialItem(string iconAddress)
        {
            SetIcon(ref _specialIconHandle, specialItemSlot, specialItemIcon, iconAddress);
        }

        /// <summary>
        /// 特殊アイテムの表示をクリアする
        /// </summary>
        public void ClearSpecialItem()
        {
            ClearIcon(ref _specialIconHandle, specialItemSlot, specialItemIcon);
        }

        /// <summary>
        /// アイコンを設定する
        /// </summary>
        /// <param name="handle">ハンドル</param>
        /// <param name="slot">スロット</param>
        /// <param name="icon">アイコン</param>
        /// <param name="iconAddress">アイコンのアドレス</param>
        private void SetIcon(ref AsyncOperationHandle<Sprite> handle, Image slot, Image icon, string iconAddress)
        {
            ResolveImagesIfNeeded();

            if (icon == null)
            {
                return;
            }

            ReleaseHandle(ref handle);

            if (slot != null)
            {
                slot.gameObject.SetActive(true);
            }

            if (string.IsNullOrEmpty(iconAddress))
            {
                icon.sprite = null;
                icon.gameObject.SetActive(false);
                return;
            }

            handle = Addressables.LoadAssetAsync<Sprite>(iconAddress);
            handle.Completed += completed =>
            {
                if (icon == null)
                {
                    return;
                }

                if (completed.Status == AsyncOperationStatus.Succeeded)
                {
                    icon.sprite = completed.Result;
                    icon.gameObject.SetActive(true);
                }
                else
                {
                    icon.sprite = null;
                    icon.gameObject.SetActive(false);
                }
            };
        }

        /// <summary>
        /// ロードしたアイコンをリリース
        /// </summary>
        /// <param name="handle">ハンドル</param>
        /// <param name="slot">スロット</param>
        /// <param name="icon">アイコン</param>
        private void ClearIcon(ref AsyncOperationHandle<Sprite> handle, Image slot, Image icon)
        {
            ReleaseHandle(ref handle);

            if (icon != null)
            {
                icon.sprite = null;
                icon.gameObject.SetActive(false);
            }

            if (slot != null)
            {
                slot.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 画像の参照を解決する
        /// </summary>
        private void ResolveImagesIfNeeded()
        {
            if (normalItemIcon == null)
            {
                normalItemIcon = transform.Find("NormalItemSlot/ItemIcon")?.GetComponent<Image>()
                                 ?? transform.Find("NormalItemIcon")?.GetComponent<Image>();
            }

            if (normalItemSlot == null)
            {
                normalItemSlot = transform.Find("NormalItemSlot")?.GetComponent<Image>();
            }

            if (specialItemIcon == null)
            {
                specialItemIcon = transform.Find("SpecialItemSlot/ItemIcon")?.GetComponent<Image>()
                                  ?? transform.Find("SpecialItemIcon")?.GetComponent<Image>();
            }

            if (specialItemSlot == null)
            {
                specialItemSlot = transform.Find("SpecialItemSlot")?.GetComponent<Image>();
            }
        }

        /// <summary>
        /// ハンドルを開放する
        /// </summary>
        /// <param name="handle">ハンドル</param>
        private static void ReleaseHandle(ref AsyncOperationHandle<Sprite> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
                handle = default;
            }
        }

        private void OnDestroy()
        {
            ReleaseHandle(ref _specialIconHandle);
            ReleaseHandle(ref _normalIconHandle);
        }
    }
}
