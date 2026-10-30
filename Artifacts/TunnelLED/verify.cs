var host = new UnityEngine.GameObject("LED Verification");
var view = host.AddComponent<Shinzui.View.GenerateTunnel.TunnelMapView>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var method = typeof(Shinzui.View.GenerateTunnel.TunnelMapView).GetMethod("CreateCeilingLights", flags);
var results = new System.Collections.Generic.List<string>();
foreach (float angle in new[] { 0f, 90f }) {
 var passage = new UnityEngine.GameObject("Probe Passage").transform;
 passage.SetParent(host.transform, false);
 passage.localRotation = UnityEngine.Quaternion.Euler(0, angle, 0);
 foreach (float side in new[] { -1f, 1f }) {
  var wall = new UnityEngine.GameObject("Wall");
  wall.transform.SetParent(passage, false);
  wall.transform.localPosition = new UnityEngine.Vector3(side * 2, 2, 0);
  wall.AddComponent<UnityEngine.BoxCollider>().size = new UnityEngine.Vector3(.2f, 4, 16);
 }
 method.Invoke(view, new object[] { passage, 16f, 4f, 0f, 0f, null, 0 });
 var lights = passage.Find("Ceiling Lights");
 if (lights.childCount != 3) throw new System.Exception("Unexpected light count " + lights.childCount);
 foreach (UnityEngine.Transform fixture in lights) {
  var pos = passage.InverseTransformPoint(fixture.position);
  if (pos.x < 1.8f || UnityEngine.Mathf.Abs(pos.y - 3.2f) > .01f) throw new System.Exception("Invalid upper side position " + pos);
  if (UnityEngine.Vector3.Dot(-fixture.up, -passage.right) < .99f) throw new System.Exception("Invalid light direction");
  if (fixture.GetComponentsInChildren<UnityEngine.MeshRenderer>().Length != 8) throw new System.Exception("Missing GLB meshes");
 }
 results.Add("rotation=" + angle + ": 3 LED fixtures, right upper wall, inward facing, 8 model meshes each");
}
UnityEngine.Object.DestroyImmediate(host);
return string.Join("\n", results);
