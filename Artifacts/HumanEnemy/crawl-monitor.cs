Application.runInBackground=true;
UnityEditor.EditorApplication.CallbackFunction check=null;
check=()=>{var i=UnityEngine.Object.FindAnyObjectByType<Shinzui.View.StageIntroView>();if(i==null||i.Phase!=Shinzui.View.StageIntroView.IntroPhase.Approach)return;var t=(float)typeof(Shinzui.View.StageIntroView).GetField("_elapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(i);if(t<3.5f)return;UnityEditor.EditorApplication.update-=check;UnityEditor.EditorApplication.isPaused=true;};
UnityEditor.EditorApplication.update+=check;return "monitor";
