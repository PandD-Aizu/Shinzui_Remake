namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// スポーンされた単一アイテムのドメイン記録情報。
    /// 配置位置、法線、回転、消費コスト、選択時ウェイト、ソース情報、シード値を追跡する。
    /// </summary>
    public record SpawnedItemRecord
    {
        public string ItemId { get; init; }
        public SpawnVector3 Position { get; init; }
        public SpawnVector3 Normal { get; init; }
        public float YawDegrees { get; init; }
        public ItemSpawnRotationPolicy RotationPolicy { get; init; }
        public string SourceSurfaceId { get; init; }
        public int SourceTriangleIndex { get; init; }
        public int Seed { get; init; }
        public int SpawnCost { get; init; }
        public float BaseWeight { get; init; }
        public float NeedWeight { get; init; }
        public float FinalItemWeight { get; init; }
        public float SelectedTriangleWeight { get; init; }
        /// <summary>サンプリングされたWeight（2D Weight Map / 旧Triangle Weight）</summary>
        public float SampledWeight => SelectedTriangleWeight;

        public SpawnedItemRecord(
            string itemId,
            SpawnVector3 position,
            SpawnVector3 normal,
            float yawDegrees,
            ItemSpawnRotationPolicy rotationPolicy,
            string sourceSurfaceId,
            int sourceTriangleIndex,
            int seed,
            int spawnCost,
            float baseWeight,
            float needWeight,
            float finalItemWeight,
            float selectedTriangleWeight)
        {
            ItemId = itemId;
            Position = position;
            Normal = normal;
            YawDegrees = yawDegrees;
            RotationPolicy = rotationPolicy;
            SourceSurfaceId = sourceSurfaceId ?? string.Empty;
            SourceTriangleIndex = sourceTriangleIndex;
            Seed = seed;
            SpawnCost = spawnCost;
            BaseWeight = baseWeight;
            NeedWeight = needWeight;
            FinalItemWeight = finalItemWeight;
            SelectedTriangleWeight = selectedTriangleWeight;
        }
    }
}
