using System;
using R3;
using UnityEngine;

namespace Shinzui.View
{
    public class PlayerThrowView : MonoBehaviour
    {
        private readonly Subject<Unit> _onStoneCollision = new();
        public Observable<Unit> OnStoneCollision => _onStoneCollision;
        [Header("Settings")]
        [SerializeField] private float throwForce = 15f;
        [SerializeField] private Vector3 equipVisualOffset = new Vector3(0.2f, -0.25f, 0.4f);
        
        private Camera _mainCamera;
        private Collider _playerCollider;
        private GameObject _equippedVisual;

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

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "ThrownStone";
            rock.transform.position = spawnPosition;
            rock.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            // 色をグレーにする
            var renderer = rock.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.gray;
            }

            // Rigidbodyを追加して投げる
            var rb = rock.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // プレイヤーのコライダーと衝突無視
            var rockCollider = rock.GetComponent<Collider>();
            if (rockCollider != null && _playerCollider != null)
            {
                Physics.IgnoreCollision(rockCollider, _playerCollider);
            }

            // 衝突ハンドラーの追加
            var handler = rock.AddComponent<StoneCollisionHandler>();
            handler.OnCollide += () => _onStoneCollision.OnNext(Unit.Default);

            // カメラの正面方向に力をかける
            rb.AddForce(_mainCamera.transform.forward * throwForce, ForceMode.Impulse);

            // 5秒後に破棄
            Destroy(rock, 5f);
        }

        private void OnDestroy()
        {
            if (_equippedVisual != null)
            {
                Destroy(_equippedVisual);
            }
            _onStoneCollision.OnCompleted();
        }
    }
}
