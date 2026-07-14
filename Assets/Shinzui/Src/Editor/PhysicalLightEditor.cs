#pragma warning disable CS0618 // CustomEditorForRenderPipelineは旧形式との警告を抑制

using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Shinzui.Infrastructure.Lighting;

namespace Shinzui.Editor.Lighting
{
    [CustomEditorForRenderPipeline(typeof(Light), typeof(UniversalRenderPipelineAsset))]
    [CanEditMultipleObjects]
    public class PhysicalLightEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            Light light = (Light)target;
            if (light == null) return;

            foreach (var t in targets)
            {
                GetOrCreatePhysicalLight((Light)t);
            }

            // 各ライトに PhysicalLight を自動アタッチ (非表示設定)
            PhysicalLight data = GetOrCreatePhysicalLight(light);
            data.EnsureValidUnitForCurrentLight();

            // More Options のトグル描画
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            bool moreOptions = EditorGUILayout.ToggleLeft("More Options", data.MoreOptions, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle More Options");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.MoreOptions = moreOptions;
                    EditorUtility.SetDirty(pl);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Light Type の描画
            EditorGUI.BeginChangeCheck();
            LightType type = (LightType)EditorGUILayout.EnumPopup("Light Type", light.type);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Light Type");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.type = type;
                    EditorUtility.SetDirty(l);
                    
                    var pl = GetOrCreatePhysicalLight(l);
                    pl.EnsureValidUnitForCurrentLight();
                    pl.UpdateIntensityFromPhysical();
                    EditorUtility.SetDirty(pl);
                }
            }
            EditorGUILayout.Space(5);

            // Color Temperature / Color の描画
            DrawColorTemperatureSection(light, data);
            EditorGUILayout.Space(5);

            // Intensity（物理単位連動）の描画
            DrawPhysicalIntensitySection(light, data);
            EditorGUILayout.Space(5);

            // Range の描画（Directional以外）
            if (light.type != LightType.Directional)
            {
                EditorGUI.BeginChangeCheck();
                float range = EditorGUILayout.FloatField("Range", light.range);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Light Range");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.range = Mathf.Max(range, 0.0f);
                        EditorUtility.SetDirty(l);
                    }
                }
                EditorGUILayout.Space(5);
            }

            if (light.type == LightType.Spot)
            {
                DrawSpotShapeSection(light);
                EditorGUILayout.Space(5);
            }

            // Indirect Multiplier
            EditorGUI.BeginChangeCheck();
            float bounce = EditorGUILayout.FloatField("Indirect Multiplier", light.bounceIntensity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Change Indirect Multiplier");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.bounceIntensity = Mathf.Max(bounce, 0.0f);
                    EditorUtility.SetDirty(l);
                }
            }
            EditorGUILayout.Space(5);

            // Cookie (Spot, Area, Directional, Pointで表示)
            if (light.type != LightType.Rectangle && light.type != LightType.Disc)
            {
                EditorGUI.BeginChangeCheck();
                Texture cookie = (Texture)EditorGUILayout.ObjectField("Cookie", light.cookie, typeof(Texture), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Light Cookie");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.cookie = cookie;
                        EditorUtility.SetDirty(l);
                    }
                }

                if (light.type == LightType.Directional && light.cookie != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float cookieSize = EditorGUILayout.FloatField("Cookie Size", light.cookieSize);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObjects(targets, "Change Cookie Size");
                        foreach (var t in targets)
                        {
                            Light l = (Light)t;
                            l.cookieSize = Mathf.Max(cookieSize, 0.0f);
                            EditorUtility.SetDirty(l);
                        }
                    }
                }
                EditorGUILayout.Space(5);
            }

            // IES Profile & Cutoff (Point, Spot, Areaで表示)
            if (light.type != LightType.Directional)
            {
                EditorGUI.BeginChangeCheck();
                Texture2D iesProfile = (Texture2D)EditorGUILayout.ObjectField("IES Profile", data.IesProfile, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Change IES Profile");
                    foreach (var t in targets)
                    {
                        var pl = GetOrCreatePhysicalLight((Light)t);
                        pl.IesProfile = iesProfile;
                        EditorUtility.SetDirty(pl);
                    }
                }

                if (data.IesProfile != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float iesCutoff = EditorGUILayout.Slider("IES Cutoff Angle (%)", data.IesCutoffAngle, 0.0f, 100.0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObjects(GetUndoTargets(), "Change IES Cutoff Angle");
                        foreach (var t in targets)
                        {
                            var pl = GetOrCreatePhysicalLight((Light)t);
                            pl.IesCutoffAngle = iesCutoff;
                            EditorUtility.SetDirty(pl);
                        }
                    }
                }
                EditorGUILayout.Space(5);
            }

            // Shadows セクション
            DrawShadowsSection(light);
            EditorGUILayout.Space(5);

            // More Options がONのときの項目
            if (data.MoreOptions)
            {
                DrawMoreOptionsSection(light, data);
            }

            // 値の最終同期
            if (GUI.changed)
            {
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.UpdateIntensityFromPhysical();
                }
            }
        }

        private PhysicalLight GetOrCreatePhysicalLight(Light light)
        {
            PhysicalLight data = light.GetComponent<PhysicalLight>();
            if (data == null)
            {
                data = Undo.AddComponent<PhysicalLight>(light.gameObject);
            }

            if (data.hideFlags != HideFlags.HideInInspector)
            {
                data.hideFlags = HideFlags.HideInInspector;
                EditorUtility.SetDirty(data);
            }

            return data;
        }

        private Object[] GetUndoTargets()
        {
            var objects = new Object[targets.Length * 2];
            for (int i = 0; i < targets.Length; i++)
            {
                Light light = (Light)targets[i];
                objects[i * 2] = light;
                objects[i * 2 + 1] = GetOrCreatePhysicalLight(light);
            }

            return objects;
        }

        private void DrawColorTemperatureSection(Light light, PhysicalLight data)
        {
            EditorGUILayout.LabelField("Color Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool useTemp = EditorGUILayout.Toggle("Use Color Temperature", light.useColorTemperature);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle Color Temperature");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.useColorTemperature = useTemp;
                    EditorUtility.SetDirty(l);
                }
            }

            if (light.useColorTemperature)
            {
                // Filter カラー
                EditorGUI.BeginChangeCheck();
                Color filterColor = EditorGUILayout.ColorField("Filter", light.color);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Change Filter Color");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.color = filterColor;
                        EditorUtility.SetDirty(l);
                    }
                }

                // Temperature
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float temp = EditorGUILayout.Slider(new GUIContent("Temperature (K)"), light.colorTemperature, 1500f, 20000f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Change Color Temperature");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.colorTemperature = temp;
                        EditorUtility.SetDirty(l);
                    }
                }

                if (GUILayout.Button("💡", GUILayout.Width(25)))
                {
                    ShowTemperaturePresetMenu(data, light);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Color ピッカー
                EditorGUI.BeginChangeCheck();
                Color color = EditorGUILayout.ColorField("Color", light.color);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Change Light Color");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.color = color;
                        EditorUtility.SetDirty(l);
                    }
                }
            }
        }

        private void DrawPhysicalIntensitySection(Light light, PhysicalLight data)
        {
            EditorGUILayout.LabelField("Intensity Settings", EditorStyles.boldLabel);

            PhysicalLight.LightUnit selectedUnit = data.Unit;
            
            EditorGUI.BeginChangeCheck();
            if (light.type == LightType.Directional)
            {
                EditorGUILayout.LabelField("Light Unit", "Lux (lx)");
                selectedUnit = PhysicalLight.LightUnit.Lux;
            }
            else if (light.type == LightType.Rectangle || light.type == LightType.Disc)
            {
                string[] options = new string[] { "Lumen (lm)", "Nits (nt)", "EV100" };
                int index = 0;
                if (selectedUnit == PhysicalLight.LightUnit.Nits) index = 1;
                else if (selectedUnit == PhysicalLight.LightUnit.EV100) index = 2;
                
                int newIndex = EditorGUILayout.Popup("Light Unit", index, options);
                if (newIndex == 1) selectedUnit = PhysicalLight.LightUnit.Nits;
                else if (newIndex == 2) selectedUnit = PhysicalLight.LightUnit.EV100;
                else selectedUnit = PhysicalLight.LightUnit.Lumen;
            }
            else
            {
                string[] options = new string[] { "Lumen (lm)", "Candela (cd)", "Lux (lx)", "EV100" };
                int index = 1; // デフォルト Candela
                if (selectedUnit == PhysicalLight.LightUnit.Lumen) index = 0;
                else if (selectedUnit == PhysicalLight.LightUnit.Lux) index = 2;
                else if (selectedUnit == PhysicalLight.LightUnit.EV100) index = 3;

                int newIndex = EditorGUILayout.Popup("Light Unit", index, options);
                if (newIndex == 0) selectedUnit = PhysicalLight.LightUnit.Lumen;
                else if (newIndex == 2) selectedUnit = PhysicalLight.LightUnit.Lux;
                else if (newIndex == 3) selectedUnit = PhysicalLight.LightUnit.EV100;
                else selectedUnit = PhysicalLight.LightUnit.Candela;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Light Unit");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.Unit = selectedUnit;
                    EditorUtility.SetDirty(pl);
                }
            }

            // 強度の入力スライダー＆テキスト
            EditorGUILayout.BeginHorizontal();
            
            float physicalInt = data.PhysicalIntensity;
            
            float minSliderLimit = selectedUnit == PhysicalLight.LightUnit.EV100 ? -16f : 0f;
            float maxSliderLimit = GetIntensitySliderMax(selectedUnit, light.type);

            EditorGUI.BeginChangeCheck();
            physicalInt = EditorGUILayout.Slider(new GUIContent("Intensity"), physicalInt, minSliderLimit, maxSliderLimit);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Physical Intensity");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.PhysicalIntensity = physicalInt;
                    EditorUtility.SetDirty(pl);
                }
            }

            if (GUILayout.Button("🌟", GUILayout.Width(25)))
            {
                ShowIntensityPresetMenu(data, light, selectedUnit);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpotShapeSection(Light light)
        {
            EditorGUILayout.LabelField("Shape Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float spotAngle = EditorGUILayout.Slider("Spot Angle", light.spotAngle, 1.0f, 179.0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Spot Angle");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.spotAngle = spotAngle;
                    if (l.innerSpotAngle > spotAngle)
                    {
                        l.innerSpotAngle = spotAngle;
                    }

                    var pl = GetOrCreatePhysicalLight(l);
                    pl.UpdateIntensityFromPhysical();
                    EditorUtility.SetDirty(l);
                    EditorUtility.SetDirty(pl);
                }
            }

            EditorGUI.BeginChangeCheck();
            float innerSpotAngle = EditorGUILayout.Slider("Inner Spot Angle", light.innerSpotAngle, 0.0f, light.spotAngle);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Inner Spot Angle");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.innerSpotAngle = Mathf.Clamp(innerSpotAngle, 0.0f, l.spotAngle);
                    EditorUtility.SetDirty(l);
                }
            }
        }

        private float GetIntensitySliderMax(PhysicalLight.LightUnit unit, LightType type)
        {
            switch (unit)
            {
                case PhysicalLight.LightUnit.Lumen:
                    return type == LightType.Rectangle || type == LightType.Disc ? 100000.0f : 50000.0f;
                case PhysicalLight.LightUnit.Lux:
                    return type == LightType.Directional ? 120000.0f : 20000.0f;
                case PhysicalLight.LightUnit.EV100:
                    return 20.0f;
                case PhysicalLight.LightUnit.Nits:
                    return 10000.0f;
                default:
                    return 20000.0f;
            }
        }

        private void DrawShadowsSection(Light light)
        {
            EditorGUILayout.LabelField("Shadow Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            LightShadows shadows = (LightShadows)EditorGUILayout.EnumPopup("Shadow Type", light.shadows);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Change Shadow Type");
                foreach (var t in targets)
                {
                    Light l = (Light)t;
                    l.shadows = shadows;
                    EditorUtility.SetDirty(l);
                }
            }

            if (light.shadows != LightShadows.None)
            {
                EditorGUI.indentLevel++;
                
                EditorGUI.BeginChangeCheck();
                float strength = EditorGUILayout.Slider("Strength", light.shadowStrength, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Shadow Strength");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.shadowStrength = strength;
                        EditorUtility.SetDirty(l);
                    }
                }

                EditorGUI.BeginChangeCheck();
                float bias = EditorGUILayout.FloatField("Bias", light.shadowBias);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Shadow Bias");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.shadowBias = bias;
                        EditorUtility.SetDirty(l);
                    }
                }

                EditorGUI.BeginChangeCheck();
                float normalBias = EditorGUILayout.FloatField("Normal Bias", light.shadowNormalBias);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Shadow Normal Bias");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.shadowNormalBias = normalBias;
                        EditorUtility.SetDirty(l);
                    }
                }

                EditorGUI.BeginChangeCheck();
                float nearPlane = EditorGUILayout.FloatField("Near Plane", light.shadowNearPlane);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change Shadow Near Plane");
                    foreach (var t in targets)
                    {
                        Light l = (Light)t;
                        l.shadowNearPlane = nearPlane;
                        EditorUtility.SetDirty(l);
                    }
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMoreOptionsSection(Light light, PhysicalLight data)
        {
            EditorGUILayout.LabelField("More Options", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            bool affectDiffuse = EditorGUILayout.Toggle("Affect Diffuse", data.AffectDiffuse);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle Affect Diffuse");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.AffectDiffuse = affectDiffuse;
                    EditorUtility.SetDirty(pl);
                }
            }

            EditorGUI.BeginChangeCheck();
            bool affectSpecular = EditorGUILayout.Toggle("Affect Specular", data.AffectSpecular);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle Affect Specular");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.AffectSpecular = affectSpecular;
                    EditorUtility.SetDirty(pl);
                }
            }

            EditorGUI.BeginChangeCheck();
            bool rangeAtten = EditorGUILayout.Toggle("Range Attenuation", data.RangeAttenuation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle Range Attenuation");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.RangeAttenuation = rangeAtten;
                    EditorUtility.SetDirty(pl);
                }
            }

            if (light.type != LightType.Directional)
            {
                EditorGUI.BeginChangeCheck();
                float fadeDist = EditorGUILayout.FloatField("Fade Distance", data.FadeDistance);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Change Fade Distance");
                    foreach (var t in targets)
                    {
                        var pl = GetOrCreatePhysicalLight((Light)t);
                        pl.FadeDistance = fadeDist;
                        EditorUtility.SetDirty(pl);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            float intMult = EditorGUILayout.FloatField("Intensity Multiplier", data.IntensityMultiplier);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Change Intensity Multiplier");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.IntensityMultiplier = intMult;
                    EditorUtility.SetDirty(pl);
                }
            }

            if (light.type == LightType.Rectangle || light.type == LightType.Disc)
            {
                EditorGUI.BeginChangeCheck();
                bool displayEmissive = EditorGUILayout.Toggle("Display Emissive Mesh", data.DisplayEmissiveMesh);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(GetUndoTargets(), "Toggle Display Emissive Mesh");
                    foreach (var t in targets)
                    {
                        var pl = GetOrCreatePhysicalLight((Light)t);
                        pl.DisplayEmissiveMesh = displayEmissive;
                        EditorUtility.SetDirty(pl);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            bool rayTracing = EditorGUILayout.Toggle("Include For Ray Tracing", data.IncludeForRayTracing);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(GetUndoTargets(), "Toggle Include For Ray Tracing");
                foreach (var t in targets)
                {
                    var pl = GetOrCreatePhysicalLight((Light)t);
                    pl.IncludeForRayTracing = rayTracing;
                    EditorUtility.SetDirty(pl);
                }
            }

            EditorGUI.indentLevel--;
        }

        // --- プリセットメニューの表示 ---
        private void ShowTemperaturePresetMenu(PhysicalLight data, Light light)
        {
            GenericMenu menu = new GenericMenu();
            AddTemperaturePreset(menu, "Candle (1500 K)", 1500f, data, light);
            AddTemperaturePreset(menu, "40W Tungsten (2200 K)", 2200f, data, light);
            AddTemperaturePreset(menu, "100W Tungsten (2800 K)", 2800f, data, light);
            AddTemperaturePreset(menu, "Halogen (3000 K)", 3000f, data, light);
            AddTemperaturePreset(menu, "Carbon Arc (5200 K)", 5200f, data, light);
            AddTemperaturePreset(menu, "Daylight (5500 K)", 5500f, data, light);
            AddTemperaturePreset(menu, "Overcast Sky (6500 K)", 6500f, data, light);
            AddTemperaturePreset(menu, "Clear Blue Sky (12000 K)", 12000f, data, light);
            menu.ShowAsContext();
        }

        private void AddTemperaturePreset(GenericMenu menu, string name, float val, PhysicalLight data, Light light)
        {
            menu.AddItem(new GUIContent(name), Mathf.Approximately(light.colorTemperature, val), () =>
            {
                Undo.RecordObjects(new Object[] { light, data }, "Apply Temperature Preset");
                light.colorTemperature = val;
                EditorUtility.SetDirty(light);
                EditorUtility.SetDirty(data);
            });
        }

        private void ShowIntensityPresetMenu(PhysicalLight data, Light light, PhysicalLight.LightUnit unit)
        {
            GenericMenu menu = new GenericMenu();

            if (unit == PhysicalLight.LightUnit.Lumen)
            {
                AddIntensityPreset(menu, "Candle (12.5 lm)", 12.5f, data, light);
                AddIntensityPreset(menu, "40W Bulb (450 lm)", 450f, data, light);
                AddIntensityPreset(menu, "60W Bulb (800 lm)", 800f, data, light);
                AddIntensityPreset(menu, "100W Bulb (1600 lm)", 1600f, data, light);
                AddIntensityPreset(menu, "500W Halogen (9500 lm)", 9500f, data, light);
                AddIntensityPreset(menu, "1000W Halogen (22000 lm)", 22000f, data, light);
            }
            else if (unit == PhysicalLight.LightUnit.Candela)
            {
                AddIntensityPreset(menu, "Candle (1.0 cd)", 1.0f, data, light);
                AddIntensityPreset(menu, "40W Bulb (35.0 cd)", 35.0f, data, light);
                AddIntensityPreset(menu, "60W Bulb (63.0 cd)", 63.0f, data, light);
                AddIntensityPreset(menu, "100W Bulb (127.0 cd)", 127.0f, data, light);
                AddIntensityPreset(menu, "500W Halogen (750.0 cd)", 750.0f, data, light);
            }
            else if (unit == PhysicalLight.LightUnit.Lux)
            {
                AddIntensityPreset(menu, "Moonless Clear Night (0.001 lx)", 0.001f, data, light);
                AddIntensityPreset(menu, "Full Moon (0.25 lx)", 0.25f, data, light);
                AddIntensityPreset(menu, "Family Living Room (50.0 lx)", 50.0f, data, light);
                AddIntensityPreset(menu, "Office Hallway (80.0 lx)", 80.0f, data, light);
                AddIntensityPreset(menu, "Very Bright Sunlight (100000.0 lx)", 100000.0f, data, light);
            }
            else if (unit == PhysicalLight.LightUnit.EV100)
            {
                AddIntensityPreset(menu, "Moonless Clear Night (-11.0 EV)", -11.0f, data, light);
                AddIntensityPreset(menu, "Full Moon (-2.0 EV)", -2.0f, data, light);
                AddIntensityPreset(menu, "Family Living Room (4.0 EV)", 4.0f, data, light);
                AddIntensityPreset(menu, "Office Hallway (5.0 EV)", 5.0f, data, light);
                AddIntensityPreset(menu, "Very Bright Sunlight (16.0 EV)", 16.0f, data, light);
            }
            else if (unit == PhysicalLight.LightUnit.Nits)
            {
                AddIntensityPreset(menu, "100 Nits (SDR Display)", 100f, data, light);
                AddIntensityPreset(menu, "1000 Nits (HDR Display)", 1000f, data, light);
            }

            menu.ShowAsContext();
        }

        private void AddIntensityPreset(GenericMenu menu, string name, float val, PhysicalLight data, Light light)
        {
            menu.AddItem(new GUIContent(name), Mathf.Approximately(data.PhysicalIntensity, val), () =>
            {
                Undo.RecordObjects(new Object[] { light, data }, "Apply Intensity Preset");
                data.PhysicalIntensity = val;
                data.UpdateIntensityFromPhysical();
                EditorUtility.SetDirty(light);
                EditorUtility.SetDirty(data);
            });
        }
    }
}
