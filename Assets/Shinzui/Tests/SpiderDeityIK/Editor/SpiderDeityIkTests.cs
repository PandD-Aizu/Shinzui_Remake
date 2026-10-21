using System.Linq;
using NUnit.Framework;
using Shinzui.Infrastructure.Animation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Shinzui.Tests.SpiderDeityIK
{
    public sealed class SpiderDeityIkTests
    {
        private const string PrefabPath = "Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_IK.prefab";
        private GameObject root;
        private RigBuilder builder;
        private Animator animator;

        private void CreateRig(float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            root = Object.Instantiate(prefab);
            root.transform.SetPositionAndRotation(new Vector3(2f, 0.3f, -1f), Quaternion.Euler(0, 37, 0));
            root.transform.localScale = Vector3.one * scale;
            Assert.That(root.GetComponent<SpiderDeityIkRig>().InitializeRig(), Is.True);
            animator = root.GetComponent<Animator>();
            animator.Rebind();
            animator.Update(0f);
            builder = root.GetComponent<RigBuilder>();
            Assert.That(builder.Build(), Is.True);
            builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Step();
        }

        private void Step()
        {
            animator.Update(1f / 60f);
            builder.Evaluate(1f / 60f);
        }

        [TearDown]
        public void Cleanup()
        {
            if (builder) builder.Clear();
            if (root) Object.DestroyImmediate(root);
        }

        [TestCase("Leg_L01"), TestCase("Leg_L02"), TestCase("Leg_L03"), TestCase("Leg_L04")]
        [TestCase("Leg_R01"), TestCase("Leg_R02"), TestCase("Leg_R03"), TestCase("Leg_R04")]
        public void EachFootReachesItsTargetWithoutMovingBodyOrOtherLegs(string name)
        {
            CreateRig();
            var constraints = root.GetComponentsInChildren<ChainIKConstraint>();
            Assert.That(constraints, Has.Length.EqualTo(8));
            var active = constraints.Single(c => c.name == name + "_IK");
            var otherTips = constraints.Where(c => c != active).Select(c => c.data.tip).ToArray();
            var otherPositions = otherTips.Select(t => t.position).ToArray();
            var body = root.GetComponentsInChildren<Transform>().Single(t => t.name == "Body");
            var bodyPosition = body.position;
            var bodyRotation = body.rotation;
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var before = new Mesh();
            var after = new Mesh();
            try
            {
                renderer.BakeMesh(before);
                active.data.target.position += root.transform.TransformVector(new Vector3(0.01f, 0.06f, 0.015f));
                var desiredPosition = active.data.target.position;
                for (int frame = 0; frame < 3; frame++) Step();
                Assert.That(Vector3.Distance(active.data.target.position, desiredPosition), Is.LessThan(0.00001f), "Animator must not overwrite scene controls.");
                Assert.That(Vector3.Distance(active.data.tip.position, desiredPosition), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(bodyPosition, body.position), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(bodyRotation, body.rotation), Is.LessThan(0.01f));
                for (int i = 0; i < otherTips.Length; i++)
                    Assert.That(Vector3.Distance(otherTips[i].position, otherPositions[i]), Is.LessThan(0.0001f), otherTips[i].name);
                renderer.BakeMesh(after);
                Assert.That(before.vertices.Zip(after.vertices, (a, b) => Vector3.Distance(a, b)).Max(), Is.GreaterThan(0.005f));
            }
            finally
            {
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
            }
        }

        [Test]
        public void UnreachableTargetDoesNotStretchBonesOrProduceInvalidTransforms()
        {
            CreateRig();
            var c = root.GetComponentsInChildren<ChainIKConstraint>()[0];
            var chain = new[] { c.data.root, c.data.root.GetChild(0), c.data.tip.parent, c.data.tip };
            var lengths = Enumerable.Range(0, 3).Select(i => Vector3.Distance(chain[i].position, chain[i + 1].position)).ToArray();
            c.data.target.position = c.data.root.position + Vector3.up * 10;
            Step();
            for (int i = 0; i < 3; i++)
                Assert.That(Vector3.Distance(chain[i].position, chain[i + 1].position), Is.EqualTo(lengths[i]).Within(0.00001f));
            Assert.That(Vector3.Distance(c.data.root.position, c.data.tip.position), Is.LessThanOrEqualTo(lengths.Sum() + 0.0001f));
            foreach (var bone in chain)
                Assert.That(float.IsNaN(bone.position.sqrMagnitude) || float.IsInfinity(bone.position.sqrMagnitude), Is.False);
        }

        [Test]
        public void WeightZeroRestoresInputPoseAndRigRebuildResumesFollowing()
        {
            CreateRig();
            var c = root.GetComponentsInChildren<ChainIKConstraint>()[0];
            var restPosition = c.data.tip.position;
            c.data.target.position += Vector3.up * 0.06f;
            Step();
            c.weight = 0;
            c.data.target.position += Vector3.up * 0.015f;
            Step();
            Assert.That(Vector3.Distance(c.data.tip.position, restPosition), Is.LessThan(0.0001f));
            c.weight = 1;
            builder.Clear();
            Assert.That(builder.Build(), Is.True);
            builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Step();
            Assert.That(Vector3.Distance(c.data.tip.position, c.data.target.position), Is.LessThan(0.001f));
        }

        [TestCase(0.5f), TestCase(3f)]
        public void UniformlyScaledRigStillReachesTargets(float scale)
        {
            CreateRig(scale);
            foreach (var c in root.GetComponentsInChildren<ChainIKConstraint>())
                c.data.target.position += root.transform.TransformVector(Vector3.up * 0.04f);
            Step();
            foreach (var c in root.GetComponentsInChildren<ChainIKConstraint>())
                Assert.That(Vector3.Distance(c.data.tip.position, c.data.target.position), Is.LessThan(0.001f));
        }

    }
}
