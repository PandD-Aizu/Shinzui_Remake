using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>セーブ地点の金網扉を横にスライドして開閉する</summary>
    public sealed class SlidingSavePointDoor : InteractableComponent
    {
        [SerializeField] private Transform movingPanel;
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, -2.4f);
        [SerializeField, Min(0.1f)] private float slideSpeed = 3f;
        [SerializeField] private Vector3 doorwaySize = new Vector3(0.6f, 4.2f, 2.3f);
        private Vector3 _closedPosition;
        private bool _open;

        public bool IsOpen => _open;
        public override string InteractableId => "save_point_sliding_door";
        public override string DisplayName => _open ? "[E] 引き戸を閉める" : "[E] 引き戸を開ける";
        public override string InteractMessage => "金網の引き戸だ";

        /// <summary>閉じた位置を記録する</summary>
        private void Awake()
        {
            _closedPosition = movingPanel.localPosition;
        }

        /// <summary>通路内のプレイヤーを挟まずに開閉方向を切り替える</summary>
        public override void ExecuteInteractEffect()
        {
            if (_open && IsDoorwayOccupied()) return;
            _open = !_open;
        }

        /// <summary>開閉位置へ移動し、閉鎖中にプレイヤーが入ったら再び開く</summary>
        private void Update()
        {
            // 閉鎖中の接触を防ぐ
            if (!_open && movingPanel.localPosition != _closedPosition && IsDoorwayOccupied())
                _open = true;

            var destination = _closedPosition + (_open ? openOffset : Vector3.zero);
            movingPanel.localPosition = Vector3.MoveTowards(movingPanel.localPosition, destination, slideSpeed * Time.deltaTime);
        }

        /// <summary>開口部にプレイヤーがいるか調べる</summary>
        /// <returns>プレイヤーのコライダーが開口部に重なっているか</returns>
        private bool IsDoorwayOccupied()
        {
            var halfSize = Vector3.Scale(doorwaySize * 0.5f, transform.lossyScale);
            foreach (var collider in Physics.OverlapBox(transform.position, halfSize, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider.GetComponentInParent<PlayerView>() != null) return true;
            }
            return false;
        }
    }
}
