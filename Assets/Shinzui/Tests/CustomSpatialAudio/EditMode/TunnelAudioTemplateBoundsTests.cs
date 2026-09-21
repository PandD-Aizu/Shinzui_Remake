using NUnit.Framework;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using UnityEngine;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Infrastructure.CustomSpatialAudio;

namespace Shinzui.Tests.CustomSpatialAudio
{
    public sealed class TunnelAudioTemplateBoundsTests
    {
        [TestCase(2777)] [TestCase(42)] [TestCase(1234)] [TestCase(1)] [TestCase(999)]
        public void ActualSceneModelDimensionsBuildValidAcousticGraph(int seed)
        {
            var root = new GameObject("Actual model graph test");
            try
            {
                var view = root.AddComponent<TunnelMapView>();
                view.SetBuildTemplates(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx"));
                float length = 153.12695f, width = 14.8f, height = 5, passageLength = 8, passageWidth = 3;
                view.ResolveTemplateDimensions(ref length, ref width, ref height, ref passageLength, ref passageWidth);
                var request = new TunnelGenerationRequestDto { Seed = seed, TunnelCount = 8, SmallRoomWidth = 12, SmallRoomLength = 12,
                    TunnelLength = length, TunnelWidth = width, TunnelHeight = height, CorridorLength = passageLength,
                    CorridorWidth = passageWidth, ConnectionPointSpacing = view.ResolveConnectionPointSpacing(51.042316f) };
                new GenerateTunnelUseCase(null).GenerateOnce(request, out var map);
                var world = new GeneratedAcousticWorld(map);
                Assert.That(world.Graph.RoomCount, Is.GreaterThan(8));
                Assert.That(world.Graph.PortalCount, Is.GreaterThanOrEqualTo(world.Graph.RoomCount - 1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MainPrefabDimensionsUseMeshDataAndEncloseAllSixEntrances()
        {
            var root = new GameObject("Template measurement test");
            try
            {
                var view = root.AddComponent<TunnelMapView>();
                view.SetBuildTemplates(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx"));
                float length = 1, width = 1, height = 5, passageLength = 1, passageWidth = 1;
                view.ResolveTemplateDimensions(ref length, ref width, ref height, ref passageLength, ref passageWidth);
                float spacing = view.ResolveConnectionPointSpacing(51.042316f);
                Assert.That(length, Is.GreaterThan(2 * spacing), "Outer mouths must lie inside the measured tunnel.");
                Assert.That(spacing, Is.GreaterThan(0));
                Assert.That(width, Is.GreaterThan(1));
                Assert.That(height, Is.GreaterThan(2));
                Assert.That(passageLength, Is.GreaterThan(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
