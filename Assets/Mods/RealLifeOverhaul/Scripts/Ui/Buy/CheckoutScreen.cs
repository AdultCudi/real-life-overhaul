using System.Collections.Generic;
using System.Text;
using BigAmbitions.Items;
using Extensions;
using TMPro;
using UI;
using UI.Purchase;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The checkout as the kit's receipt: the goods or the taxes, the total, the queue, the game's own buttons.</summary>
    internal sealed class CheckoutScreen : IUiScreen, IUiTypingSubmit
    {
        private const float Every = 0.25f;   // seconds between looks at the game's panel
        private readonly PurchaseUI _ui;
        private readonly UiCheckoutModel _m = new UiCheckoutModel();
        private readonly List<PurchaseUiItem> _rows = new List<PurchaseUiItem>(10);
        private readonly List<PurchaseUiTax> _taxes = new List<PurchaseUiTax>(2);
        private readonly List<UiCheckoutLine> _lines = new List<UiCheckoutLine>(10);
        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _seen;
        private float _next;
        private bool _dirty = true;
        private int _taxRow;

        public CheckoutScreen(PurchaseUI ui)
        {
            _ui = ui;
            _m.ColumnGoods = UiText.T("reallife_ui_buy_goods");
            _m.ColumnPrice = UiText.T("common_price");
            _m.ColumnPacks = UiText.T("common_amount");
            _m.ColumnTotal = UiText.T("common_total");
            _m.PriceEach = UiText.T("reallife_ui_buy_each");
            _m.PageWords = UiText.T("reallife_ui_buy_page");
            _m.TotalLabel = UiText.T("purchaseui_totalprice");
            _m.Pay = UiText.T("reallife_ui_buy_pay");
            _m.AmountHint = UiText.T("reallife_ui_buy_amount_hint");
            _m.OwedWords = UiText.T("reallife_ui_buy_owed");
            _m.EmptyTitle = UiText.T("reallife_ui_buy_empty");
            _m.EmptyBody = UiText.T("reallife_ui_buy_empty_body");
            _m.Prompts = BuyKits.Prompts();
            _m.PromptCount = _m.Prompts.Length;
        }

        public string Id => BuyKits.CheckoutId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    string now = Read();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_seen == null)
            {
                _seen = Read();
            }
            UiCheckout.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiCheckout.LessCommand:
                    return Step(arg, GameContract.StepLess);
                case UiCheckout.MoreCommand:
                    return Step(arg, GameContract.StepMore);
                case UiCheckout.PackCommand:
                    return arg >= 0 && arg < _rows.Count && UiWidgets.Press(BuyKits.Get<Button>(GameContract.BuyRowButton, _rows[arg]));
                case UiCheckout.PageCommand:
                {
                    int pages = UiCheckout.Pages(_m), page = Mathf.Clamp(_m.Page + arg, 0, pages - 1);
                    if (page == _m.Page)
                    {
                        return false;
                    }
                    _m.Page = page;
                    _dirty = true;
                    return true;
                }
                case UiCheckout.TaxPayCommand:
                    return Pay(arg);
                case UiCheckout.OrderCommand:
                    return Again(UiWidgets.Press(_ui.orderButton));
                case UiCheckout.CancelCommand:
                    return Again(UiWidgets.Press(_ui.cancelButton));
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (command == UiCheckout.TaxTypeCommand || command == UiCheckout.TaxPayCommand)
            {
                _taxRow = arg;
            }
        }

        public bool Back() => Again(UiWidgets.Press(_ui.cancelButton));

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Again(UiWidgets.Press(_ui.orderButton));

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        // ------------------------------------------------------------------------------------------------- the tax's field

        public int TypingCommand => UiCheckout.TaxTypeCommand;

        public string TypingText
        {
            get
            {
                TMP_InputField f = Field(_taxRow);
                return f != null ? f.text : string.Empty;
            }
        }

        public int TypingLimit => 16;

        public void Typed(string text)
        {
            UI.Components.InputField input = _taxRow >= 0 && _taxRow < _taxes.Count ? BuyKits.Get<UI.Components.InputField>(GameContract.TaxInput, _taxes[_taxRow]) : null;
            if (input != null)
            {
                input.SetText(text ?? string.Empty);   // through the game's own field, so its most and its format hold
                Again(true);
            }
        }

        public void Submitted(string text)
        {
            Typed(text);
            Pay(_taxRow);
        }

        // ------------------------------------------------------------------------------------------------- the game's panel read

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;   // the game changed its panel: look again this frame
                _dirty = true;
            }
            return done;
        }

        private bool Step(int row, GameMember button)
        {
            if (row < 0 || row >= _rows.Count)
            {
                return false;
            }
            AmountSelector stepper = BuyKits.Get<AmountSelector>(GameContract.BuyRowStepper, _rows[row]);
            return Again(UiWidgets.Press(BuyKits.Get<Button>(button, stepper)));
        }

        private bool Pay(int row) => row >= 0 && row < _taxes.Count && Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.TaxPay, _taxes[row])));

        private TMP_InputField Field(int row)
        {
            UI.Components.InputField input = row >= 0 && row < _taxes.Count ? BuyKits.Get<UI.Components.InputField>(GameContract.TaxInput, _taxes[row]) : null;
            return input != null ? input.tmpInputField : null;
        }

        // The panel as the game filled it: its rows in order (the last ones, as a row the game let go may stand a frame
        // longer), the taxes, the total, the queue and its buttons. Returns what the screen shows, to see a change by.
        private string Read()
        {
            _rows.Clear();
            _taxes.Clear();
            Transform container = _ui.itemsContainer;
            for (int i = 0; container != null && i < container.childCount; i++)
            {
                Transform c = container.GetChild(i);
                if (c == _ui.itemTemplate || c == _ui.taxItemTemplate || !c.gameObject.activeSelf)
                {
                    continue;
                }
                var row = c.GetComponent<PurchaseUiItem>();
                if (row != null)
                {
                    _rows.Add(row);
                    continue;
                }
                var tax = c.GetComponent<PurchaseUiTax>();
                if (tax != null)
                {
                    _taxes.Add(tax);
                }
            }
            var cargo = BuyKits.Get<List<CargoInstance>>(GameContract.BuyCargo, _ui);
            Trim(_rows, cargo != null ? cargo.Count : 1);
            var types = BuyKits.Get<System.Collections.IList>(GameContract.BuyTaxTypes, _ui);
            Trim(_taxes, types != null ? types.Count : _taxes.Count);
            _sb.Length = 0;
            _m.Title = BuyKits.Words(BuyKits.Get<Component>(GameContract.BuyTitle, _ui)) ?? UiText.T("common_purchase");
            _sb.Append(_m.Title).Append('|');
            bool itemsShown = container != null && container.gameObject.activeSelf;
            _lines.Clear();
            _m.Choose = false;
            for (int i = 0; itemsShown && i < _rows.Count; i++)
            {
                PurchaseUiItem row = _rows[i];
                AmountSelector stepper = BuyKits.Get<AmountSelector>(GameContract.BuyRowStepper, row);
                bool picks = stepper != null && stepper.gameObject.activeSelf;
                _m.Choose |= picks;
                CargoInstance ci = cargo != null && i < cargo.Count ? cargo[i] : null;
                Button less = BuyKits.Get<Button>(GameContract.StepLess, stepper), more = BuyKits.Get<Button>(GameContract.StepMore, stepper);
                var line = new UiCheckoutLine
                {
                    Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.BuyRowName, row)),
                    Pack = ci != null && ci.amount > 1 ? UiText.T("reallife_ui_buy_pack").Replace("{count}", ci.amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)) : null,
                    Picture = ci != null ? BuyPictures.Item(ci.itemName) : null,
                    Icon = ci != null ? UiIcon.Package : UiIcon.Tag,
                    Price = ci != null ? (ci.amount * ci.pricePerUnit).ToCurrencyFormat() : null,
                    Total = UiWidgets.Text(BuyKits.Get<TMP_Text>(GameContract.BuyRowPrice, row)),
                    Packs = picks ? row.Amount : 0,
                    CanLess = less != null && less.interactable,
                    CanMore = more != null && more.interactable
                };
                line.Aspect = 1f;
                _lines.Add(line);
                _sb.Append(line.Name).Append(';').Append(line.Total).Append(';').Append(line.Packs).Append(line.CanLess ? '<' : '.').Append(line.CanMore ? '>' : '.').Append('|');
            }
            if (_m.Lines.Length != _lines.Count)
            {
                _m.Lines = new UiCheckoutLine[_lines.Count];
            }
            _lines.CopyTo(_m.Lines);
            if (_m.Taxes.Length != (itemsShown ? _taxes.Count : 0))
            {
                _m.Taxes = new UiCheckoutTax[itemsShown ? _taxes.Count : 0];
            }
            for (int i = 0; i < _m.Taxes.Length; i++)
            {
                TMP_InputField f = Field(i);
                _m.Taxes[i] = new UiCheckoutTax
                {
                    Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.TaxName, _taxes[i])),
                    Owed = UiWidgets.Text(BuyKits.Get<TMP_Text>(GameContract.TaxOwed, _taxes[i])),
                    Amount = f != null ? f.text : null
                };
                _sb.Append(_m.Taxes[i].Owed).Append(';').Append(_m.Taxes[i].Amount).Append('|');
            }
            _m.Total = _ui.totalPrice != null && _ui.totalPrice.activeSelf ? UiWidgets.Text(_ui.totalPriceValue) : null;
            _m.Queue = _ui.inQueueLabel != null && _ui.inQueueLabel.gameObject.activeSelf ? UiWidgets.Text(_ui.inQueueLabel) : null;
            _m.Paused = _m.Queue == null ? UiText.T("reallife_ui_buy_paused") : null;   // the game holds time while the register is open
            _m.Order = _ui.orderButton.gameObject.activeSelf ? BuyKits.Words(_ui.orderButton) : null;
            _m.OrderEnabled = _ui.orderButton.interactable;
            _m.Cancel = _ui.cancelButton.gameObject.activeSelf ? BuyKits.Words(_ui.cancelButton) : null;
            _m.CancelEnabled = _ui.cancelButton.interactable;
            _sb.Append(_m.Total).Append('|').Append(_m.Queue).Append('|').Append(_m.Order).Append(_m.OrderEnabled).Append('|').Append(_m.Cancel).Append(_m.CancelEnabled);
            if (_m.Page > 0 && _m.Page >= UiCheckout.Pages(_m))
            {
                _m.Page = 0;
            }
            if (Field(_taxRow) is TMP_InputField typing && typing.isFocused)
            {
                typing.DeactivateInputField();   // the game focuses its field as it opens: the kit's own field types, so no key lands twice
            }
            return _sb.ToString();
        }

        // Keeps the last of a list: what the game made for this opening.
        private static void Trim<T>(List<T> list, int keep)
        {
            if (keep >= 0 && list.Count > keep)
            {
                list.RemoveRange(0, list.Count - keep);
            }
        }
    }
}
