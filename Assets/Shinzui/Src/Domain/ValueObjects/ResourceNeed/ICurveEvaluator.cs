namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// 純粋C#でカーブを評価する抽象インターフェース（Domain層のUnity非依存性担保）
    /// </summary>
    public interface ICurveEvaluator
    {
        float Evaluate(float t);
    }
}
