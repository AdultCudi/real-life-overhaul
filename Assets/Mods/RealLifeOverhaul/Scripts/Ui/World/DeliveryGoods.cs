using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UI;
using UI.Dialog;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// An order's chosen goods (UI.Dialog.DeliveryContractSettingsBase: the furniture store's and the food supplier's
    /// delivery order) as the kit's goods rows (UiGoodsRow), for a form that lays them out with UiItemsList.Rows, and its
    /// steps back into the game: each row's minus and plus are the game's own buttons on its row, a typed amount goes into
    /// the row's own field, so the order's most and a row taken out at nought stay the order's rules. Game side. Cost: a
    /// read of the order's list and a picture key a row, on a layout.
    /// </summary>
    public static class DeliveryGoods
    {
        /// <summary>
        /// The order's goods into <paramref name="rows"/> (grown when short), in the order's own order; returns how many. Each
        /// row's name and picture are the item's, its line its price each, its figure the row's sum, its amount between the
        /// minus and the plus as the game's buttons allow.
        /// </summary>
        public static int Read(DeliveryContractSettingsBase order, ref UiGoodsRow[] rows)
        {
            List<DeliveryContractSettingsBase.ItemToDeliver> goods = order != null ? order.itemsToDeliver : null;
            int n = goods != null ? goods.Count : 0;
            if (rows == null || rows.Length < n)
            {
                rows = new UiGoodsRow[n < 8 ? 8 : n];
            }
            for (int i = 0; i < n; i++)
            {
                DeliveryContractSettingsBase.ItemToDeliver item = goods[i];
                if (item == null)
                {
                    rows[i] = new UiGoodsRow { Name = string.Empty, Icon = UiIcon.Package };
                    continue;
                }
                AmountSelector amount = item.amountSelector;
                rows[i] = new UiGoodsRow
                {
                    Name = WorldHost.Plain(UiText.T(item.itemName)) ?? item.itemName, Picture = WorldPictures.Of(ItemHelper.GetIconWithFallback(item.itemName)), Icon = UiIcon.Package,
                    Sub = UiText.Fill("reallife_ui_world_items_each", "price", UiText.Money(item.price)), Figure = UiText.Money(item.price * item.amount),
                    Amount = UiText.Whole(item.amount), CanLess = Interactable(GameContract.ItemsLess, amount), CanMore = Interactable(GameContract.ItemsMore, amount),
                };
            }
            return n;
        }

        /// <summary>A row's minus (-1) or plus (1), pressed on the game's own row; false when it takes no press.</summary>
        public static bool Step(DeliveryContractSettingsBase order, int row, int step)
        {
            AmountSelector amount = Selector(order, row);
            GameMember button = step < 0 ? GameContract.ItemsLess : GameContract.ItemsMore;
            return amount != null && button.Field != null && UiWidgets.Press(button.Field.GetValue(amount) as Button);
        }

        /// <summary>A row's amount as it is typed, into the game's own field; a nought or nothing waits for a figure.</summary>
        public static bool Type(DeliveryContractSettingsBase order, int row, string text)
        {
            AmountSelector amount = Selector(order, row);
            if (amount == null || GameContract.ItemsAmount.Field == null || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1)
            {
                return false;
            }
            return UiWidgets.Type(GameContract.ItemsAmount.Field.GetValue(amount) as TMP_InputField, n.ToString(CultureInfo.InvariantCulture), true);
        }

        /// <summary>A row's amount as the typing starts with it.</summary>
        public static string Amount(DeliveryContractSettingsBase order, int row)
        {
            List<DeliveryContractSettingsBase.ItemToDeliver> goods = order != null ? order.itemsToDeliver : null;
            return goods != null && row >= 0 && row < goods.Count && goods[row] != null ? UiText.Whole(goods[row].amount) : string.Empty;
        }

        /// <summary>A number that changes when the order's goods change, for a screen's look for a change.</summary>
        public static long Signature(DeliveryContractSettingsBase order)
        {
            List<DeliveryContractSettingsBase.ItemToDeliver> goods = order != null ? order.itemsToDeliver : null;
            long seen = goods != null ? goods.Count : -1;
            for (int i = 0; goods != null && i < goods.Count; i++)
            {
                seen = unchecked(seen * 31 + (goods[i] != null ? goods[i].amount * 7919 + (goods[i].itemName != null ? goods[i].itemName.GetHashCode() : 0) : 0));
            }
            return seen;
        }

        private static AmountSelector Selector(DeliveryContractSettingsBase order, int row)
        {
            List<DeliveryContractSettingsBase.ItemToDeliver> goods = order != null ? order.itemsToDeliver : null;
            return goods != null && row >= 0 && row < goods.Count && goods[row] != null ? goods[row].amountSelector : null;
        }

        private static bool Interactable(GameMember member, object owner) => owner != null && member.Field != null && member.Field.GetValue(owner) is Button b && b.interactable;
    }
}
