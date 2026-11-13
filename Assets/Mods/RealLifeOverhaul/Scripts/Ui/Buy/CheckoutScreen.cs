using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.Items;
using EmployeeStations;
using Extensions;
using Helpers;
using Localizor;
using Streets;
using TMPro;
using UI;
using UI.ItemPanel;
using UI.Notification;
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
        private readonly List<Transform> _copies = new List<Transform>(12);   // a template's copies, read into the rows and the taxes
        private int _taken;                                                    // packs taken in all while the register lets the player choose
        private readonly List<UiCheckoutLine> _lines = new List<UiCheckoutLine>(10);
        private readonly UiInfoRow[] _sums = new UiInfoRow[2];
        private float _sumsOf = -1f;            // the order's price the sums were written for
        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _seen;
        private float _next;
        private bool _dirty = true;
        private int _taxRow;
        private readonly UiCheckoutHead _head = new UiCheckoutHead();
        private BuildingRegistration _headOf;   // the registration the head was made for, and what took the goods then
        private string _carrier;
        private string _queueWords;             // the line's words, made again only when a figure changes
        private int _queuePlace = -2, _queueCount = -1;
        private bool _queueServed;

        public CheckoutScreen(PurchaseUI ui)
        {
            _ui = ui;
            _m.ColumnGoods = UiText.T("reallife_ui_buy_goods");
            _m.ColumnPrice = UiText.T("common_price");
            _m.ColumnPacks = UiText.T("common_amount");
            _m.ColumnTotal = UiText.T("common_total");
            _m.PriceEach = UiText.T("reallife_ui_buy_each");
            _m.TotalLabel = UiText.T("purchaseui_totalprice");
            _m.Pay = UiText.T("reallife_ui_buy_pay");
            _m.AmountHint = UiText.T("reallife_ui_buy_amount_hint");
            _m.OwedWords = UiText.T("reallife_ui_buy_owed");
            _m.EmptyTitle = UiText.T("reallife_ui_buy_empty");
            _m.EmptyBody = UiText.T("reallife_ui_buy_empty_body");
            _sums[0].Label = UiText.T("reallife_ui_buy_goods");
            _sums[1].Label = UiText.T("reallife_ui_buy_sales_tax")
                .Replace("{rate}", (RealLifeOverhaul.Population.PopulationCatalogue.SalesTax / 10.0).ToString("0.#", CultureInfo.CurrentCulture) + "%");
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
                    // The row itself takes one more, as the game's row does (PurchaseUiItem.IncreaseAmount), through the plus: the row's own
                    // button answers a click at the order's most too and adds nothing.
                    return Step(arg, GameContract.StepMore);
                case UiCheckout.TaxPayCommand:
                    return Pay(arg);
                case UiCheckout.OrderCommand:
                    return Order();
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
            else if (command == UiCheckout.LessCommand || command == UiCheckout.MoreCommand || command == UiCheckout.PackCommand)
            {
                _m.Reveal = arg;   // the keys moved to a row of a long cart: it scrolls into view
                _dirty = true;
            }
        }

        public bool Back() => Again(UiWidgets.Press(_ui.cancelButton));

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Order();

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
            _m.Offset = Mathf.Max(0f, _m.Offset - notches * UiTokens.S16);
            _dirty = true;
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

        // The game's Place order. At a counter the game leaves it ready with nothing chosen and then waits in a queue that
        // never serves (PurchaseUI.SetTotalPrice, PlaceOrder with an empty list): refused here, and said in words.
        private bool Order()
        {
            if (CheckoutRules.NothingChosen(_m.Choose, _taken))
            {
                UiRefusals.Because("nothing is chosen yet (the player's own doing: told in a notice)");
                Notifications.Show(NotificationType.Warning, "reallife_ui_buy_choose_first", null, 8f);
                return false;
            }
            return Again(UiWidgets.Press(_ui.orderButton));
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
            // The game makes each row as a copy of its template beside the template (PurchaseUI.CreateItemEntry, CreateTaxEntry),
            // inside the list's scroll view. The list the game switches on and off (itemsContainer) is that scroll view itself,
            // not the rows' parent: looked for among its children, no row was ever found (1.0.0 to 1.0.2).
            BuyKits.Copies(_ui.itemTemplate, _copies);
            for (int i = 0; i < _copies.Count; i++)
            {
                var row = _copies[i].GetComponent<PurchaseUiItem>();
                if (row != null)
                {
                    _rows.Add(row);
                }
            }
            BuyKits.Copies(_ui.taxItemTemplate, _copies);
            for (int i = 0; i < _copies.Count; i++)
            {
                var tax = _copies[i].GetComponent<PurchaseUiTax>();
                if (tax != null)
                {
                    _taxes.Add(tax);
                }
            }
            Transform container = _ui.itemsContainer;
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
            _taken = 0;
            float order = 0f;   // what the order costs as the game totals it: a row's pack price for each pack taken
            for (int i = 0; itemsShown && i < _rows.Count; i++)
            {
                PurchaseUiItem row = _rows[i];
                AmountSelector stepper = BuyKits.Get<AmountSelector>(GameContract.BuyRowStepper, row);
                bool picks = stepper != null && stepper.gameObject.activeSelf;
                _m.Choose |= picks;
                _taken += picks ? row.Amount : 0;
                CargoInstance ci = cargo != null && i < cargo.Count ? cargo[i] : null;
                if (ci != null)
                {
                    order += ci.amount * ci.pricePerUnit * (picks ? row.Amount : 1);
                }
                Button less = BuyKits.Get<Button>(GameContract.StepLess, stepper), more = BuyKits.Get<Button>(GameContract.StepMore, stepper);
                var line = new UiCheckoutLine
                {
                    Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.BuyRowName, row)),
                    Pack = ci != null && ci.amount > 1 ? UiText.T("reallife_ui_buy_pack").Replace("{count}", ci.amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)) : null,
                    Picture = ci != null ? BuyPictures.Item(ci.itemName) : null,
                    Icon = ci != null ? UiIcon.Package : UiIcon.Tag,
                    Price = ci != null ? (ci.amount * ci.pricePerUnit).ToCurrencyFormat() : null,
                    // The game writes a pack's price on a row of none taken; the row adds nothing to the order, so it reads nothing.
                    Total = picks && row.Amount == 0 ? 0f.ToCurrencyFormat() : UiWidgets.Text(BuyKits.Get<TMP_Text>(GameContract.BuyRowPrice, row)),
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
            ReadHead();
            _m.Total = _ui.totalPrice != null && _ui.totalPrice.activeSelf ? UiWidgets.Text(_ui.totalPriceValue) : null;
            _m.Queue = _ui.inQueueLabel != null && _ui.inQueueLabel.gameObject.activeSelf ? UiWidgets.Text(_ui.inQueueLabel) : null;
            _m.QueueDetail = _m.Queue != null || _lines.Count > 0 ? QueueWords() : null;   // while choosing, the place in the line alone
            Sums(itemsShown && _lines.Count > 0 && cargo != null ? order : -1f);
            _m.Paused = _m.Queue == null ? UiText.T("reallife_ui_buy_paused") : null;   // the game holds time while the register is open
            _m.Order = _ui.orderButton.gameObject.activeSelf ? BuyKits.Words(_ui.orderButton) : null;
            _m.OrderEnabled = _ui.orderButton.interactable;
            _m.Cancel = _ui.cancelButton.gameObject.activeSelf ? BuyKits.Words(_ui.cancelButton) : null;
            _m.CancelEnabled = _ui.cancelButton.interactable;
            _sb.Append(_m.Total).Append('|').Append(_m.Queue).Append('|').Append(_m.QueueDetail).Append('|');
            _sb.Append(_m.Order).Append(_m.OrderEnabled).Append('|').Append(_m.Cancel).Append(_m.CancelEnabled);
            if (Field(_taxRow) is TMP_InputField typing && typing.isFocused)
            {
                typing.DeactivateInputField();   // the game focuses its field as it opens: the kit's own field types, so no key lands twice
            }
            return _sb.ToString();
        }

        // The order's price as the goods and the sales tax in it, as the city's rule takes it from every price (the council's
        // share, PopulationCatalogue.SalesTax): shown only, nothing charged beyond the game's own total. None for a ticket or
        // the tax office. Written again only when the price changes.
        private void Sums(float order)
        {
            if (order < 0f)
            {
                _m.Sums = System.Array.Empty<UiInfoRow>();
                _sumsOf = -1f;
                return;
            }
            if (order != _sumsOf)
            {
                _sumsOf = order;
                float tax = order * RealLifeOverhaul.Population.PopulationCatalogue.SalesTax / 1000f;
                _sums[0].Value = (order - tax).ToCurrencyFormat();
                _sums[1].Value = tax.ToCurrencyFormat();
            }
            _m.Sums = _sums;
            _sb.Append(_sums[0].Value).Append(';').Append(_sums[1].Value).Append('|');
        }

        // The shop the register stands in as the game registers it (its name, its address with the district) and, for goods,
        // what takes them: the vehicle in use (a hand truck is one) or what the player carries, by the name the item panel
        // gives it. None for the tax office's payment or outside a building. Made again only when either changes.
        private void ReadHead()
        {
            BuildingManager bm = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            BuildingRegistration reg = bm != null && _m.Taxes.Length == 0 ? bm.buildingRegistration : null;
            if (reg == null)
            {
                _m.Head = null;
                _headOf = null;
                return;
            }
            string carrier = _lines.Count > 0 ? Carrier() : null;
            if (!ReferenceEquals(reg, _headOf) || carrier != _carrier)
            {
                _headOf = reg;
                _carrier = carrier;
                string name = reg.GetDisplayName(), address = reg.Address != null ? reg.Address.ToFormattedString() : null;
                string district = bm.building != null && !string.IsNullOrEmpty(bm.building.Neighbourhood) ? bm.building.Neighbourhood.GetLocalization() : null;
                address = address == name ? district : string.IsNullOrEmpty(district) ? address : address + ", " + district;
                _head.Mark = UiIcon.Store;
                _head.Name = name;
                _head.Address = address;
                _head.DeliveredLabel = carrier != null ? UiText.T("reallife_ui_buy_delivered_to") : null;
                _head.DeliveredTo = carrier;
            }
            _m.Head = _head;
            _sb.Append(_head.Name).Append('|').Append(_head.Address).Append('|').Append(_head.DeliveredTo).Append('|');
        }

        // What takes the goods, by the words of the item panel (the checkout fades that panel while open; its words stay):
        // null when the player neither uses a vehicle nor carries anything.
        private static string Carrier()
        {
            if (!PlayerHelper.IsUsingVehicle && !PlayerHelper.IsHoldingItem)
            {
                return null;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            ItemPanelUI panel = uis != null && uis.playerHUD != null ? uis.playerHUD.itemPanelUI : null;
            TMP_Text label = panel != null && panel.itemNameLabel != null ? panel.itemNameLabel.GetComponent<TMP_Text>() : null;
            string words = UiWidgets.Text(label);
            return string.IsNullOrEmpty(words) ? null : words;
        }

        // The player's place in the register's line, as the game queues them (the register gives the player a Customer and
        // joins its line, the first in it the one being served): being served, next, or the place and how many are ahead.
        // The line holds the game's own customers too, so they count as they stand. Null when the player is in no line.
        private string QueueWords()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            Customer me = gm != null && gm.playerController != null ? gm.playerController.GetComponent<Customer>() : null;
            WaitingLine line = me != null && me.isPlayer ? me.assignedWaitingLine : null;
            List<Customer> customers = line != null && line.data != null ? line.data.customers : null;
            bool served = customers != null && me.state == CustomerState.BeingServed;
            int place = customers != null ? customers.IndexOf(me) : -1, count = customers != null ? customers.Count : 0;
            if (!served && place < 0)
            {
                return null;
            }
            if (served != _queueServed || place != _queuePlace || count != _queueCount || _queueWords == null)
            {
                _queueServed = served;
                _queuePlace = place;
                _queueCount = count;
                _queueWords = served ? UiText.T("reallife_ui_buy_queue_served") : place == 0 ? UiText.T("reallife_ui_buy_queue_next")
                    : UiText.T("reallife_ui_buy_queue_place").Replace("{place}", (place + 1).ToString(CultureInfo.InvariantCulture))
                        .Replace("{count}", count.ToString(CultureInfo.InvariantCulture)).Replace("{ahead}", place.ToString(CultureInfo.InvariantCulture));
            }
            return _queueWords;
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
