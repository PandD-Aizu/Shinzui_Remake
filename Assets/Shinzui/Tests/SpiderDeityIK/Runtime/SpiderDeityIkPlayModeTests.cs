#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Shinzui.Infrastructure.Animation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shinzui.Tests.SpiderDeityIK.Runtime
{
    public sealed class SpiderDeityIkPlayModeTests
    {
        private const string ScenePath = "Assets/Shinzui/Scenes/SpiderDeityIKPreview.unity";
        private Scene scene;

        [UnityTest]
        public IEnumerator SavedPreviewSceneMovesFeetAndAllowsManualControl()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(ScenePath);
            var motion = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<SpiderLegIkPreviewMotion>()).Single();
            var constraints = motion.GetComponentsInChildren<ChainIKConstraint>();
            Assert.That(constraints, Has.Length.EqualTo(8));
            var controls = constraints.Select(c => c.data.target).ToArray();
            var tips = constraints.Select(c => c.data.tip).ToArray();
            Assert.That(controls.All(t => t), Is.True);
            Assert.That(tips.All(t => t), Is.True);
            var initial = controls.Select(t => t.position).ToArray();
            yield return new WaitForSeconds(0.35f);
            Assert.That(controls.Select((t, i) => Vector3.Distance(initial[i], t.position)).Max(), Is.GreaterThan(0.005f));
            for (int i = 0; i < controls.Length; i++)
                Assert.That(Vector3.Distance(tips[i].position, controls[i].position), Is.LessThan(0.002f));
            motion.enabled = false;
            controls[0].position += Vector3.up * 0.05f;
            var desired = controls[0].position;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(controls[0].position, desired), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(tips[0].position, desired), Is.LessThan(0.001f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
