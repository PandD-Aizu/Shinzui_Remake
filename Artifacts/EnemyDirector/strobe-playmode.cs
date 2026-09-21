var scope = UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.PlayerLifetimeScope>();
var presenter = (Shinzui.Presentation.EnemyPresenter)scope.Container.Resolve(typeof(Shinzui.Presentation.EnemyPresenter));
var target = presenter.FindStrobeTarget(default(UnityEngine.RaycastHit), 1000f);
if (!target.IsValid) throw new System.Exception("No strobe target");
var agents = UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(UnityEngine.FindObjectsSortMode.None);
var agent = agents.OrderBy(a => UnityEngine.Vector3.Distance(a.transform.position + UnityEngine.Vector3.up, target.StrobeTargetPosition)).First();
target.ApplyStrobeEffect(true, .25f, 2f);
if (!agent.isStopped || agent.speed != 0f) throw new System.Exception("Strobe did not stop agent");
return new { target = target.Id, name = agent.name, stopped = agent.isStopped, speed = agent.speed };