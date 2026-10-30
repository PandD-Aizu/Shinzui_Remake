using System.Linq;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Tests.TunnelGeneration
{
    /// <summary>ロッカー追加が既存ギミックの抽選を変えないことを確認</summary>
    public sealed class HidingLockerPlacementTests
    {
        /// <summary>追加前後の巣と報知器が完全一致し、小部屋を優先する</summary>
        [Test] public void LockersPreserveExistingPlacementsAndPreferRooms()
        {
            var config = new TunnelGenerationConfig { CarCount = 0 };
            var layout = new TunnelLayoutGenerator().GenerateLayout(config);
            var planner = new TunnelGimmickPlanner();
            var old = planner.Plan(layout, new TunnelGenerationConfig { CarCount = 0, HidingLockerCount = 0 }, null, 0);
            var added = planner.Plan(layout, config, null, 0);
            Assert.That(added.Where(p => p.Kind != TunnelGimmickKind.HidingLocker).Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed)),
                Is.EqualTo(old.Select(p => (p.Kind, p.HostId, p.LocalZ, p.Side, p.VisualSeed))));
            var lockers = added.Where(p => p.Kind == TunnelGimmickKind.HidingLocker).ToArray();
            Assert.That(lockers.Length, Is.EqualTo(4));
            if (layout.SmallRooms.Count > 0) Assert.That(lockers[0].HostId, Is.LessThan(0));
            var positions = lockers.Select(p => Position(layout, config, p)).ToArray();
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That((positions[i] - positions[j]).Magnitude, Is.GreaterThanOrEqualTo(25f));
            foreach (var locker in lockers.Where(p => p.HostId >= 0))
            {
                var host = layout.Tunnels.Single(t => t.Id == locker.HostId);
                Assert.That(host.Id, Is.Not.Zero); Assert.That(host.IsSpecial, Is.False); Assert.That(host.Slope, Is.Zero);
            }
        }
        /// <summary>小部屋が集中しても遠方のトンネルへ分散する</summary>
        [Test] public void LockersSpreadBeyondClusteredRooms()
        {
            var rooms = Enumerable.Range(0, 4).Select(i => new SmallRoomLayout(i, 0,
                new TunnelVector3(i * 30f, 0, 0), new TunnelVector3(0, 0, 1), 10, 5, 6, 4)).ToArray();
            var tunnels = Enumerable.Range(1, 3).Select(i => new TunnelNodeLayout(i, "Tunnel",
                new TunnelVector3(i * 500f, 0, 0), new TunnelVector2(0, 1), 200, false,
                System.Array.Empty<TunnelEntranceMarkerLayout>())).ToArray();
            var layout = new TunnelLayoutResult(tunnels, System.Array.Empty<NormalCorridorLayout>(), rooms,
                System.Array.Empty<WarpCorridorLayout>(), null, null, 1234);
            var config = new TunnelGenerationConfig { CarCount = 0, SpiderWebCount = 0, FireAlarmCount = 0 };

            // 小部屋に全数を置かず、離れた3本へ1個ずつ配置する
            var lockers = new TunnelGimmickPlanner().Plan(layout, config, null, 0);
            Assert.That(lockers.Count, Is.EqualTo(4));
            Assert.That(lockers.Count(p => p.HostId < 0), Is.EqualTo(1));
            Assert.That(lockers.Select(p => p.HostId).Distinct().Count(), Is.EqualTo(4));
        }

        /// <summary>配置先が1本だけでも最低間隔を守って不足分を補う</summary>
        [Test] public void LockersFallBackToSameTunnelWhenNecessary()
        {
            var tunnel = new TunnelNodeLayout(1, "Tunnel", new TunnelVector3(0, 0, 0),
                new TunnelVector2(0, 1), 1000, false, System.Array.Empty<TunnelEntranceMarkerLayout>());
            var layout = new TunnelLayoutResult(new[] { tunnel }, System.Array.Empty<NormalCorridorLayout>(),
                System.Array.Empty<SmallRoomLayout>(), System.Array.Empty<WarpCorridorLayout>(), null, null, 1234);
            var config = new TunnelGenerationConfig { CarCount = 0, SpiderWebCount = 0, FireAlarmCount = 0 };

            // 同一シードの再現性と追加配置の間隔を確認する
            var planner = new TunnelGimmickPlanner();
            var lockers = planner.Plan(layout, config, null, 0);
            Assert.That(lockers.Count, Is.EqualTo(4));
            Assert.That(planner.Plan(layout, config, null, 0).Select(p => (p.HostId, p.LocalZ, p.Side)),
                Is.EqualTo(lockers.Select(p => (p.HostId, p.LocalZ, p.Side))));
            var positions = lockers.Select(p => Position(layout, config, p)).ToArray();
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That((positions[i] - positions[j]).Magnitude, Is.GreaterThanOrEqualTo(25f));
        }

        /// <summary>配置計画をワールド位置へ変換</summary>
        /// <param name="layout">地形</param>
        /// <param name="config">設定</param>
        /// <param name="p">配置</param>
        /// <returns>位置</returns>
        private static TunnelVector3 Position(TunnelLayoutResult layout, TunnelGenerationConfig config, TunnelGimmickPlacement p)
        {
            if (p.HostId < 0)
            {
                var r = layout.SmallRooms.Single(r => r.RoomNumber == -p.HostId - 1);
                return r.Center + new TunnelVector3(r.Direction.Z, 0, -r.Direction.X) * (p.Side * (r.Width * .5f - .4f));
            }
            var t = layout.Tunnels.Single(t => t.Id == p.HostId);
            return t.Position + new TunnelVector3(t.Forward.X, 0, t.Forward.Y) * p.LocalZ
                + new TunnelVector3(t.Forward.Y, 0, -t.Forward.X) * (p.Side * (config.TunnelWidth * .5f - .4f));
        }
    }
}
