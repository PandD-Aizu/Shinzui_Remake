using System;
using System.Collections;
using UnityEngine;

namespace Shinzui.View
{
    public class MatchStickCollisionHandler : MonoBehaviour
    {
        public event Action OnCollide;
        private bool _hasCollided = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.name != "Spider Web") return;
            
            // 多重衝突による複数回発火を防止
            if (_hasCollided) return;
            _hasCollided = true;

            OnCollide?.Invoke();

            // 衝突した2秒後にマッチ棒オブジェクトを破棄
            DestroyAfter(2.0f);
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