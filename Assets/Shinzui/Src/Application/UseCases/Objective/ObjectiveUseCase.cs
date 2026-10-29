using R3;
using Shinzui.Domain.Entities.Objective;

namespace Shinzui.Application.UseCases.Objective
{
    /// <summary>
    /// 現在の目標を更新・公開するユースケース
    /// </summary>
    public class ObjectiveUseCase
    {
        // ステージ開始時の目標
        public const string FindExit = "出口を見つけよう";

        private readonly ObjectiveEntity _entity;

        // Presenter向けに現在の目標を公開
        public ReadOnlyReactiveProperty<string> Current => _entity.Current;

        /// <summary>
        /// 目標エンティティを受け取る
        /// </summary>
        /// <param name="entity">目標エンティティ</param>
        public ObjectiveUseCase(ObjectiveEntity entity)
        {
            _entity = entity;
        }

        /// <summary>
        /// 目標を設定する
        /// </summary>
        /// <param name="objective">目標文言</param>
        public void SetObjective(string objective)
        {
            _entity.Set(objective);
        }

        /// <summary>
        /// 目標を消去して非表示にする
        /// </summary>
        public void ClearObjective()
        {
            _entity.Clear();
        }
    }
}
