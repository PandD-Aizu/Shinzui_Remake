using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>
        /// ループを止めたトンネルの片端にゴール、反対端に壁を作る
        /// </summary>
        /// <param name="root">ゴールを配置する平坦なトンネル</param>
        /// <param name="length">トンネルの長さ</param>
        /// <param name="width">モデルを測定できない場合の幅</param>
        /// <param name="height">モデルを測定できない場合の高さ</param>
        /// <param name="endSign">ゴール側の端、前端は1、後端は-1</param>
        public void CreateTunnelGoal(Transform root, float length, float width, float height, int endSign)
        {
            // 両端のゲートを停止し、描画とループ判定を一緒に無効化する
            foreach (TunnelGateView gate in root.GetComponentsInChildren<TunnelGateView>(true))
                gate.gameObject.SetActive(false);

            Transform body = root.Find("TunnelBaseModel/tunnel_6_exit");
            Transform floor = root.Find("TunnelBaseModel/tunnel_floor");
            float floorY = ShellThickness * 0.75f;
            float centerX = 0;
            if (floor != null && TryGetMeshBoundsInLocalSpace(floor, root, out Bounds floorBounds))
                floorY = floorBounds.max.y;
            if (body != null && TryGetMeshBoundsInLocalSpace(body, root, out Bounds bodyBounds))
            {
                width = bodyBounds.size.x;
                height = bodyBounds.max.y - floorY;
                centerX = bodyBounds.center.x;
            }

            // 端全体を覆い、側面の通路や落下口はそのまま維持する
            const float thickness = 0.5f;
            float endZ = (length - thickness) * 0.5f;
            Vector3 size = new Vector3(width + ShellThickness * 2, height + ShellThickness, thickness);
            Vector3 center = new Vector3(centerX, floorY + size.y * 0.5f, endSign * endZ);
            GameObject block = CreateStageBox(root, "Goal Block", center, size, _goalMaterial);
            CreateStageBox(root, "Goal Tunnel End Wall", new Vector3(centerX, center.y, -endSign * endZ), size, tunnelWall != null ? tunnelWall : _tunnelMaterial);

            // ブロックの手前へ判定を張り、壁に接触するプレイヤーを検知する
            var trigger = new GameObject("Goal Trigger");
            trigger.transform.SetParent(block.transform, false);
            trigger.layer = block.layer;
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1, 1, 2.5f);
            trigger.AddComponent<GeneratedTunnelGoal>();
        }
    }
}
