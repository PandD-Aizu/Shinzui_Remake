var host = new UnityEngine.GameObject("LED Curved Tunnel Verification");
var view = host.AddComponent<Shinzui.View.GenerateTunnel.TunnelMapView>();
var model = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"), host.transform);
model.SetActive(true); foreach (var mesh in model.GetComponentsInChildren<UnityEngine.MeshFilter>()) { if (mesh.GetComponent<UnityEngine.Collider>() == null) mesh.gameObject.AddComponent<UnityEngine.MeshCollider>().sharedMesh = mesh.sharedMesh; } var method = typeof(Shinzui.View.GenerateTunnel.TunnelMapView).GetMethod("CreateCeilingLights", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
method.Invoke(view, new object[] { host.transform, 40f, 10.4f, 0f, 0f, null, 0 });
var fixtures = host.transform.Find("Ceiling Lights");
var positions = new System.Collections.Generic.List<string>();
foreach (UnityEngine.Transform fixture in fixtures) {
 var p = host.transform.InverseTransformPoint(fixture.position);
 if (p.x <= 0 || p.y < 8) throw new System.Exception("Invalid curved wall position " + p);
 positions.Add(p.ToString());
}
int count = fixtures.childCount;
UnityEngine.Object.DestroyImmediate(host);
if (count != 8) throw new System.Exception("Expected 8 fixtures, got " + count);
return "Curved tunnel: " + count + " fixtures on upper right side: " + string.Join(", ", positions);


