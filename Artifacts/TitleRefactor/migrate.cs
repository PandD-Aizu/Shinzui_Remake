var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Shinzui/Scenes/Title.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var oldScope = UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.SettingsLifetimeScope>(UnityEngine.FindObjectsInactive.Include);
var oldCredits = UnityEngine.Object.FindFirstObjectByType<Shinzui.Src.Title.CreditScreenPresenter>(UnityEngine.FindObjectsInactive.Include);
if (oldScope == null || oldCredits == null) throw new System.InvalidOperationException("Expected legacy components for the one-time migration.");
if (!System.IO.File.Exists("Artifacts/TitleRefactor/Title.before.unity")) throw new System.InvalidOperationException("Scene backup is required.");
var title = UnityEngine.Object.FindFirstObjectByType<Shinzui.Src.Title.ButtonController>(UnityEngine.FindObjectsInactive.Include);
var oldSettings = new UnityEditor.SerializedObject(oldScope);
var oldCreditSettings = new UnityEditor.SerializedObject(oldCredits);
var options = (Shinzui.View.Settings.OptionWindowView)oldSettings.FindProperty("optionWindowView").objectReferenceValue;
var optionSettings = new UnityEditor.SerializedObject(options);
var content = options.transform.Find("Scroll View/Viewport/Content");
if (content == null) throw new System.InvalidOperationException("Option content was not found.");
optionSettings.FindProperty("generateCategoryTabs").boolValue = false;
optionSettings.FindProperty("autoDiscoverCategoryPanels").boolValue = false;
optionSettings.FindProperty("categoryPanelParent").objectReferenceValue = content;
var ids = new[] { "Controls", "Camera", "GameSettings", "Graphics", "Audio", "Language", "Accessibility" };
var panels = new[] { "Control", "Camera", "GameSetting", "Graphics", "Audio", "Language", "Accessibility" };
var bindings = optionSettings.FindProperty("categoryPanels");
bindings.arraySize = ids.Length;
for (var i = 0; i < ids.Length; i++)
{
    var panel = content.Find(panels[i]);
    if (panel == null) throw new System.InvalidOperationException("Missing option panel: " + panels[i]);
    bindings.GetArrayElementAtIndex(i).FindPropertyRelative("categoryId").stringValue = ids[i];
    bindings.GetArrayElementAtIndex(i).FindPropertyRelative("panel").objectReferenceValue = panel.gameObject;
}
var sliderFields = new[] { "masterVolumeSlider", "bgmVolumeSlider", "seVolumeSlider", "voiceVolumeSlider", "controllerSpeedSlider", "mouseSensitivitySlider", "brightnessSlider" };
var sliderPaths = new[] { "Audio/Element/Slider", "Audio/Element (1)/Slider", "Audio/Element (2)/Slider", "Audio/Element (3)/Slider", "Camera/Element/Slider", "Camera/Element (1)/Slider", "Graphics/Element/Slider" };
for (var i = 0; i < sliderFields.Length; i++)
{
    var slider = content.Find(sliderPaths[i]).GetComponent<UnityEngine.UI.Slider>();
    if (slider == null) throw new System.InvalidOperationException("Missing slider: " + sliderFields[i]);
    optionSettings.FindProperty(sliderFields[i]).objectReferenceValue = slider;
    if (i == 4 || i == 5)
    {
        slider.minValue = 1f;
        slider.maxValue = 100f;
    }
}
optionSettings.FindProperty("centerDotToggle").objectReferenceValue = content.Find("Accessibility/Element/Toggle").GetComponent<UnityEngine.UI.Toggle>();
optionSettings.ApplyModifiedPropertiesWithoutUndo();

var newScope = UnityEditor.Undo.AddComponent<Shinzui.DI.TitleLifetimeScope>(oldScope.gameObject);
var scopeSettings = new UnityEditor.SerializedObject(newScope);
scopeSettings.FindProperty("titleView").objectReferenceValue = title;
scopeSettings.FindProperty("creditView").objectReferenceValue = oldCreditSettings.FindProperty("view").objectReferenceValue;
scopeSettings.FindProperty("optionView").objectReferenceValue = options;
scopeSettings.FindProperty("creditData").objectReferenceValue = oldCreditSettings.FindProperty("creditData").objectReferenceValue;
scopeSettings.FindProperty("gameSceneAddress").stringValue = "Assets/Shinzui/Scenes/StageTemp.unity";
scopeSettings.FindProperty("loopCredits").boolValue = oldCreditSettings.FindProperty("loopCredits").boolValue;
scopeSettings.FindProperty("allowSkip").boolValue = oldCreditSettings.FindProperty("allowSkip").boolValue;
scopeSettings.FindProperty("autoStartCredits").boolValue = oldCreditSettings.FindProperty("autoStart").boolValue;
scopeSettings.FindProperty("nextSceneName").stringValue = oldCreditSettings.FindProperty("nextSceneName").stringValue;
scopeSettings.FindProperty("creditsFadeOutDuration").floatValue = oldCreditSettings.FindProperty("fadeOutDuration").floatValue;
scopeSettings.FindProperty("parentReference.TypeName").stringValue = oldSettings.FindProperty("parentReference.TypeName").stringValue;
scopeSettings.FindProperty("autoRun").boolValue = true;
scopeSettings.ApplyModifiedPropertiesWithoutUndo();
newScope.ValidateConfiguration();

// Preserve authored UnityEvents while updating their target assembly after moving scripts.
foreach (var root in scene.GetRootGameObjects())
foreach (var component in root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))
{
    if (component == null) throw new System.InvalidOperationException("Missing script before migration.");
    var serialized = new UnityEditor.SerializedObject(component);
    var iterator = serialized.GetIterator();
    while (iterator.Next(true))
    {
        if (!iterator.propertyPath.EndsWith(".m_TargetAssemblyTypeName")) continue;
        var targetPath = iterator.propertyPath.Substring(0, iterator.propertyPath.Length - "m_TargetAssemblyTypeName".Length) + "m_Target";
        var target = serialized.FindProperty(targetPath).objectReferenceValue;
        if (target is Shinzui.Src.Title.ButtonController || target is Shinzui.Src.Title.ButtonAnimation ||
            target is Shinzui.Src.Title.FadeController || target is OnOffText)
            iterator.stringValue = target.GetType().AssemblyQualifiedName;
    }
    serialized.ApplyModifiedPropertiesWithoutUndo();
}

// The replacement and all references have been validated. Remove the two replaced
// components in this same transaction; the scene file has a full pre-migration backup.
UnityEditor.Undo.DestroyObjectImmediate(oldCredits);
UnityEditor.Undo.DestroyObjectImmediate(oldScope);
newScope.gameObject.name = "TitleLifetimeScope";
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Could not save Title scene.");
return "TitleLifetimeScope configured; 7 tabs and 8 settings controls connected; legacy components replaced; scene saved.";
