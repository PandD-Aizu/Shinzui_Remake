using UnityEngine;

namespace Shinzui.Application.Interfaces
{
    /// <summary>Engine operations supplied by Infrastructure; no View or Presenter dependency.</summary>
    public interface IEnemyRuntime
    {
        bool IsAvailable { get; }
        bool IsReady { get; }
        Vector3 Position { get; }
        void Tick(float deltaTime);
        bool CanSee(Vector3 playerPosition, float targetHeight);
        bool HasClearContact(Vector3 playerPosition, float targetHeight);
        bool MoveTo(Vector3 targetPosition);
        void Wander(float radius);
        void Stop();
        void SetSpeed(float multiplier, bool stopped);
        void WarpZ(float z);
    }
}
