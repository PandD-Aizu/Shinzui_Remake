using System.Collections.Generic;
using Shinzui.View.ItemSpawn;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// インスタンスの描画面と衝突面から落下用の直方体領域を差し引く
    /// </summary>
    internal static class TunnelDropOpeningCutter
    {
        private const float MinimumAreaSquared = 1e-12f;

        private struct Vertex
        {
            internal Vector3 Position;
            internal Vector3 Normal;
            internal Vector4[] UV;
        }

        /// <summary>
        /// 指定領域と交差する面を分割し、領域外の面だけをインスタンスへ設定する
        /// コライダーだけの BoxCollider は対象外とする
        /// </summary>
        /// <param name="root">切断対象の階層</param>
        /// <param name="worldBounds">ワールド軸に平行な切断領域</param>
        /// <param name="ownedMeshes">呼び出し側で破棄する生成メッシュの格納先</param>
        internal static void Cut(Transform root, Bounds worldBounds, ICollection<Mesh> ownedMeshes)
        {
            if (root == null || worldBounds.size.x <= 0f || worldBounds.size.y <= 0f || worldBounds.size.z <= 0f)
                return;

            // 切断前の器具形状を使い、開口部に残る照明を停止する
            DisableLights(root, worldBounds);

            var replacements = new Dictionary<(Mesh, Transform), Mesh>();
            Plane[] planes = CreatePlanes(worldBounds);

            // 元の共有メッシュを変更せず、描画インスタンスだけを置換する
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                Mesh cut = GetCutMesh(source, filter.transform, worldBounds, planes, replacements, ownedMeshes);
                if (cut == source) continue;
                filter.sharedMesh = cut;

                // 直方体の衝突面が穴を塞がないよう、切断後の面へ置換する
                MeshCollider replacementCollider = null;
                foreach (MeshCollider collider in filter.GetComponents<MeshCollider>())
                {
                    if (collider.enabled && !collider.isTrigger && (collider.sharedMesh == source || collider.sharedMesh == cut))
                    {
                        replacementCollider = collider;
                        break;
                    }
                }

                foreach (BoxCollider box in filter.GetComponents<BoxCollider>())
                {
                    if (box.isTrigger || !box.enabled) continue;
                    box.enabled = false;
                    if (replacementCollider == null)
                    {
                        replacementCollider = filter.gameObject.AddComponent<MeshCollider>();
                        replacementCollider.sharedMaterial = box.sharedMaterial;
                        if (cut.vertexCount > 0) replacementCollider.sharedMesh = cut;
                        else replacementCollider.enabled = false;
                    }
                }
            }

            // 描画を持たない ItemSpawnWeight の重複床も独立して切断する
            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.isTrigger) continue;
                Mesh source = collider.sharedMesh;
                Mesh cut = GetCutMesh(source, collider.transform, worldBounds, planes, replacements, ownedMeshes);
                if (cut == source) continue;
                collider.sharedMesh = null;
                collider.convex = false;
                if (cut.vertexCount > 0) collider.sharedMesh = cut;
                else collider.enabled = false;
            }

            // 重みマップを維持したまま生成面とトポロジーの署名を更新する
            foreach (WeightedSpawnSurface surface in root.GetComponentsInChildren<WeightedSpawnSurface>(true))
            {
                Mesh source = surface.SurfaceMesh;
                Mesh cut = GetCutMesh(source, surface.transform, worldBounds, planes, replacements, ownedMeshes);
                if (cut != source) surface.CaptureMeshSignature(cut);
            }
        }

        /// <summary>
        /// 直方体の内側を正とする六つの平面を作る
        /// </summary>
        /// <param name="bounds">切断領域</param>
        /// <returns>内向きの境界平面</returns>
        private static Plane[] CreatePlanes(Bounds bounds)
        {
            return new[]
            {
                new Plane(Vector3.right, bounds.min), new Plane(Vector3.left, bounds.max),
                new Plane(Vector3.up, bounds.min), new Plane(Vector3.down, bounds.max),
                new Plane(Vector3.forward, bounds.min), new Plane(Vector3.back, bounds.max)
            };
        }

        /// <summary>
        /// 同じ座標系の切断結果を再利用し、生成メッシュの所有権を登録する
        /// </summary>
        /// <param name="source">切断前のメッシュ</param>
        /// <param name="space">メッシュの座標系</param>
        /// <param name="bounds">ワールド切断領域</param>
        /// <param name="planes">内向きの境界平面</param>
        /// <param name="replacements">座標系ごとの結果キャッシュ</param>
        /// <param name="ownedMeshes">生成メッシュの格納先</param>
        /// <returns>切断後のメッシュ、または変更のない元メッシュ</returns>
        private static Mesh GetCutMesh(Mesh source, Transform space, Bounds bounds, Plane[] planes,
            Dictionary<(Mesh, Transform), Mesh> replacements, ICollection<Mesh> ownedMeshes)
        {
            if (source == null || !source.isReadable || !Intersects(source.bounds, space, bounds)) return source;
            var key = (source, space);
            if (replacements.TryGetValue(key, out Mesh cached)) return cached;

            Mesh cut = Subtract(source, space.localToWorldMatrix, planes);
            replacements.Add(key, cut);
            if (cut != source)
            {
                ownedMeshes.Add(cut);
                // 同じ呼び出し内で新しいコライダーのメッシュを再切断しない
                replacements[(cut, space)] = cut;
            }
            return cut;
        }

        /// <summary>
        /// ローカル境界の八頂点を変換して交差候補を判定する
        /// </summary>
        /// <param name="localBounds">ローカル境界</param>
        /// <param name="space">変換元の座標系</param>
        /// <param name="worldBounds">ワールド切断領域</param>
        /// <returns>ワールド境界が交差するか</returns>
        private static bool Intersects(Bounds localBounds, Transform space, Bounds worldBounds)
        {
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            var transformed = new Bounds(space.TransformPoint(min), Vector3.zero);
            for (int i = 1; i < 8; i++)
                transformed.Encapsulate(space.TransformPoint(new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z)));
            return transformed.Intersects(worldBounds);
        }

        /// <summary>
        /// 各三角形から六平面の共通内部を差し引き、外側の断片を三角形へ戻す
        /// </summary>
        /// <param name="source">切断前のメッシュ</param>
        /// <param name="localToWorld">ワールドへの変換行列</param>
        /// <param name="planes">内向きの境界平面</param>
        /// <returns>切断後のメッシュ、または交差しない元メッシュ</returns>
        private static Mesh Subtract(Mesh source, Matrix4x4 localToWorld, Plane[] planes)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            var inputUV = new List<Vector4>[8];
            var outputUV = new List<Vector4>[8];
            for (int channel = 0; channel < 8; channel++)
            {
                inputUV[channel] = new List<Vector4>();
                source.GetUVs(channel, inputUV[channel]);
                if (inputUV[channel].Count == positions.Length) outputUV[channel] = new List<Vector4>();
            }

            var vertices = new Vertex[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                vertices[i] = new Vertex
                {
                    Position = positions[i],
                    Normal = normals.Length == positions.Length ? normals[i] : Vector3.zero,
                    UV = new Vector4[8]
                };
                for (int channel = 0; channel < 8; channel++)
                    if (outputUV[channel] != null) vertices[i].UV[channel] = inputUV[channel][i];
            }

            var outputPositions = new List<Vector3>();
            var outputNormals = new List<Vector3>();
            var submeshes = new List<int>[source.subMeshCount];
            bool changed = false;

            // 外側へ出た断片は確定し、内側の断片だけを次の平面で分割する
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                if (source.GetTopology(submesh) != MeshTopology.Triangles) return source;
                submeshes[submesh] = new List<int>();
                int[] indices = source.GetTriangles(submesh);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var inside = new List<Vertex> { vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]] };
                    foreach (Plane plane in planes)
                    {
                        if (inside.Count < 3) break;
                        Split(inside, plane, localToWorld, out List<Vertex> nextInside, out List<Vertex> outside);
                        Append(outside, outputPositions, outputNormals, outputUV, submeshes[submesh]);
                        inside = nextInside;
                    }
                    if (HasArea(inside)) changed = true;
                }
            }
            if (!changed) return source;

            // サブメッシュの順序と UV チャンネルを維持してインスタンスを作る
            var result = new Mesh
            {
                name = source.name + " (Drop Opening)",
                indexFormat = outputPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            result.SetVertices(outputPositions);
            if (normals.Length == positions.Length) result.SetNormals(outputNormals);
            for (int channel = 0; channel < 8; channel++)
                if (outputUV[channel] != null) result.SetUVs(channel, outputUV[channel]);
            result.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) result.SetTriangles(submeshes[i], i, false);
            result.RecalculateBounds();
            if (normals.Length != positions.Length) result.RecalculateNormals();
            if (outputUV[0] != null && outputPositions.Count > 0) result.RecalculateTangents();
            return result;
        }

        /// <summary>
        /// 多角形を平面の内外へ分割し、交点の頂点属性を補間する
        /// </summary>
        /// <param name="polygon">分割対象の凸多角形</param>
        /// <param name="plane">内側を正とする平面</param>
        /// <param name="localToWorld">ワールドへの変換行列</param>
        /// <param name="inside">内側の断片</param>
        /// <param name="outside">外側の断片</param>
        private static void Split(List<Vertex> polygon, Plane plane, Matrix4x4 localToWorld,
            out List<Vertex> inside, out List<Vertex> outside)
        {
            inside = new List<Vertex>(polygon.Count + 1);
            outside = new List<Vertex>(polygon.Count + 1);
            float minDistance = float.PositiveInfinity;
            float maxDistance = float.NegativeInfinity;
            foreach (Vertex vertex in polygon)
            {
                float distance = plane.GetDistanceToPoint(localToWorld.MultiplyPoint3x4(vertex.Position));
                minDistance = Mathf.Min(minDistance, distance);
                maxDistance = Mathf.Max(maxDistance, distance);
            }

            // 境界に沿う面を一度だけ残し、接触だけの場合は切断しない
            if (maxDistance <= 0f)
            {
                outside.AddRange(polygon);
                return;
            }
            if (minDistance >= 0f)
            {
                inside.AddRange(polygon);
                return;
            }

            Vertex previous = polygon[polygon.Count - 1];
            float previousDistance = plane.GetDistanceToPoint(localToWorld.MultiplyPoint3x4(previous.Position));
            foreach (Vertex current in polygon)
            {
                float distance = plane.GetDistanceToPoint(localToWorld.MultiplyPoint3x4(current.Position));
                if ((previousDistance < 0f && distance > 0f) || (previousDistance > 0f && distance < 0f))
                {
                    Vertex intersection = Interpolate(previous, current, previousDistance / (previousDistance - distance));
                    inside.Add(intersection);
                    outside.Add(intersection);
                }
                if (distance >= 0f) inside.Add(current);
                if (distance <= 0f) outside.Add(current);
                previous = current;
                previousDistance = distance;
            }
        }

        /// <summary>
        /// 辺上の位置と法線と UV を線形補間する
        /// </summary>
        /// <param name="a">辺の始点</param>
        /// <param name="b">辺の終点</param>
        /// <param name="t">始点からの比率</param>
        /// <returns>補間された頂点</returns>
        private static Vertex Interpolate(Vertex a, Vertex b, float t)
        {
            var vertex = new Vertex
            {
                Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t),
                UV = new Vector4[8]
            };
            for (int i = 0; i < 8; i++) vertex.UV[i] = Vector4.LerpUnclamped(a.UV[i], b.UV[i], t);
            return vertex;
        }

        /// <summary>
        /// 面積を持つ多角形か判定する
        /// </summary>
        /// <param name="polygon">判定対象の多角形</param>
        /// <returns>有効な三角形を含むか</returns>
        private static bool HasArea(List<Vertex> polygon)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
                if (Vector3.Cross(polygon[i].Position - polygon[0].Position,
                    polygon[i + 1].Position - polygon[0].Position).sqrMagnitude > MinimumAreaSquared) return true;
            return false;
        }

        /// <summary>
        /// 頂点の順序を維持し、縮退面を除いて扇状に三角形化する
        /// </summary>
        /// <param name="polygon">出力する凸多角形</param>
        /// <param name="positions">出力位置</param>
        /// <param name="normals">出力法線</param>
        /// <param name="uv">出力 UV チャンネル</param>
        /// <param name="indices">出力サブメッシュの頂点番号</param>
        private static void Append(List<Vertex> polygon, List<Vector3> positions, List<Vector3> normals,
            List<Vector4>[] uv, List<int> indices)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                Vertex a = polygon[0], b = polygon[i], c = polygon[i + 1];
                if (Vector3.Cross(b.Position - a.Position, c.Position - a.Position).sqrMagnitude <= MinimumAreaSquared)
                    continue;
                foreach (Vertex vertex in new[] { a, b, c })
                {
                    indices.Add(positions.Count);
                    positions.Add(vertex.Position);
                    normals.Add(vertex.Normal.normalized);
                    for (int channel = 0; channel < 8; channel++)
                        if (uv[channel] != null) uv[channel].Add(vertex.UV[channel]);
                }
            }
        }

        /// <summary>
        /// 光源位置または器具の描画境界が開口部と交差する照明を停止する
        /// </summary>
        /// <param name="root">切断対象の階層</param>
        /// <param name="bounds">ワールド切断領域</param>
        private static void DisableLights(Transform root, Bounds bounds)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                Transform fixture = light.transform;
                // 複数光源を持つ器具も、Ceiling Lights 直下のルート単位で停止する
                for (Transform ancestor = light.transform; ancestor != null && ancestor != root; ancestor = ancestor.parent)
                {
                    if (ancestor.parent == null || ancestor.parent.name != "Ceiling Lights") continue;
                    fixture = ancestor;
                    break;
                }

                // 器具名または描画を持つ直近の親だけを扱い、モデル全体を停止しない
                if (fixture == light.transform && fixture.parent != null && fixture.parent != root &&
                    (fixture.parent.GetComponent<Renderer>() != null ||
                     fixture.parent.name.IndexOf("Light", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     fixture.parent.name.IndexOf("Fixture", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    fixture = fixture.parent;
                bool intersects = bounds.Contains(light.transform.position);
                foreach (Renderer renderer in fixture.GetComponentsInChildren<Renderer>(true))
                    if (renderer.bounds.Intersects(bounds)) intersects = true;
                if (intersects) fixture.gameObject.SetActive(false);
            }
        }
    }
}
