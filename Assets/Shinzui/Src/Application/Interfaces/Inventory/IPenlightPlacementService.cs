namespace Shinzui.Application.Interfaces.Inventory
{
    /// <summary>ペンライトの地面への設置を仲介する</summary>
    public interface IPenlightPlacementService
    {
        /// <summary>プレイヤーの前方に点灯したペンライトを置く</summary>
        /// <returns>設置が成功したか</returns>
        bool TryPlace();
    }
}
