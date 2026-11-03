using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A character screen as the kit's catalogue panel: tabs down its edge, the pieces as tiles, the colours, the buttons.</summary>
    internal sealed class WardrobeScreen : IUiScreen, IUiStepping, IUiTyping
    {
        private const float Every = 0.25f;
        private const string ModBand = "RealLifeOverhaul band";   // GridLooks' bands on the mod's tiles: the name, then the corner
        private readonly WardrobeKind _kind;
        private readonly CharacterCustomizer _ui;
        private readonly UiWardrobeModel _m = new UiWardrobeModel();
        private readonly List<MenuVertical.Category> _tabs = new List<MenuVertical.Category>(8);
        private readonly List<MenuVertical.SubCategory> _subs = new List<MenuVertical.SubCategory>(8);
        private readonly List<Transform> _pieces = new List<Transform>(64), _colours = new List<Transform>(24), _bands = new List<Transform>(2);
        private readonly List<Button> _buttons = new List<Button>(4);
        private readonly List<UiActionModel> _actions = new List<UiActionModel>(4);
        private readonly List<string> _words = new List<string>(2);
        private readonly StringBuilder _sb = new StringBuilder(512);
        private Slider[] _sliders = new Slider[0];
        private string _seen;
        private float _next;
        private bool _dirty = true;
        private const int All = 0, Owned = 1, Mod = 2;                // the grid's filters
        private readonly List<int> _shown = new List<int>(64);        // the pieces the filter and the search let through, in the game's order
        private UiCatalogueCard[] _all = new UiCatalogueCard[0];     // every piece's card, and whether it is owned or the mod's
        private bool[] _owned = new bool[0], _mod = new bool[0];
        private readonly int[] _filterOf = new int[3];                // the filter each pill stands for
        private int _filterKind, _filterSig = -1;
        private string _search = string.Empty;
        private readonly List<string> _basketNames = new List<string>(4);
        private readonly List<int> _basketPrices = new List<int>(4);
        private int _basketStamp = int.MinValue;
        private int _resetAt = -1;                                    // the clothes' Reset among the actions (the kit's own button), or -1

        public WardrobeScreen(WardrobeKind kind, CharacterCustomizer ui)
        {
            _kind = kind;
            _ui = ui;
            _m.Leave = UiText.T("dialog_leave_button");
            _m.ColourLabel = UiText.T("reallife_ui_buy_colour");
            _m.EmptyTitle = UiText.T("reallife_ui_buy_empty_grid");
            _m.EmptyBody = UiText.T("reallife_ui_buy_empty_grid_body");
            _m.Prompts = BuyKits.Prompts();
            _m.PromptCount = _m.Prompts.Length;
        }

        internal static string TitleOf(WardrobeKind kind) => UiText.T(kind == WardrobeKind.Clothes ? "change_character_clothes_title"
            : kind == WardrobeKind.Hair ? "change_character_hair_title" : "plastic_surgery_title");

        public string Id => BuyKits.WardrobeId;

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
            UiWardrobe.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            MenuVertical menu = BuyKits.Get<MenuVertical>(GameContract.LookMenu, _ui);
            switch (command)
            {
                case UiWardrobe.TabCommand:
                    if (menu == null || arg < 0 || arg >= _tabs.Count)
                    {
                        return false;
                    }
                    menu.OnCategoryButtonClick(_tabs[arg]);
                    return Again(true);
                case UiWardrobe.SubCommand:
                {
                    var sub = BuyKits.Get<Component>(GameContract.LookSubMenu, menu);
                    UI.Components.MenuVerticalCategorized.SubCategoryButton[] buttons = sub != null
                        ? sub.GetComponentsInChildren<UI.Components.MenuVerticalCategorized.SubCategoryButton>(false) : null;
                    return buttons != null && arg >= 0 && arg < buttons.Length && Again(UiWidgets.Press(buttons[arg].GetComponentInChildren<Button>(false)));
                }
                case UiWardrobe.GridCommand + UiCatalogue.Card:
                {
                    int piece = arg >= 0 && arg < _shown.Count ? _shown[arg] : -1;
                    return piece >= 0 && piece < _pieces.Count && Again(UiWidgets.Press(_pieces[piece].GetComponent<Button>()));
                }
                case UiWardrobe.FilterCommand:
                    if (arg < 0 || arg >= _m.FilterCount)
                    {
                        return false;
                    }
                    _filterKind = _filterOf[arg];
                    _m.Grid.Offset = 0f;
                    return Again(true);
                case UiWardrobe.SwatchCommand:
                    return arg >= 0 && arg < _colours.Count && Again(UiWidgets.Press(_colours[arg].GetComponent<Button>()));
                case UiWardrobe.SliderCommand:
                    if (arg < 0 || arg >= _sliders.Length)
                    {
                        return false;
                    }
                    _m.Held = _m.Held == arg ? -1 : arg;   // pressed, the slider takes Left and Right; pressed again, it lets go
                    _dirty = true;
                    return true;
                case UiWardrobe.ActionCommand:
                    if (arg >= 0 && arg == _resetAt)
                    {
                        bool reset = Leisure.ClothesScreen.Reset();
                        if (reset && _ui is ChangeCharacterClothesUI shown)
                        {
                            shown.Show(BuyKits.Value(GameContract.LookShown, _ui, AppearanceElementType.Torso));   // its grid marks what is worn again
                        }
                        return Again(reset);
                    }
                    return arg >= 0 && arg < _buttons.Count && Again(UiWidgets.Press(_buttons[arg]));
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (command != UiWardrobe.SliderCommand && _m.Held >= 0)
            {
                _m.Held = -1;   // focus moved off the slider: it lets go
                _dirty = true;
            }
        }

        // Leave: the game's own close, which asks first when something is not kept (the clothes' question).
        public bool Back()
        {
            if (_ui is ChangeCharacterClothesUI clothes)
            {
                clothes.Hide();
            }
            else if (_ui is ChangeCharacterHairUI hair)
            {
                hair.Hide();
            }
            else if (_ui is PlasticSurgeryUI surgery)
            {
                surgery.Hide();
            }
            return Again(true);
        }

        public void Tab(int step)
        {
            if (_tabs.Count == 0)
            {
                return;
            }
            int now = Math.Max(0, _m.Tab);
            Press(UiWardrobe.TabCommand, ((now + step) % _tabs.Count + _tabs.Count) % _tabs.Count);
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Option && _resetAt >= 0)
            {
                return !_actions[_resetAt].Disabled && Press(UiWardrobe.ActionCommand, _resetAt);
            }
            if (action != UiAction.Alt || _actions.Count == 0)
            {
                return false;
            }
            int main = _actions.Count - 1;   // the main one is the last
            return main != _resetAt && Press(UiWardrobe.ActionCommand, main);
        }

        public void Reveal(int list, int index)
        {
            _m.Grid.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _m.Grid.Offset = Mathf.Max(0f, _m.Grid.Offset - notches * UiTokens.S12);
            _dirty = true;
        }

        // The search over the clothes' grid: by a piece's name, any case.
        public int TypingCommand => UiWardrobe.SearchCommand;

        public string TypingText => _search;

        public void Typed(string text)
        {
            _search = text ?? string.Empty;
            _m.Grid.Offset = 0f;
            Again(true);
        }

        public bool Steps(int command) => command == UiWardrobe.SliderCommand && _m.Held >= 0;

        public void Step(int command, int arg, int step)
        {
            if (command != UiWardrobe.SliderCommand || arg < 0 || arg >= _sliders.Length || _sliders[arg] == null)
            {
                return;
            }
            Slider s = _sliders[arg];
            float range = s.maxValue - s.minValue;
            Again(UiWidgets.Slide(s, Mathf.Clamp(s.value + step * (s.wholeNumbers ? Mathf.Max(1f, Mathf.Round(range / 20f)) : range / 20f), s.minValue, s.maxValue)));
        }

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;
                _dirty = true;
            }
            return done;
        }

        // ------------------------------------------------------------------------------------------------- the game's screen read

        // The screen as the game filled it for the category shown. Returns what the screen shows, to see a change by.
        private string Read()
        {
            _sb.Length = 0;
            ReadMenu();
            ReadPieces();
            ReadColours();
            ReadSliders();
            ReadActions();
            ReadBasket();
            string kind = _m.Tab >= 0 && _m.Tab < _m.Tabs.Length ? _m.Tabs[_m.Tab].Label : null;
            _m.Title = _kind == WardrobeKind.Clothes && kind != null ? TitleOf(_kind) + ", " + kind : TitleOf(_kind);
            _sb.Append(_m.Title);
            return _sb.ToString();
        }

        // The categories the game shows (each with its picture), the one open, and its own kinds when it has them.
        private void ReadMenu()
        {
            _tabs.Clear();
            _subs.Clear();
            MenuVertical menu = BuyKits.Get<MenuVertical>(GameContract.LookMenu, _ui);
            MenuVertical.Category[] all = menu != null ? menu.categories : null;
            for (int id = 0; all != null && id < all.Length; id++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].categoryId == id)
                    {
                        _tabs.Add(all[i]);
                        break;
                    }
                }
            }
            if (_m.Tabs.Length != _tabs.Count)
            {
                _m.Tabs = new UiEdgeTab[_tabs.Count];
            }
            for (int i = 0; i < _tabs.Count; i++)
            {
                MenuVertical.Category c = _tabs[i];
                string link = !string.IsNullOrEmpty(c.linkId) ? c.linkId : c.subCategories != null && c.subCategories.Length > 0 ? c.subCategories[0].linkId : null;
                _m.Tabs[i] = new UiEdgeTab { Picture = BuyPictures.Of(c.icon), Icon = UiIcon.Person, Label = KindWords(link) };
                _sb.Append(_m.Tabs[i].Picture).Append(';');
            }
            _m.TabCount = _tabs.Count;
            int chosen = BuyKits.Value(GameContract.LookCategory, menu, -1);
            _m.Tab = chosen >= 0 && chosen < _tabs.Count ? chosen : -1;
            var sub = BuyKits.Get<Component>(GameContract.LookSubMenu, menu);
            if (_m.Tab >= 0 && sub != null && sub.gameObject.activeSelf && _tabs[_m.Tab].subCategories != null)
            {
                MenuVertical.SubCategory[] kinds = _tabs[_m.Tab].subCategories;
                for (int id = 0; id < kinds.Length; id++)
                {
                    for (int i = 0; i < kinds.Length; i++)
                    {
                        if (kinds[i] != null && kinds[i].subCategoryId == id)
                        {
                            _subs.Add(kinds[i]);
                            break;
                        }
                    }
                }
            }
            if (_m.Subs.Length != _subs.Count)
            {
                _m.Subs = new string[_subs.Count];
            }
            string now = BuyKits.Value(GameContract.LookShown, _ui, AppearanceElementType.Torso).ToString();
            _m.Sub = -1;
            for (int i = 0; i < _subs.Count; i++)
            {
                _m.Subs[i] = KindWords(_subs[i].linkId) ?? (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (string.Equals(_subs[i].linkId, now, StringComparison.OrdinalIgnoreCase))
                {
                    _m.Sub = i;
                }
            }
            _m.SubCount = _subs.Count;
            _sb.Append(_m.Tab).Append('/').Append(_m.Sub).Append('|');
        }

        // The pieces of the kind shown, as the game lists them: its picture, the chosen one; the mod's tiles carry their
        // name and their corner (the price, Owned, or the mod's tag) on the bands GridLooks adds.
        private void ReadPieces()
        {
            ElementPicker picker = BuyKits.Get<ElementPicker>(GameContract.LookPicker, _ui);
            if (picker != null && picker.gameObject.activeInHierarchy)
            {
                BuyKits.Copies(BuyKits.Get<Transform>(GameContract.LookPieceTemplate, picker), _pieces);
            }
            else
            {
                _pieces.Clear();
            }
            if (_all.Length < _pieces.Count)
            {
                _all = new UiCatalogueCard[_pieces.Count];
                _owned = new bool[_pieces.Count];
                _mod = new bool[_pieces.Count];
            }
            int chosenAt = -1, ownedCount = 0, modCount = 0;
            _m.ChosenName = null;
            _m.ChosenDetail = null;
            string owned = UiText.T("reallife_clothes_screen_owned");
            for (int i = 0; i < _pieces.Count; i++)
            {
                Transform p = _pieces[i];
                Transform icon = p.Find("Icon");
                var image = icon != null ? icon.GetComponent<Image>() : null;
                Sprite sprite = image != null ? image.sprite : null;
                _bands.Clear();
                for (int c = 0; c < p.childCount; c++)
                {
                    Transform band = p.GetChild(c);
                    if (band.name == ModBand && band.gameObject.activeSelf)
                    {
                        _bands.Add(band);
                    }
                }
                string name = _bands.Count > 0 ? BuyKits.Words(_bands[0]) : null, corner = _bands.Count > 1 ? BuyKits.Words(_bands[1]) : null;
                bool price = corner != null && corner.Length > 0 && !char.IsLetter(corner[0]);
                _all[i] = new UiCatalogueCard
                {
                    Picture = BuyPictures.Of(sprite), Aspect = BuyPictures.Aspect(sprite), Icon = UiIcon.Tag, Name = name,
                    Figure = price ? corner : null, State = price ? null : corner, StateTone = corner == owned ? UiRole.Ok : UiRole.Accent
                };
                _owned[i] = corner != null && corner == owned;
                _mod[i] = _bands.Count > 0;
                ownedCount += _owned[i] ? 1 : 0;
                modCount += _mod[i] ? 1 : 0;
                Transform chosen = p.Find("Selected");
                if (chosen != null && chosen.gameObject.activeSelf)
                {
                    chosenAt = i;
                    _m.ChosenName = name;
                    _m.ChosenDetail = price ? corner : null;
                }
                _sb.Append(_all[i].Picture).Append(name).Append(corner).Append(';');
            }
            Filter(_pieces.Count, ownedCount, modCount, chosenAt);
            _sb.Append(chosenAt).Append('|').Append(_filterKind).Append(_search).Append('|');
        }

        // The pills over the grid with their counts, where they tell pieces apart (Owned in the shop, the mod's own beside the
        // game's in the wardrobe), and the clothes' search by name: the grid shows what both let through, in the game's order.
        private void Filter(int n, int owned, int mod, int chosen)
        {
            int pills = 0;
            _filterOf[pills++] = All;
            if (owned > 0 && owned < n)
            {
                _filterOf[pills++] = Owned;
            }
            if (mod > 0 && mod < n)
            {
                _filterOf[pills++] = Mod;
            }
            pills = pills > 1 ? pills : 0;   // All alone tells nothing apart
            int at = -1;
            for (int i = 0; i < pills; i++)
            {
                at = _filterOf[i] == _filterKind ? i : at;
            }
            if (at < 0)
            {
                _filterKind = All;
                at = pills > 0 ? 0 : -1;
            }
            int sig = (n * 1000 + owned) * 1000 + mod;
            if (sig != _filterSig || _m.Filters.Length < pills)
            {
                _filterSig = sig;
                _m.Filters = new string[pills];
                for (int i = 0; i < pills; i++)
                {
                    int k = _filterOf[i];
                    _m.Filters[i] = UiText.T(k == Owned ? "reallife_ui_buy_filter_owned" : k == Mod ? "reallife_ui_buy_filter_rlo" : "reallife_ui_buy_filter_all")
                        .Replace("{count}", (k == Owned ? owned : k == Mod ? mod : n).ToString(CultureInfo.InvariantCulture));
                }
            }
            _m.FilterCount = pills;
            _m.Filter = at;
            _m.Searching = _kind == WardrobeKind.Clothes && n > 0;
            _m.Search = _search;
            _m.SearchHint = UiText.T("reallife_ui_buy_find");
            string q = _m.Searching ? _search.Trim() : string.Empty;
            _shown.Clear();
            for (int i = 0; i < n; i++)
            {
                bool through = (_filterKind != Owned || _owned[i]) && (_filterKind != Mod || _mod[i]);
                if (through && (q.Length == 0 || (_all[i].Name != null && _all[i].Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    _shown.Add(i);
                }
            }
            UiCatalogueModel g = _m.Grid;
            if (g.Cards.Length < _shown.Count)
            {
                g.Cards = new UiCatalogueCard[_shown.Count];
            }
            g.Chosen = -1;
            for (int i = 0; i < _shown.Count; i++)
            {
                g.Cards[i] = _all[_shown[i]];
                g.Chosen = _shown[i] == chosen ? i : g.Chosen;
            }
            g.CardCount = _shown.Count;
        }

        // What the shop's basket holds (the mod's clothes shop: each piece on and not owned, with its price), made again only
        // when the shop prices what is worn again. None outside the shop.
        private void ReadBasket()
        {
            int stamp = _kind == WardrobeKind.Clothes && Leisure.ClothesScreen.Shopping ? Leisure.ClothesScreen.BasketStamp : -1;
            if (stamp != _basketStamp)
            {
                _basketStamp = stamp;
                FillBasket(stamp >= 0);
            }
            _sb.Append(_m.BasketTotal).Append('|');
        }

        private void FillBasket(bool shop)
        {
            _m.BasketTotal = null;
            if (!shop || !Leisure.ClothesScreen.Basket(_basketNames, _basketPrices) || _basketNames.Count == 0)
            {
                _m.Basket = System.Array.Empty<UiInfoRow>();
                return;
            }
            int total = 0;
            _m.Basket = new UiInfoRow[_basketNames.Count];
            for (int i = 0; i < _basketNames.Count; i++)
            {
                _m.Basket[i] = new UiInfoRow { Label = _basketNames[i], Value = UiText.Money(_basketPrices[i]) };
                total += _basketPrices[i];
            }
            _m.BasketLabel = UiText.T("reallife_ui_buy_basket");
            _m.BasketTotalLabel = UiText.T("reallife_ui_buy_to_pay");
            _m.BasketTotal = UiText.Money(total);
        }

        // The colours of the piece shown, as the game tints its cells (or the game's own picture of a pattern).
        private void ReadColours()
        {
            AppearanceColorPicker picker = _ui.appearanceColorPicker;
            if (picker != null && picker.gameObject.activeInHierarchy)
            {
                BuyKits.Copies(BuyKits.Get<Transform>(GameContract.LookColourTemplate, picker), _colours);
            }
            else
            {
                _colours.Clear();
            }
            if (_m.Swatches.Length < _colours.Count)
            {
                _m.Swatches = new string[_colours.Count];
            }
            _m.Swatch = -1;
            for (int i = 0; i < _colours.Count; i++)
            {
                Transform cell = _colours[i].Find("Sprite");
                var image = cell != null ? cell.GetComponent<Image>() : null;
                bool tint = image == null || image.sprite == null || image.color.r < 0.99f || image.color.g < 0.99f || image.color.b < 0.99f;
                _m.Swatches[i] = tint ? UiDraw.SwatchKey(image != null ? BuyKits.Rgb(image.color) : 0x808080) : BuyPictures.Of(image.sprite);
                Transform chosen = _colours[i].Find("Selected");
                if (chosen != null && chosen.gameObject.activeSelf)
                {
                    _m.Swatch = i;
                }
                _sb.Append(_m.Swatches[i]);
            }
            _m.SwatchCount = _colours.Count;
            _sb.Append(_m.Swatch).Append('|');
        }

        // The surgeon's body: strength and weight as the game's own sliders hold them.
        private void ReadSliders()
        {
            int n = 0;
            BodyValues body = _kind == WardrobeKind.Surgery ? BuyKits.Get<BodyValues>(GameContract.LookBody, _ui) : null;
            Slider strength = null, weight = null;
            if (body != null && body.gameObject.activeInHierarchy)
            {
                strength = BuyKits.Get<Slider>(GameContract.LookStrength, body);
                weight = BuyKits.Get<Slider>(GameContract.LookWeight, body);
                n = (strength != null ? 1 : 0) + (weight != null ? 1 : 0);
            }
            if (_sliders.Length != n)
            {
                _sliders = new Slider[n];
                _m.Sliders = new UiSliderRow[n];
            }
            int at = 0;
            if (strength != null && n > 0)
            {
                _sliders[at] = strength;
                _m.Sliders[at++] = Row(UiText.T("reallife_ui_buy_strength"), strength);
            }
            if (weight != null && n > 0)
            {
                _sliders[at] = weight;
                _m.Sliders[at] = Row(UiText.T("reallife_ui_buy_weight"), weight);
            }
            _m.SliderCount = n;
            if (_m.Held >= n)
            {
                _m.Held = -1;
            }
            for (int i = 0; i < n; i++)
            {
                _sb.Append(_m.Sliders[i].Figure).Append(';');
            }
        }

        private static UiSliderRow Row(string label, Slider s)
        {
            float range = s.maxValue - s.minValue, share = range > 0f ? Mathf.Clamp01((s.value - s.minValue) / range) : 0f;
            return new UiSliderRow { Label = label, Share = share, Figure = Mathf.RoundToInt(share * 100f).ToString(System.Globalization.CultureInfo.InvariantCulture), Disabled = !s.interactable };
        }

        // The screen's own buttons, the main one last: the clothes' Save (the mod's shop writes Buy with the price there),
        // the hairdresser's Change hair, the surgeon's Randomize, Revert and Apply.
        private void ReadActions()
        {
            _buttons.Clear();
            _actions.Clear();
            _resetAt = -1;
            switch (_kind)
            {
                case WardrobeKind.Clothes:
                    AddReset();
                    Add(BuyKits.Get<Button>(GameContract.LookSave, _ui), null, UiButtonKind.Primary, UiAction.Alt);
                    break;
                case WardrobeKind.Hair:
                    Add(BuyKits.Get<Button>(GameContract.LookHairButton, _ui), BuyKits.Get<Component>(GameContract.LookHairLabel, _ui), UiButtonKind.Primary, UiAction.Alt);
                    break;
                default:
                    Add(BuyKits.Get<Button>(GameContract.LookRandomize, _ui), null, UiButtonKind.Secondary, UiAction.Count, "intro_tooltip_randomize_appearance");
                    Add(BuyKits.Get<Button>(GameContract.LookRevert, _ui), null, UiButtonKind.Secondary, UiAction.Count, "common_revert");
                    Add(BuyKits.Get<Button>(GameContract.LookApply, _ui), BuyKits.Get<Component>(GameContract.LookApplyLabel, _ui), UiButtonKind.Primary, UiAction.Alt);
                    break;
            }
            if (_m.Actions.Length != _actions.Count)
            {
                _m.Actions = new UiActionModel[_actions.Count];
            }
            _actions.CopyTo(_m.Actions);
            for (int i = 0; i < _actions.Count; i++)
            {
                _sb.Append(_actions[i].Label).Append(_actions[i].Disabled ? '-' : '+');
            }
        }

        // The clothes' Reset, the kit's own button (the game's screen has none): everything tried taken off again.
        private void AddReset()
        {
            if (!Leisure.ClothesScreen.Ready)
            {
                return;   // the mod's clothes are off: the game's screen as it is
            }
            _resetAt = _actions.Count;
            _buttons.Add(null);
            _actions.Add(new UiActionModel
            {
                Label = UiText.T("reallife_ui_buy_reset"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = UiWardrobe.ActionCommand, Arg = _resetAt,
                Disabled = !Leisure.ClothesScreen.CanReset
            });
        }

        private void Add(Button b, Component label, UiButtonKind kind, UiAction key, string fallback = null)
        {
            if (b == null || !b.gameObject.activeSelf)
            {
                return;
            }
            string words = BuyKits.Words(label != null ? label : b) ?? (fallback != null ? UiText.T(fallback) : null);
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            _buttons.Add(b);
            _actions.Add(new UiActionModel { Label = words, Kind = kind, Key = key, Command = UiWardrobe.ActionCommand, Arg = _actions.Count, Disabled = !b.interactable });
        }

        // The game's words for a kind of piece; the kinds the game names nowhere have the mod's own words.
        private static string KindWords(string link)
        {
            string key;
            switch (link)
            {
                case "Hair": key = "character_customization_hair"; break;
                case "Head": key = "character_customization_head"; break;
                case "Torso": key = "character_customization_torso"; break;
                case "Legs": key = "character_customization_legs"; break;
                case "Feet": key = "character_customization_shoes"; break;
                case "Gender": key = "reallife_ui_buy_kind_body"; break;
                case "HairAccessory": key = "reallife_ui_buy_kind_hair_accessory"; break;
                case "HeadAccessory": key = "reallife_ui_buy_kind_head_accessory"; break;
                case "TorsoAccessory": key = "reallife_ui_buy_kind_torso_accessory"; break;
                case "LegsAccessory": key = "reallife_ui_buy_kind_legs_accessory"; break;
                case "FeetAccessory": key = "reallife_ui_buy_kind_feet_accessory"; break;
                case "Eyes": key = "reallife_ui_buy_kind_eyes"; break;
                case "Mouth": key = "reallife_ui_buy_kind_mouth"; break;
                case "Nose": key = "reallife_ui_buy_kind_nose"; break;
                case "Beard": key = "reallife_ui_buy_kind_beard"; break;
                case "Eyebrows": key = "reallife_ui_buy_kind_eyebrows"; break;
                default: return null;
            }
            string words = UiText.T(key);
            return string.IsNullOrEmpty(words) || words == key ? null : words;
        }
    }
}
