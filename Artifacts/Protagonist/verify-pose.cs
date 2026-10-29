var player = UnityEngine.Object.FindFirstObjectByType<Shinzui.View.PlayerView>();
var body = player.GetComponentInChildren<Shinzui.View.PlayerBodyView>();
var flashlight = player.GetComponentInChildren<Shinzui.View.Flashlight.FlashlightView>();
var camera = player.MainCamera;
player.IsCinematicControlled = true;
camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
flashlight.enabled = false;
body.enabled = false;
var light = flashlight.HeldLightTransform;
var bones = body.GetComponentsInChildren<UnityEngine.Transform>().ToDictionary(t => t.name);
var head = body.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Single(r => r.name == "HeadMesh");
var torso = body.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Single(r => r.name == "BodyMesh");
var results = new System.Collections.Generic.List<object>();
bool hiddenByDefault = !head.enabled;
foreach (float pitch in new[] { 0f, -75f, 75f })
{
    camera.transform.rotation = UnityEngine.Quaternion.Euler(pitch, 0f, 0f);
    light.SetPositionAndRotation(camera.transform.TransformPoint(new UnityEngine.Vector3(-.45f, -.25f, .52f)), camera.transform.rotation);
    body.UpdatePose(0f);
    float error = UnityEngine.Vector3.Distance(bones["LeftHand"].position, light.TransformPoint(new UnityEngine.Vector3(-.02f, -.065f, -.22f)));
    if (error > .002f) throw new System.Exception("Grip detached at pitch " + pitch + ": " + error);
    results.Add(new { pitch, gripError = error });
}

// しゃがみ時の骨盤・脚と消灯中の握りを確認
camera.transform.rotation = UnityEngine.Quaternion.identity;
player.SetHeight(1f);
camera.transform.position = player.transform.Find("PlayerHead").position;
light.SetPositionAndRotation(camera.transform.TransformPoint(new UnityEngine.Vector3(-.45f, -.25f, .52f)), camera.transform.rotation);
flashlight.SetLightActive(false);
body.UpdatePose(0f);
float crouchError = UnityEngine.Vector3.Distance(bones["LeftHand"].position, light.TransformPoint(new UnityEngine.Vector3(-.02f, -.065f, -.22f)));
if (crouchError > .002f) throw new System.Exception("Grip detached while crouched");
var baked = new UnityEngine.Mesh();
torso.BakeMesh(baked);
bool finite = baked.vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z));
if (!finite) throw new System.Exception("Non-finite skinned vertices");
UnityEngine.Object.Destroy(baked);
body.SetHeadVisible(true);
bool headToggle = head.enabled && torso.enabled;
body.SetHeadVisible(false);
if (!hiddenByDefault || !headToggle || head.enabled) throw new System.Exception("Head visibility failed");

// 検証後は通常の立位と点灯へ戻す
player.SetHeight(2f);
camera.transform.position = player.transform.Find("PlayerHead").position;
light.SetPositionAndRotation(camera.transform.TransformPoint(new UnityEngine.Vector3(-.45f, -.25f, .52f)), camera.transform.rotation);
flashlight.SetLightActive(true);
body.UpdatePose(0f);
body.enabled = true;
flashlight.enabled = true;
return Newtonsoft.Json.JsonConvert.SerializeObject(new { frame = UnityEngine.Time.frameCount, hiddenByDefault, headToggle,
    finite, crouchError, poses = results, bones = torso.bones.Length,
    avatar = body.GetComponentInChildren<UnityEngine.Animator>().avatar.isHuman,
    leftFoot = bones["LeftFoot"].position.ToString("F3"), rightFoot = bones["RightFoot"].position.ToString("F3") });
