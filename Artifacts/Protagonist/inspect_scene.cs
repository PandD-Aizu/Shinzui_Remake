var player = UnityEngine.Object.FindFirstObjectByType<Shinzui.View.PlayerView>();
var light = player.GetComponentInChildren<Shinzui.View.Flashlight.FlashlightView>();
var ps = new UnityEditor.SerializedObject(player);
var fs = new UnityEditor.SerializedObject(light);
var cc = player.GetComponent<UnityEngine.CharacterController>();
var camera = player.MainCamera;
return Newtonsoft.Json.JsonConvert.SerializeObject(new {
    playerPrefab = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player),
    controller = new { height = cc.height, center = cc.center.ToString(), radius = cc.radius },
    camera = new { position = camera.transform.position.ToString("F3"), local = camera.transform.localPosition.ToString("F3"), parent = camera.transform.parent == null ? "none" : camera.transform.parent.name, near = camera.nearClipPlane, fov = camera.fieldOfView },
    cameraTarget = ps.FindProperty("cameraTarget").objectReferenceValue.name,
    light = new { offset = fs.FindProperty("modelLightOffset").vector3Value.ToString("F3"), light = fs.FindProperty("flashlightLight").objectReferenceValue.name, model = fs.FindProperty("flashlightModel").objectReferenceValue.name },
    renderers = player.GetComponentsInChildren<UnityEngine.Renderer>(true).Select(r => new {name=r.name,enabled=r.enabled,bounds=r.bounds.ToString()}).ToArray()
});
