if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play mode required");
var scope = UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.PlayerLifetimeScope>();
if (scope == null || scope.Container == null) throw new System.InvalidOperationException("PlayerLifetimeScope not ready");
var director = (Shinzui.Application.UseCases.Enemy.EnemyDirectorUseCase)scope.Container.Resolve(typeof(Shinzui.Application.UseCases.Enemy.EnemyDirectorUseCase));
var runtimes = (Shinzui.Application.Interfaces.IEnemyRuntime[])scope.Container.Resolve(typeof(Shinzui.Application.Interfaces.IEnemyRuntime[]));
var presenter = (Shinzui.Presentation.EnemyPresenter)scope.Container.Resolve(typeof(Shinzui.Presentation.EnemyPresenter));
var tracker = (Shinzui.Application.Interfaces.IPlayerTracker)scope.Container.Resolve(typeof(Shinzui.Application.Interfaces.IPlayerTracker));
var views = UnityEngine.Object.FindObjectsByType<Shinzui.View.EnemyView>(UnityEngine.FindObjectsSortMode.None);
return new {
    phase = director.Phase.ToString(), pressure = director.Pressure, focus = director.FocusEnemyId,
    found = director.IsPlayerFound.CurrentValue, player = tracker.PlayerPosition.ToString(),
    runtimeCount = runtimes.Length,
    runtimes = runtimes.Select(r => r == null ? null : new { available = r.IsAvailable, ready = r.IsReady, position = r.Position.ToString(), sight = r.CanSee(tracker.PlayerPosition, 2f) }).ToArray(),
    agents = views.Select(v => new { name = v.name, agent = v.ResolveAgent() != null ? v.ResolveAgent().name : "missing", path = v.ResolveAgent() != null && v.ResolveAgent().isOnNavMesh ? v.ResolveAgent().pathStatus.ToString() : "offmesh", position = v.EnemyPosition.ToString(), destination = v.ResolveAgent() != null && v.ResolveAgent().isOnNavMesh ? v.ResolveAgent().destination.ToString() : "none" }).ToArray()
};