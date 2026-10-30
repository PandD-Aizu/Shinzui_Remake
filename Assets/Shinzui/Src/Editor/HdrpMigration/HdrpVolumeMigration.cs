using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Shinzui.Infrastructure.Rendering;
using Shinzui.Infrastructure.Rendering.Scanline;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>消失したURPのVolume値を同じプロファイル内のHDRP成分へ移す</summary>
    internal static class HdrpVolumeMigration
    {
        private static readonly string[] Supported = { "Bloom", "Tonemapping", "ColorAdjustments", "WhiteBalance",
            "ChannelMixer", "LiftGammaGain", "ShadowsMidtonesHighlights", "SplitToning", "Vignette", "FilmGrain",
            "DepthOfField", "MotionBlur", "ChromaticAberration", "LensDistortion", "PaniniProjection", "ColorLookup", "ColorCurves" };

        /// <summary>プロファイルのGUIDと既存HDRP成分を保ったまま欠落成分を置き換える</summary>
        /// <param name="path">プロファイルパス</param>
        /// <returns>変換した成分数</returns>
        internal static int Migrate(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null || !File.Exists(path)) return 0;
            int converted = 0, skipped = 0;
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            /* VolumeProfile.Addの型検査より先に欠落参照を除く */
            int missing = profile.components.RemoveAll(c => c == null);
            foreach (string block in Regex.Split(text, @"(?m)^--- !u!"))
            {
                string name = Scalar(block, "m_Name");
                bool fog = name == "FPVolumetricFogVolume" || name == "VolumetricFogVolume";
                if (!Supported.Contains(name) && !fog) continue;
                Type type = typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition." + (fog ? "Fog" : name));
                if (type == null || profile.components.Any(c => c != null && c.GetType() == type)) continue;
                var component = profile.Add(type, false);
                component.name = fog ? "Fog" : name;
                component.active = Scalar(block, "active") != "0";
                var serialized = new SerializedObject(component);

                /* パラメータ名が一致する値だけを転記し未知の上書きはパス付きで報告する */
                foreach (Match parameter in Regex.Matches(block, @"(?m)^  (\w+):\n((?:[ \t]{4,}[^\n]*\n?)*)"))
                {
                    string field = parameter.Groups[1].Value;
                    string valueBlock = parameter.Groups[2].Value;
                    if (!valueBlock.Contains("m_OverrideState:")) continue;
                    bool overridden = Regex.IsMatch(valueBlock, @"m_OverrideState:\s*1");
                    if (name == "DepthOfField" && (field == "mode" || field.StartsWith("gaussian"))) continue;
                    if (fog && field == "fogAttenuationDistance") field = "meanFreePath";
                    SerializedProperty target = serialized.FindProperty(field);
                    SerializedProperty value = target?.FindPropertyRelative("m_Value");
                    if (value == null || !SetValue(value, valueBlock))
                    {
                        if (overridden)
                        {
                            Debug.LogWarning($"HDRP volume override REVIEW: {path} / {name}.{field}");
                            skipped++;
                        }
                        continue;
                    }
                    target.FindPropertyRelative("m_OverrideState").boolValue = overridden;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (component is DepthOfField depth) MapDepthOfField(depth, block, path);
                if (component is Fog hdFog)
                {
                    hdFog.enabled.Override(true);
                    hdFog.enableVolumetricFog.Override(true);
                    Debug.Log($"HDRP fog: {path}, global meanFreePath={hdFog.meanFreePath.value}, no local region inferred");
                }
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
                converted++;
            }

            foreach (string block in Regex.Split(text, @"(?m)^--- !u!"))
            {
                string name = Scalar(block, "m_Name");
                if (!block.Contains("  active:") || Supported.Contains(name) || name == "FPVolumetricFogVolume" || name == "VolumetricFogVolume") continue;
                if (profile.components.Any(c => c != null && (c.name == name || c.GetType().Name == name))) continue;
                skipped++;
                Debug.LogWarning($"HDRP missing/unsupported volume component dropped from profile list: {path} / {name}");
            }

            /* 通常画面のCRT効果を無効化する */
            if (profile.TryGet(out PixelationVolume pixel))
            {
                pixel.intensity.Override(0);
                EditorUtility.SetDirty(pixel);
            }
            if (profile.TryGet(out ScanlineVolume scanline))
            {
                scanline.strength.Override(0);
                scanline.curvature.Override(0);
                scanline.fineLines.Override(0);
                scanline.vignette.Override(0);
                scanline.flicker.Override(0);
                EditorUtility.SetDirty(scanline);
            }
            EditorUtility.SetDirty(profile);
            /* 値を保存してから参照の外れた欠落サブアセットを除去する */
            AssetDatabase.SaveAssetIfDirty(profile);
            int orphaned = AssetDatabase.RemoveScriptableObjectsWithMissingScript(path);
            if (converted > 0 || missing > 0 || orphaned > 0)
                Debug.Log($"HDRP volume: {path}, converted={converted}, missing references removed={missing}, missing subassets removed={orphaned}, unsupported overrides={skipped}, GUID preserved");
            return converted;
        }

        /// <summary>保存行から単純な文字列を取得する</summary>
        /// <param name="text">保存テキスト</param>
        /// <param name="field">項目名</param>
        /// <returns>値または空文字</returns>
        internal static string Scalar(string text, string field) => Regex.Match(text,
            @"(?m)^\s{2}" + Regex.Escape(field) + @":\s*([^\r\n]*)").Groups[1].Value.Trim();

        /// <summary>数値、色、ベクトルとアセット参照を転記する</summary>
        /// <param name="target">保存先</param>
        /// <param name="block">旧パラメータ</param>
        /// <returns>対応する値を転記したか</returns>
        private static bool SetValue(SerializedProperty target, string block)
        {
            Match match = Regex.Match(block, @"m_Value:\s*([^\n]*)");
            string value = match.Groups[1].Value.Trim();
            switch (target.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) return false;
                    target.floatValue = number; return true;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    if (!int.TryParse(value, out int integer)) return false;
                    target.intValue = integer; return true;
                case SerializedPropertyType.Boolean:
                    if (value != "0" && value != "1") return false;
                    target.boolValue = value == "1"; return true;
                case SerializedPropertyType.Color:
                    if (!block.Contains("r:")) return false;
                    target.colorValue = new Color(Number(block, "r"), Number(block, "g"), Number(block, "b"), Number(block, "a", 1)); return true;
                case SerializedPropertyType.Vector2:
                    target.vector2Value = new Vector2(Number(block, "x"), Number(block, "y")); return true;
                case SerializedPropertyType.Vector3:
                    target.vector3Value = new Vector3(Number(block, "x"), Number(block, "y"), Number(block, "z")); return true;
                case SerializedPropertyType.Vector4:
                    target.vector4Value = new Vector4(Number(block, "x"), Number(block, "y"), Number(block, "z"), Number(block, "w")); return true;
                case SerializedPropertyType.ObjectReference:
                    string guid = Regex.Match(value, @"guid:\s*([a-f0-9]+)").Groups[1].Value;
                    if (guid.Length == 0) { target.objectReferenceValue = null; return value.Contains("fileID: 0"); }
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    long.TryParse(Regex.Match(value, @"fileID:\s*(-?\d+)").Groups[1].Value, out long fileId);
                    target.objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(o => o != null &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string _, out long id) && id == fileId);
                    return target.objectReferenceValue != null;
                case SerializedPropertyType.Generic:
                    /* TextureCurveのキーとラップ設定をそのまま転記する */
                    var curve = target.FindPropertyRelative("m_Curve");
                    if (curve == null || curve.propertyType != SerializedPropertyType.AnimationCurve) return false;
                    var keys = Regex.Matches(block, @"(?m)^\s*- serializedVersion: 3\n((?:\s{10,}[^\n]*\n?)*)")
                        .Cast<Match>().Select(key => new Keyframe(Number(key.Value, "time"), Number(key.Value, "value"),
                            Number(key.Value, "inSlope"), Number(key.Value, "outSlope"),
                            Number(key.Value, "inWeight"), Number(key.Value, "outWeight"))
                        { weightedMode = (WeightedMode)(int)Number(key.Value, "weightedMode") }).ToArray();
                    curve.animationCurveValue = new AnimationCurve(keys)
                    {
                        preWrapMode = (WrapMode)(int)Number(block, "m_PreInfinity", 2),
                        postWrapMode = (WrapMode)(int)Number(block, "m_PostInfinity", 2)
                    };
                    target.FindPropertyRelative("m_Loop").boolValue = Number(block, "m_Loop") != 0;
                    target.FindPropertyRelative("m_ZeroValue").floatValue = Number(block, "m_ZeroValue");
                    target.FindPropertyRelative("m_Range").floatValue = Number(block, "m_Range", 1);
                    var length = target.FindPropertyRelative("<length>k__BackingField");
                    if (length != null) length.intValue = keys.Length;
                    return true;
                default: return false;
            }
        }

        /// <summary>色やベクトル内の数値を読み取る</summary>
        /// <param name="text">保存テキスト</param>
        /// <param name="key">要素名</param>
        /// <param name="fallback">未指定値</param>
        /// <returns>数値</returns>
        private static float Number(string text, string key, float fallback = 0)
        {
            string value = Regex.Match(text, @"\b" + key + @":\s*([-+\d.eE]+)").Groups[1].Value;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;
        }

        /// <summary>URPのぼけ範囲をHDRPの手動焦点範囲へ近似する</summary>
        /// <param name="depth">更新先</param>
        /// <param name="block">旧設定</param>
        /// <param name="path">診断パス</param>
        private static void MapDepthOfField(DepthOfField depth, string block, string path)
        {
            Match mode = Regex.Match(block, @"(?m)^  mode:\n((?: {4,}[^\n]*\n?)*)");
            float oldMode = Number(mode.Value, "m_Value");
            bool overridden = mode.Value.Contains("m_OverrideState: 1");
            depth.focusMode.value = oldMode == 0 ? DepthOfFieldMode.Off : DepthOfFieldMode.Manual;
            depth.focusMode.overrideState = overridden;
            if (oldMode == 0) return;
            float focus = depth.focusDistance.value;
            depth.nearFocusStart.Override(oldMode == 1 ? 0 : Mathf.Max(0, focus * .25f));
            depth.nearFocusEnd.Override(oldMode == 1 ? 0 : Mathf.Max(.01f, focus * .5f));
            float start = Number(Regex.Match(block, @"(?m)^  gaussianStart:\n((?: {4,}[^\n]*\n?)*)").Value, "m_Value", focus);
            float end = Number(Regex.Match(block, @"(?m)^  gaussianEnd:\n((?: {4,}[^\n]*\n?)*)").Value, "m_Value", focus * 2);
            depth.farFocusStart.Override(oldMode == 1 ? start : focus * 1.5f);
            depth.farFocusEnd.Override(oldMode == 1 ? Mathf.Max(end, start + .01f) : focus * 3);
            Debug.LogWarning($"HDRP DoF approximated with manual ranges: {path}; inspect blur radius and bokeh");
        }
    }
}
