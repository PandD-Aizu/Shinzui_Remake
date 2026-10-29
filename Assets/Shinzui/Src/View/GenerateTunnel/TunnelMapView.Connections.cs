using System;
using System.Collections.Generic;
using Shinzui.View.ItemSpawn;
using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>
        /// 基本トンネル全体ではなく側面口の高さを通路の天井高に使う
        /// </summary>
        /// <param name="fallback">モデルを測定できない場合の高さ</param>
        /// <returns>床の基準位置から通路の天井中心までの高さ</returns>
        public float ResolvePassageHeight(float fallback)
        {
            if (_tunnelPrefab == null) return fallback;
            Transform mouth = _tunnelPrefab.transform.Find(ExitPathNames[1]);
            if (mouth == null || !TryGetMouthDeck(mouth, _tunnelPrefab.transform, out Bounds deck)
                || !TryGetMeshBoundsInLocalSpace(mouth, _tunnelPrefab.transform, out Bounds bounds)) return fallback;

            float height = (bounds.max.y - deck.center.y) * Mathf.Abs(GetTunnelModelScale().y)
                + ShellThickness * 0.75f - ShellThickness * 1.5f;
            return Mathf.Min(fallback, Mathf.Max(2.2f, height));
        }

        /// <summary>
        /// アーチ形の入口と平坦な通路天井の間を閉じる
        /// </summary>
        /// <param name="model">配置済みモデル</param>
        /// <param name="root">トンネルの座標系</param>
        /// <param name="openEntrances">使用中の側面口</param>
        /// <param name="width">通路の内幅</param>
        /// <param name="height">通路の天井中心高</param>
        private void CreateMouthHeaders(Transform model, Transform root, IReadOnlyList<bool> openEntrances, float width, float height)
        {
            for (int i = 0; i < ExitPathNames.Length; i++)
            {
                if (openEntrances == null || i >= openEntrances.Count || !openEntrances[i]) continue;
                Transform mouth = model.Find(ExitPathNames[i]);
                if (mouth == null || !TryGetMouthDeck(mouth, root, out Bounds deck)
                    || !TryGetMeshBoundsInLocalSpace(mouth, root, out Bounds bounds)) continue;
                float sign = i < 3 ? -1 : 1;
                float x = sign < 0 ? bounds.min.x : bounds.max.x;
                Vector3 start = new Vector3(x, 0, deck.center.z - width * 0.5f);
                Vector3 end = new Vector3(x, 0, deck.center.z + width * 0.5f);

                // 入口の少し内側で天井を測定し、屋根の厚みまで継ぎ目を覆う
                CreateProfiledWall(root, "Mouth Ceiling Header", start, end, ShellThickness, point =>
                {
                    Vector3 sample = point;
                    sample.x = x - sign * 0.1f;
                    sample.z = Mathf.Clamp(sample.z, deck.min.z + 0.02f, deck.max.z - 0.02f);
                    Vector3 mapPoint = _geometryRoot.InverseTransformPoint(root.TransformPoint(sample));
                    float ceiling = SampleTunnelCeiling(mouth, mapPoint, root.localPosition.y + deck.center.y,
                        root.localPosition.y + height) - root.localPosition.y;
                    return (Mathf.Min(ceiling, height - ShellThickness * 0.5f),
                        Mathf.Max(ceiling + ShellThickness, height + ShellThickness * 0.5f));
                });
            }
        }

        /// <summary>
        /// 指定位置の天井内面を対象の衝突面から測定する
        /// </summary>
        /// <param name="tunnel">測定対象</param>
        /// <param name="point">マップ内の水平位置</param>
        /// <param name="floor">床上面の高さ</param>
        /// <param name="fallback">天井を取得できない場合の高さ</param>
        /// <returns>マップ内の天井内面の高さ</returns>
        private float SampleTunnelCeiling(Transform tunnel, Vector3 point, float floor, float fallback)
        {
            var ray = new Ray(_geometryRoot.TransformPoint(new Vector3(point.x, floor + 0.5f, point.z)), _geometryRoot.up);
            float distance = float.PositiveInfinity;
            float ceiling = fallback;
            foreach (Collider collider in tunnel.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                if (!collider.Raycast(ray, out RaycastHit hit, Mathf.Max(2, fallback - floor + 2) * _geometryRoot.lossyScale.y)
                    || Vector3.Dot(hit.normal, _geometryRoot.up) >= -0.1f || hit.distance >= distance) continue;
                distance = hit.distance;
                ceiling = _geometryRoot.InverseTransformPoint(hit.point).y;
            }
            return ceiling;
        }

        /// <summary>
        /// 上下端が天井の曲面に追従する厚み付きの壁を作る
        /// </summary>
        /// <param name="parent">壁の配置先</param>
        /// <param name="name">壁の名前</param>
        /// <param name="start">壁の始点</param>
        /// <param name="end">壁の終点</param>
        /// <param name="thickness">壁の厚み</param>
        /// <param name="sample">各水平位置での上下端</param>
        private void CreateProfiledWall(Transform parent, string name, Vector3 start, Vector3 end, float thickness,
            Func<Vector3, (float bottom, float top)> sample)
        {
            const int segments = 16;
            Vector3 direction = end - start;
            Vector3 normal = Vector3.Cross(Vector3.up, direction).normalized;
            var vertices = new Vector3[(segments + 1) * 4];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                Vector3 center = Vector3.Lerp(start, end, (float)i / segments);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = center + normal * (corner == 0 || corner == 3 ? -thickness * 0.5f : thickness * 0.5f);
                    var limits = sample(point);
                    point.y = corner < 2 ? limits.bottom : limits.top;
                    vertices[i * 4 + corner] = point;
                    uv[i * 4 + corner] = new Vector2(direction.magnitude * i / segments, point.y);
                    if (i == segments) continue;
                    int a = i * 4 + corner;
                    int b = i * 4 + (corner + 1) % 4;
                    triangles.AddRange(new[] { a, b, b + 4, a, b + 4, a + 4 });
                }
            }
            int last = segments * 4;
            triangles.AddRange(new[] { 0, 3, 2, 0, 2, 1, last, last + 1, last + 2, last, last + 2, last + 3 });

            // 描画と衝突に同じメッシュを使う
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _generatedMeshes.Add(mesh);
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.AddComponent<MeshFilter>().sharedMesh = mesh;
            wall.AddComponent<MeshRenderer>().sharedMaterial = _tunnelMaterial;
            ApplyTunnelPassageMaterial(wall);
            ApplyStageCollisionRecursive(wall);
        }

        /// <summary>
        /// 接続口の内幅を保ちながら本体を伸縮し、床面を通路へ揃える
        /// </summary>
        /// <param name="model">生成したモデル</param>
        /// <param name="root">トンネルの座標系</param>
        /// <param name="length">配置上のトンネル長</param>
        /// <param name="corridorWidth">接続通路の内幅</param>
        /// <returns>接続口を持つモデルを補正できたか</returns>
        private bool FitTunnelConnections(Transform model, Transform root, float length, float corridorWidth)
        {
            Transform floor = model.Find("tunnel_floor");
            if (floor == null || !TryGetMeshBoundsInLocalSpace(floor, root, out Bounds floorBounds)) return false;

            // 左右各三つの接続口の外形と歩ける床の幅を別々に測定する
            var mouths = new List<(Bounds shell, Bounds deck)>();
            for (int i = 0; i < 6; i++)
            {
                Transform mouth = model.Find(ExitPathNames[i]);
                if (mouth == null || !TryGetMeshBoundsInLocalSpace(mouth, root, out Bounds shell)
                    || !TryGetMouthDeck(mouth, root, out Bounds deck)) return false;
                mouths.Add((shell, deck));
            }
            mouths.Sort((a, b) => (a.deck.center.x < 0) == (b.deck.center.x < 0)
                ? a.deck.center.z.CompareTo(b.deck.center.z) : a.deck.center.x.CompareTo(b.deck.center.x));

            float ratio = length / floorBounds.size.z;
            float spacing = (mouths[2].deck.center.z - mouths[0].deck.center.z
                + mouths[5].deck.center.z - mouths[3].deck.center.z) * 0.25f;
            float deckY = mouths[1].deck.center.y;
            var sourceZ = new List<float>[2];
            var targetZ = new List<float>[2];
            for (int side = 0; side < 2; side++)
            {
                sourceZ[side] = new List<float> { floorBounds.min.z };
                targetZ[side] = new List<float> { -length * 0.5f };
                for (int i = 0; i < 3; i++)
                {
                    var mouth = mouths[side * 3 + i];
                    float center = (i - 1) * spacing * ratio;
                    float apertureScale = corridorWidth / mouth.deck.size.z;
                    sourceZ[side].Add(mouth.shell.min.z);
                    sourceZ[side].Add(mouth.shell.max.z);
                    targetZ[side].Add(center + (mouth.shell.min.z - mouth.deck.center.z) * apertureScale);
                    targetZ[side].Add(center + (mouth.shell.max.z - mouth.deck.center.z) * apertureScale);
                }
                sourceZ[side].Add(floorBounds.max.z);
                targetZ[side].Add(length * 0.5f);
            }

            // 同じ頂点変形を描画、衝突、アイテム生成面へ適用する
            var replacements = new Dictionary<(Mesh, Transform), Mesh>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = FitConnectionMesh(filter.sharedMesh, filter.transform, root,
                    sourceZ, targetZ, floorBounds, deckY, replacements);
            foreach (MeshCollider collider in model.GetComponentsInChildren<MeshCollider>(true))
            {
                Mesh mesh = FitConnectionMesh(collider.sharedMesh, collider.transform, root,
                    sourceZ, targetZ, floorBounds, deckY, replacements);
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
            foreach (WeightedSpawnSurface surface in model.GetComponentsInChildren<WeightedSpawnSurface>(true))
                surface.CaptureMeshSignature(FitConnectionMesh(surface.SurfaceMesh, surface.transform, root,
                    sourceZ, targetZ, floorBounds, deckY, replacements));

            // メッシュとは別のTransformを持つループ用ゲートも伸縮後の端へ移す
            FitTunnelGates(model, root, sourceZ, targetZ);
            return true;
        }

        /// <summary>
        /// ループ用ゲートの平面と判定範囲を本体の伸縮後の位置へ揃える
        /// </summary>
        /// <param name="model">生成したモデル</param>
        /// <param name="root">トンネルの座標系</param>
        /// <param name="sourceZ">変形前の区切り</param>
        /// <param name="targetZ">変形後の区切り</param>
        private static void FitTunnelGates(Transform model, Transform root, List<float>[] sourceZ, List<float>[] targetZ)
        {
            foreach (TunnelGateView gate in model.GetComponentsInChildren<TunnelGateView>(true))
            {
                BoxCollider box = gate.GetComponent<BoxCollider>();
                if (box == null) continue;

                Vector3 center = root.InverseTransformPoint(gate.transform.TransformPoint(box.center));
                Vector3 target = center;
                target.z = (RemapConnectionZ(center.z, sourceZ[0], targetZ[0])
                    + RemapConnectionZ(center.z, sourceZ[1], targetZ[1])) * 0.5f;
                float height = root.InverseTransformVector(gate.transform.TransformVector(Vector3.up * box.size.y)).magnitude;
                target.y = ShellThickness * 0.75f + height * 0.5f;
                gate.transform.position += root.TransformVector(target - center);
            }
        }

        /// <summary>
        /// 入口メッシュの水平な上向き面から歩行用の床を取得する
        /// </summary>
        /// <param name="mouth">入口のモデル</param>
        /// <param name="space">測定先の座標系</param>
        /// <param name="deck">床上面の範囲</param>
        /// <returns>水平な床を取得できたか</returns>
        private static bool TryGetMouthDeck(Transform mouth, Transform space, out Bounds deck)
        {
            deck = default;
            MeshFilter filter = mouth.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;
            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Matrix4x4 matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            TryGetMeshBoundsInLocalSpace(mouth, space, out Bounds mouthBounds);
            float area = 0;
            float floorY = 0;

            // 最も面積の広い水平な上向き三角形を床の基準とする
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (a.y >= mouthBounds.center.y || normal.y <= area
                    || Mathf.Abs(normal.x) + Mathf.Abs(normal.z) > normal.y * 0.01f) continue;
                area = normal.y;
                floorY = a.y;
            }
            if (area <= 0) return false;

            bool found = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                if (Mathf.Abs(a.y - floorY) > 0.005f || Mathf.Abs(b.y - floorY) > 0.005f
                    || Mathf.Abs(c.y - floorY) > 0.005f || Vector3.Cross(b - a, c - a).y <= 0) continue;
                if (!found) deck = new Bounds(a, Vector3.zero);
                else deck.Encapsulate(a);
                deck.Encapsulate(b);
                deck.Encapsulate(c);
                found = true;
            }
            return found && deck.size.z > 0.01f;
        }

        /// <summary>
        /// インスタンス専用メッシュを作り、接続口と床の座標を補正する
        /// </summary>
        /// <param name="source">元のメッシュ</param>
        /// <param name="part">メッシュの座標系</param>
        /// <param name="root">トンネルの座標系</param>
        /// <param name="sourceZ">元の長さ方向の区切り</param>
        /// <param name="targetZ">補正後の区切り</param>
        /// <param name="floorBounds">中央床の範囲</param>
        /// <param name="deckY">接続口の床上面</param>
        /// <param name="replacements">同じ面の変形結果</param>
        /// <returns>補正後のメッシュ</returns>
        private Mesh FitConnectionMesh(Mesh source, Transform part, Transform root,
            List<float>[] sourceZ, List<float>[] targetZ, Bounds floorBounds, float deckY,
            Dictionary<(Mesh, Transform), Mesh> replacements)
        {
            if (source == null) return null;
            if (replacements.TryGetValue((source, part), out Mesh existing)) return existing;
            Mesh mesh = Instantiate(source);
            mesh.name = "Generated Connection " + source.name;
            Matrix4x4 toRoot = root.worldToLocalMatrix * part.localToWorldMatrix;
            Matrix4x4 fromRoot = toRoot.inverse;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(vertices[i]);
                float leftZ = RemapConnectionZ(p.z, sourceZ[0], targetZ[0]);
                float rightZ = RemapConnectionZ(p.z, sourceZ[1], targetZ[1]);
                p.z = Mathf.Lerp(leftZ, rightZ, Mathf.InverseLerp(floorBounds.min.x, floorBounds.max.x, p.x));

                // 中央床から接続口までの段差をなくし、床厚と天井は残す
                p.y = ShellThickness * 0.75f + (p.y < floorBounds.max.y ? p.y - floorBounds.max.y : Mathf.Max(0, p.y - deckY));
                vertices[i] = fromRoot.MultiplyPoint3x4(p);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            replacements.Add((source, part), mesh);
            _generatedMeshes.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// 接続口の区間を固定幅にし、間の本体だけを補間する
        /// </summary>
        /// <param name="z">変形前の位置</param>
        /// <param name="source">変形前の区切り</param>
        /// <param name="target">変形後の区切り</param>
        /// <returns>変形後の位置</returns>
        private static float RemapConnectionZ(float z, List<float> source, List<float> target)
        {
            int interval = 0;
            while (interval < source.Count - 2 && z > source[interval + 1]) interval++;
            float t = (z - source[interval]) / (source[interval + 1] - source[interval]);
            return Mathf.LerpUnclamped(target[interval], target[interval + 1], t);
        }
    }
}
