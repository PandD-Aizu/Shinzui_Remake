using System;
using System.Collections.Generic;
using Shinzui.Infrastructure.Lighting;
using Shinzui.View.LoopTunnel;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>生成トンネルの材質と湿気と器具の故障を局所的に構成する</summary>
    public sealed partial class TunnelMapView
    {
        [Header("Tunnel Surface Materials")]
        [SerializeField] private Material tunnelWall;
        [SerializeField] private Material tunnelRoad;
        [SerializeField] private Material tunnelWalkway;
        [SerializeField] private Material tunnelCeiling;
        private readonly HashSet<MeshRenderer> _lookRenderers = new();
        private readonly List<(Transform root, float length, float height, Collider[] surfaces)> _lookSections = new();
        private int _lookSplits, _lookUnknown, _lookDead, _lookFlicker, _lookFog;
        private bool _weatheringBuilt;
        private bool _roadSurfaceAudited;

        /// <summary>再生成前に参照とカウンターをリセットする</summary>
        private void PrepareTunnelLook()
        {
            _lookRenderers.Clear();
            _lookSections.Clear();
            _lookSplits = _lookUnknown = _lookDead = _lookFlicker = _lookFog = 0;
            _weatheringBuilt = false;
            _roadSurfaceAudited = false;
            if (tunnelWall == null) tunnelWall = Resources.Load<Material>("TunnelLook/TunnelWall");
            if (tunnelRoad == null) tunnelRoad = Resources.Load<Material>("TunnelLook/TunnelRoad");
            if (tunnelWalkway == null) tunnelWalkway = Resources.Load<Material>("TunnelLook/TunnelWalkway");
            if (tunnelCeiling == null) tunnelCeiling = Resources.Load<Material>("TunnelLook/TunnelCeiling");
            if (tunnelWall == null) Debug.LogWarning("Tunnel look materials missing: run Shinzui/LookDev/1 Build Tunnel Materials; retaining legacy fallback");
        }

        /// <summary>面名と複合メッシュの面方向に応じて材質を割り当てる</summary>
        /// <param name="model">装飾や家具を追加する前の構造物</param>
        /// <returns>新材質を適用できる場合はtrue</returns>
        private bool ApplyLookSurfaces(GameObject model)
        {
            if (tunnelWall == null || tunnelRoad == null || tunnelWalkway == null || tunnelCeiling == null) return false;
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                if (name.Contains("portal") || !_lookRenderers.Add(renderer)) continue;
                bool room = renderer.transform.GetComponentInParent<GeneratedCorridorInfo>() != null;
                Material material = name.Contains("side_floor") || name.Contains("walkway") || name.Contains("room floor") ? tunnelWalkway
                    : name.Contains("floor") ? (room ? tunnelWalkway : tunnelRoad)
                    : name.Contains("ceiling") || name.Contains("header") ? tunnelCeiling
                    : name.Contains("wall") || name.Contains("cover") ? tunnelWall : null;
                if (material != null)
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                    continue;
                }
                /* 床とアーチが一枚のメッシュでも頂点とUVを変えず三角形を材質別に分ける */
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh source = filter != null ? filter.sharedMesh : null;
                if (source == null || !source.isReadable)
                {
                    renderer.sharedMaterial = tunnelWall;
                    _lookUnknown++;
                    Debug.LogWarning($"Tunnel surface fallback (unreadable/missing mesh): {renderer.name}, mesh={source?.name}");
                    continue;
                }
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                Vector3[] vertices = source.vertices;
                int[] indices = source.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = renderer.transform.TransformVector(vertices[indices[i + 1]] - vertices[indices[i]]);
                    Vector3 b = renderer.transform.TransformVector(vertices[indices[i + 2]] - vertices[indices[i]]);
                    float up = Vector3.Cross(a, b).normalized.y;
                    int surface = up > 0.65f ? 1 : up < -0.85f ? 2 : 0;
                    triangles[surface].Add(indices[i]);
                    triangles[surface].Add(indices[i + 1]);
                    triangles[surface].Add(indices[i + 2]);
                }
                Mesh mesh = Instantiate(source);
                mesh.name = source.name + " Look Surfaces";
                mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i, false);
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { tunnelWall, room ? tunnelWalkway : tunnelRoad, tunnelCeiling };
                _generatedMeshes.Add(mesh);
                _lookSplits++;
            }
            ApplyLookLoopTiling(model);
            AuditLookRoad(model);
            return true;
        }

        /// <summary>中央道路の実三角形を調べ歩道の境界箱を重なりと誤認しないよう報告する</summary>
        /// <param name="model">材質を割り当てた構造物</param>
        private void AuditLookRoad(GameObject model)
        {
            if (_roadSurfaceAudited) return;
            MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>(false);
            MeshRenderer road = Array.Find(renderers, r => r.name.Equals("tunnel_floor", StringComparison.OrdinalIgnoreCase));
            if (road == null) return;
            _roadSurfaceAudited = true;
            /* 中央と前後の中心線で実際の描画三角形を直接照合する */
            for (int sample = -1; sample <= 1; sample++)
            {
                Vector3 point = road.bounds.center;
                int axis = road.bounds.size.x > road.bounds.size.z ? 0 : 2;
                point[axis] += sample * road.bounds.size[axis] * 0.25f;
                var hits = new List<(float height, string name, string material)>();
                foreach (MeshRenderer renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.TryGetComponent(out MeshFilter filter)) continue;
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    Vector3[] vertices = mesh.vertices;
                    Material[] materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < mesh.subMeshCount; slot++)
                    {
                        int[] triangles = mesh.GetTriangles(slot);
                        float highest = float.NegativeInfinity;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector3 a = renderer.transform.TransformPoint(vertices[triangles[i]]);
                            Vector3 b = renderer.transform.TransformPoint(vertices[triangles[i + 1]]);
                            Vector3 c = renderer.transform.TransformPoint(vertices[triangles[i + 2]]);
                            if (TryLookFloorHeight(point, a, b, c, out float y) && y <= point.y + 0.5f)
                                highest = Mathf.Max(highest, y);
                        }
                        if (float.IsNegativeInfinity(highest)) continue;
                        hits.Add((highest, renderer.name, slot < materials.Length && materials[slot] != null ? materials[slot].name : "missing"));
                    }
                }
                hits.Sort((a, b) => b.height.CompareTo(a.height));
                if (hits.Count == 0)
                {
                    Debug.LogWarning($"Tunnel road audit: {model.name}, centerline={point}, no readable upward triangle");
                    continue;
                }
                var top = hits[0];
                string layers = string.Join("; ", hits.ConvertAll(h => $"{h.name}/{h.material}@y={h.height:F3}"));
                bool overlap = hits.Count > 1 && Mathf.Abs(top.height - hits[1].height) < 0.005f;
                Debug.Log($"Tunnel road audit: {model.name}, centerline={point}, visible={top.name}/{top.material}, layers={layers}, coplanar={overlap}");
                if (overlap || top.material != tunnelRoad.name)
                    Debug.LogWarning($"Tunnel road receiver needs review: {model.name}, centerline={point}, {layers}");
            }
        }

        /// <summary>XZ平面の重心座標で上向き三角形の床高を求める</summary>
        /// <param name="point">確認する水平位置</param>
        /// <param name="a">頂点A</param>
        /// <param name="b">頂点B</param>
        /// <param name="c">頂点C</param>
        /// <param name="height">三角形上の高さ</param>
        /// <returns>水平位置が上向き面の内側ならtrue</returns>
        private static bool TryLookFloorHeight(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out float height)
        {
            height = 0f;
            Vector3 ab = b - a, ac = c - a, ap = point - a;
            if (Vector3.Cross(ab, ac).normalized.y < 0.6f) return false;
            float determinant = ab.x * ac.z - ab.z * ac.x;
            if (Mathf.Abs(determinant) < 0.000001f) return false;
            float u = (ap.x * ac.z - ap.z * ac.x) / determinant;
            float v = (ab.x * ap.z - ab.z * ap.x) / determinant;
            if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) return false;
            height = a.y + u * ab.y + v * ac.y;
            return true;
        }

        /// <summary>装飾を配置できる構造物の衝突面を登録する</summary>
        /// <param name="root">区間のルート</param>
        /// <param name="length">区間長</param>
        /// <param name="height">区間高</param>
        private void RegisterLookSection(Transform root, float length, float height)
        {
            foreach (var section in _lookSections) if (section.root == root) return;
            RemoveLookBounceLights(root.gameObject);
            _lookSections.Add((root, length, height, root.GetComponentsInChildren<Collider>(false)));
        }

        /// <summary>区間内で相互に接続された平行移動ループの両端を求める</summary>
        /// <param name="section">生成区間</param>
        /// <param name="minimum">入口のローカルZ</param>
        /// <param name="maximum">出口のローカルZ</param>
        /// <returns>水平な平行移動ループならtrue</returns>
        private static bool TryGetLookLoop(Transform section, out float minimum, out float maximum)
        {
            minimum = maximum = 0f;
            foreach (TunnelGateView gate in section.GetComponentsInChildren<TunnelGateView>(false))
            {
                if (gate.TargetGate == null || !gate.TargetGate.transform.IsChildOf(section)) continue;
                Vector3 a = section.InverseTransformPoint(gate.transform.position);
                Vector3 b = section.InverseTransformPoint(gate.TargetGate.transform.position);
                if (Mathf.Abs(a.x - b.x) > 0.05f || Mathf.Abs(a.y - b.y) > 0.05f || Mathf.Abs(a.z - b.z) < 4f) continue;
                minimum = Mathf.Min(a.z, b.z);
                maximum = Mathf.Max(a.z, b.z);
                return true;
            }
            return false;
        }

        /// <summary>ワールド投影の模様がポータル移動後も同じ位相になる周期へ微調整する</summary>
        /// <param name="model">材質を割り当てた区間</param>
        private void ApplyLookLoopTiling(GameObject model)
        {
            if (!TryGetLookLoop(model.transform, out float min, out float max)) return;
            Vector3 offset = model.transform.TransformVector(Vector3.forward * (max - min));
            if (Mathf.Abs(offset.y) > 0.05f || Mathf.Min(Mathf.Abs(offset.x), Mathf.Abs(offset.z)) > 0.05f)
            {
                Debug.LogWarning($"Tunnel loop world tiling skipped (non-axis-aligned): {model.name}, offset={offset}");
                return;
            }
            float period = offset.magnitude;
            float scale = Mathf.Max(1f, Mathf.Round(period * 0.4f)) / period;
            var block = new MaterialPropertyBlock();
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!_lookRenderers.Contains(renderer)) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat("_TexWorldScale", scale);
                renderer.SetPropertyBlock(block);
            }
            Debug.Log($"Tunnel loop texture period: {model.name}, length={period:F2}m, world UV scale={scale:F5}, repeats={period * scale:F0}");
        }

        /// <summary>境界近傍の器具を一周期先へ複製して光の減衰を連続させる</summary>
        /// <param name="fixture">周期内の元器具</param>
        /// <param name="section">元区間</param>
        /// <param name="index">周期内の器具番号</param>
        /// <param name="offset">一周期のワールド移動量</param>
        private void CreateLookFixtureImage(GameObject fixture, Transform section, int index, Vector3 offset)
        {
            GameObject image = Instantiate(fixture, fixture.transform.parent, true);
            image.name = fixture.name + " Periodic Image";
            image.transform.position = fixture.transform.position + offset;
            ConfigureLookFixtureFault(image, section, index);
            foreach (Light light in image.GetComponentsInChildren<Light>(true))
            {
                if (TunnelLookLighting.IsCeilingBounce(light)) continue;
                _ceilingLights.Add((light, LightShadows.Soft, 1f));
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>配置順序と区間座標から安定した局所シードを得る</summary>
        /// <param name="root">区間</param>
        /// <param name="salt">用途別の識別子</param>
        /// <returns>グローバル乱数に影響しないシード</returns>
        private int LookSeed(Transform root, int salt)
        {
            Vector3 position = _geometryRoot != null ? _geometryRoot.InverseTransformPoint(root.position) : root.position;
            unchecked
            {
                int seed = (_generator != null ? _generator.Seed : 2777) ^ salt;
                seed = seed * 397 ^ Mathf.RoundToInt(position.x * 100f);
                seed = seed * 397 ^ Mathf.RoundToInt(position.y * 100f);
                seed = seed * 397 ^ Mathf.RoundToInt(position.z * 100f);
                foreach (char c in root.name) seed = seed * 31 + c;
                return seed;
            }
        }

        /// <summary>器具の物理照明と決定的な故障状態を設定する</summary>
        /// <param name="fixture">生成した器具</param>
        /// <param name="section">区間</param>
        /// <param name="index">器具番号</param>
        /// <param name="spacing">ループ周期に合わせた設置間隔</param>
        private void ConfigureLookFixture(GameObject fixture, Transform section, int index, float spacing)
        {
            RemoveLookBounceLights(fixture);
            Collider[] surfaces = section.GetComponentsInChildren<Collider>(false);
            Renderer[] meshes = section.GetComponentsInChildren<Renderer>(false);
            bool fluorescent = fixture.name.IndexOf("fluorescent", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (Light light in fixture.GetComponentsInChildren<Light>(true))
            {
                if (TunnelLookLighting.IsCeilingBounce(light)) continue;
                /* SaveStageの既存配光は12mの光学基準で校正されているため配置間隔とは分離する */
                string calibration = TunnelLookLighting.ConfigureFixture(light, surfaces, meshes, fluorescent, fluorescent ? 12f : spacing);
                if (index == 0 || calibration.Contains("UNMEASURED"))
                    Debug.Log($"Tunnel runtime fixture: {section.name} / {fixture.name} / {light.name}, {calibration}");
            }
            ConfigureLookFixtureFault(fixture, section, index);
        }

        /// <summary>周期内の器具番号から同じ故障状態と点滅位相を再現する</summary>
        /// <param name="fixture">器具本体または周期鏡像</param>
        /// <param name="section">元の区間</param>
        /// <param name="index">周期内の器具番号</param>
        private void ConfigureLookFixtureFault(GameObject fixture, Transform section, int index)
        {
            int seed = LookSeed(section, index + 3719);
            double roll = new System.Random(seed).NextDouble();
            float dead = _generator != null ? _generator.DeadFixtureFraction : 0f;
            float flicker = _generator != null ? _generator.FlickerFixtureFraction : 0f;
            if (roll >= dead + flicker) return;
            bool isDead = roll < dead;
            if (!fixture.TryGetComponent(out TunnelFixtureFlicker condition)) condition = fixture.AddComponent<TunnelFixtureFlicker>();
            condition.Initialize(seed, isDead);
            if (isDead) _lookDead++; else _lookFlicker++;
        }

        /// <summary>旧テンプレートの補助光を即時に消灯し階層から取り除く</summary>
        /// <param name="root">生成した構造物または器具</param>
        private void RemoveLookBounceLights(GameObject root)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (!TunnelLookLighting.IsCeilingBounce(light) || !light.gameObject.activeSelf) continue;
                light.enabled = false;
                Debug.Log($"Tunnel fake GI light removed at runtime: {root.name} / {light.name}");
                if (light.gameObject == root)
                {
                    /* ルートを破棄せず補助光だけを停止する */
                    continue;
                }
                light.gameObject.SetActive(false);
                Destroy(light.gameObject);
            }
        }

        /// <summary>各区間へ順番に投影を配分して総数と描画距離を制限する</summary>
        private void BuildTunnelWeathering()
        {
            if (_weatheringBuilt) return;
            _weatheringBuilt = true;
            Material[] materials = { Resources.Load<Material>("TunnelLook/Puddle"), Resources.Load<Material>("TunnelLook/Leak"),
                Resources.Load<Material>("TunnelLook/Grime"), Resources.Load<Material>("TunnelLook/Seepage") };
            int budget = _generator != null ? _generator.MaxTunnelDecals : 96;
            float density = _generator != null ? _generator.DecalDensity : 0.08f;
            int placed = 0, missed = 0;
            Physics.SyncTransforms();
            /* 全区間の一つ目を先に配置して最初のトンネルだけで予算を消費しない */
            for (int ordinal = 0; ordinal < 16 && placed < budget; ordinal++)
            {
                foreach (var section in _lookSections)
                {
                    if (placed >= budget) break;
                    if (section.root == null || ordinal >= Mathf.Min(16, Mathf.FloorToInt(section.length * density))) continue;
                    int kind = ordinal % 4;
                    if (materials[kind] == null) { missed++; continue; }
                    var random = new System.Random(LookSeed(section.root, ordinal + 17041));
                    float z = ((float)random.NextDouble() - 0.5f) * Mathf.Max(0f, section.length - 3f);
                    float top = section.root.position.y + section.height;
                    float bottom = section.root.position.y;
                    foreach (Collider surface in section.surfaces)
                    {
                        if (surface == null || !surface.enabled) continue;
                        top = Mathf.Max(top, surface.bounds.max.y);
                        bottom = Mathf.Min(bottom, surface.bounds.min.y);
                    }
                    Vector3 origin = section.root.TransformPoint(new Vector3(0f, 0f, z));
                    origin.y = top + 1f;
                    float rayDistance = top - bottom + 2f;
                    if (!RaycastFloor(section.surfaces, origin, rayDistance, out RaycastHit centerFloor)) { missed++; continue; }
                    float height = Mathf.Min(section.height * 0.5f, 2f);
                    RaycastHit hit;
                    if (kind == 0)
                    {
                        /* 近傍の床を複数測り最も低い実面を水溜まりの中心にする */
                        bool found = false;
                        hit = default;
                        for (int sample = 0; sample < 4; sample++)
                        {
                            float sign = (ordinal / 4) % 2 == 0 ? -1f : 1f;
                            float width = 2f;
                            if (RaycastStructure(section.surfaces, centerFloor.point + Vector3.up, section.root.right * sign, 25f, out RaycastHit wall)) width = wall.distance;
                            bool walkway = (ordinal / 4) % 2 == 1;
                            float fraction = walkway ? 0.85f + (float)random.NextDouble() * 0.08f : (float)random.NextDouble() * 0.65f;
                            Vector3 start = origin + section.root.right * (sign * width * fraction);
                            if (!RaycastFloor(section.surfaces, start, rayDistance, out RaycastHit floor) || floor.normal.y < 0.6f) continue;
                            if (!found || floor.point.y < hit.point.y) { hit = floor; found = true; }
                        }
                        if (!found) { missed++; continue; }
                    }
                    else
                    {
                        /* 漏水は器具間隔に揃え汚れは床から低い壁面へ寄せる */
                        if (kind == 1)
                        {
                            float spacing = _generator != null ? _generator.CeilingLightSpacing : 12f;
                            z = Mathf.Clamp(Mathf.Round(z / spacing) * spacing, -section.length * 0.5f + 1f, section.length * 0.5f - 1f);
                        }
                        origin = section.root.TransformPoint(new Vector3(0f, 0f, z));
                        origin.y = top + 1f;
                        if (!RaycastFloor(section.surfaces, origin, rayDistance, out centerFloor)) { missed++; continue; }
                        origin = centerFloor.point + Vector3.up * (kind == 2 ? 0.45f : height);
                        Vector3 side = section.root.right * (random.NextDouble() < 0.5 ? -1f : 1f);
                        if (!RaycastStructure(section.surfaces, origin, side, 25f, out hit) || Mathf.Abs(hit.normal.y) > 0.8f) { missed++; continue; }
                    }
                    /* 局所霧の半幅3mとデカールの半幅を含む余白を両端で確保する */
                    if (TryGetLookLoop(section.root, out float loopMin, out float loopMax))
                    {
                        float hitZ = section.root.InverseTransformPoint(hit.point).z;
                        if (Mathf.Abs(hitZ - loopMin) < 4.5f || Mathf.Abs(hitZ - loopMax) < 4.5f)
                        {
                            missed++;
                            continue;
                        }
                    }
                    var decalObject = new GameObject("Tunnel " + materials[kind].name + " " + ordinal);
                    decalObject.layer = hit.collider.gameObject.layer;
                    decalObject.transform.SetParent(section.root, true);
                    decalObject.transform.position = hit.point + hit.normal * 0.03f;
                    decalObject.transform.rotation = Quaternion.LookRotation(-hit.normal, kind == 0 ? section.root.forward : Vector3.up);
                    var projector = decalObject.AddComponent<DecalProjector>();
                    projector.material = materials[kind];
                    float scale = 0.7f + (float)random.NextDouble() * 0.6f;
                    projector.size = kind == 0 ? new Vector3(2.4f * scale, 3f * scale, 0.6f)
                        : new Vector3((kind == 1 ? 0.8f : 1.6f) * scale, (kind == 2 ? 0.65f : 2.2f) * scale, 1.0f);
                    /* +Zを面内へ向け投影体積の中心を衝突面に一致させる */
                    projector.pivot = new Vector3(0f, 0f, 0.03f);
                    projector.scaleMode = DecalScaleMode.ScaleInvariant;
                    projector.fadeFactor = 1f;
                    projector.drawDistance = _generator != null ? _generator.DecalFadeDistance : 35f;
                    projector.fadeScale = 0.8f;
                    projector.affectsTransparency = false;
                    if (kind == 0) CreateGroundMoisture(section.root, hit);
                    if (placed < 4) Debug.Log($"Tunnel decal placement: {section.root.name} / {decalObject.name}, receiver={hit.collider.name}, hit={hit.point}, forward dot normal={Vector3.Dot(decalObject.transform.forward, hit.normal):F2}, size={projector.size}, volume center={decalObject.transform.TransformPoint(projector.pivot)}");
                    placed++;
                }
            }
            Debug.Log($"Tunnel look summary: renderers={_lookRenderers.Count}, split meshes={_lookSplits}, unknown surfaces={_lookUnknown}, sections={_lookSections.Count}, decals={placed}/{budget}, skipped placements={missed}, dead fixtures={_lookDead}, flicker fixtures={_lookFlicker}, ground fog={_lookFog}, seed={(_generator != null ? _generator.Seed : 2777)}");
            foreach (Material material in materials) if (material == null) { Debug.LogWarning("Tunnel decal resources missing: run Shinzui/LookDev/2 Build Decal Materials"); break; }
        }

        /// <summary>湿った床の近くに薄い局所霧を置き階層ごとの床高へ追従させる</summary>
        /// <param name="root">区間</param>
        /// <param name="floor">床の衝突面</param>
        private void CreateGroundMoisture(Transform root, RaycastHit floor)
        {
            int limit = _generator != null ? _generator.MaxGroundFogPatches : 16;
            if (_lookFog >= limit) return;
            var holder = new GameObject("Ground Moisture");
            holder.transform.SetParent(root, true);
            holder.transform.position = floor.point + Vector3.up * 0.3f;
            holder.transform.rotation = Quaternion.FromToRotation(Vector3.up, floor.normal) * root.rotation;
            var fog = holder.AddComponent<LocalVolumetricFog>();
            var parameters = fog.parameters;
            parameters.albedo = new Color(0.75f, 0.79f, 0.82f);
            parameters.meanFreePath = 35f;
            parameters.size = new Vector3(4f, 0.7f, 6f);
            parameters.positiveFade = new Vector3(0.3f, 0.7f, 0.3f);
            parameters.negativeFade = new Vector3(0.3f, 0.1f, 0.3f);
            parameters.distanceFadeStart = 25f;
            parameters.distanceFadeEnd = 40f;
            fog.parameters = parameters;
            _lookFog++;
        }

        /// <summary>天井を通過しシェル底面より実際の道路と歩道を優先する</summary>
        /// <param name="surfaces">区間の構造物</param>
        /// <param name="origin">構造物より高い始点</param>
        /// <param name="distance">探索深さ</param>
        /// <param name="floor">描画される床上面</param>
        /// <returns>床を見つけた場合はtrue</returns>
        private static bool RaycastFloor(Collider[] surfaces, Vector3 origin, float distance, out RaycastHit floor)
        {
            floor = default;
            bool found = false;
            bool namedFloorFound = false;
            foreach (Collider surface in surfaces)
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger) continue;
                string name = surface.name.ToLowerInvariant();
                if (name.Contains("ceiling") || name.Contains("header")) continue;
                bool namedFloor = name.Contains("floor") || name.Contains("road") || name.Contains("walkway");
                Vector3 start = origin;
                float remaining = distance;
                /* 複合メッシュの屋根に当たった場合もその直下から床まで調べる */
                for (int layer = 0; layer < 4 && remaining > 0f; layer++)
                {
                    if (!surface.Raycast(new Ray(start, Vector3.down), out RaycastHit hit, remaining)) break;
                    /* 名前付き床同士は上面を選び複合シェルだけの場合は屋根を避けて底面を選ぶ */
                    if (hit.normal.y > 0.6f && (!found || namedFloor && !namedFloorFound
                        || namedFloor == namedFloorFound && (namedFloor ? hit.point.y > floor.point.y : hit.point.y < floor.point.y)))
                    {
                        floor = hit;
                        found = true;
                        namedFloorFound = namedFloor;
                    }
                    float step = hit.distance + 0.02f;
                    start += Vector3.down * step;
                    remaining -= step;
                }
            }
            return found;
        }

        /// <summary>登録した構造物だけを対象に最短の衝突面を探す</summary>
        /// <param name="surfaces">区間の構造物</param>
        /// <param name="origin">始点</param>
        /// <param name="direction">照射方向</param>
        /// <param name="distance">最大距離</param>
        /// <param name="nearest">最も近い衝突</param>
        /// <returns>衝突面を得た場合はtrue</returns>
        private static bool RaycastStructure(Collider[] surfaces, Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            var ray = new Ray(origin, direction);
            foreach (Collider surface in surfaces)
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger) continue;
                if (!surface.Raycast(ray, out RaycastHit hit, distance)) continue;
                if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
            }
            return found;
        }
    }
}
