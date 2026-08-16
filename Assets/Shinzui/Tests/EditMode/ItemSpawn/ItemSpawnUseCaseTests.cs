using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Application.UseCases.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnUseCaseTests
    {
        private static List<SpawnSurfaceTriangleDto> CreateTestTrianglesDto()
        {
            return new List<SpawnSurfaceTriangleDto>
            {
                new SpawnSurfaceTriangleDto
                {
                    VertexA = new Vector3(0, 0, 0),
                    VertexB = new Vector3(10, 0, 0),
                    VertexC = new Vector3(0, 0, 10),
                    PaintWeight = 1.0f,
                    RegionWeight = 1.0f,
                    SurfaceId = 1,
                    SurfaceName = "Floor_1",
                    TriangleIndex = 0
                }
            };
        }

        [Test]
        public void ExecuteSpawn_WithNullResourceNeedUseCase_UsesNeutralFallback()
        {
            // IPlayerResourceNeedUseCaseがnullの場合（DI未注入時）
            var useCase = new ItemSpawnUseCase(resourceNeedUseCase: null);

            var settings = new ItemSpawnSettingsDto
            {
                Budget = 10,
                MaxTotalCount = 5,
                MaxPlacementAttempts = 10
            };

            var definitions = new List<ItemSpawnDefinitionDto>
            {
                new ItemSpawnDefinitionDto
                {
                    Id = "item_herb",
                    BaseWeight = 2.0f,
                    Category = ResourceCategory.Healing,
                    SpawnCost = 1
                }
            };

            var triangles = CreateTestTrianglesDto();

            var result = useCase.ExecuteSpawn(settings, definitions, triangles, seed: 12345);

            Assert.That(result.Success, Is.True);
            Assert.That(result.SpawnedItems.Count, Is.GreaterThan(0));
            // 中立フォールバックによりNeedWeightは1.0f
            Assert.That(result.CategoryNeedWeights[ResourceCategory.Healing], Is.EqualTo(1.0f));
            // Final Weight = BaseWeight(2.0) * NeedWeight(1.0) = 2.0f
            Assert.That(result.FinalItemWeights["item_herb"], Is.EqualTo(2.0f));
        }

        [Test]
        public void ExecuteSpawn_WithInjectedResourceNeedUseCase_ReflectsDynamicNeedWeights()
        {
            var mockNeedUseCase = new MockResourceNeedUseCase(new Dictionary<ResourceCategory, float>
            {
                { ResourceCategory.Ammo, 2.5f },
                { ResourceCategory.Healing, 0.5f }
            });

            var useCase = new ItemSpawnUseCase(resourceNeedUseCase: mockNeedUseCase);

            var settings = new ItemSpawnSettingsDto
            {
                Budget = 20,
                MaxTotalCount = 10,
                MaxPlacementAttempts = 10
            };

            var definitions = new List<ItemSpawnDefinitionDto>
            {
                new ItemSpawnDefinitionDto
                {
                    Id = "ammo_handgun",
                    BaseWeight = 1.0f,
                    Category = ResourceCategory.Ammo,
                    SpawnCost = 1
                },
                new ItemSpawnDefinitionDto
                {
                    Id = "herb_green",
                    BaseWeight = 1.0f,
                    Category = ResourceCategory.Healing,
                    SpawnCost = 1
                }
            };

            var triangles = CreateTestTrianglesDto();

            var result = useCase.ExecuteSpawn(settings, definitions, triangles, seed: 9876);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CategoryNeedWeights[ResourceCategory.Ammo], Is.EqualTo(2.5f));
            Assert.That(result.CategoryNeedWeights[ResourceCategory.Healing], Is.EqualTo(0.5f));
            Assert.That(result.FinalItemWeights["ammo_handgun"], Is.EqualTo(2.5f));
            Assert.That(result.FinalItemWeights["herb_green"], Is.EqualTo(0.5f));
        }

        [Test]
        public void ExecuteSpawn_WithPhysicsCollisionChecker_RejectsBlockedPositions()
        {
            var mockPhysics = new MockPhysicsCollisionChecker(alwaysOverlap: true);
            var useCase = new ItemSpawnUseCase(physicsCollisionChecker: mockPhysics);

            var settings = new ItemSpawnSettingsDto
            {
                Budget = 10,
                MaxTotalCount = 5,
                MaxPlacementAttempts = 5,
                CheckPhysicsCollision = true
            };

            var definitions = new List<ItemSpawnDefinitionDto>
            {
                new ItemSpawnDefinitionDto
                {
                    Id = "blocked_item",
                    BaseWeight = 1.0f,
                    Category = ResourceCategory.Utility,
                    SpawnCost = 1
                }
            };

            var triangles = CreateTestTrianglesDto();

            var result = useCase.ExecuteSpawn(settings, definitions, triangles, seed: 1111);

            // 物理判定で全て遮断されるため、生成数は0で終了
            Assert.That(result.SpawnedItems.Count, Is.EqualTo(0));
            Assert.That(result.FailedPlacementAttempts, Is.GreaterThan(0));
            Assert.That(result.SkipReasons.Count, Is.GreaterThan(0));
        }

        private sealed class MockResourceNeedUseCase : IPlayerResourceNeedUseCase
        {
            private readonly Dictionary<ResourceCategory, float> _weights;

            public MockResourceNeedUseCase(Dictionary<ResourceCategory, float> weights)
            {
                _weights = weights ?? new Dictionary<ResourceCategory, float>();
            }

            public float GetNeedWeight(ResourceCategory category)
            {
                return _weights.TryGetValue(category, out float w) ? w : 1.0f;
            }

            public float GetNeedWeight(ResourceCategory category, PlayerResourceSnapshot snapshot) => GetNeedWeight(category);
            public PlayerResourceSnapshot CaptureSnapshot() => default;
            public PlayerResourceNeedEvaluationResult Evaluate() => null;
            public PlayerResourceNeedEvaluationResult Evaluate(PlayerResourceSnapshot snapshot) => null;
            public ResourceRatioBreakdown GetRatios() => default;
            public ResourceRatioBreakdown GetRatios(PlayerResourceSnapshot snapshot) => default;
        }

        private sealed class MockPhysicsCollisionChecker : IPhysicsCollisionChecker
        {
            private readonly bool _alwaysOverlap;

            public MockPhysicsCollisionChecker(bool alwaysOverlap)
            {
                _alwaysOverlap = alwaysOverlap;
            }

            public bool CheckOverlap(
                Vector3 position,
                Quaternion rotation,
                Vector3 extents,
                PlacementCollisionShape shape,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return _alwaysOverlap;
            }
        }
    }
}
