using System;

namespace Shinzui.Domain.ValueObjects.Inventory
{
    public record ItemStack
    {
        public ItemDefinition Item { get; init; }
        public int Quantity { get; init; }

        public ItemStack(ItemDefinition item, int quantity)
        {
            if (quantity <= 0) throw new ArgumentException("数量は1以上である必要があります。");
            if (quantity > item.MaxStackSize) throw new ArgumentException("最大スタック数を超えています。");
            Item = item;
            Quantity = quantity;
        }

        // 数量を加算した新しいItemStackを返す
        public ItemStack Add(int amount, out int overflow)
        {
            int newQuantity = Quantity + amount;
            if (newQuantity > Item.MaxStackSize)
            {
                overflow = newQuantity - Item.MaxStackSize;
                return this with { Quantity = Item.MaxStackSize };
            }
            overflow = 0;
            return this with { Quantity = newQuantity };
        }

        // 数量を減算した新しいItemStackを返す（0以下になる場合はnullを返す）
        public ItemStack Remove(int amount, out int deficit)
        {
            int newQuantity = Quantity - amount;
            if (newQuantity <= 0)
            {
                deficit = Math.Abs(newQuantity);
                return null;
            }
            deficit = 0;
            return this with { Quantity = newQuantity };
        }
    }
}
