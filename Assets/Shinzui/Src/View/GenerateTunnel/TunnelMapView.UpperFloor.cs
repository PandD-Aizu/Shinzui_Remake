using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        private readonly List<Mesh> _generatedMeshes = new();

        /// <summary>
        /// 傾斜と開口部のために複製したメッシュを破棄する
        /// </summary>
        private void ReleaseGeneratedMeshes()
        {
            foreach (Mesh mesh in _generatedMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }
            _generatedMeshes.Clear();
        }

        /// <summary>
        /// 既存トンネルの水平配置を保ち床と天井を接続先の階層へ傾斜させる
        /// </summary>
        /// <param name="root">トンネルの配置先</param>
        /// <param name="width">トンネルの幅</param>
        /// <param name="height">トンネルの高さ</param>
        /// <param name="length">傾斜に沿った長さ</param>
        /// <param name="slope">水平距離あたりの高さ</param>
        /// <param name="entranceZ">下階の接続口の位置</param>
        /// <param name="corridorWidth">接続口の幅</param>
        /// <param name="openEntrances">下階と接続する側面口</param>
        /// <param name="upperLanding">接続先の階層の実モデル</param>
        /// <param name="upperEntranceIndex">接続先の側面口番号</param>
        /// <param name="slopeExitSign">接続先の階層へ向かう端の符号</param>
        private void CreateInclinedTunnel(Transform root, float width, float height, float length, float slope,
            float entranceZ, float corridorWidth, IReadOnlyList<bool> openEntrances, Transform upperLanding, int upperEntranceIndex,
            int slopeExitSign)
        {
            float horizontalLength = length / Mathf.Sqrt(1.0f + slope * slope);
            float sign = slopeExitSign != 0 ? slopeExitSign : Mathf.Sign(slope);

            // 接続先の床の上面と開口幅を実モデルの面から測定する
            ResolveUpperMouth(upperLanding, upperEntranceIndex, corridorWidth,
                out float upperMouthFloor, out float upperMouthWidth, out float upperMouthHeight);
            float lowerFloor = ShellThickness * 0.75f;
            float rampStartZ = entranceZ + sign * (corridorWidth * 0.5f + 0.4f);
            float run = horizontalLength * 0.5f - sign * rampStartZ;
            float upperFloorY = root.localPosition.y + slope * (sign * horizontalLength * 0.5f);
            float renderedSlope = sign * (upperFloorY + upperMouthFloor - lowerFloor) / run;
            float floorOffset = lowerFloor - root.localPosition.y;

            CreateSlopeBox(root, "Inclined Floor", new Vector3(0, floorOffset - ShellThickness * 0.5f, 0),
                new Vector3(width, ShellThickness, horizontalLength), rampStartZ);
            CreateSlopeBox(root, "Inclined Ceiling", new Vector3(0, floorOffset + height, 0),
                new Vector3(width, ShellThickness, horizontalLength), rampStartZ);

            // 接続済みの側面口だけを残し、他の側面は連続した壁にする
            for (int side = 0; side < 2; side++)
            {
                float x = (side == 0 ? -1 : 1) * (width + ShellThickness) * 0.5f;
                bool open = false;
                for (int i = side * 3; i < side * 3 + 3; i++)
                    open |= openEntrances != null && i < openEntrances.Count && openEntrances[i];
                if (!open)
                {
                    CreateSlopeBox(root, "Inclined Side Wall", new Vector3(x, floorOffset + height * 0.5f, 0),
                        new Vector3(ShellThickness, height, horizontalLength), rampStartZ);
                    continue;
                }

                float before = entranceZ - corridorWidth * 0.5f + horizontalLength * 0.5f;
                float after = horizontalLength * 0.5f - entranceZ - corridorWidth * 0.5f;
                CreateSlopeBox(root, "Side Wall Before Entrance", new Vector3(x, floorOffset + height * 0.5f,
                    -horizontalLength * 0.5f + before * 0.5f), new Vector3(ShellThickness, height, before), rampStartZ);
                CreateSlopeBox(root, "Side Wall After Entrance", new Vector3(x, floorOffset + height * 0.5f,
                    horizontalLength * 0.5f - after * 0.5f), new Vector3(ShellThickness, height, after), rampStartZ);
                float passageTop = ResolvePassageHeight(height) + ShellThickness * 0.5f - lowerFloor;
                if (height > passageTop)
                    CreateSlopeBox(root, "Side Entrance Header", new Vector3(x, floorOffset + (height + passageTop) * 0.5f, entranceZ),
                        new Vector3(ShellThickness, height - passageTop, corridorWidth), rampStartZ);
            }

            // 出口は接続先の側面口に合わせた幅だけを開ける
            float openingWidth = Mathf.Min(width - ShellThickness, upperMouthWidth);
            float capWidth = (width - openingWidth) * 0.5f;
            float highZ = sign * horizontalLength * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                CreateSlopeBox(root, "Upper Landing Side Wall", new Vector3(side * (openingWidth + capWidth) * 0.5f,
                    floorOffset + height * 0.5f, highZ), new Vector3(capWidth, height, ShellThickness), rampStartZ);
            float headerHeight = Mathf.Max(0.0f, height - upperMouthHeight);
            if (headerHeight > 0.0f)
                CreateSlopeBox(root, "Upper Landing Header", new Vector3(0, floorOffset + upperMouthHeight + headerHeight * 0.5f, highZ),
                    new Vector3(openingWidth, headerHeight, ShellThickness), rampStartZ);
            CreateSlopeBox(root, "Lower End Wall", new Vector3(0, floorOffset + height * 0.5f, -highZ),
                new Vector3(width, height, ShellThickness), rampStartZ);

            // BoxColliderを傾斜した実メッシュの当たり判定へ置き換える
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.name = "Generated Inclined " + filter.name;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                    p.y += renderedSlope * sign * Mathf.Max(0.0f, sign * (p.z - rampStartZ));
                    vertices[i] = filter.transform.InverseTransformPoint(root.TransformPoint(p));
                }
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                filter.sharedMesh = mesh;
                _generatedMeshes.Add(mesh);
                var box = filter.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.enabled = false;
                    Destroy(box);
                }
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            ApplyTunnelPassageMaterial(root.gameObject);
            CreateCeilingLights(root, horizontalLength, height, renderedSlope, floorOffset, rampStartZ, (int)sign, mainTunnel: true);
        }

        /// <summary>
        /// 平坦な踊り場と傾斜が接する位置で直方体を分割する
        /// </summary>
        /// <param name="root">配置先</param>
        /// <param name="name">部品名</param>
        /// <param name="center">変形前の中心</param>
        /// <param name="size">変形前の寸法</param>
        /// <param name="breakZ">傾斜の開始位置</param>
        private void CreateSlopeBox(Transform root, string name, Vector3 center, Vector3 size, float breakZ)
        {
            float min = center.z - size.z * 0.5f;
            float max = center.z + size.z * 0.5f;
            if (breakZ <= min || breakZ >= max)
            {
                CreateStageBox(root, name, center, size, _tunnelMaterial);
                return;
            }

            CreateStageBox(root, name + " Landing", new Vector3(center.x, center.y, (min + breakZ) * 0.5f),
                new Vector3(size.x, size.y, breakZ - min), _tunnelMaterial);
            CreateStageBox(root, name + " Slope", new Vector3(center.x, center.y, (breakZ + max) * 0.5f),
                new Vector3(size.x, size.y, max - breakZ), _tunnelMaterial);
        }

        /// <summary>
        /// 接続先モデルの入口内部から床の上面と入口寸法を測定する
        /// </summary>
        /// <param name="landing">上階トンネル</param>
        /// <param name="entranceIndex">側面口の番号</param>
        /// <param name="fallbackWidth">モデルがない場合の幅</param>
        /// <param name="floor">床の上面の高さ</param>
        /// <param name="width">実際の入口幅</param>
        /// <param name="height">入口の高さ</param>
        private void ResolveUpperMouth(Transform landing, int entranceIndex, float fallbackWidth,
            out float floor, out float width, out float height)
        {
            floor = ShellThickness * 0.75f;
            width = fallbackWidth;
            height = 3.0f;
            Transform mouth = landing != null ? landing.Find("TunnelBaseModel/" + ExitPathNames[entranceIndex]) : null;
            if (mouth == null || !TryGetMeshBoundsInLocalSpace(mouth, landing, out Bounds bounds)) return;

            Physics.SyncTransforms();
            var ray = new Ray(landing.TransformPoint(bounds.center), -landing.up);
            float nearest = float.PositiveInfinity;
            foreach (Collider collider in mouth.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (!collider.Raycast(ray, out var hit, bounds.size.y * landing.lossyScale.y)
                    || Vector3.Dot(hit.normal, landing.up) < 0.5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                floor = landing.InverseTransformPoint(hit.point).y;
            }
            width = TryGetMouthDeck(mouth, landing, out Bounds deck) ? deck.size.z : fallbackWidth;
            height = Mathf.Max(2.0f, bounds.max.y - floor - ShellThickness * 2.0f);
        }

        /// <summary>
        /// 上階の床と下階の天井に同じ落下口を開ける
        /// </summary>
        /// <param name="lower">着地先のトンネル</param>
        /// <param name="upper">落下元のトンネル</param>
        /// <param name="center">マップ内の水平中心</param>
        /// <param name="size">開口部の幅</param>
        /// <param name="lowerFloorY">着地先の床高</param>
        /// <param name="upperFloorY">落下元の床高</param>
        /// <param name="tunnelHeight">下階のトンネル高</param>
        public void CreateDropOpening(Transform lower, Transform upper, Vector3 center, float size,
            float lowerFloorY, float upperFloorY, float tunnelHeight)
        {
            // 切断前の天井を測定し、囲いの下端をアーチ形状に揃える
            float lowerSurface = SampleTunnelFloor(lower, center, lowerFloorY);
            float upperSurface = SampleTunnelFloor(upper, center, upperFloorY);
            var shaft = new GameObject("Drop Opening Shaft").transform;
            shaft.SetParent(_geometryRoot, false);
            shaft.localPosition = new Vector3(center.x, 0, center.z);
            float edge = (size + ShellThickness) * 0.5f;
            foreach (int sign in new[] { -1, 1 })
            {
                var walls = new[]
                {
                    (name: "Shaft X Wall", start: new Vector3(sign * edge, 0, -(size + ShellThickness * 2) * 0.5f),
                        end: new Vector3(sign * edge, 0, (size + ShellThickness * 2) * 0.5f)),
                    (name: "Shaft Z Wall", start: new Vector3(-size * 0.5f, 0, sign * edge),
                        end: new Vector3(size * 0.5f, 0, sign * edge))
                };
                foreach (var wall in walls)
                    CreateProfiledWall(shaft, wall.name, wall.start, wall.end, ShellThickness, point =>
                    {
                        Vector3 mapPoint = point + shaft.localPosition;
                        float ceiling = SampleTunnelCeiling(lower, mapPoint, lowerSurface, lowerFloorY + tunnelHeight);
                        return (ceiling - 0.01f, upperSurface - 0.02f);
                    });
            }
            ApplyTunnelPassageMaterial(shaft.gameObject);

            // 下階の床と上階の天井は残し、間の床や天井だけを切り取る
            float bottom = lowerSurface + 2.4f;
            float top = Mathf.Max(upperSurface + 0.25f, upperFloorY + 2.0f);
            Vector3 worldCenter = _geometryRoot.TransformPoint(new Vector3(center.x, (bottom + top) * 0.5f, center.z));
            var bounds = new Bounds(worldCenter, Vector3.Scale(new Vector3(size, top - bottom, size), _geometryRoot.lossyScale));
            TunnelDropOpeningCutter.Cut(lower, bounds, _generatedMeshes);
            TunnelDropOpeningCutter.Cut(upper, bounds, _generatedMeshes);

        }

        /// <summary>
        /// 開口位置の実際の床高を対象トンネルの当たり判定から取得する
        /// </summary>
        /// <param name="tunnel">床を測定するトンネル</param>
        /// <param name="center">マップ内の水平位置</param>
        /// <param name="fallback">レイアウト上の床高</param>
        /// <returns>マップ内の床の上面高</returns>
        private float SampleTunnelFloor(Transform tunnel, Vector3 center, float fallback)
        {
            Physics.SyncTransforms();
            var ray = new Ray(_geometryRoot.TransformPoint(new Vector3(center.x, fallback + 2.0f, center.z)), -_geometryRoot.up);
            float distance = float.PositiveInfinity;
            float floor = fallback;
            foreach (var collider in tunnel.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (!collider.Raycast(ray, out var hit, 5.0f * _geometryRoot.lossyScale.y)
                    || Vector3.Dot(hit.normal, _geometryRoot.up) < 0.5f || hit.distance >= distance) continue;
                distance = hit.distance;
                floor = _geometryRoot.InverseTransformPoint(hit.point).y;
            }
            return floor;
        }
    }
}
