using System;
using System.Collections;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Shinzui.View
{
    public class PlayerBurnView : MonoBehaviour
    {
        private const string MatchStickPrefabAddress = "MatchStickPrefab";

        private readonly Subject<Unit> _onMatchStickCollision = new();
        public Observable<Unit> OnMatchStickCollision => _onMatchStickCollision;
        [Header("Settings")]
        [SerializeField] private Vector3 equipVisualOffset = new Vector3(0.2f, -0.25f, 0.4f);
        [SerializeField] private float moveSpeed = 0.5f;
        [SerializeField] private float moveSize = 10.0f;
        
        private Camera _mainCamera;
        private Collider _playerCollider;
        private GameObject _equippedVisual;
        private AsyncOperationHandle<GameObject> _matchStickPrefabLoadHandle;
        private bool _isDestroyed;

        private void Start()
        {
            // PlayerView経由などでカメラとコライダーを取得
            var playerView = GetComponent<PlayerView>();
            if (playerView != null)
            {
                _mainCamera = playerView.GetComponentInChildren<Camera>();
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                }
                _playerCollider = playerView.PlayerCollider;
            }
            else
            {
                _mainCamera = Camera.main;
                _playerCollider = GetComponent<Collider>();
            }
        }

        public void SetEquippedVisualActive(bool active)
        {
            if (active)
            {
                if (_equippedVisual == null && _mainCamera != null)
                {
                    _equippedVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _equippedVisual.name = "EquippedMatchSticlVisual";
                    _equippedVisual.transform.SetParent(_mainCamera.transform, false);
                    _equippedVisual.transform.localPosition = equipVisualOffset;
                    _equippedVisual.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

                    // 手元のビジュアルなのでコライダーは不要
                    var col = _equippedVisual.GetComponent<Collider>();
                    if (col != null)
                    {
                        Destroy(col);
                    }
                }
                if (_equippedVisual != null)
                {
                    _equippedVisual.SetActive(true);
                }
            }
            else
            {
                if (_equippedVisual != null)
                {
                    _equippedVisual.SetActive(false);
                }
            }
        }

        public void BurnSpiderweb()
        {
            if (_mainCamera == null) return;

            // カメラの少し前方からマッチ棒を出現させる
            Vector3 spawnPosition = _mainCamera.transform.position + 
                                    _mainCamera.transform.forward * 0.5f + 
                                    _mainCamera.transform.right * 0.2f + 
                                    _mainCamera.transform.up * -0.2f;
            Vector3 moveDirection = _mainCamera.transform.forward * moveSize;

            if (!_matchStickPrefabLoadHandle.IsValid())
            {
                _matchStickPrefabLoadHandle = Addressables.LoadAssetAsync<GameObject>(MatchStickPrefabAddress);
            }

            if (_matchStickPrefabLoadHandle.IsDone)
            {
                SpawnAndMoveMatchStick(_matchStickPrefabLoadHandle, spawnPosition, moveDirection);
                return;
            }

            _matchStickPrefabLoadHandle.Completed += handle =>
            {
                SpawnAndMoveMatchStick(handle, spawnPosition, moveDirection);
            };
        }

        private void SpawnAndMoveMatchStick(AsyncOperationHandle<GameObject> handle, Vector3 spawnPosition, Vector3 moveDirection)
        {
            if (_isDestroyed)
            {
                return;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load addressable stone prefab: {MatchStickPrefabAddress}");
                if (_matchStickPrefabLoadHandle.IsValid())
                {
                    Addressables.Release(_matchStickPrefabLoadHandle);
                    _matchStickPrefabLoadHandle = default;
                }
                return;
            }

            GameObject matchStick = Instantiate(handle.Result, spawnPosition, Quaternion.LookRotation(moveDirection));
            matchStick.name = "MatchStick";
            
            // Rigidbodyを追加して動かす
            var rb = matchStick.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = matchStick.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.mass = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // プレイヤーのコライダーと衝突無視
            if (_playerCollider != null)
            {
                foreach (var rockCollider in matchStick.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(rockCollider, _playerCollider);
                }
            }
            
            // 衝突ハンドラーの追加
            var handler = matchStick.GetComponent<MatchStickCollisionHandler>();
            if (handler == null)
            {
                handler = matchStick.AddComponent<MatchStickCollisionHandler>();
            }
            handler.OnCollide += () => _onMatchStickCollision.OnNext(Unit.Default);

            StartCoroutine(MoveMatchStick(matchStick, moveDirection));

            // 5秒後に破棄
            handler.DestroyAfter(5f);
        }

        //前方にマッチ棒を突き出す
        private IEnumerator MoveMatchStick(GameObject matchStick, Vector3 moveDirection)
        {
            Vector3 startPos = matchStick.transform.position;
            float t = 0f;
            while (t < 1.0f)
            {
                matchStick.transform.localPosition = Vector3.Lerp(startPos, moveDirection, t);
                t += moveSpeed;
            }

            yield return null;
        }

        private void OnDestroy()
        {
            _isDestroyed = true;

            if (_equippedVisual != null)
            {
                Destroy(_equippedVisual);
            }
            if (_matchStickPrefabLoadHandle.IsValid())
            {
                Addressables.Release(_matchStickPrefabLoadHandle);
            }
            _onMatchStickCollision.OnCompleted();
        }
    }
}
