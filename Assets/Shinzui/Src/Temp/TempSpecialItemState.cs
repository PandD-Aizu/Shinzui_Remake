namespace Shinzui.Temp
{
    /// <summary>
    /// 特殊アイテムを持ってるかどうか。1個しか持てない。セッション中だけ（セーブ非対応）
    /// </summary>
    public static class TempSpecialItemState
    {
        public static bool HasItem { get; private set; }

        /// <summary>
        /// 拾う。もう持ってたらfalse
        /// </summary>
        public static bool TryCollect()
        {
            if (HasItem) return false;
            HasItem = true;
            return true;
        }

        /// <summary>
        /// 使う（彼女に渡すなど）。持ってなければfalse
        /// </summary>
        public static bool Consume()
        {
            if (!HasItem) return false;
            HasItem = false;
            return true;
        }
    }
}
