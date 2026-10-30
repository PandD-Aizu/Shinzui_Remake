using Shinzui.Infrastructure.Lighting;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor.Lighting
{
    /// <summary>
    /// HDRP標準Lightインスペクターと独立して既存の物理強度データを編集する
    /// </summary>
    [CustomEditor(typeof(PhysicalLight))]
    [CanEditMultipleObjects]
    public class PhysicalLightEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 物理強度を編集し変更をHDRPライトへ反映する
        /// </summary>
        public override void OnInspectorGUI()
        {
            // HDRP固有の影や形状は標準Lightインスペクターで編集する
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            bool sync = GUILayout.Button("Lightの強度を取り込む");
            if (!changed && !sync) return;

            foreach (Object item in targets)
            {
                var data = (PhysicalLight)item;
                var light = data.GetComponent<Light>();
                Undo.RecordObjects(new Object[] { data, light }, "Edit Physical Light");
                if (sync) data.SyncFromLight();
                else data.UpdateIntensityFromPhysical();
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(light);
            }
        }
    }
}
