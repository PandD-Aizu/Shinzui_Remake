using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>
    /// Resolves the instantiated model's bones before Animation Rigging builds its graph.
    /// This also refreshes nested-prefab references across an Editor play-mode reload.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RigBuilder))]
    public sealed class SpiderDeityIkRig : MonoBehaviour
    {
        private RigBuilder builder;

        private void OnEnable()
        {
            if (InitializeRig()) builder.enabled = true;
        }

        public bool InitializeRig()
        {
            builder = GetComponent<RigBuilder>();
            builder.enabled = false;
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
                if (candidate.name.StartsWith("Leg_", StringComparison.Ordinal) && !candidate.name.EndsWith("_IK", StringComparison.Ordinal))
                    if (!bones.TryAdd(candidate.name, candidate))
                        return Fail("Duplicate bone name: " + candidate.name);

            var constraints = GetComponentsInChildren<ChainIKConstraint>(true);
            if (constraints.Length != 8) return Fail("Expected exactly eight leg constraints.");
            foreach (var constraint in constraints)
            {
                string leg = constraint.name.EndsWith("_IK", StringComparison.Ordinal)
                    ? constraint.name.Substring(0, constraint.name.Length - 3) : string.Empty;
                var target = transform.Find("LegRig/Targets/IK_" + leg);
                if (!bones.TryGetValue(leg + "_Upper", out var upper) ||
                    !bones.TryGetValue(leg + "_Tip", out var tip) || !target || !tip.IsChildOf(upper))
                    return Fail("Incomplete chain or target for " + constraint.name);
                var data = constraint.data;
                data.root = upper;
                data.tip = tip;
                data.target = target;
                constraint.data = data;
            }
            return true;
        }

        private bool Fail(string reason)
        {
            Debug.LogError("Spider IK could not initialize: " + reason, this);
            return false;
        }

        private void OnDisable()
        {
            if (builder) builder.enabled = false;
        }
    }
}
