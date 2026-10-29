namespace Shinzui.View.Interaction
{
    /// <summary>取得途中の演出による進行指示</summary>
    public enum PickupInterruptionResult { Continue, Wait, Cancel }

    /// <summary>取得の途中へ任意の演出を接続する境界</summary>
    public interface IPickupInterruption
    {
        float TriggerProgress { get; }
        PickupInterruptionResult Result { get; }

        /// <summary>指定進捗に達した演出を開始</summary>
        void Begin();

        /// <summary>演出を終了し、停止した音やカメラ等を解放</summary>
        /// <param name="acquired">取得を完了したか</param>
        void End(bool acquired);
    }
}
