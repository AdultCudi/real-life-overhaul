using System;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A character screen as the kit's catalogue panel: tabs down its edge, the pieces as tiles, the colours, the buttons.</summary>
    internal sealed class WardrobeScreen : IUiScreen, IUiStepping
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
                    return arg >= 0 && arg < _pieces.Count && Again(UiWidgets.Press(_pieces[arg].GetComponent<Button>()));
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
            if (action != UiAction.Alt || _actions.Count == 0)
            {
                return false;
            }
            return Press(UiWardrobe.ActionCommand, _actions.Count - 1);   // the main one is the last
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
            UiCatalogueModel g = _m.Grid;
            if (g.Cards.Length < _pieces.Count)
            {
                g.Cards = new UiCatalogueCard[_pieces.Count];
            }
            g.Chosen = -1;
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
                g.Cards[i] = new UiCatalogueCard
                {
                    Picture = BuyPictures.Of(sprite), Aspect = BuyPictures.Aspect(sprite), Icon = UiIcon.Tag, Name = name,
                    Figure = price ? corner : null, State = price ? null : corner, StateTone = corner == owned ? UiRole.Ok : UiRole.Accent
                };
                Transform chosen = p.Find("Selected");
                if (chosen != null && chosen.gameObject.activeSelf)
                {
                    g.Chosen = i;
                    _m.ChosenName = name;
                    _m.ChosenDetail = price ? corner : null;
                }
                _sb.Append(g.Cards[i].Picture).Append(name).Append(corner).Append(';');
            }
            g.CardCount = _pieces.Count;
            _sb.Append(g.Chosen).Append('|');
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
            switch (_kind)
            {
                case WardrobeKind.Clothes:
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
