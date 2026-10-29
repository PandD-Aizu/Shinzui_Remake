var root=new UnityEngine.GameObject("HoldOverlayCheck");
root.hideFlags=UnityEngine.HideFlags.HideAndDontSave;
try
{
    var cameraObject=new UnityEngine.GameObject("Camera",typeof(UnityEngine.Camera));
    cameraObject.transform.SetParent(root.transform,false);
    cameraObject.transform.position=new UnityEngine.Vector3(20000f,20000f,20000f);
    var camera=cameraObject.GetComponent<UnityEngine.Camera>();
    var item=new UnityEngine.GameObject("Target",typeof(UnityEngine.BoxCollider),typeof(Shinzui.View.Interaction.ItemTest));
    item.transform.SetParent(root.transform,false);
    item.transform.position=camera.transform.position+UnityEngine.Vector3.forward*2f;
    var target=item.GetComponent<Shinzui.View.Interaction.InteractableComponent>();
    var type=typeof(Shinzui.View.Interaction.PlayerInteractionView).Assembly.GetType("Shinzui.View.Interaction.InteractionMarkerDisplay");
    var markers=System.Activator.CreateInstance(type,new object[]{root.transform});
    var ui=Shinzui.View.Interaction.HoldInteractionProgressView.Create(root.transform,TMPro.TMP_Settings.defaultFontAsset,UnityEngine.Vector2.zero,72f);
    UnityEngine.Canvas.ForceUpdateCanvases();
    UnityEngine.Physics.SyncTransforms();
    var refresh=type.GetMethod("Refresh");
    object[] arguments={camera,target,5f,(UnityEngine.LayerMask)(~0),14f,24f};
    refresh.Invoke(markers,arguments);
    var ring=root.GetComponentInChildren<Shinzui.View.Interaction.InteractionRingGraphic>();
    if(ring==null) throw new System.Exception("Marker missing");
    object[] replacement={target,UnityEngine.Vector2.zero};
    if(!(bool)type.GetMethod("TryReplaceWithHold").Invoke(markers,replacement)) throw new System.Exception("Replacement failed");
    var position=(UnityEngine.Vector2)replacement[1];
    ui.SetScreenPosition(position);
    ui.Refresh(true,.5f);
    UnityEngine.Canvas.ForceUpdateCanvases();
    var actual=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,ui.GetComponentInChildren<Shinzui.View.Interaction.HoldProgressGraphic>().rectTransform.position);
    if(ring.enabled || UnityEngine.Vector2.Distance(position,actual)>1f) throw new System.Exception("Overlay mismatch");
    refresh.Invoke(markers,arguments);
    if(!ring.enabled) throw new System.Exception("Original marker was not restored");
    item.transform.position+=UnityEngine.Vector3.right*.3f;
    UnityEngine.Physics.SyncTransforms();
    refresh.Invoke(markers,arguments);
    type.GetMethod("TryReplaceWithHold").Invoke(markers,replacement);
    var moved=(UnityEngine.Vector2)replacement[1];
    if(UnityEngine.Vector2.Distance(position,moved)<1f) throw new System.Exception("Marker did not follow target");
    ui.SetScreenPosition(moved);
    UnityEngine.Canvas.ForceUpdateCanvases();
    actual=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,ui.GetComponentInChildren<Shinzui.View.Interaction.HoldProgressGraphic>().rectTransform.position);
    if(UnityEngine.Vector2.Distance(moved,actual)>1f) throw new System.Exception("Moving overlay mismatch");
    return "Marker replacement, restoration and moving target alignment passed";
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
}
