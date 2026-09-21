var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Shinzui/Scenes/Title.unity") throw new System.InvalidOperationException("Title must be the active scene.");
var roots = scene.GetRootGameObjects();
var components = new System.Collections.Generic.List<UnityEngine.MonoBehaviour>();
foreach (var root in roots) components.AddRange(root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true));
var scopeCount = 0;
var listenerCount = 0;
foreach (var component in components)
{
    if (component == null) throw new System.InvalidOperationException("Missing script in Title scene.");
    if (component is Shinzui.DI.TitleLifetimeScope scope) { scope.ValidateConfiguration(); scopeCount++; }
    if (component is Shinzui.DI.SettingsLifetimeScope) throw new System.InvalidOperationException("Duplicate settings scope.");
    if (component is UnityEngine.UI.Button button)
    {
        for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            var target = button.onClick.GetPersistentTarget(i);
            var method = button.onClick.GetPersistentMethodName(i);
            if (target == null || target.GetType().GetMethod(method) == null)
                throw new System.InvalidOperationException("Broken button binding: " + button.name + " -> " + method);
            listenerCount++;
        }
    }
}
if (scopeCount != 1) throw new System.InvalidOperationException("Expected one title scope, got " + scopeCount);
return new { scene = scene.path, scopeCount, listenerCount, missingScripts = 0 };
