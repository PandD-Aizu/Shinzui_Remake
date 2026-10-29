using System;
using System.Collections;
using UnityEngine;

namespace Shinzui.View
{
    public class StoneCollisionHandler : MonoBehaviour
    {
        public event Action OnCollide;
        /// <summary>動的に生成される石の初回着弾を通知</summary>
        public static event Action<StoneCollisionHandler, Vector3, Vector3> Impacted;
        private bool _hasCollided = false;

        /// <summary>初回の着弾位置と飛来方向を通知して石を破棄</summary>
        /// <param name="collision">衝突情報</param>
        private void OnCollisionEnter(Collision collision)
        {
            // 多重衝突による複数回発火を防止
            if (_hasCollided) return;
            _hasCollided = true;

            Vector3 position = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Impacted?.Invoke(this, position, collision.relativeVelocity.normalized);
            OnCollide?.Invoke();

            // 衝突した瞬間に石オブジェクトを破棄
            DestroyStone();
        }

        public void DestroyAfter(float delay)
        {
            StartCoroutine(DestroyAfterDelay(delay));
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            DestroyStone();
        }

        private void DestroyStone()
        {
            EditorSelectionGuard.ClearIfSelected(gameObject);
            Destroy(gameObject);
        }
    }
}
