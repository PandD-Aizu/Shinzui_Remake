#if UNITY_EDITOR
using NUnit.Framework;
using Shinzui.View.GenerateTunnel;
using UnityEngine;

namespace Shinzui.Tests.TunnelGeneration
{
    /// <summary>取り込んだ車の接地と重複配置を検証する</summary>
    public sealed class TunnelCarPlacementTests
    {
        /// <summary>両モデルが床へ接地し、同じ場所への追加配置を拒否する</summary>
        /// <param name="seed">モデル選択</param>
        [TestCase(0)]
        [TestCase(1)]
        public void CarRestsOnFloorAndRejectsOverlap(int seed)
        {
            var host = new GameObject("Car Test Host");
            host.transform.position = new Vector3(10000f, 10000f, 10000f);
            host.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            try
            {
                // 他のシーンから離れた床を作り、任意の向きで車を生成する
                var floor = new GameObject("Floor");
                floor.transform.SetParent(host.transform, false);
                var collider = floor.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, -.1f, 0f);
                collider.size = new Vector3(15f, .2f, 30f);
                var view = host.AddComponent<TunnelMapView>();
                var car = view.CreateCar(host.transform, 2f, 4f, 123f, seed);
                Assert.That(car, Is.Not.Null);
                var shape = car.GetComponent<BoxCollider>();
                Assert.That(shape.size.z, Is.EqualTo(3.4f).Within(.01f));
                Assert.That(new Vector2(shape.size.x, shape.size.z).magnitude * .5f, Is.LessThan(2.1f));
                Assert.That(shape.bounds.min.y - host.transform.position.y, Is.EqualTo(.03f).Within(.01f));
                Assert.That(Quaternion.Angle(car.transform.localRotation, Quaternion.Euler(0f, 123f, 0f)), Is.LessThan(.01f));
                Assert.That(view.CreateCar(host.transform, 2f, 4f, 123f, seed), Is.Null);
                Assert.That(view.CreateCar(host.transform, 0f, 40f, 0f, seed), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
