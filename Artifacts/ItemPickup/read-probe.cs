var scope=UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.PlayerLifetimeScope>();
var ticks=(System.Collections.Generic.IEnumerable<VContainer.Unity.ITickable>)scope.Container.Resolve(typeof(System.Collections.Generic.IEnumerable<VContainer.Unity.ITickable>));
var presenter=ticks.OfType<Shinzui.Presentation.Interaction.InteractionPresenter>().Single();
var pickup=(Shinzui.Application.UseCases.Interaction.ItemPickupUseCase)typeof(Shinzui.Presentation.Interaction.InteractionPresenter).GetField("_pickup",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(presenter);
var view=UnityEngine.Object.FindFirstObjectByType<Shinzui.View.Interaction.PlayerInteractionView>();
var body=UnityEngine.Object.FindFirstObjectByType<Shinzui.View.PlayerBodyView>();
var hand=body.GetComponentsInChildren<UnityEngine.Transform>().First(t=>t.name=="RightHand");
return Newtonsoft.Json.JsonConvert.SerializeObject(new {frame=UnityEngine.Time.frameCount,state=pickup.State.ToString(),pickup.Progress,pickup.Succeeded,active=view.IsPickupActive,current=view.CurrentInteractable==null ? "none":view.CurrentInteractable.name,hand=hand.position.ToString(),exists=UnityEngine.GameObject.Find("PickupProbeStone")!=null});
