using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Shinzui.View.Inventory
{
    public class SpecialItemHudView : MonoBehaviour
    {
        [SerializeField] private Image itemImage;

        private AsyncOperationHandle<Sprite> _iconLoadHandle;

        private void Awake()
        {
            ResolveImageIfNeeded();
            Clear();
        }

        public void Configure(Image image)
        {
            itemImage = image;
            Clear();
        }

        public void SetItem(string iconAddress)
        {
            ResolveImageIfNeeded();

            if (itemImage == null)
            {
                return;
            }

            ReleaseIconHandle();

            if (string.IsNullOrEmpty(iconAddress))
            {
                ClearImage();
                return;
            }

            _iconLoadHandle = Addressables.LoadAssetAsync<Sprite>(iconAddress);
            _iconLoadHandle.Completed += handle =>
            {
                if (itemImage == null)
                {
                    return;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    itemImage.sprite = handle.Result;
                    itemImage.gameObject.SetActive(true);
                }
                else
                {
                    ClearImage();
                }
            };
        }

        public void Clear()
        {
            ReleaseIconHandle();
            ClearImage();
        }

        private void ResolveImageIfNeeded()
        {
            if (itemImage != null)
            {
                return;
            }

            var itemSprite = transform.Find("ItemSprite");
            if (itemSprite != null)
            {
                itemImage = itemSprite.GetComponent<Image>();
            }

            if (itemImage == null)
            {
                itemImage = GetComponent<Image>();
            }
        }

        private void ClearImage()
        {
            if (itemImage == null)
            {
                return;
            }

            itemImage.sprite = null;
            itemImage.gameObject.SetActive(false);
        }

        private void ReleaseIconHandle()
        {
            if (_iconLoadHandle.IsValid())
            {
                Addressables.Release(_iconLoadHandle);
            }
        }

        private void OnDestroy()
        {
            ReleaseIconHandle();
        }
    }
}
