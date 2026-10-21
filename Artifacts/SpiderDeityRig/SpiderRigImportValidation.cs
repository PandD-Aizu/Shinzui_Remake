using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpiderRigImportValidation
{
    [Serializable] public class Report
    {
        public string unityVersion;
        public string animationType;
        public int rendererCount;
        public int vertexCount;
        public int skinBoneCount;
        public int tipCount;
        public int unweightedVertices;
        public string[] validatedLegs;
        public Vector3 boundsSize;
        public bool passed;
    }
    public static void Run()
    {
        const string path = "Assets/Shinzui/3DModels/SpiderDeity/SpiderDeity_Rigged.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
        importer.preserveHierarchy = true;
        importer.isReadable = true;
        importer.SaveAndReimport();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new Exception("URP Lit shader unavailable");
        const string matPath = "Assets/Shinzui/3DModels/SpiderDeity/SpiderDeity_Gray.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
        material.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 1));
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", 0.5f);
        var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var m in sourceAsset.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), material);
        importer.SaveAndReimport();
        sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = UnityEngine.Object.Instantiate(sourceAsset);
        try
        {
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1) throw new Exception("Expected one skinned mesh");
            var smr = renderers[0];
            var mesh = smr.sharedMesh;
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            var tips = transforms.Where(t => t.name.StartsWith("Leg_") && t.name.EndsWith("_Tip")).ToArray();
            if (tips.Length != 8 || mesh.bindposes.Length != smr.bones.Length) throw new Exception("Invalid skeleton");
            var weights = mesh.boneWeights;
            int missing = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) > 0.0001f);
            if (missing != 0) throw new Exception("Missing or unnormalized weights");
            var validated = new System.Collections.Generic.List<string>();
            var before = new Mesh(); var after = new Mesh();
            try
            {
                foreach (var tip in tips)
                {
                    var foot = tip.parent;
                    if (!foot.name.EndsWith("_Foot") || !foot.parent.name.EndsWith("_Lower") || !foot.parent.parent.name.EndsWith("_Upper")) throw new Exception("Invalid leg chain");
                    smr.BakeMesh(before);
                    var rotation = foot.localRotation;
                    foot.localRotation = rotation * Quaternion.Euler(12, 0, 0);
                    smr.BakeMesh(after);
                    if (!before.vertices.Zip(after.vertices, (a,b) => (a-b).sqrMagnitude).Any(d => d > 1e-10f)) throw new Exception("Leg failed deformation: " + foot.name);
                    foot.localRotation = rotation;
                    validated.Add(tip.name.Replace("_Tip", ""));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
            var report = new Report { unityVersion = Application.unityVersion, animationType = importer.animationType.ToString(), rendererCount = renderers.Length, vertexCount = mesh.vertexCount, skinBoneCount = smr.bones.Length, tipCount = tips.Length, unweightedVertices = missing, validatedLegs = validated.ToArray(), boundsSize = smr.bounds.size, passed = true };
            File.WriteAllText("Artifacts/SpiderDeityRig/unity_validation.json", JsonUtility.ToJson(report, true));
            Debug.Log("SPIDER_RIG_VALIDATION_PASSED " + JsonUtility.ToJson(report));
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        importer.isReadable = false;
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
    }
}
