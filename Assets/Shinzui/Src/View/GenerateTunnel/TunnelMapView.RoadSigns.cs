using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>中央の通行幅と接続口を空けて立った看板と倒れた看板を配置する</summary>
        /// <param name="host">配置先のトンネル</param>
        /// <param name="width">床の幅</param>
        /// <param name="length">区間の長さ</param>
        /// <param name="passageWidth">中央に確保する通行幅</param>
        /// <param name="seed">配置シード</param>
        /// <param name="openingZ">側面接続口の長さ方向座標</param>
        /// <param name="openingWidth">側面接続口の幅</param>
        public void CreateRoadSigns(Transform host, float width, float length, float passageWidth, int seed,
            float[] openingZ = null, float openingWidth = 0f)
        {
            var prefab = Resources.Load<GameObject>("RoadClosedSign");
            if (prefab == null) return;
            var random = new System.Random(unchecked(seed ^ 0x5349474E));
            int target = Mathf.Clamp(Mathf.CeilToInt(length / 18f), 2, 4);
            int placed = 0;
            Physics.SyncTransforms();

            // 回転後の外接寸法で中央と壁際の余白を確認する
            for (int attempt = 0; attempt < target * 30 && placed < target; attempt++)
            {
                var shape = prefab.GetComponent<BoxCollider>();
                float yaw = (float)random.NextDouble() * 360f;
                var localRotation = Quaternion.Euler(0f, yaw, 0f)
                    * Quaternion.Euler(placed % 2 == 0 ? 0f : 90f, 0f, 0f);
                Vector3 half = shape.size * .5f;
                Vector3 right = localRotation * Vector3.right * half.x;
                Vector3 up = localRotation * Vector3.up * half.y;
                Vector3 forward = localRotation * Vector3.forward * half.z;
                var extent = new Vector3(Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
                    Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
                    Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z));
                float inner = Mathf.Max(1.5f, passageWidth) * .5f + extent.x + .03f;
                float outer = width * .5f - extent.x - .04f;
                float range = length * .5f - extent.z - Mathf.Max(1.2f, openingWidth * .5f);
                if (inner > outer || range <= 0f) continue;
                float x = Mathf.Lerp(inner, outer, (float)random.NextDouble()) * (random.Next(2) * 2 - 1);
                float z = ((float)random.NextDouble() * 2f - 1f) * range;
                if (openingZ != null && System.Array.Exists(openingZ,
                    opening => Mathf.Abs(z - opening) < openingWidth * .5f + extent.z + .6f)) continue;

                // 平坦な床だけに置き、家具や壁と重なる候補を除外する
                float floor = SampleGimmickFloor(host, z);
                Vector3 origin = host.TransformPoint(new Vector3(x, floor + .5f, z));
                if (!RaycastGimmickHost(host, origin, -host.up, 1f, out var hit)
                    || Vector3.Dot(hit.normal, host.up) < .99f) continue;
                Quaternion rotation = host.rotation * localRotation;
                Vector3 position = hit.point + host.up * (extent.y + .025f);
                if (Physics.CheckBox(position, half + Vector3.one * .01f, rotation,
                    ~0, QueryTriggerInteraction.Ignore)) continue;

                // 固定の衝突形状をNavMesh構築前に生成する
                var sign = Instantiate(prefab, position, rotation, host);
                sign.name = $"Road Sign {placed:00} {(placed % 2 == 0 ? "Upright" : "Fallen")}";
                Physics.SyncTransforms();
                placed++;
            }
        }
    }
}

