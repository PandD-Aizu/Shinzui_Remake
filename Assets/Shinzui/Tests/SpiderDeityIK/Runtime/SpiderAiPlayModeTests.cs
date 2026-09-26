#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Infrastructure.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shinzui.Tests.SpiderDeityIK.Runtime
{
    public sealed class SpiderAiPlayModeTests
    {
        private Scene scene;
        private SpiderNavigationDriver driver;
        private SpiderBodyGrounding body;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            const string path = "Assets/Shinzui/Scenes/SpiderDeityAiPreview.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            driver = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpiderNavigationDriver>()).Single();
            body = driver.GetComponentInChildren<SpiderBodyGrounding>();
            yield return new WaitForSeconds(.7f);
            Assert.That(driver.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator SlopeAlignmentPreservesUprightNavigationAndFootReach()
        {
            driver.Target = null;
            var legs = body.GetComponentsInChildren<ChainIKConstraint>();
            var start = driver.transform.position;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return null;
                Assert.That(body.SupportCount, Is.EqualTo(8));
                Assert.That(body.TiltAngle, Is.InRange(5.5f, 8.5f));
                Assert.That(Vector3.Angle(driver.transform.up, Vector3.up), Is.LessThan(.01f));
                Assert.That(Mathf.Abs(body.HeightOffset), Is.LessThanOrEqualTo(.15f));
                foreach (var leg in legs)
                    Assert.That(Vector3.Distance(leg.data.tip.position, leg.data.target.position), Is.LessThan(.025f), leg.name);
            }
            Assert.That(Vector3.Distance(start, driver.transform.position), Is.GreaterThan(.1f));
            var camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>()).Single();
            SpiderWalkPlayModeTests.CaptureCamera(camera, "Artifacts/SpiderDeityIK/unity_ai_preview.png");
        }

        [UnityTest]
        public IEnumerator SightChaseStopOcclusionMemoryAndTargetRemoval()
        {
            var target = driver.Target;
            target.position = driver.transform.position + Vector3.forward * .9f;
            yield return new WaitForSeconds(.35f);
            Assert.That(driver.State, Is.EqualTo(SpiderMovementState.Chase));
            Assert.That(driver.RouteBlocked, Is.False);
            target.position = driver.transform.position + Vector3.forward * .3f;
            yield return new WaitForSeconds(.3f);
            Assert.That(driver.State, Is.EqualTo(SpiderMovementState.Hold));
            var holdPosition = driver.transform.position;
            yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(holdPosition, driver.transform.position), Is.LessThan(.005f));
            target.position = driver.transform.position + Vector3.forward * .9f;
            yield return new WaitForSeconds(.1f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = driver.transform.position + Vector3.forward * .45f + Vector3.up * .5f;
            wall.transform.localScale = new Vector3(.4f, 1.2f, .05f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.1f);
            Assert.That(driver.State, Is.EqualTo(SpiderMovementState.Search));
            yield return new WaitForSeconds(2.2f);
            Assert.That(driver.State, Is.EqualTo(SpiderMovementState.Patrol));
            Object.Destroy(target.gameObject);
            yield return null;
            Assert.That(driver.State, Is.EqualTo(SpiderMovementState.Patrol));
        }

        [UnityTest]
        public IEnumerator ObstacleHasDetourAndUnreachableDestinationStops()
        {
            driver.Target = null;
            var waypoint = new GameObject("Test destination").transform;
            SceneManager.MoveGameObjectToScene(waypoint.gameObject, scene);
            waypoint.position = new Vector3(1.3f, .2f, 1.2f);
            driver.SetPatrolPoints(new[] { waypoint });
            yield return new WaitForSeconds(.6f);
            var agent = driver.GetComponent<NavMeshAgent>();
            Assert.That(driver.RouteBlocked, Is.False);
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(agent.path.corners.Length, Is.GreaterThan(2));
            waypoint.position = new Vector3(50f, 0f, 50f);
            yield return new WaitForSeconds(.5f);
            Assert.That(driver.RouteBlocked, Is.True);
            Assert.That(agent.hasPath, Is.False);
            var stopped = driver.transform.position;
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(stopped, driver.transform.position), Is.LessThan(.005f));
        }

        [UnityTest]
        public IEnumerator PatrolAdvancesAfterArrivalAndDisabledDriverStops()
        {
            driver.Target = null;
            var first = new GameObject("Near patrol point").transform;
            var second = new GameObject("Next patrol point").transform;
            SceneManager.MoveGameObjectToScene(first.gameObject, scene);
            SceneManager.MoveGameObjectToScene(second.gameObject, scene);
            first.position = driver.transform.position + Vector3.forward * .07f;
            second.position = first.position + Vector3.forward * .25f;
            driver.SetPatrolPoints(new[] { first, second });
            yield return new WaitForSeconds(2.5f);
            var agent = driver.GetComponent<NavMeshAgent>();
            Assert.That(Vector3.Distance(agent.destination, second.position), Is.LessThan(.08f));
            driver.enabled = false;
            var stopped = driver.transform.position;
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(stopped, driver.transform.position), Is.LessThan(.005f));
        }

        [UnityTest]
        public IEnumerator MissingNavMeshDoesNotMoveAndMissingSupportReturnsBodyToNeutral()
        {
            driver.enabled = false;
            foreach (var collider in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider>())) collider.enabled = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.8f);
            Assert.That(body.SupportCount, Is.Zero);
            Assert.That(body.TiltAngle, Is.LessThan(.2f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_AI.prefab");
            var isolated = Object.Instantiate(prefab, new Vector3(50, 0, 50), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(isolated, scene);
            yield return new WaitForSeconds(.7f);
            Assert.That(isolated.GetComponent<SpiderNavigationDriver>().IsReady, Is.False);
            Assert.That(isolated.transform.position, Is.EqualTo(new Vector3(50, 0, 50)));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
