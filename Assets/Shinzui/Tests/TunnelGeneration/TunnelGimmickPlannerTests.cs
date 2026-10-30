#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Tests.TunnelGeneration
{
    public sealed class TunnelGimmickPlannerTests
    {
        /// <summary>同じシードから配置と外観が一致することを確認する</summary>
        [Test]
        public void SameSeedProducesIdenticalPlacements()
        {
            var config = new TunnelGenerationConfig { CarCount = 0 };
            var layout = new TunnelLayoutGenerator().GenerateLayout(config);
            var planner = new TunnelGimmickPlanner();
            var first = planner.Plan(layout, config, 1, 1);
            var second = planner.Plan(layout, config, 1, 1);
            Assert.That(first, Is.Not.Empty);
            Assert.That(second.Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed)),
                Is.EqualTo(first.Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed))));
        }

        /// <summary>開始地点と特殊区間と傾斜を除き、開口部とゴールから余白を取る</summary>
        [Test]
        public void AlarmsRespectTunnelAndOpeningConstraints()
        {
            var config = new TunnelGenerationConfig { CarCount = 0, FireAlarmCount = 30, FireAlarmMinSpacing = 0 };
            var layout = CreateLayout();
            var alarms = new TunnelGimmickPlanner().Plan(layout, config, 1, 1)
                .Where(p => p.Kind == TunnelGimmickKind.FireAlarm).ToArray();
            Assert.That(alarms.Length, Is.EqualTo(3));
            Assert.That(alarms.Select(p => p.HostId).Distinct().Count(), Is.EqualTo(alarms.Length));
            foreach (var alarm in alarms)
            {
                var tunnel = layout.Tunnels.Single(t => t.Id == alarm.HostId);
                Assert.That(tunnel.Id, Is.Not.Zero);
                Assert.That(tunnel.IsSpecial, Is.False);
                Assert.That(tunnel.Slope, Is.Zero);
                Assert.That(Math.Abs(alarm.LocalZ), Is.LessThanOrEqualTo(tunnel.Length / 2 - config.FireAlarmEndMargin));
                if (tunnel.Id == 1) Assert.That(alarm.LocalZ, Is.LessThanOrEqualTo(tunnel.Length / 2 - 20));
                foreach (var mouth in tunnel.EntranceMarkers.Where(m => m.IsOpen && Math.Sign(m.LocalPosition.X) == alarm.Side))
                    Assert.That(Math.Abs(mouth.LocalPosition.Z - alarm.LocalZ), Is.GreaterThanOrEqualTo(config.FireAlarmMouthMargin));
                foreach (var opening in layout.DropOpenings.Where(o => o.LowerTunnelId == tunnel.Id || o.UpperTunnelId == tunnel.Id))
                    Assert.That(Math.Abs(opening.Center.Z - tunnel.Position.Z - alarm.LocalZ), Is.GreaterThanOrEqualTo(opening.Size + 2));
            }
        }

        /// <summary>採用した報知器同士のワールド距離を確認する</summary>
        [Test]
        public void AlarmsKeepMinimumWorldSpacing()
        {
            var config = new TunnelGenerationConfig { CarCount = 0, FireAlarmCount = 30, FireAlarmMinSpacing = 40 };
            var layout = CreateLayout();
            var alarms = new TunnelGimmickPlanner().Plan(layout, config, null, 0)
                .Where(p => p.Kind == TunnelGimmickKind.FireAlarm).ToArray();
            Assert.That(alarms.Length, Is.GreaterThanOrEqualTo(2));
            var positions = alarms.Select(a =>
            {
                var t = layout.Tunnels.Single(t => t.Id == a.HostId);
                return t.Position + new TunnelVector3(t.Forward.X, 0, t.Forward.Y) * a.LocalZ +
                    new TunnelVector3(t.Forward.Y, 0, -t.Forward.X) * (a.Side * config.TunnelWidth / 2);
            }).ToArray();
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That((positions[i] - positions[j]).Magnitude, Is.GreaterThanOrEqualTo(config.FireAlarmMinSpacing));
        }

        /// <summary>両側が閉じた十分な長さの通路だけに一枚ずつ配置する</summary>
        [Test]
        public void WebsUseClosedCorridorsOnceAndKeepEndMargins()
        {
            var config = new TunnelGenerationConfig { CarCount = 0, SpiderWebCount = 30, FireAlarmCount = 0, HidingLockerCount = 0 };
            var layout = CreateLayout();
            var webs = new TunnelGimmickPlanner().Plan(layout, config, null, 0);
            Assert.That(webs.Count, Is.EqualTo(2));
            Assert.That(webs.Select(p => p.HostId).Distinct().Count(), Is.EqualTo(webs.Count));
            foreach (var web in webs)
            {
                var corridor = layout.NormalCorridors[web.HostId];
                Assert.That(web.Kind, Is.EqualTo(TunnelGimmickKind.SpiderWeb));
                Assert.That(corridor.OpenLeft || corridor.OpenRight, Is.False);
                Assert.That(Math.Abs(web.LocalZ), Is.LessThanOrEqualTo(corridor.Length / 2 - 1.5f));
            }
        }

        /// <summary>両方の個数をゼロにした場合は配置しない</summary>
        [Test]
        public void ZeroCountsProduceNoPlacements()
        {
            var config = new TunnelGenerationConfig { CarCount = 0, SpiderWebCount = 0, FireAlarmCount = 0, HidingLockerCount = 0 };
            Assert.That(new TunnelGimmickPlanner().Plan(CreateLayout(), config, null, 0), Is.Empty);
        }

        /// <summary>DTO化後も従来のゴール抽選順と追加設定が維持される</summary>
        [Test]
        public void UseCasePreservesLegacyGoalSelectionAndGimmickSettings()
        {
            var request = new TunnelGenerationRequestDto { CarCount = 0, Seed = 1234, SpiderWebCount = 0, FireAlarmCount = 0, HidingLockerCount = 0,
                FireAlarmEndMargin = 15, FireAlarmMouthMargin = 6, FireAlarmMinSpacing = 50 };
            var config = request.ToDomainConfig();
            Assert.That(config.FireAlarmEndMargin, Is.EqualTo(15));
            Assert.That(config.FireAlarmMouthMargin, Is.EqualTo(6));
            Assert.That(config.FireAlarmMinSpacing, Is.EqualTo(50));
            new GenerateTunnelUseCase(null).GenerateOnce(request, out var map);
            var candidates = map.Tunnels.Where(t => t.Slope == 0 && !t.IsSpecial && t.Id != 0).ToArray();
            var random = new Random(unchecked(map.Seed ^ 0x47504c));
            Assert.That(map.GoalTunnelId, Is.EqualTo(candidates[random.Next(candidates.Length)].Id));
            Assert.That(map.GoalEndSign, Is.EqualTo(random.Next(2) == 0 ? -1 : 1));
            Assert.That(map.Gimmicks, Is.Empty);
        }

        /// <summary>車は大きい平坦なトンネルだけに配置し、開口部と他の車から離す</summary>
        [Test]
        public void CarsRespectClearanceAndPreserveExistingPlacements()
        {
            var config = new TunnelGenerationConfig { CarCount = 6 };
            var layout = CreateLayout();
            var planner = new TunnelGimmickPlanner();
            var placements = planner.Plan(layout, config, 1, 1);
            var cars = placements.Where(p => p.Kind == TunnelGimmickKind.Car).ToArray();
            Assert.That(cars.Length, Is.EqualTo(6));

            // 追加前のギミックと同じシードの再生成を比較する
            Assert.That(placements.Where(p => p.Kind != TunnelGimmickKind.Car)
                .Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed)),
                Is.EqualTo(planner.Plan(layout, new TunnelGenerationConfig { CarCount = 0 }, 1, 1)
                    .Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed))));
            Assert.That(planner.Plan(layout, config, 1, 1).Where(p => p.Kind == TunnelGimmickKind.Car)
                .Select(p => (p.HostId, p.LocalX, p.LocalZ, p.Yaw, p.VisualSeed)),
                Is.EqualTo(cars.Select(p => (p.HostId, p.LocalX, p.LocalZ, p.Yaw, p.VisualSeed))));
            Assert.That(cars.Select(p => p.Yaw).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(cars.Select(p => p.LocalX).Distinct().Count(), Is.GreaterThan(1));

            var positions = cars.Select(p =>
            {
                var t = layout.Tunnels.Single(t => t.Id == p.HostId);
                Assert.That(t.Id, Is.Not.Zero);
                Assert.That(t.IsSpecial, Is.False);
                Assert.That(t.Slope, Is.Zero);
                Assert.That(Math.Abs(p.LocalX) + 2.1f + 1f, Is.LessThanOrEqualTo(config.TunnelWidth * .5f + .001f));
                Assert.That(Math.Abs(p.LocalZ), Is.LessThanOrEqualTo(t.Length * .5f - 12f));
                Assert.That(p.Yaw, Is.InRange(0f, 360f));
                if (t.Id == 1) Assert.That(p.LocalZ, Is.LessThanOrEqualTo(t.Length * .5f - 22.1f));
                foreach (var mouth in t.EntranceMarkers.Where(m => m.IsOpen))
                    Assert.That(Math.Abs(mouth.LocalPosition.Z - p.LocalZ), Is.GreaterThanOrEqualTo(config.CorridorWidth * .5f + 3.1f));
                foreach (var opening in layout.DropOpenings.Where(o => o.LowerTunnelId == t.Id || o.UpperTunnelId == t.Id))
                    Assert.That(Math.Abs(opening.Center.Z - t.Position.Z - p.LocalZ), Is.GreaterThanOrEqualTo(opening.Size + 2.1f));
                return t.Position + new TunnelVector3(p.LocalX, 0f, p.LocalZ);
            }).ToArray();
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That((positions[i] - positions[j]).Magnitude, Is.GreaterThanOrEqualTo(config.CarMinSpacing));
        }

        /// <summary>車の設定と座標がDTOを通り、狭いトンネルには生成しない</summary>
        [Test]
        public void CarSettingsAndCoordinatesReachDto()
        {
            var request = new TunnelGenerationRequestDto { CarCount = 5, CarMinSpacing = 18f };
            Assert.That(request.ToDomainConfig().CarCount, Is.EqualTo(5));
            Assert.That(request.ToDomainConfig().CarMinSpacing, Is.EqualTo(18f));
            new GenerateTunnelUseCase(null).GenerateOnce(request, out var map);
            var cars = map.Gimmicks.Where(p => p.Kind == TunnelGimmickKindDto.Car).ToArray();
            Assert.That(cars.Length, Is.EqualTo(5));
            Assert.That(cars.Any(p => p.LocalX != 0f && p.Yaw != 0f), Is.True);
            var narrow = new TunnelGenerationConfig { TunnelWidth = 3f, CarCount = 5 };
            Assert.That(new TunnelGimmickPlanner().Plan(CreateLayout(), narrow, null, 0)
                .Any(p => p.Kind == TunnelGimmickKind.Car), Is.False);
        }

        /// <summary>除外対象と境界条件を含む小さな配置を作る</summary>
        /// <returns>配置制約の検証用レイアウト</returns>
        private static TunnelLayoutResult CreateLayout()
        {
            var tunnels = Enumerable.Range(0, 6).Select(i => new TunnelNodeLayout(i, "Tunnel " + i,
                new TunnelVector3((i == 5 ? 4 : i) * 45, 0, 0), new TunnelVector2(0, 1), 100,
                i == 2, new[] { new TunnelEntranceMarkerLayout(0, "Mouth", new TunnelVector3(7, 0, 10), true, true) },
                slope: i == 3 ? 0.2f : 0)).ToArray();
            var corridors = Enumerable.Range(0, 5).Select(i => new NormalCorridorLayout(1,
                TunnelVector3.Zero, TunnelVector3.Forward, TunnelVector3.Zero, TunnelVector3.Forward,
                i == 3 ? 2 : i == 4 ? 3 : 8, openLeft: i == 1, openRight: i == 2)).ToArray();
            return new TunnelLayoutResult(tunnels, corridors, Array.Empty<SmallRoomLayout>(),
                Array.Empty<WarpCorridorLayout>(), 2, new TunnelBoundsLayout(TunnelVector3.Zero, TunnelVector3.One), 2777,
                new[] { new TunnelDropOpeningLayout(1, 4, new TunnelVector3(45, 0, -10), 3, 0, 12) });
        }
    }
}
#endif
