var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Shinzui/Scenes/Title.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var legacy = UnityEngine.Object.FindFirstObjectByType<Shinzui.Src.Title.CreditScreenPresenter>(UnityEngine.FindObjectsInactive.Include);
var settings = UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.SettingsLifetimeScope>(UnityEngine.FindObjectsInactive.Include);
var lines = new System.Collections.Generic.List<string>();
var serialized = new UnityEditor.SerializedObject(legacy);
foreach (var name in new[] { "view", "creditData", "creditsPanel", "skipHintCanvasGroup" })
    lines.Add(name + "\t" + UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(serialized.FindProperty(name).objectReferenceValue));
foreach (var name in new[] { "loopCredits", "allowSkip", "autoStart" })
    lines.Add(name + "\t" + serialized.FindProperty(name).boolValue);
lines.Add("nextSceneName\t" + serialized.FindProperty("nextSceneName").stringValue);
lines.Add("fadeOutDuration\t" + serialized.FindProperty("fadeOutDuration").floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
lines.Add("scopeObject\t" + UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(settings.gameObject));
lines.Add("optionWindowView\t" + UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(new UnityEditor.SerializedObject(settings).FindProperty("optionWindowView").objectReferenceValue));
System.IO.File.WriteAllLines("Artifacts/TitleRefactor/scene-bindings.tsv", lines);
System.IO.File.Copy("Assets/Shinzui/Scenes/Title.unity", "Artifacts/TitleRefactor/Title.before.unity", true);
UnityEditor.AssetDatabase.DisallowAutoRefresh();
var controls = new System.Collections.Generic.List<string>();
foreach (var root in scene.GetRootGameObjects())
foreach (var control in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
{
    var path = control.name;
    for (var p = control.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
    controls.Add(control.GetType().Name + "\t" + path);
}
return string.Join("\n", controls);
