using Shinzui.Application.Interfaces.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>床と遮蔽物を確認して赤いペンライトを設置する</summary>
    public sealed class PenlightPlacementService : IPenlightPlacementService
    {
        private readonly Transform _player;
        private readonly GameObject _prefab;

        /// <summary>設置元と点灯プレハブを保持する</summary>
        /// <param name="player">プレイヤーのルート</param>
        /// <param name="prefab">点灯済みの設置用プレハブ</param>
        public PenlightPlacementService(Transform player, GameObject prefab)
        {
            _player = player;
            _prefab = prefab;
        }

        /// <summary>前方の床へ設置し、設置不能なら生成しない</summary>
        /// <returns>設置が成功したか</returns>
        public bool TryPlace()
        {
            if (!TryGetPlacement(out Pose pose)) return false;

            // 細長い本体を床面に寝かせ、プレイヤーと同じシーンに所属させる
            GameObject placed = Object.Instantiate(_prefab, pose.position, pose.rotation);
            SceneManager.MoveGameObjectToScene(placed, _player.gameObject.scene);
            return true;
        }

        /// <summary>アニメーション開始前に床面の設置位置を取得する</summary>
        /// <param name="pose">床面に沿った設置姿勢</param>
        /// <returns>設置可能な床があるか</returns>
        public bool TryGetPlacement(out Pose pose)
        {
            pose = default;
            if (_player == null || _prefab == null) return false;

            // 壁越しの設置を防ぎ、足元から届く床だけを対象にする
            Vector3 forward = Vector3.ProjectOnPlane(_player.forward, Vector3.up).normalized;
            Vector3 origin = _player.position + Vector3.up * .5f;
            var controller = _player.GetComponent<CharacterController>();
            if (controller != null) origin.y = controller.bounds.min.y + .5f;
            if (Physics.Raycast(origin, forward, .7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (!Physics.Raycast(origin + forward * .7f, Vector3.down, out RaycastHit hit,
                    1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.normal.y < .8f || hit.transform.IsChildOf(_player)) return false;

            Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, hit.normal), hit.normal);
            pose = new Pose(hit.point + hit.normal * .003f, rotation);
            return true;
        }
    }
}
