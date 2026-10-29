using R3;

namespace Shinzui.Domain.Entities.Objective
{
    /// <summary>
    /// 現在の目標を保持するエンティティ
    /// 空文字は目標なしを表す
    /// </summary>
    public class ObjectiveEntity
    {
        private readonly ReactiveProperty<string> _current = new(string.Empty);
        public ReadOnlyReactiveProperty<string> Current => _current;

        /// <summary>
        /// 現在の目標を設定する
        /// </summary>
        /// <param name="objective">目標文言</param>
        public void Set(string objective)
        {
            _current.Value = objective ?? string.Empty;
        }

        /// <summary>
        /// 現在の目標を消去する
        /// </summary>
        public void Clear()
        {
            _current.Value = string.Empty;
        }
    }
}
