using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public sealed partial class TunnelMapView
    {
        /// <summary>大きいトンネルの側方へ黒いごみ袋を3～5個の群として配置する</summary>
        /// <param name="host">トンネル本体</param>
        /// <param name="width">本体の床幅</param>
        /// <param name="length">本体の長さ</param>
        /// <param name="passageWidth">中央に残す通行幅</param>
        /// <param name="seed">配置の乱数シード</param>
        public void CreateGarbageBagGroups(Transform host, float width, float length, float passageWidth, int seed)
        {
            var prefab = Resources.Load<GameObject>("BlackGarbageBag");
            if (prefab == null) return;
            var shape = prefab.GetComponent<BoxCollider>();
            if (shape == null) return;

            // 回転後も袋同士が重ならない間隔を取り、中央と両端を空ける
            float radius = shape.size.magnitude * .5f * 1.1f;
            float spacing = radius * 2f + .04f;
            float innerX = passageWidth * .5f + radius + .25f;
            float outerX = width * .5f - radius - .25f - spacing;
            float zRange = length * .5f - spacing - radius - 2f;
            if (outerX < innerX || zRange <= 0f) return;
            var random = new System.Random(unchecked(seed ^ 0x42414753));
            int target = Mathf.Clamp(Mathf.RoundToInt(length / 15f), 1, 3);
            int placed = 0;
            Physics.SyncTransforms();

            // 群の全候補を検証し、一部だけ残って単独配置になることを防ぐ
            for (int attempt = 0; attempt < target * 24 && placed < target; attempt++)
            {
                int count = random.Next(3, 6);
                int side = random.Next(2) == 0 ? -1 : 1;
                float x = Mathf.Lerp(innerX, outerX, (float)random.NextDouble());
                float z = ((float)random.NextDouble() * 2f - 1f) * zRange;
                var positions = new Vector3[count];
                var rotations = new Quaternion[count];
                var scales = new float[count];
                int baseCount = random.Next(2, count);
                float groupYaw = (float)random.NextDouble() * 360f;
                var group = new GameObject($"Garbage Bag Group {placed:00}");
                group.transform.SetParent(host, false);
                bool valid = true;
                for (int i = 0; i < count; i++)
                {
                    // 床上の袋は円周上にばらし、残りは既存の袋へ積み上げる
                    float angle = (groupYaw + i * 360f / baseCount) * Mathf.Deg2Rad;
                    float spread = spacing * Mathf.Lerp(.60f, .72f, (float)random.NextDouble());
                    float localX = side * (x + spacing * .5f) + Mathf.Cos(angle) * spread;
                    float localZ = z + Mathf.Sin(angle) * spread;
                    Transform support = i >= baseCount ? group.transform.GetChild(random.Next(i)) : null;
                    if (support != null)
                    {
                        Vector3 supportCenter = host.InverseTransformPoint(support.GetComponent<BoxCollider>().bounds.center);
                        localX = supportCenter.x + ((float)random.NextDouble() - .5f) * .12f;
                        localZ = supportCenter.z + ((float)random.NextDouble() - .5f) * .12f;
                    }
                    float floor = SampleGimmickFloor(host, localZ);
                    Vector3 origin = host.TransformPoint(new Vector3(localX, floor + .5f, localZ));
                    if (!RaycastGimmickHost(host, origin, -host.up, 1f, out var hit)
                        || Vector3.Dot(hit.normal, host.up) < .95f)
                    {
                        valid = false;
                        break;
                    }

                    // 向きと大きさを変え、壁や既存家具に接触する群を除外する
                    scales[i] = Mathf.Lerp(.85f, 1.1f, (float)random.NextDouble());
                    float tilt = new[] { 0f, 90f, 180f, 270f }[random.Next(4)];
                    rotations[i] = host.rotation * Quaternion.Euler(tilt,
                        (float)random.NextDouble() * 360f, ((float)random.NextDouble() - .5f) * 24f);
                    Vector3 halfSize = shape.size * (scales[i] * .5f);
                    float height = Mathf.Abs(Vector3.Dot(rotations[i] * Vector3.right, host.up)) * halfSize.x
                        + Mathf.Abs(Vector3.Dot(rotations[i] * Vector3.up, host.up)) * halfSize.y
                        + Mathf.Abs(Vector3.Dot(rotations[i] * Vector3.forward, host.up)) * halfSize.z;
                    Vector3 center = hit.point + host.up * (height + .03f);
                    if (support != null)
                    {
                        // 外接箱ではなく描画メッシュの下面と支持面を合わせる
                        Vector3 start = center + host.up * (count * radius * 2f);
                        Vector3 pivot = start - rotations[i] * (shape.center * scales[i]);
                        if (!TryResolveGarbageBagContact(prefab, group.transform, host, pivot,
                            rotations[i], scales[i], radius, out float offset))
                        {
                            valid = false;
                            break;
                        }
                        center = start + host.up * offset;
                    }
                    positions[i] = center - rotations[i] * (shape.center * scales[i]);
                    Vector3 localCenter = host.InverseTransformPoint(center);
                    if (Mathf.Abs(localCenter.x) - radius < passageWidth * .5f + .1f
                        || Mathf.Abs(localCenter.x) + radius > width * .5f - .1f
                        || HasGarbageBagObstacle(center, halfSize, rotations[i], support != null ? group.transform : null))
                    {
                        valid = false;
                        break;
                    }

                    // 候補を物理へ反映して後続の袋の支持面と衝突判定に使う
                    var bag = Instantiate(prefab, positions[i], rotations[i], group.transform);
                    bag.transform.localScale = Vector3.one * scales[i];
                    Physics.SyncTransforms();
                }
                if (!valid)
                {
                    // 失敗した群は直ちに判定対象から外し、単独の袋を残さない
                    group.SetActive(false);
                    if (UnityEngine.Application.isPlaying) Destroy(group);
                    else DestroyImmediate(group);
                    Physics.SyncTransforms();
                    continue;
                }
                placed++;
            }
        }

        /// <summary>袋の外接箱と周囲の障害物の重なりを調べる</summary>
        /// <param name="center">箱の中心</param>
        /// <param name="halfSize">箱の半寸法</param>
        /// <param name="rotation">箱の回転</param>
        /// <param name="group">表面で接触を検証済みの群</param>
        /// <returns>群以外の障害物に接触しているか</returns>
        private static bool HasGarbageBagObstacle(Vector3 center, Vector3 halfSize, Quaternion rotation, Transform group)
        {
            // 丸い袋同士では外接箱が重なるため、群内の箱だけを除外する
            foreach (var collider in Physics.OverlapBox(center, halfSize, rotation, ~0, QueryTriggerInteraction.Ignore))
                if (group == null || !collider.transform.IsChildOf(group)) return true;
            return false;
        }

        /// <summary>描画メッシュを鉛直に走査して袋同士が接する移動量を求める</summary>
        /// <param name="prefab">袋のモデル</param>
        /// <param name="group">配置済みの群</param>
        /// <param name="host">トンネルの座標系</param>
        /// <param name="position">上空の候補位置</param>
        /// <param name="rotation">候補の回転</param>
        /// <param name="scale">候補の倍率</param>
        /// <param name="radius">袋を包む半径</param>
        /// <param name="offset">接触位置までの鉛直移動量</param>
        /// <returns>支持面が見つかったか</returns>
        private static bool TryResolveGarbageBagContact(GameObject prefab, Transform group, Transform host,
            Vector3 position, Quaternion rotation, float scale, float radius, out float offset)
        {
            // 一時的なメッシュ形状を使い、結び目を含む実際の表面を測る
            var probe = Instantiate(prefab, position, rotation);
            probe.transform.localScale = Vector3.one * scale;
            var upper = new System.Collections.Generic.List<MeshCollider>();
            var lower = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in probe.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                upper.Add(collider);
            }
            foreach (var filter in group.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                lower.Add(collider);
            }
            Physics.SyncTransforms();

            // 同じ水平位置で上の袋の下面と下の袋の上面を採り、最初の接触を選ぶ
            offset = float.NegativeInfinity;
            Vector3 center = probe.transform.TransformPoint(probe.GetComponent<BoxCollider>().center);
            const int samples = 48;
            float distance = radius * 20f;
            for (int x = 0; x <= samples; x++)
            for (int z = 0; z <= samples; z++)
            {
                Vector3 point = center + host.right * ((x * 2f / samples - 1f) * radius)
                    + host.forward * ((z * 2f / samples - 1f) * radius);
                var upward = new Ray(point - host.up * distance, host.up);
                var downward = new Ray(point + host.up * distance, -host.up);
                float bottom = float.PositiveInfinity;
                float top = float.NegativeInfinity;
                foreach (var collider in upper)
                    if (collider.Raycast(upward, out var hit, distance * 2f))
                        bottom = Mathf.Min(bottom, Vector3.Dot(hit.point, host.up));
                foreach (var collider in lower)
                    if (collider.Raycast(downward, out var hit, distance * 2f))
                        top = Mathf.Max(top, Vector3.Dot(hit.point, host.up));
                if (!float.IsInfinity(bottom) && !float.IsInfinity(top))
                    offset = Mathf.Max(offset, top - bottom);
            }

            // 柔らかい袋の接触部を僅かに馴染ませ、一時形状を残さない
            foreach (var collider in lower)
            {
                collider.enabled = false;
                if (UnityEngine.Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            probe.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(probe);
            else DestroyImmediate(probe);
            offset -= .01f;
            return !float.IsInfinity(offset);
        }
    }
}
