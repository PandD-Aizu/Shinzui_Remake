Application.runInBackground=true; Time.captureFramerate=30;
int frame=0,lastFrame=-1,endFrames=0;
var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
UnityEditor.EditorApplication.CallbackFunction record=null;
record=()=>{
 if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
 var intro=UnityEngine.Object.FindAnyObjectByType<Shinzui.View.StageIntroView>();if(intro==null)return;
 if(intro.Phase!=Shinzui.View.StageIntroView.IntroPhase.Approach && intro.Phase!=Shinzui.View.StageIntroView.IntroPhase.Failed)return;
 var c=UnityEngine.Object.FindAnyObjectByType<Shinzui.View.PlayerView>().MainCamera;var data=c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();bool post=data.renderPostProcessing;data.renderPostProcessing=false;var previous=c.targetTexture;var active=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();c.targetTexture=previous;RenderTexture.active=active;data.renderPostProcessing=post;
 System.IO.File.WriteAllBytes($"D:/PandD/Shinzui/Artifacts/HumanEnemy/crawl-video-frames/{frame:D5}.png",tex.EncodeToPNG());System.IO.File.AppendAllText("D:/PandD/Shinzui/Artifacts/HumanEnemy/crawl-video-frames/timing.csv",$"{frame},{Time.frameCount}\n");frame++;
 if(intro.Phase==Shinzui.View.StageIntroView.IntroPhase.Failed)endFrames++;
 if(endFrames<30 && frame<600)return;
 UnityEditor.EditorApplication.update-=record;Time.captureFramerate=0;UnityEditor.EditorApplication.isPaused=true;
 System.IO.File.WriteAllText("D:/PandD/Shinzui/Artifacts/HumanEnemy/crawl-video-result.txt",$"frames={frame} phase={intro.Phase}");UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
};UnityEditor.EditorApplication.update+=record;return "recording";

