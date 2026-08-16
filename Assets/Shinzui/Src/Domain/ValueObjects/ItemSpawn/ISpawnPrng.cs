namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// シード値に基づき、UnityEngine.Randomの状態を変更せずに決定論的な乱数を生成するPRNGインターフェース。
    /// </summary>
    public interface ISpawnPrng
    {
        int NextInt();
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        float NextFloat(float minInclusive, float maxInclusive);
        double NextDouble();
    }
}
