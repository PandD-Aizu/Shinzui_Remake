var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new UnityEngine.GameObject("HoldUiPreview");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var cameraObject = new UnityEngine.GameObject("PreviewCamera", typeof(UnityEngine.Camera));
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
var camera = cameraObject.GetComponent<UnityEngine.Camera>();
camera.scene = scene;
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
camera.backgroundColor = new UnityEngine.Color(.045f,.05f,.055f,1f);
camera.nearClipPlane=.01f;
camera.farClipPlane=10f;
var texture = new UnityEngine.RenderTexture(960,320,24);
var previous = UnityEngine.RenderTexture.active;
var image = new UnityEngine.Texture2D(960,320,UnityEngine.TextureFormat.RGB24,false);
UnityEngine.InputSystem.InputDevice pad = null;
try
{
    for(int i=0;i<3;i++)
    {
        var ui=Shinzui.View.Interaction.HoldInteractionProgressView.Create(root.transform,TMPro.TMP_Settings.defaultFontAsset,new UnityEngine.Vector2((i-1)*280f,0f),120f);
        var canvas=ui.GetComponent<UnityEngine.Canvas>();
        canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;
        canvas.worldCamera=camera;
        canvas.planeDistance=1f;
        var scaler=ui.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
        if(i==2)
        {
            pad=UnityEngine.InputSystem.InputSystem.AddDevice("DualShock4GamepadHID");
            typeof(Shinzui.View.Interaction.HoldInteractionProgressView).GetField("_usingGamepad",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(ui,true);
            ui.UpdateInputDevice();
        }
        if(i==1) ui.ShowCompleted();
        ui.Refresh(true,.65f);
        var label=ui.GetComponentInChildren<TMPro.TMP_Text>();
        if(i<2 && label.text!="E") throw new System.Exception("Keyboard label mismatch");
        if(i==2 && label.text!="×") throw new System.Exception("PlayStation label mismatch: "+label.text);
        label.ForceMeshUpdate();
    }
    camera.targetTexture=texture;
    UnityEngine.Canvas.ForceUpdateCanvases();
    camera.Render();
    UnityEngine.RenderTexture.active=texture;
    image.ReadPixels(new UnityEngine.Rect(0,0,960,320),0,0);
    image.Apply();
    System.IO.File.WriteAllBytes("D:/Pandd/ShinShinzui/Artifacts/HoldProgress/preview.png",UnityEngine.ImageConversion.EncodeToPNG(image));
    return "Keyboard E, complete ring, PlayStation cross verified";
}
finally
{
    if(pad!=null) UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
    UnityEngine.RenderTexture.active=previous;
    UnityEngine.Object.DestroyImmediate(image);
    UnityEngine.Object.DestroyImmediate(texture);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
