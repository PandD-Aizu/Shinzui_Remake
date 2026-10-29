var player=UnityEngine.Object.FindFirstObjectByType<Shinzui.View.PlayerView>();
var source=player.GetComponentInChildren<Shinzui.View.PlayerBodyView>();
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/ProtagonistBody.prefab");
var preview=new UnityEditor.PreviewRenderUtility();
var clone=UnityEngine.Object.Instantiate(prefab);preview.AddSingleGO(clone);
try {
clone.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
var body=clone.GetComponent<Shinzui.View.PlayerBodyView>();var model=clone.transform.Find("Model");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var type=body.GetType();
body.Configure(player,null,model,clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="HeadMesh"));
type.GetField("_standingEyeHeight",flags).SetValue(body,type.GetField("_standingEyeHeight",flags).GetValue(source));
body.UpdatePose(.016f);
var bones=model.GetComponentsInChildren<Transform>().ToDictionary(x=>x.name);
var predict=type.GetMethod("PredictPickupShoulder",flags);
float arm=(float)type.GetField("_pickupArmLength",flags).GetValue(body);
var shoulder=bones["RightUpperArm"].position;
var moved=(Vector3)predict.Invoke(body,new object[]{new Vector2(25,0)});
var wrist=moved+model.forward*(arm*.90f);
var rotation=Quaternion.LookRotation(model.forward,model.up);
var contact=wrist+model.forward*.09f;
bool outsideArm=Vector3.Distance(shoulder,wrist)>arm;
bool reachable=body.CanReachPickup(contact,rotation);
bool far=body.CanReachPickup(contact+model.forward*2f,rotation);
if(!outsideArm||!reachable||far)throw new System.Exception("reach validation failed: "+outsideArm+"/"+reachable+"/"+far);
var foot=bones["RightFoot"].position;
preview.camera.fieldOfView=40;preview.camera.nearClipPlane=.03f;preview.camera.farClipPlane=20;
preview.camera.transform.position=clone.transform.position+model.right*3+model.up*1+model.forward*1.1f;
preview.camera.transform.LookAt(clone.transform.position+model.up*.8f+model.forward*.2f);
preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);preview.lights[1].intensity=.7f;preview.ambientColor=Color.gray;
for(int pose=0;pose<2;pose++){
 if(pose==1){body.SetPickupPose(contact,rotation,1, .8f);for(int i=0;i<120;i++)body.UpdatePose(1f/60);}
 preview.BeginStaticPreview(new Rect(0,0,720,720));preview.Render(true);var tex=preview.EndStaticPreview();
 System.IO.File.WriteAllBytes("D:/Pandd/ShinShinzui/Artifacts/HoldProgress/torso-"+pose+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
}
float error=Vector3.Distance(bones["RightHand"].position,wrist);
float footDelta=Vector3.Distance(foot,bones["RightFoot"].position);
var lean=(Vector2)type.GetField("_pickupLean",flags).GetValue(body);
bool sustained=body.CanReachPickup(contact,rotation);
body.SetPickupPose(contact,rotation,0,0);for(int i=0;i<120;i++)body.UpdatePose(1f/60);
var reset=(Vector2)type.GetField("_pickupLean",flags).GetValue(body);
if(error>.025f||footDelta>.02f||!sustained||reset.magnitude>.01f)throw new System.Exception("pose validation failed: "+error+"/"+footDelta+"/"+sustained+"/"+reset);
return new{outsideArm,reachable,far,error,footDelta,lean=lean.ToString(),reset=reset.ToString()};
} finally {preview.Cleanup();}
