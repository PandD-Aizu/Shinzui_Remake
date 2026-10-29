namespace Shinzui.Application.Title
{
    /// <summary>タイトルからゲームへ引き継ぐ開始方法を保持する</summary>
    public static class GameStartRequest
    {
        public static int? Slot { get; private set; }

        /// <summary>既存セーブを使用せず新規開始する</summary>
        public static void NewGame() => Slot = null;

        /// <summary>再開する保存スロットを指定する</summary>
        /// <param name="slot">保存スロット番号</param>
        public static void Continue(int slot) => Slot = slot;
    }
}
