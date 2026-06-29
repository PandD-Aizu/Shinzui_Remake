using System;
using R3;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View.Inventory;
using VContainer.Unity;

namespace Shinzui.Presentation.Inventory
{
    public class ItemGetPresenter : IInitializable, IDisposable
    {
        private readonly InventoryUseCase _useCase;
        private readonly ItemGetView _view;
        private IDisposable _disposable;

        public ItemGetPresenter(InventoryUseCase useCase, ItemGetView view)
        {
            _useCase = useCase;
            _view = view;
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            // アイテム獲得通知を監視して演出用Viewへ伝達
            _useCase.OnItemGot
                .Subscribe(dto =>
                {
                    if (_view != null)
                    {
                        _view.Show(dto.ItemName, dto.IconAssetAddress);
                    }
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
