using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>側方の接続通路を含めず、本体の床幅から車の配置幅を求める</summary>
        /// <param name="layoutWidth">レイアウト上のトンネル幅</param>
        /// <returns>本体の床幅、モデル未設定時はレイアウトの幅</returns>
        public float ResolveCarPlacementWidth(float layoutWidth)
        {
            var floor = _tunnelPrefab != null ? _tunnelPrefab.transform.Find("tunnel_floor") : null;
            if (floor == null || !TryGetMeshBoundsInLocalSpace(floor, _tunnelPrefab.transform, out var bounds))
                return layoutWidth;

            // モデルの断面倍率を反映し、全体の占有幅以内に収める
            Vector3 size = Vector3.Scale(bounds.size, Abs(GetTunnelModelScale()));
            return Mathf.Min(layoutWidth, _tunnelTemplateLongAxisIsX ? size.z : size.x);
        }

        /// <summary>大きいトンネルの床へ衝突付きの車を配置する</summary>
        /// <param name="host">配置先のトンネル</param>
        /// <param name="localX">幅方向の位置</param>
        /// <param name="localZ">長さ方向の位置</param>
        /// <param name="yaw">水平回転角</param>
        /// <param name="seed">モデル選択のシード</param>
        /// <returns>生成した車、床や空きがない場合はnull</returns>
        public GameObject CreateCar(Transform host, float localX, float localZ, float yaw, int seed)
        {
            var prefab = Resources.Load<GameObject>((seed & 1) == 0 ? "Cars/KeiCar" : "Cars/KeiHatchback");
            if (prefab == null) return null;
            var shape = prefab.GetComponent<BoxCollider>();
            if (shape == null) return null;

            // 中央の床高を基準に、車の真下にホストの床があることを確認する
            Physics.SyncTransforms();
            float floor = SampleGimmickFloor(host, localZ);
            Vector3 origin = host.TransformPoint(new Vector3(localX, floor + .5f, localZ));
            if (!RaycastGimmickHost(host, origin, -host.up, 1f, out var hit)
                || Vector3.Dot(hit.normal, host.up) < .95f) return null;

            // 壁、天井、既存ギミックに接触する候補は生成しない
            Quaternion rotation = host.rotation * Quaternion.Euler(0f, yaw, 0f);
            Vector3 position = hit.point + host.up * .03f;
            Vector3 center = position + rotation * shape.center;
            if (Physics.CheckBox(center, shape.size * .5f + new Vector3(.05f, 0f, .05f),
                rotation, ~0, QueryTriggerInteraction.Ignore)) return null;

            // NavMesh構築前に静的な障害物として配置する
            var car = Instantiate(prefab, position, rotation, host);
            car.name = "Tunnel Car " + prefab.name;
            car.SetActive(true);
            Physics.SyncTransforms();
            return car;
        }
    }
}
