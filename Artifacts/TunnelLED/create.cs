var model = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Shinzui/Models/TunnelLEDLight/Tunnel_LED_Light.glb");
var root = new UnityEngine.GameObject("Tunnel LED Light");
var visual = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model);
visual.transform.SetParent(root.transform, false);
visual.transform.localRotation = UnityEngine.Quaternion.Euler(0, 90, 180);
visual.transform.localPosition = new UnityEngine.Vector3(0, -.12f, 0);
var old = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Shinzui/Resources/TunnelFluorescentLight.prefab");
foreach (var source in old.GetComponentsInChildren<UnityEngine.Light>()) {
 var copy = UnityEngine.Object.Instantiate(source.gameObject, root.transform, false);
 copy.name = source.name.Replace("Fluorescent", "LED");
}
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, "Assets/Shinzui/Resources/TunnelLEDLight.prefab");
UnityEngine.Object.DestroyImmediate(root);
UnityEditor.AssetDatabase.SaveAssets();
return "LED prefab saved";
