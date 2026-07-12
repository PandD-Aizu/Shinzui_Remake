using System;
using R3;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View.Inventory;
using VContainer.Unity;

namespace Shinzui.Presentation.Inventory
{
    public class SpecialItemHudPresenter : IInitializable, IDisposable
    {
        private readonly SpecialItemUseCase _useCase;
        private readonly SpecialItemHudView _view;
        private IDisposable _disposable;

        public SpecialItemHudPresenter(SpecialItemUseCase useCase, SpecialItemHudView view)
        {
            _useCase = useCase;
            _view = view;
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            _useCase.SlotDto
                .Subscribe(dto =>
                {
                    if (dto.HasItem)
                    {
                        _view.SetItem(dto.IconAssetAddress);
                    }
                    else
                    {
                        _view.Clear();
                    }
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
            _ = _useCase.LoadAsync();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
