namespace Shinzui.Application.Interfaces.Tunnel
{
    /// <summary>Bakes navigation for the stage after all geometry has been placed.</summary>
    public interface ITunnelNavigationBuilder
    {
        bool BuildNavMesh();
    }
}
