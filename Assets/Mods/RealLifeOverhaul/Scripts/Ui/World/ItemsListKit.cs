using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UI.Dialog;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's items list (ItemsList, UIs.itemsList: a store's goods from BizMan's Browse inventory, and the goods an
    /// order adds at the furniture store, the food supplier and the vehicle store) drawn as the kit's items list while it
    /// is open. A box: its keys are its own while it stands (the search is typed, the rows walked), Back closes it as the
    /// game's Cancel does. The dimmer under it keeps holding the clicks off the world. Game side.
    /// </summary>
    public sealed class ItemsListKit : IUiGameScreen
    {
        internal const string Dimmer = "Dimmer", Container = "Container";

        public string Id => UiWorld.ItemsId;

        public string Name => "the items list";

        public string Title => UiText.T("common_search");

        public UiIcon Icon => UiIcon.ShoppingCart;

        public UiGameKind Kind => UiGameKind.Box;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.itemsList : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldItems.Off)
            {
                return GameContract.UiWorldItems.Why;
            }
            return found.transform.Find(Dimmer) == null || found.transform.Find(Container) == null ? "its dimmer or its container is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            conceal.Add(found.transform.Find(Dimmer), true);
            conceal.Add(found.transform.Find(Container));
        }

        public IUiScreen Make(Component found) => new ItemsListScreen((ItemsList)found);
    }

    /// <summary>
    /// The items list as the kit's (UiItemsList): the game's title, the search with how many show, each thing's picture,
    /// name and price, and in an order its add button, or its amount between the game's minus and plus. Every press goes
    /// through the game's own row (its add button, its amount's buttons and field, its search), so an order's rules (the
    /// most it holds, a row taken out at nought) stay the order's. The list the game filters is the list shown. NPCs order
    /// no goods through this window, so there is no other side. The game keeps a row's add button on an outline it
    /// switches on only under the mouse (UI.HoverEffect), which never happens under the kit, so the add is pressed and
    /// read through UiWidgets.PressUnder and ShowsUnder: asking whether that button shows refused every add (1.0.0 to
    /// 1.0.2). Cost: a layout as something changes; four times a second a walk of the rows' switches and amounts.
    /// </summary>
    public sealed class ItemsListScreen : IUiScreen, IUiTyping
    {
        private const float Every = 0.25f;

        private readonly ItemsList _list;
        private readonly UiItemsListModel _m = new UiItemsListModel();
        private readonly UiPrompt[] _prompts = new UiPrompt[3];
        private ItemsListEntry[] _shown = new ItemsListEntry[32];
        private DeliveryItemEntry[] _order = new DeliveryItemEntry[32];
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _typing = UiItemsList.SearchArg;

        public ItemsListScreen(ItemsList list)
        {
            _list = list;
            _m.Rows = new UiGoodsRow[32];
        }

        public string Id => UiWorld.ItemsId;

        /// <summary>The model last read, for the tests.</summary>
        public UiItemsListModel Model => _m;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Read();
            _prompts[0] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
            _prompts[1] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") };
            _prompts[2] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("common_close") };
            UiItemsList.Lay(s, _m, frame, dark, keys, _prompts, _prompts.Length);
            _seen = Signature();
            _dirty = false;
        }

        // The rows the game shows (its search hides the rest), each as the game's row has it.
        private void Read()
        {
            var entries = GameContract.ItemsEntries.Field != null ? GameContract.ItemsEntries.Field.GetValue(_list) as List<ItemsListEntry> : null;
            int all = entries != null ? entries.Count : 0, n = 0;
            for (int i = 0; i < all; i++)
            {
                ItemsListEntry e = entries[i];
                if (e != null && e.gameObject.activeSelf)
                {
                    if (n == _shown.Length)
                    {
                        Array.Resize(ref _shown, n * 2);
                        Array.Resize(ref _order, n * 2);
                    }
                    _shown[n] = e;
                    _order[n] = e.GetComponent<DeliveryItemEntry>();
                    n++;
                }
            }
            if (_m.Rows.Length < n)
            {
                _m.Rows = new UiGoodsRow[Math.Max(n, _m.Rows.Length * 2)];
            }
            for (int i = 0; i < n; i++)
            {
                _m.Rows[i] = Row(_shown[i], _order[i]);
            }
            for (int i = n; i < _shown.Length; i++)
            {
                _shown[i] = null;
                _order[i] = null;
            }
            _m.RowCount = n;
            _m.Title = WorldHost.Plain(Title()) ?? UiText.T("common_search");
            _m.Search = Search();
            _m.Placeholder = UiText.T("common_search");
            bool searching = !string.IsNullOrEmpty(_m.Search);
            _m.Count = searching ? UiText.Fill("reallife_ui_world_items_count", "shown", UiText.Whole(n), "all", UiText.Whole(all)) : null;
            _m.EmptyTitle = UiText.T(searching ? "reallife_ui_world_items_none_found" : "reallife_ui_world_items_empty");
            _m.EmptyBody = searching ? UiText.Fill("reallife_ui_world_items_none_found_body", "search", _m.Search) : UiText.T("reallife_ui_world_items_empty_body");
            _m.ClearLabel = searching ? UiText.T("reallife_ui_world_items_clear") : null;
            _m.Offset = Math.Min(_m.Offset, UiItemsList.MostOffset(_m));
        }

        // A row as the game's: its name, its price, its picture, and in an order its amount or its add button.
        private static UiGoodsRow Row(ItemsListEntry e, DeliveryItemEntry d)
        {
            var price = GameContract.ItemsPrice.Field != null ? GameContract.ItemsPrice.Field.GetValue(e) as TMP_Text : null;
            var icon = GameContract.ItemsIcon.Field != null ? GameContract.ItemsIcon.Field.GetValue(e) as Image : null;
            string cost = price != null && price.gameObject.activeSelf ? WorldHost.Plain(price.text) : null;
            var row = new UiGoodsRow
            {
                Name = WorldHost.Plain(e.NameLocalized) ?? string.Empty, Picture = icon != null ? WorldPictures.Of(icon.sprite) : null, Icon = UiIcon.Package,
                Sub = cost,
            };
            if (d == null)
            {
                return row;
            }
            if (cost != null)
            {
                row.Sub = UiText.Fill("reallife_ui_world_items_each", "price", cost);
            }
            AmountSelector amount = d.AmountSelector;
            if (amount != null && amount.gameObject.activeSelf)
            {
                row.Amount = UiText.Whole(amount.Amount);
                row.CanLess = Interactable(GameContract.ItemsLess, amount);
                row.CanMore = Interactable(GameContract.ItemsMore, amount);
            }
            else
            {
                row.Add = CanAdd(d) ? UiGoodsAdd.Add : UiGoodsAdd.Full;
            }
            return row;
        }

        private static bool Interactable(GameMember member, object owner) => member.Field != null && member.Field.GetValue(owner) is Button b && b.interactable;

        // The row's add button as the game's own row would take a click: by the same test the press uses, so the plus is
        // never drawn ready and then refused.
        private static Button AddButton(DeliveryItemEntry d) => d != null && GameContract.ItemsAdd.Field != null ? GameContract.ItemsAdd.Field.GetValue(d) as Button : null;

        private static bool CanAdd(DeliveryItemEntry d) => UiWidgets.ShowsUnder(AddButton(d), d);

        // The add pressed on the game's own row. An order that holds all it may says so in words: the greyed plus alone
        // gave only the error sound.
        private static bool Add(DeliveryItemEntry d)
        {
            if (UiWidgets.PressUnder(AddButton(d), d))
            {
                return true;
            }
            Button b = AddButton(d);
            if (d != null && d.gameObject.activeInHierarchy && b != null && !b.interactable)
            {
                UiRefusals.Because("the order holds all it may (the game holds the row's add button)");
                Notifications.Show(NotificationType.Warning, "reallife_ui_world_items_full", null, 8f);
            }
            return false;
        }

        private string Title()
        {
            var t = GameContract.ItemsTitle.Field != null ? GameContract.ItemsTitle.Field.GetValue(_list) as TMP_Text : null;
            return t != null ? t.text : null;
        }

        private TMP_InputField SearchField()
        {
            var f = GameContract.ItemsSearch.Field != null ? GameContract.ItemsSearch.Field.GetValue(_list) as UI.Components.InputField : null;
            return f != null ? f.tmpInputField : null;
        }

        private string Search()
        {
            TMP_InputField f = SearchField();
            return f != null && !string.IsNullOrEmpty(f.text) ? f.text : null;
        }

        // What the rows show: how many, which are seen, each order row's amount and add state, the title and the search.
        private long Signature()
        {
            var entries = GameContract.ItemsEntries.Field != null ? GameContract.ItemsEntries.Field.GetValue(_list) as List<ItemsListEntry> : null;
            long seen = entries != null ? entries.Count : -1;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ItemsListEntry e = entries[i];
                seen = unchecked(seen * 31 + (e != null && e.gameObject.activeSelf ? e.GetInstanceID() : 0));
            }
            for (int i = 0; i < _order.Length && _order[i] != null; i++)
            {
                AmountSelector a = _order[i].AmountSelector;
                bool on = a != null && a.gameObject.activeSelf;
                seen = unchecked(seen * 31 + (on ? a.Amount + 1 : 0));
                seen = unchecked(seen * 31 + (on ? (Interactable(GameContract.ItemsMore, a) ? 2 : 1) : CanAdd(_order[i]) ? 4 : 3));
            }
            string title = Title(), search = Search();
            seen = unchecked(seen * 31 + (title != null ? title.GetHashCode() : 0));
            return unchecked(seen * 31 + (search != null ? search.GetHashCode() : 0));
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            DeliveryItemEntry d = arg >= 0 && arg < _order.Length ? _order[arg] : null;
            switch (command)
            {
                case UiItemsList.AddCommand:
                    return Add(d);
                case UiItemsList.LessCommand:
                    return d != null && d.AmountSelector != null && UiWidgets.Press(GameContract.ItemsLess.Field?.GetValue(d.AmountSelector) as Button);
                case UiItemsList.MoreCommand:
                    return d != null && d.AmountSelector != null && UiWidgets.Press(GameContract.ItemsMore.Field?.GetValue(d.AmountSelector) as Button);
                case UiItemsList.ClearCommand:
                    Typed(UiItemsList.SearchArg, string.Empty);
                    return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (command == UiItemsList.TypeCommand)
            {
                _typing = arg;
            }
        }

        public bool Back()
        {
            _list.Toggle(false);   // as the game's Cancel and its close do
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            float top = index * UiItemsList.RowHeight, shown = UiItemsList.ViewHeight(_m);
            if (top < _m.Offset)
            {
                _m.Offset = top;
            }
            else if (top + UiItemsList.RowHeight > _m.Offset + shown)
            {
                _m.Offset = top + UiItemsList.RowHeight - shown;
            }
            _m.Offset = Math.Max(0f, Math.Min(_m.Offset, UiItemsList.MostOffset(_m)));
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _m.Offset = Math.Max(0f, Math.Min(_m.Offset + notches * UiItemsList.RowHeight, UiItemsList.MostOffset(_m)));
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => UiItemsList.TypeCommand;

        public string TypingText
        {
            get
            {
                if (_typing == UiItemsList.SearchArg)
                {
                    return Search() ?? string.Empty;
                }
                AmountSelector a = _typing >= 0 && _typing < _order.Length && _order[_typing] != null ? _order[_typing].AmountSelector : null;
                return a != null ? UiText.Whole(a.Amount) : string.Empty;
            }
        }

        public void Typed(string text) => Typed(_typing, text);

        // The search into the game's own field, which filters its rows; an amount into the row's own field, which the game
        // holds to the order's most (a nought, which would take the row out, waits for a figure after it).
        private void Typed(int target, string text)
        {
            _dirty = true;
            if (target == UiItemsList.SearchArg)
            {
                TMP_InputField f = SearchField();
                if (f != null)
                {
                    f.text = text ?? string.Empty;
                    _list.OnSearchBarTextChanged();
                }
                _m.Offset = 0f;
                return;
            }
            AmountSelector a = target >= 0 && target < _order.Length && _order[target] != null ? _order[target].AmountSelector : null;
            if (a != null && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= 1)
            {
                UiWidgets.Type(GameContract.ItemsAmount.Field?.GetValue(a) as TMP_InputField, n.ToString(System.Globalization.CultureInfo.InvariantCulture), true);
            }
        }
    }
}
