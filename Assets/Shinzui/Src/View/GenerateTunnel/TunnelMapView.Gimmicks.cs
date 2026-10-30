using Shinzui.View.Interaction;
using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        public float FireAlarmHeight => _generator != null ? _generator.FireAlarmHeight : 1.4f;

        /// <summary>側壁の手前に実モデルのロッカーを配置</summary>
        /// <param name="host">配置先</param>
        /// <param name="localZ">長さ方向</param>
        /// <param name="side">左右</param>
        /// <param name="width">壁の探索距離</param>
        /// <returns>生成したロッカー</returns>
        public HidingLockerView CreateHidingLocker(Transform host, float localZ, int side, float width)
        {
            var prefab = _generator != null ? _generator.HidingLockerPrefab : Resources.Load<GameObject>("HidingLocker");
            if (prefab == null) return null;
            float floor = SampleGimmickFloor(host, localZ);
            Vector3 origin = host.TransformPoint(new Vector3(0f, floor + 1f, localZ));
            if (!RaycastGimmickHost(host, origin, host.right * side, width, out var hit)) return null;
            Vector3 position = hit.point + hit.normal * .4f - host.up;
            var instance = Instantiate(prefab, position, Quaternion.LookRotation(hit.normal, host.up), host);
            instance.SetActive(true);
            return instance.GetComponent<HidingLockerView>();
        }

        /// <summary>通路の断面に寸法を合わせた蜘蛛の巣を生成する</summary>
        /// <param name="corridor">配置先の通路</param>
        /// <param name="localZ">通路内の長さ方向の位置</param>
        /// <param name="width">巣の幅</param>
        /// <param name="height">巣の高さ</param>
        /// <param name="seed">外観のシード</param>
        /// <returns>生成した蜘蛛の巣</returns>
        public SpiderWeb CreateSpiderWeb(Transform corridor, float localZ, float width, float height, int seed)
        {
            // 非アクティブな親の下で複製し、設定前のOnEnableを防ぐ
            var holder = new GameObject("SpiderWeb Placement");
            holder.SetActive(false);
            holder.transform.SetParent(corridor, false);
            GameObject template = _generator != null ? _generator.SpiderWebTemplate : null;
            GameObject instance = template != null ? Instantiate(template, holder.transform, false) : new GameObject("SpiderWeb");
            instance.SetActive(false);
            instance.transform.SetParent(holder.transform, false);
            var web = instance.GetComponent<SpiderWeb>();
            if (web == null) web = instance.AddComponent<SpiderWeb>();

            // 床と天井の内面を測り、生成する巣を断面内へ収める
            float floorY = SampleGimmickFloor(corridor, localZ);
            var origin = corridor.TransformPoint(new Vector3(0, floorY + 0.05f, localZ));
            if (RaycastGimmickHost(corridor, origin, corridor.up, height + 1, out var ceiling))
                height = Mathf.Min(height, corridor.InverseTransformPoint(ceiling.point).y - floorY - 0.1f);
            instance.transform.localPosition = new Vector3(0, floorY + height * 0.5f, localZ);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            web.ConfigureShape(width, height, seed);
            instance.SetActive(true);
            holder.SetActive(true);
            return web;
        }

        /// <summary>配置先の側壁へ報知器の正面を通路側に向けて取り付ける</summary>
        /// <param name="tunnel">配置先のトンネル</param>
        /// <param name="localZ">トンネル内の長さ方向の位置</param>
        /// <param name="side">側壁の符号</param>
        /// <param name="height">床からの高さ</param>
        /// <param name="tunnelWidth">壁を探す最大距離</param>
        /// <returns>生成した報知器、未設定または壁がない場合はnull</returns>
        public FireAlarm CreateFireAlarm(Transform tunnel, float localZ, int side, float height, float tunnelWidth)
        {
            GameObject prefab = _generator != null ? _generator.FireAlarmPrefab : null;
            if (prefab == null) return null;

            // 他トンネルやトリガーへの誤配置を避け、ホストの壁だけを探す
            float floorY = SampleGimmickFloor(tunnel, localZ);
            Vector3 origin = tunnel.TransformPoint(new Vector3(0, floorY + height, localZ));
            if (!RaycastGimmickHost(tunnel, origin, tunnel.right * side, tunnelWidth, out var hit))
            {
                Debug.LogWarning($"[TunnelMapView] FireAlarm wall not found: {tunnel.name}, z={localZ}");
                return null;
            }

            // Prefabの印字面と押しボタンがある+Z側を壁の法線へ向ける
            var instance = Instantiate(prefab, tunnel);
            instance.transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f,
                Quaternion.LookRotation(hit.normal, tunnel.up));
            instance.SetActive(true);
            return instance.GetComponent<FireAlarm>();
        }

        /// <summary>配置先の床上面をローカル座標で取得する</summary>
        /// <param name="host">配置先</param>
        /// <param name="localZ">長さ方向の位置</param>
        /// <returns>床上面のローカルY</returns>
        private static float SampleGimmickFloor(Transform host, float localZ)
        {
            Vector3 origin = host.TransformPoint(new Vector3(0, 1, localZ));
            return RaycastGimmickHost(host, origin, -host.up, 3, out var hit)
                ? host.InverseTransformPoint(hit.point).y : ShellThickness * 0.75f;
        }

        /// <summary>配置先の非トリガー形状への最短の交点を取得する</summary>
        /// <param name="host">配置先</param>
        /// <param name="origin">ワールド座標の始点</param>
        /// <param name="direction">探索方向</param>
        /// <param name="distance">最大距離</param>
        /// <param name="nearest">最短の交点</param>
        /// <returns>配置先の形状に当たったか</returns>
        private static bool RaycastGimmickHost(Transform host, Vector3 origin, Vector3 direction,
            float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(host) || (found && hit.distance >= nearest.distance)) continue;
                nearest = hit;
                found = true;
            }
            return found;
        }
    }
}
