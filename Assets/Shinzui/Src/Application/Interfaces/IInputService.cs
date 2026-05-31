using UnityEngine;

namespace Shinzui.Application.Interfaces
{
    public interface IInputService
    {
        Vector2 MoveInput { get; }
        bool SprintPressed { get; }
        bool CrouchPressed { get; }
    }
}
