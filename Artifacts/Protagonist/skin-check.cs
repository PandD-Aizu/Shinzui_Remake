var source=UnityEngine.Object.FindFirstObjectByType<Shinzui.View.PlayerView>();
var preview=new UnityEditor.PreviewRenderUtility();var clone=UnityEngine.Object.Instantiate(source.gameObject);preview.AddSingleGO(clone);
var p=clone.GetComponent<Shinzui.View.PlayerView>();var b=clone.GetComponentInChildren<Shinzui.View.PlayerBodyView>();var model=b.transform.Find("Model");
foreach(var r in clone.GetComponentsInChildren<Renderer>())r.enabled=r.name=="BodyMesh";
foreach(var l in clone.GetComponentsInChildren<Light>())l.enabled=false;
b.Configure(p,null,model,clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="HeadMesh"));b.UpdatePose(0);
var body=clone.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="BodyMesh");
var baked=new Mesh();body.BakeMesh(baked);var vertices=baked.vertices;
var top=vertices.Select((v,i)=>new {i,pos=p.transform.InverseTransformPoint(body.transform.TransformPoint(v)).ToString("F5")}).OrderByDescending(x=>p.transform.InverseTransformPoint(body.transform.TransformPoint(vertices[x.i])).y).Take(8).ToArray();
preview.lights[0].intensity=1;preview.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);preview.lights[1].intensity=.5f;preview.ambientColor=Color.gray;
try{
for(int k=0;k<2;k++){
if(k==0)body.quality=SkinQuality.Bone4;
else {body.enabled=false;var go=new GameObject("Baked");go.transform.SetParent(body.transform,false);go.AddComponent<MeshFilter>().sharedMesh=baked;go.AddComponent<MeshRenderer>().sharedMaterials=body.sharedMaterials;}
preview.camera.transform.position=p.transform.TransformPoint(new Vector3(1.5f,.3f,2));preview.camera.transform.LookAt(p.transform.TransformPoint(new Vector3(.1f,0f,0f)));preview.camera.fieldOfView=40;preview.camera.nearClipPlane=.03f;preview.camera.farClipPlane=10;
preview.BeginStaticPreview(new Rect(0,0,800,1000));preview.Render(true);var tex=preview.EndStaticPreview();System.IO.File.WriteAllBytes("D:/Pandd/ShinShinzui/Artifacts/Protagonist/skin-check-"+k+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
}return Newtonsoft.Json.JsonConvert.SerializeObject(new {top,quality=QualitySettings.skinWeights.ToString(),mesh=body.sharedMesh.name,materials=body.sharedMaterials.Select(m=>m.shader.name).ToArray()});
}finally{preview.Cleanup();UnityEngine.Object.DestroyImmediate(baked);}
