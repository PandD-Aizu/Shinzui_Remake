using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.Interfaces.ResourceNeed
{
    public interface IPlayerResourceSnapshotProvider
    {
        PlayerResourceSnapshot CaptureSnapshot();
    }
}
