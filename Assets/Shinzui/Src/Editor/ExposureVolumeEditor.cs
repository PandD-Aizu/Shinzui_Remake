using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Shinzui.Infrastructure.Rendering.Exposure;

namespace Shinzui.Editor.Rendering.Exposure
{
    [CustomEditor(typeof(ExposureVolume))]
    public class ExposureVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_Mode;
        private SerializedDataParameter m_Filtering;
        private SerializedDataParameter m_MinLuminance;
        private SerializedDataParameter m_MaxLuminance;
        private SerializedDataParameter m_ExposureCompensation;
        private SerializedDataParameter m_EyeAdaptation;
        private SerializedDataParameter m_SpeedUp;
        private SerializedDataParameter m_SpeedDown;
        private SerializedDataParameter m_PortalExposureCompensation;

        public override void OnEnable()
        {
            m_Mode = Unpack(serializedObject.FindProperty("mode"));
            m_Filtering = Unpack(serializedObject.FindProperty("filtering"));
            m_MinLuminance = Unpack(serializedObject.FindProperty("minLuminance"));
            m_MaxLuminance = Unpack(serializedObject.FindProperty("maxLuminance"));
            m_ExposureCompensation = Unpack(serializedObject.FindProperty("exposureCompensation"));
            m_EyeAdaptation = Unpack(serializedObject.FindProperty("eyeAdaptation"));
            m_SpeedUp = Unpack(serializedObject.FindProperty("speedUp"));
            m_SpeedDown = Unpack(serializedObject.FindProperty("speedDown"));
            m_PortalExposureCompensation = Unpack(serializedObject.FindProperty("portalExposureCompensation"));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_Mode);

            // Filteringのカスタム二点スライダー描画
            DrawFilteringField();

            // Min/Max Luminanceのカスタム描画 (スライダー + 目盛り + アイコン + 下部数値入力)
            DrawLuminanceField("Min Luminance", m_MinLuminance, -10.0f, 20.0f);
            DrawLuminanceField("Max Luminance", m_MaxLuminance, -10.0f, 20.0f);

            PropertyField(m_ExposureCompensation);
            PropertyField(m_EyeAdaptation);
            PropertyField(m_SpeedUp);
            PropertyField(m_SpeedDown);
            PropertyField(m_PortalExposureCompensation);
        }

        private void DrawFilteringField()
        {
            if (m_Filtering == null) return;

            SerializedProperty valueProp = m_Filtering.value;
            SerializedProperty overrideProp = m_Filtering.overrideState;

            // 水平レイアウトでオーバーライドトグル、ラベル、二点スライダー、数値を描画
            EditorGUILayout.BeginHorizontal();

            // オーバーライドトグル
            bool overriden = EditorGUILayout.ToggleLeft("", overrideProp.boolValue, GUILayout.Width(15));
            overrideProp.boolValue = overriden;

            // トグルが無効な場合はGUIをグレーアウト
            using (new EditorGUI.DisabledScope(!overriden))
            {
                EditorGUILayout.PrefixLabel("Filtering");

                Vector2 val = valueProp.vector2Value;
                float minVal = val.x;
                float maxVal = val.y;

                EditorGUI.BeginChangeCheck();

                // 左側の数値入力
                minVal = EditorGUILayout.DelayedFloatField((float)System.Math.Round(minVal, 1), GUILayout.Width(35));
                // 二点スライダー
                EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, 0.0f, 100.0f);
                // 右側の数値入力
                maxVal = EditorGUILayout.DelayedFloatField((float)System.Math.Round(maxVal, 1), GUILayout.Width(35));

                if (EditorGUI.EndChangeCheck())
                {
                    minVal = Mathf.Clamp(minVal, 0.0f, maxVal);
                    maxVal = Mathf.Clamp(maxVal, minVal, 100.0f);
                    valueProp.vector2Value = new Vector2(minVal, maxVal);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLuminanceField(string label, SerializedDataParameter parameter, float minLimit, float maxLimit)
        {
            if (parameter == null) return;

            SerializedProperty valueProp = parameter.value;
            SerializedProperty overrideProp = parameter.overrideState;

            EditorGUILayout.BeginVertical();

            // 1行目: トグル + ラベル + スライダー + アイコン
            EditorGUILayout.BeginHorizontal();

            bool overriden = EditorGUILayout.ToggleLeft("", overrideProp.boolValue, GUILayout.Width(15));
            overrideProp.boolValue = overriden;

            using (new EditorGUI.DisabledScope(!overriden))
            {
                EditorGUILayout.PrefixLabel(label);

                float val = valueProp.floatValue;

                // スライダーのRectを取得して自前で描画
                Rect sliderRect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));

                // スライダー上に目盛りを描画
                if (Event.current.type == EventType.Repaint)
                {
                    int numTicks = 5;
                    for (int i = 0; i <= numTicks; i++)
                    {
                        float t = (float)i / numTicks;
                        float x = Mathf.Lerp(sliderRect.x + 5, sliderRect.xMax - 5, t);
                        Rect tickRect = new Rect(x - 1, sliderRect.y + 4, 2, 2);
                        EditorGUI.DrawRect(tickRect, new Color(0.7f, 0.7f, 0.7f, 0.6f));
                    }
                }

                EditorGUI.BeginChangeCheck();
                val = GUI.HorizontalSlider(sliderRect, val, minLimit, maxLimit);
                if (EditorGUI.EndChangeCheck())
                {
                    valueProp.floatValue = val;
                }

                // 右端に値に応じたアイコンを表示
                GUILayout.Label(GetLuminanceIcon(val), GUILayout.Width(20));
            }
            EditorGUILayout.EndHorizontal();

            // 2行目: 数値入力フィールド (トグルとラベル分のインデント)
            using (new EditorGUI.DisabledScope(!overriden))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(140); // インデント用スペース

                float val2 = valueProp.floatValue;
                EditorGUI.BeginChangeCheck();

                val2 = EditorGUILayout.FloatField(val2);

                if (EditorGUI.EndChangeCheck())
                {
                    valueProp.floatValue = val2;
                }

                GUILayout.Space(25); // 右端スペース

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        private string GetLuminanceIcon(float ev)
        {
            if (ev < -4.0f)
                return "🌙"; // 夜/月
            else if (ev < -1.0f)
                return "⛅"; // 雲と太陽
            else if (ev < 4.0f)
                return "☁️"; // 曇り/雲
            else if (ev < 9.0f)
                return "☀️"; // 晴れ/太陽
            else
                return "😎"; // 非常に明るい
        }
    }
}
