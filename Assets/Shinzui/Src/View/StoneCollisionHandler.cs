using System;
using UnityEngine;

namespace Shinzui.View
{
    public class StoneCollisionHandler : MonoBehaviour
    {
        public event Action OnCollide;
        private bool _hasCollided = false;

        private void OnCollisionEnter(Collision collision)
        {
            // 多重衝突による複数回発火を防止
            if (_hasCollided) return;
            _hasCollided = true;

            OnCollide?.Invoke();

            // 衝突した瞬間に石オブジェクトを破棄
            Destroy(gameObject);
        }
    }
}
