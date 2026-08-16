namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// UnityEngine.Randomのグローバル状態に依存しない、シード指定で決定論的に再現可能なPRNGインターフェース
    /// </summary>
    public interface IRandomNumberGenerator
    {
        int Seed { get; }
        uint NextUInt();
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        float NextFloat(float minInclusive, float maxInclusive);
        double NextDouble();
    }
}
