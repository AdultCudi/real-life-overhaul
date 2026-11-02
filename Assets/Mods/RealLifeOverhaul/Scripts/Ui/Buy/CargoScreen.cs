using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.Items;
using Extensions;
using TMPro;
using UI;
using UI.MergeCargo;
using UI.PlayerHUD;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Manage cargo as the kit's catalogue: the holder, the slots used, a card each stack, the chosen one's buttons.</summary>
    internal sealed class CargoScreen : IUiScreen
    {
        private const float Every = 0.25f;
        private readonly ManageCargoUi _ui;
        private readonly UiCargoModel _m = new UiCargoModel();
        private readonly List<Transform> _stacks = new List<Transform>(16);
        private readonly List<Button> _buttons = new List<Button>(4);
        private readonly List<UiActionModel> _actions = new List<UiActionModel>(4);
        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _seen;
        private float _next;
        private bool _dirty = true;
        private CargoItem _detailOf;   // the stack the detail was made for, and its packs then
        private int _detailPacks;
        private string _sell;          // "Sell for $2,407": the chosen stack's own Sell, or null for the game's word
        private const int Quick = 3;   // days: goods that keep no longer out of the cold say so on their tile
        private readonly string[] _quickWords = new string[Quick + 1];

        public CargoScreen(ManageCargoUi ui)
        {
            _ui = ui;
            _m.Title = UiText.T("reallife_ui_buy_manage_cargo");
            _m.SlotsLabel = UiText.T("reallife_ui_buy_slots_used");
            _m.Catalogue.EmptyTitle = UiText.T("reallife_ui_buy_empty_cargo");
            _m.Catalogue.EmptyBody = UiText.T("reallife_ui_buy_empty_cargo_body");
            _m.Catalogue.EmptyIcon = UiIcon.Package;
            _m.Catalogue.DetailKicker = UiText.T("reallife_ui_buy_in_cargo");
            _m.Prompts = BuyKits.Prompts();
            _m.PromptCount = _m.Prompts.Length;
        }

        public string Id => BuyKits.CargoId;

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
            UiCargo.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiCargo.CatalogueCommand + UiCatalogue.Card)
            {
                if (arg < 0 || arg >= _stacks.Count || arg == _m.Catalogue.Chosen)
                {
                    return arg == _m.Catalogue.Chosen;
                }
                _m.Catalogue.Chosen = arg;
                _m.Catalogue.Reveal = arg;
                return Again(true);
            }
            if (command == UiCargo.CatalogueCommand + UiCatalogue.Action)
            {
                return arg >= 0 && arg < _buttons.Count && Again(UiWidgets.Press(_buttons[arg]));
            }
            return command == UiCargo.SellAllCommand && Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CargoSellAll, _ui)));
        }

        public void Focused(int command, int arg)
        {
            if (command == UiCargo.CatalogueCommand + UiCatalogue.Card && arg >= 0 && arg < _stacks.Count && arg != _m.Catalogue.Chosen)
            {
                _m.Catalogue.Chosen = arg;   // the detail follows the focus, as games follow the cursor
                Again(true);
            }
        }

        public bool Back()
        {
            _ui.Close();
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Main() >= 0 && Again(UiWidgets.Press(_buttons[Main()]));

        public void Reveal(int list, int index)
        {
            _m.Catalogue.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _m.Catalogue.Offset = Mathf.Max(0f, _m.Catalogue.Offset - notches * UiTokens.S12);
            _dirty = true;
        }

        private int Main() => _actions.Count > 0 && _actions[_actions.Count - 1].Key == UiAction.Alt ? _actions.Count - 1 : -1;

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;
                _dirty = true;
            }
            return done;
        }

        // The panel as the game filled it: its stacks in its own order (the last ones, as the game's ReloadCargo lets the
        // old ones go at the end of the frame), the holder and its slots. Returns what the screen shows, to see a change by.
        private string Read()
        {
            _sb.Length = 0;
            BuyKits.Copies(BuyKits.Get<Transform>(GameContract.CargoTemplate, _ui), _stacks);
            ICargoHolder holder = _ui.currentCargoHolder;
            List<CargoInstance> held = holder != null ? holder.GetCargoInstances() : null;
            var items = BuyKits.Get<System.Collections.IList>(GameContract.CargoItems, _ui);
            if (items != null && _stacks.Count > items.Count)
            {
                _stacks.RemoveRange(0, _stacks.Count - items.Count);
            }
            int used = held != null ? held.Count : 0, most = holder != null ? holder.GetMaxCargoSize() : 0;
            UIs uis = InstanceBehavior<UIs>.Instance;
            var panel = uis != null && uis.playerHUD != null ? uis.playerHUD.itemPanelUI : null;
            _m.Holder = panel != null ? BuyKits.Words(panel.itemNameLabel) : null;
            _m.HolderDetail = BuyKits.Words(BuyKits.Get<Component>(GameContract.CargoContents, _ui));
            _m.HolderPicture = holder is ItemInstance thing ? BuyPictures.Item(thing.itemName) : null;
            _m.HolderAspect = 1f;
            _m.SlotsValue = UiText.T("reallife_ui_buy_slots").Replace("{used}", used.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{most}", most.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _m.SlotsShare = most > 0 ? Mathf.Clamp01(used / (float)most) : 0f;
            _m.Paused = null;
            _sb.Append(_m.Holder).Append('|').Append(_m.HolderDetail).Append('|').Append(_m.SlotsValue).Append('|');
            UiCatalogueModel c = _m.Catalogue;
            if (c.Cards.Length < _stacks.Count)
            {
                c.Cards = new UiCatalogueCard[_stacks.Count];
            }
            for (int i = 0; i < _stacks.Count; i++)
            {
                var stack = _stacks[i].GetComponent<CargoItemUi>();
                TMP_Text pieces = BuyKits.Get<TMP_Text>(GameContract.SlotPieces, stack), count = BuyKits.Get<TMP_Text>(GameContract.SlotCount, stack);
                TMP_Text price = BuyKits.Get<TMP_Text>(GameContract.SlotPrice, stack);
                bool owed = price != null && price.gameObject.activeSelf;
                int keeps = !owed && items != null && items.Count == _stacks.Count ? SpoilRead.Keeps(GoodsOf(items[i] as CargoItem), false) : -1;
                string figure = pieces != null && pieces.gameObject.activeSelf ? UiWidgets.Text(pieces) : count != null && count.gameObject.activeSelf ? UiWidgets.Text(count) : null;
                c.Cards[i] = new UiCatalogueCard
                {
                    Picture = BuyPictures.Item(_stacks[i].name), Aspect = 1f, Icon = UiIcon.Package, Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.SlotName, stack)),
                    Figure = figure != null ? UiText.T("reallife_ui_buy_count").Replace("{count}", figure) : null,
                    State = owed ? UiText.T("reallife_ui_buy_unpaid") + " " + UiWidgets.Text(price) : keeps > 0 && keeps <= Quick ? QuickWords(keeps) : null,
                    StateTone = UiRole.Warn
                };
                _sb.Append(c.Cards[i].Name).Append(c.Cards[i].Figure).Append(c.Cards[i].State).Append(';');
            }
            c.CardCount = _stacks.Count;
            if (c.Chosen >= c.CardCount)
            {
                c.Chosen = c.CardCount - 1;
            }
            if (c.Chosen < 0 && c.CardCount > 0)
            {
                c.Chosen = 0;
            }
            Detail(items != null && items.Count == _stacks.Count && c.Chosen >= 0 ? items[c.Chosen] as CargoItem : null);
            ReadActions(c.Chosen >= 0 ? _stacks[c.Chosen].GetComponent<CargoItemUi>() : null);
            Button all = BuyKits.Get<Button>(GameContract.CargoSellAll, _ui);
            _m.SellAll = all != null ? BuyKits.Words(all) ?? UiText.T("bizman_inventory_sell_all") : null;
            _m.SellAllShown = all != null && all.gameObject.activeSelf;
            _m.SellAllEnabled = all != null && all.interactable;
            _sb.Append(c.Chosen).Append('|').Append(_m.SellAllShown).Append(_m.SellAllEnabled);
            return _sb.ToString();
        }

        // The chosen stack's figures as the game reckons them: its packs and pieces, what was paid for them (or is owed),
        // what a pack sells for and the whole stack now (ItemHelper.GetSellingPrice, the game's selling share, as the stack's
        // own Sell asks it). Made again only when the stack or its packs change.
        private void Detail(CargoItem item)
        {
            int packs = item != null && item.cargoInstances != null ? item.cargoInstances.Count : 0;
            if (ReferenceEquals(item, _detailOf) && packs == _detailPacks)
            {
                return;
            }
            _detailOf = item;
            _detailPacks = packs;
            _sell = null;
            UiCatalogueModel c = _m.Catalogue;
            if (packs == 0)
            {
                c.DetailLabels = System.Array.Empty<string>();
                c.DetailValues = System.Array.Empty<string>();
                return;
            }
            int pieces = 0;
            float paid = 0f;
            for (int i = 0; i < packs; i++)
            {
                pieces += item.cargoInstances[i].amount;
                paid += item.cargoInstances[i].GetWorth();
            }
            bool sells = item.IsSellable;
            float each = sells ? item.cargoInstances[0].GetSellingPrice() : 0f;
            string goods = GoodsOf(item);
            int keeps = SpoilRead.Keeps(goods, false), cold = keeps > 0 ? SpoilRead.Keeps(goods, true) : -1;
            int n = 3 + (sells ? 2 : 0) + (keeps > 0 ? 1 : 0) + (cold > keeps ? 1 : 0), at = 3;
            c.DetailLabels = new string[n];
            c.DetailValues = new string[n];
            c.DetailLabels[0] = UiText.T("reallife_ui_buy_packs");
            c.DetailValues[0] = packs.ToString("N0", CultureInfo.InvariantCulture);
            c.DetailLabels[1] = UiText.T("reallife_ui_buy_pieces");
            c.DetailValues[1] = pieces.ToString("N0", CultureInfo.InvariantCulture);
            c.DetailLabels[2] = UiText.T(item.paid ? "reallife_ui_buy_paid" : "reallife_ui_buy_to_pay");
            c.DetailValues[2] = paid.ToShortCurrencyFormat();
            if (sells)
            {
                c.DetailLabels[at] = UiText.T("reallife_ui_buy_sells_for");
                c.DetailValues[at++] = UiText.T("reallife_ui_buy_each").Replace("{price}", each.ToShortCurrencyFormat());
            }
            if (keeps > 0)
            {
                c.DetailLabels[at] = UiText.T("reallife_ui_buy_keeps");   // from the day it is stocked: goods on the road do not age
                c.DetailValues[at++] = Days(keeps);
            }
            if (cold > keeps)
            {
                c.DetailLabels[at] = UiText.T("reallife_ui_buy_keeps_cold");
                c.DetailValues[at++] = Days(cold);
            }
            if (sells)
            {
                string all = (each * packs).ToShortCurrencyFormat();
                c.DetailLabels[at] = UiText.T("reallife_ui_buy_worth_now");
                c.DetailValues[at] = all;
                _sell = UiText.T("reallife_ui_buy_sell_for").Replace("{price}", all);
            }
        }

        // The product a stack holds, by the name the game keys it with; null for none.
        private static string GoodsOf(CargoItem item) =>
            item != null && item.cargoInstances != null && item.cargoInstances.Count > 0 && item.cargoInstances[0] != null ? item.cargoInstances[0].itemName : null;

        private static string Days(int days) => UiText.T("reallife_ui_buy_days").Replace("{days}", days.ToString(CultureInfo.InvariantCulture));

        // "Keeps 3 days" on a tile, made once a screen for each short life.
        private string QuickWords(int days)
        {
            if (_quickWords[days] == null)
            {
                _quickWords[days] = UiText.T("reallife_ui_buy_keeps_tile").Replace("{days}", days.ToString(CultureInfo.InvariantCulture));
            }
            return _quickWords[days];
        }

        // The chosen stack's own buttons as the detail's actions: its action (Take, Place, Read), Discard, then Sell as the main.
        private void ReadActions(CargoItemUi stack)
        {
            _buttons.Clear();
            _actions.Clear();
            if (stack != null)
            {
                Button act = BuyKits.Get<Button>(GameContract.SlotAction, stack);
                Add(act, BuyKits.Words(BuyKits.Get<Component>(GameContract.SlotActionLabel, stack)), UiButtonKind.Secondary, UiIcon.None, UiAction.Count);
                Add(BuyKits.Get<Button>(GameContract.SlotDiscard, stack), UiText.T("itempanelui_buttons_discard"), UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count);
                Add(BuyKits.Get<Button>(GameContract.SlotSell, stack), _sell ?? UiText.T("itempanelui_sell"), UiButtonKind.Primary, UiIcon.Coin, UiAction.Alt);
            }
            if (_m.Catalogue.Actions.Length != _actions.Count)
            {
                _m.Catalogue.Actions = new UiActionModel[_actions.Count];
            }
            _actions.CopyTo(_m.Catalogue.Actions);
            for (int i = 0; i < _actions.Count; i++)
            {
                _sb.Append(_actions[i].Label).Append(_actions[i].Disabled ? '-' : '+');
            }
        }

        private void Add(Button b, string label, UiButtonKind kind, UiIcon icon, UiAction key)
        {
            if (b == null || !b.gameObject.activeSelf || string.IsNullOrEmpty(label))
            {
                return;
            }
            _buttons.Add(b);
            _actions.Add(new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = _actions.Count, Disabled = !b.interactable });
        }
    }
}
