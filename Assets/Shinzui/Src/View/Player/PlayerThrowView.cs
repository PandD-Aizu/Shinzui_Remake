using System;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Shinzui.View
{
    public class PlayerThrowView : MonoBehaviour
    {
        private const string StonePrefabAddress = "StonePrefab";

        private readonly Subject<Unit> _onStoneCollision = new();
        public Observable<Unit> OnStoneCollision => _onStoneCollision;
        [Header("Settings")]
        [SerializeField] private float throwForce = 15f;
        [SerializeField] private Vector3 equipVisualOffset = new Vector3(0.2f, -0.25f, 0.4f);
        
        private Camera _mainCamera;
        private Collider _playerCollider;
        private GameObject _equippedVisual;
        private AsyncOperationHandle<GameObject> _stonePrefabLoadHandle;
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
                    _equippedVisual.name = "EquippedStoneVisual";
                    _equippedVisual.transform.SetParent(_mainCamera.transform, false);
                    _equippedVisual.transform.localPosition = equipVisualOffset;
                    _equippedVisual.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

                    // 手元のビジュアルなのでコライダーは不要
                    var col = _equippedVisual.GetComponent<Collider>();
                    if (col != null)
                    {
                        Destroy(col);
                    }

                    // 色をグレーにする
                    var renderer = _equippedVisual.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.gray;
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

        public void ThrowRock()
        {
            if (_mainCamera == null) return;

            // カメラの少し前方から石を出現させる
            Vector3 spawnPosition = _mainCamera.transform.position + 
                                    _mainCamera.transform.forward * 0.5f + 
                                    _mainCamera.transform.right * 0.2f + 
                                    _mainCamera.transform.up * -0.2f;
            Vector3 throwDirection = _mainCamera.transform.forward;

            if (!_stonePrefabLoadHandle.IsValid())
            {
                _stonePrefabLoadHandle = Addressables.LoadAssetAsync<GameObject>(StonePrefabAddress);
            }

            if (_stonePrefabLoadHandle.IsDone)
            {
                SpawnAndThrowRock(_stonePrefabLoadHandle, spawnPosition, throwDirection);
                return;
            }

            _stonePrefabLoadHandle.Completed += handle =>
            {
                SpawnAndThrowRock(handle, spawnPosition, throwDirection);
            };
        }

        private void SpawnAndThrowRock(AsyncOperationHandle<GameObject> handle, Vector3 spawnPosition, Vector3 throwDirection)
        {
            if (_isDestroyed)
            {
                return;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load addressable stone prefab: {StonePrefabAddress}");
                if (_stonePrefabLoadHandle.IsValid())
                {
                    Addressables.Release(_stonePrefabLoadHandle);
                    _stonePrefabLoadHandle = default;
                }
                return;
            }

            GameObject rock = Instantiate(handle.Result, spawnPosition, Quaternion.LookRotation(throwDirection));
            rock.name = "ThrownStone";

            // Rigidbodyを追加して投げる
            var rb = rock.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = rock.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // プレイヤーのコライダーと衝突無視
            if (_playerCollider != null)
            {
                foreach (var rockCollider in rock.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(rockCollider, _playerCollider);
                }
            }

            // 衝突ハンドラーの追加
            var handler = rock.GetComponent<StoneCollisionHandler>();
            if (handler == null)
            {
                handler = rock.AddComponent<StoneCollisionHandler>();
            }
            handler.OnCollide += () => _onStoneCollision.OnNext(Unit.Default);

            // カメラの正面方向に力をかける
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            // 5秒後に破棄
            handler.DestroyAfter(5f);
        }

        private void OnDestroy()
        {
            _isDestroyed = true;

            if (_equippedVisual != null)
            {
                Destroy(_equippedVisual);
            }
            if (_stonePrefabLoadHandle.IsValid())
            {
                Addressables.Release(_stonePrefabLoadHandle);
            }
            _onStoneCollision.OnCompleted();
        }
    }
}
