using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace Shinzui.Temp
{
    /// <summary>
    /// アイテム入手時に表示されるUI
    /// 画面の左上に表示する
    ///
    /// テスト用：Spaceキーで仮のアイテムUIと名前を表示する
    /// </summary>
    public class TempItemGetUI : MonoBehaviour
    {
        [SerializeField, Tooltip("表示上限")] private int maxItem = 5;
        [SerializeField, Tooltip("UI１つの表示時間")] private float displayTime = 3.0f;

        [SerializeField, Tooltip("表示するUIの親")] private Transform parentTransform;
        [SerializeField, Tooltip("UIのプレハブ")] private GameObject prefab;

        private ObjectPool<GameObject> _uiPool;
        private List<GameObject> _activeUIList = new ();

        private void Start()
        {
            _uiPool = new ObjectPool<GameObject>(
                createFunc: OnCreateUI,
                actionOnGet: OnGetUI,
                actionOnRelease: OnReleaseUI,
                actionOnDestroy: OnDestroyUI,
                collectionCheck: true,
                defaultCapacity: maxItem / 2,
                maxSize: maxItem
            );
        }

        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ShowItemGetUI();    
            }
        }

        /// <summary>
        /// アイテムを表示する
        /// </summary>
        private async void ShowItemGetUI()
        {
            if (_activeUIList.Count >= maxItem)
            {
                GameObject oldestUI = _activeUIList[0];
                _uiPool.Release(oldestUI);
            }

            // プールからUIを取得
            GameObject uiInstance = _uiPool.Get();
            
            // 取得したUIを最下部に配置
            uiInstance.transform.SetAsLastSibling();
            
            // TODO: 左から湧き出るようにアニメーションさせながら表示(LitMotionを使う)
            // TODO: 表示時にパーティクルを表示させる
            
            // 一定時間後に非表示にする非同期メソッドを開始する
            ReleaseUIAfterDelay(uiInstance, displayTime).Forget();
        }
        
        #region プールのコールバック

        private GameObject OnCreateUI()
        {
            return Instantiate(prefab, parentTransform);
        }

        private void OnGetUI(GameObject ui)
        {
            ui.SetActive(true);
            _activeUIList.Add(ui);
        }

        private void OnReleaseUI(GameObject ui)
        {
            ui.SetActive(false);
            _activeUIList.Remove(ui);
        }

        private void OnDestroyUI(GameObject ui)
        {
            Destroy(ui);
        } 
        
        #endregion

        /// <summary>
        /// 指定時間後にUIをプールに返却する非同期メソッド
        /// </summary>
        /// <param name="ui"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private async UniTask ReleaseUIAfterDelay(GameObject ui, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            // TODO: フェードアウトで非表示にする
            
            if (ui != null && ui.activeSelf)
                _uiPool.Release(ui);
        }
    }
}