using System;
using R3;
using Shinzui.Application.UseCases.Objective;
using Shinzui.View.Objective;
using VContainer.Unity;

namespace Shinzui.Presentation.Objective
{
    /// <summary>
    /// 目標の変化を画面右上の目標表示へ反映する
    /// </summary>
    public class ObjectivePresenter : IInitializable, IDisposable
    {
        private readonly ObjectiveUseCase _useCase;
        private readonly ObjectiveView _view;
        private IDisposable _disposable;

        /// <summary>
        /// 目標ユースケースと表示ビューを受け取る
        /// </summary>
        /// <param name="useCase">目標ユースケース</param>
        /// <param name="view">目標表示ビュー</param>
        public ObjectivePresenter(ObjectiveUseCase useCase, ObjectiveView view)
        {
            _useCase = useCase;
            _view = view;
        }

        /// <summary>
        /// 目標の購読を開始し、ステージ開始時の目標を設定する
        /// </summary>
        public void Initialize()
        {
            // 目標が設定されたら表示し、空になったら隠す
            _disposable = _useCase.Current
                .Subscribe(objective =>
                {
                    if (string.IsNullOrEmpty(objective))
                    {
                        _view.Hide();
                    }
                    else
                    {
                        _view.Show(objective);
                    }
                });

            // ステージ開始時の目標
            _useCase.SetObjective(ObjectiveUseCase.FindExit);
        }

        /// <summary>
        /// 購読を解除する
        /// </summary>
        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
