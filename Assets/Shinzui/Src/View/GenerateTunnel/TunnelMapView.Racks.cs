using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>小部屋の側壁沿いに通行幅と空きを確保してラックを配置する</summary>
        /// <param name="room">小部屋接続のルート</param>
        /// <param name="width">部屋の幅</param>
        /// <param name="length">部屋の長さ</param>
        /// <param name="passageWidth">中央に確保する通行幅</param>
        /// <param name="seed">配置の乱数シード</param>
        public void CreateSmallRoomRacks(Transform room, float width, float length, float passageWidth, int seed)
        {
            var prefab = _generator != null ? _generator.SmallRoomRackPrefab : Resources.Load<GameObject>("RustedWireRack");
            int count = _generator != null ? _generator.SmallRoomRackCount : 2;
            if (prefab == null || count <= 0) return;

            // プレハブの占有寸法を使い、入口から続く中央の通路を空ける
            var shape = prefab.GetComponent<BoxCollider>();
            if (shape == null) return;
            Vector3 size = shape.size;
            float zRange = length * 0.5f - size.x * 0.5f - 0.2f;
            float x = width * 0.5f - size.z * 0.5f - 0.15f;
            if (zRange < 0f || x - size.z * 0.5f < passageWidth * 0.5f + 0.2f) return;
            var random = new System.Random(unchecked(seed ^ 0x5241434b));

            // 候補を有限回抽選し、壁、天井、既存家具と重なる位置を除外する
            int placed = 0;
            for (int attempt = 0; attempt < count * 16 && placed < count; attempt++)
            {
                int side = random.Next(2) == 0 ? -1 : 1;
                float z = ((float)random.NextDouble() * 2f - 1f) * zRange;
                float floor = SampleGimmickFloor(room, z);
                float wallX = x;
                Vector3 origin = room.TransformPoint(new Vector3(0f, floor + size.y * 0.5f, z));
                if (RaycastGimmickHost(room, origin, room.right * side, width * 0.5f, out var wall))
                    wallX = Mathf.Min(x, Mathf.Abs(room.InverseTransformPoint(wall.point).x) - size.z * 0.5f - 0.15f);
                if (wallX - size.z * 0.5f < passageWidth * 0.5f + 0.2f) continue;

                Quaternion rotation = room.rotation * Quaternion.Euler(0f, -side * 90f, 0f);
                Vector3 position = room.TransformPoint(new Vector3(side * wallX, floor + 0.03f, z));
                Vector3 center = position + rotation * shape.center;
                if (Physics.CheckBox(center, size * 0.5f + new Vector3(0.05f, 0f, 0.05f), rotation,
                    ~0, QueryTriggerInteraction.Ignore)) continue;

                // 衝突付きの家具をNavMesh構築前に生成し、次の候補にも反映する
                var rack = Instantiate(prefab, position, rotation, room);
                rack.name = $"Rusted Wire Rack {placed:00}";
                rack.SetActive(true);
                Physics.SyncTransforms();
                placed++;
            }
        }
    }
}
