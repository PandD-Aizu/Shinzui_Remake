using NUnit.Framework;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Player;
using UnityEngine;

namespace Shinzui.Tests
{
    public class PlayerEntityMovementTests
    {
        [Test]
        public void CalculateVelocity_PreservesAnalogInputMagnitudeAfterDeadZone()
        {
            PlayerEntity player = CreatePlayer(new PlayerSpeedStatus
            {
                MoveSpeed = 2.0f,
                AccelerationRate = 100.0f,
                InputDeadZone = 0.1f
            });
            player.UpdateState(true, false, false);

            player.CalculateVelocity(
                new Vector2(0.0f, 0.5f),
                Vector3.right,
                Vector3.forward,
                Vector3.up,
                true,
                1.0f);

            float expectedSpeed = 2.0f * Mathf.InverseLerp(0.1f, 1.0f, 0.5f);
            Assert.That(player.CurrentVelocity.z, Is.EqualTo(expectedSpeed).Within(0.001f));
        }

        [Test]
        public void CalculateVelocity_BrakesBeforeReversingDirection()
        {
            PlayerEntity player = CreatePlayer(new PlayerSpeedStatus
            {
                MoveSpeed = 2.0f,
                AccelerationRate = 100.0f,
                DirectionChangeDecelerationRate = 10.0f,
                ReversalDotThreshold = -0.35f,
                InputDeadZone = 0.1f
            });
            player.UpdateState(true, false, false);
            player.CalculateVelocity(
                Vector2.up,
                Vector3.right,
                Vector3.forward,
                Vector3.up,
                true,
                1.0f);

            player.CalculateVelocity(
                Vector2.down,
                Vector3.right,
                Vector3.forward,
                Vector3.up,
                true,
                0.1f);

            Assert.That(player.CurrentVelocity.z, Is.GreaterThanOrEqualTo(0.0f));
            Assert.That(player.CurrentVelocity.z, Is.LessThan(2.0f));
        }

        [Test]
        public void ApplyGravity_WhenGrounded_UsesGroundStickVelocity()
        {
            PlayerEntity player = CreatePlayer(new PlayerSpeedStatus
            {
                GroundStickVelocity = -2.5f
            });

            player.ApplyGravity(true, 1.0f);

            Assert.That(player.CurrentVelocity.y, Is.EqualTo(-2.5f).Within(0.001f));
        }

        private static PlayerEntity CreatePlayer(PlayerSpeedStatus speedStatus)
        {
            return new PlayerEntity(
                speedStatus,
                new PlayerCrouchStatus(),
                new PlayerStamina());
        }
    }
}
