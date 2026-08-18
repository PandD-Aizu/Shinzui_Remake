namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュのバウンディングボックスに対する2D Weight Mapの投影平面。
    /// 水平面（XZ）または垂直面（XY, YZ）をサポートする。
    /// </summary>
    public enum SpawnSurfaceProjectionPlane
    {
        XZ = 0,
        XY = 1,
        YZ = 2
    }
}
