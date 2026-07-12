using R3;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Domain.Entities.Inventory
{
    /// <summary>
    /// プレイヤーの特殊アイテム専用スロット
    /// 保持できるのは常に一つだけ
    /// </summary>
    public class SpecialItemEntity
    {
        private readonly ReactiveProperty<SpecialItemDefinition> _heldItem = new(null);

        public ReadOnlyReactiveProperty<SpecialItemDefinition> HeldItem => _heldItem;

        public SpecialItemDefinition Replace(SpecialItemDefinition item)
        {
            if (item == null) return null;

            var previous = _heldItem.Value;
            _heldItem.Value = item;
            return previous;
        }

        public SpecialItemDefinition Clear()
        {
            var previous = _heldItem.Value;
            _heldItem.Value = null;
            return previous;
        }

        /// <summary>
        /// 死亡防止効果を消費し、成功時は特殊アイテム自体も消える
        /// </summary>
        public bool TryConsumeDeathPrevention(out SpecialItemDefinition consumedItem)
        {
            var current = _heldItem.Value;
            if (current == null || !current.Modifiers.PreventDeathOnce)
            {
                consumedItem = null;
                return false;
            }

            consumedItem = Clear();
            return true;
        }
    }
}
