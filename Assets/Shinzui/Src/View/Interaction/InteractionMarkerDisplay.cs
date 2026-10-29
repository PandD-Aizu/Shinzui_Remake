using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// 近傍のインタラクト対象に画面上の円を重ねて表示
    /// </summary>
    internal sealed class InteractionMarkerDisplay
    {
        private readonly Canvas _canvas;
        private readonly Transform _owner;
        private readonly Dictionary<InteractableComponent, InteractionRingGraphic> _markers = new();
        private readonly HashSet<InteractableComponent> _visible = new();
        private readonly List<InteractableComponent> _removed = new();
        private readonly Stack<InteractionRingGraphic> _pool = new();
        private Collider[] _colliders = new Collider[64];

        /// <summary>
        /// マーカー専用の非入力Canvasを生成
        /// </summary>
        /// <param name="parent">表示の所有者</param>
        public InteractionMarkerDisplay(Transform parent)
        {
            var player = parent.GetComponentInParent<PlayerView>();
            _owner = player != null ? player.transform : parent;
            var root = new GameObject("InteractionMarkers", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(parent, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>
        /// 視認できる近傍の対象へ円を配置して操作可能時に拡大
        /// </summary>
        /// <param name="camera">プレイヤーカメラ</param>
        /// <param name="current">実際に操作可能な対象</param>
        /// <param name="range">表示距離</param>
        /// <param name="layers">対象の検出レイヤー</param>
        /// <param name="diameter">通常時の直径</param>
        /// <param name="activeDiameter">操作可能時の直径</param>
        public void Refresh(Camera camera, InteractableComponent current, float range,
            LayerMask layers, float diameter, float activeDiameter)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                Hide();
                return;
            }

            // 密集時も検出対象を取りこぼさないようにバッファを拡張
            _canvas.gameObject.SetActive(true);
            _visible.Clear();
            Vector3 origin = camera.transform.position;
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(origin, range, _colliders, layers,
                QueryTriggerInteraction.Collide)) == _colliders.Length)
            {
                System.Array.Resize(ref _colliders, _colliders.Length * 2);
            }

            for (int i = 0; i < count; i++)
            {
                Collider collider = _colliders[i];
                var target = collider.GetComponentInParent<InteractableComponent>();
                if (target == null || !target.isActiveAndEnabled || !target.CanInteract || _visible.Contains(target)) continue;

                // 対象の中心が画面内にあり、壁などで遮られていない場合だけ表示
                Vector3 position = collider.bounds.center;
                Vector3 screen = camera.WorldToScreenPoint(position);
                if (screen.z <= camera.nearClipPlane || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) continue;
                if (!InteractionVisibility.IsVisible(origin, position, target, _owner)) continue;

                _visible.Add(target);
                if (!_markers.TryGetValue(target, out InteractionRingGraphic marker))
                {
                    marker = GetMarker();
                    marker.rectTransform.sizeDelta = Vector2.one * diameter;
                    _markers.Add(target, marker);
                }

                // 解像度に依存しない画面位置と滑らかなサイズ変化を反映
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform,
                    screen, null, out Vector2 localPoint);
                marker.rectTransform.anchoredPosition = localPoint;
                marker.enabled = true;
                float desired = target == current ? activeDiameter : diameter;
                float size = Mathf.Lerp(marker.rectTransform.sizeDelta.x, desired, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                marker.rectTransform.sizeDelta = Vector2.one * size;
            }

            // 範囲外や取得済みのマーカーは再利用用に戻す
            _removed.Clear();
            foreach (var pair in _markers)
            {
                if (_visible.Contains(pair.Key)) continue;
                pair.Value.gameObject.SetActive(false);
                _pool.Push(pair.Value);
                _removed.Add(pair.Key);
            }
            foreach (var target in _removed) _markers.Remove(target);
        }

        /// <summary>指定対象の円を長押しUIへ置き換え、同じ画面位置を返す</summary>
        /// <param name="target">置き換える対象</param>
        /// <param name="screenPosition">円の画面座標</param>
        /// <returns>表示中の円を置き換えたか</returns>
        public bool TryReplaceWithHold(InteractableComponent target, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (target == null || !_canvas.gameObject.activeInHierarchy || !_visible.Contains(target)
                || !_markers.TryGetValue(target, out var marker)) return false;

            // 検出・遮蔽判定と表示位置を元の円と共用し、重複描画だけを止める
            screenPosition = RectTransformUtility.WorldToScreenPoint(null, marker.rectTransform.position);
            marker.enabled = false;
            return true;
        }

        /// <summary>
        /// 円の表示を取得または生成
        /// </summary>
        /// <returns>表示状態の円</returns>
        private InteractionRingGraphic GetMarker()
        {
            if (_pool.Count > 0)
            {
                var reused = _pool.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var root = new GameObject("InteractionRing", typeof(RectTransform), typeof(InteractionRingGraphic));
            root.transform.SetParent(_canvas.transform, false);
            var marker = root.GetComponent<InteractionRingGraphic>();
            marker.raycastTarget = false;
            marker.color = new Color(1f, 1f, 1f, 0.8f);
            return marker;
        }

        /// <summary>
        /// 全マーカーを非表示にする
        /// </summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// 生成したCanvasと円を破棄
        /// </summary>
        public void Dispose()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
        }
    }
}
