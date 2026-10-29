using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>プレイヤー自身を除外した対象への遮蔽判定</summary>
    internal static class InteractionVisibility
    {
        private static RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>壁などに遮られず対象まで見通せるか判定</summary>
        /// <param name="origin">視点の位置</param>
        /// <param name="position">対象の位置</param>
        /// <param name="target">操作対象</param>
        /// <param name="owner">判定から除外するプレイヤー</param>
        /// <returns>対象を視認できるか</returns>
        public static bool IsVisible(Vector3 origin, Vector3 position, InteractableComponent target, Transform owner)
        {
            Vector3 direction = position - origin;
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction.normalized, _hits, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == _hits.Length)
                System.Array.Resize(ref _hits, _hits.Length * 2);

            // 自分の体と対象自身を遮蔽物として扱わない
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i].collider;
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                if (hit.GetComponentInParent<InteractableComponent>() == target) continue;
                return false;
            }
            return true;
        }
    }
}
