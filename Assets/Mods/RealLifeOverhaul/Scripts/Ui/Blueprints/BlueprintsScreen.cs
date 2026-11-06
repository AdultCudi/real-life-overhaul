using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Blueprints;
using BlueprintsUI;
using Buildings;
using Helpers;
using Localizor;
using TMPro;
using UI.Load;
using UI.MiniMenu;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pause menu's blueprints panel as the kit's window (UiBlueprints): the game's own list, page, sort, filters and search are
    /// read after the game ran them, and everything the player does is pressed through the game's own buttons, toggles and
    /// fields (a tab, a card, a page, a filter, the sort, add to library, upload or update, remove, the Workshop terms), so its
    /// checks, its Steam fetches and its questions stay the game's. The Workshop has no search of its own in the game, so the
    /// window filters the page that is loaded by name. What the chosen blueprint holds is counted from its layout by the game's
    /// item types, read after the game has it on disk. Cost: a look at the game's panel four times a second (a signature of its
    /// state), a layout when it changed, and one pass over a layout's items once a blueprint is chosen; nothing a frame.
    /// </summary>
    public sealed class BlueprintsScreen : IUiScreen, IUiTyping, IUiGameBack
    {
        private const float Every = 0.25f;
        private const int Gallery = 0, MyLibrary = 1;   // BlueprintCategory
        private const int GroupCount = 4;

        private readonly BlueprintsPanel _panel;
        private readonly MiniMenu _menu;
        private readonly UiBlueprintsModel _m = new UiBlueprintsModel();
        private readonly List<BlueprintElementUI> _elements = new List<BlueprintElementUI>(BlueprintsRules.PageSize);
        private readonly List<Blueprint> _blueprints = new List<Blueprint>(BlueprintsRules.PageSize);
        private readonly List<int> _shown = new List<int>(BlueprintsRules.PageSize);    // the cards the page shows, each an index into _elements
        private readonly List<Button> _tabs = new List<Button>(4);
        private readonly List<Toggle> _sorts = new List<Toggle>(4);
        private readonly List<Button> _actions = new List<Button>(4);
        private readonly List<Toggle[]> _filterToggles = new List<Toggle[]>(GroupCount);
        private readonly List<int> _rowGroup = new List<int>(96), _rowOption = new List<int>(96);
        private readonly UiPrompt[] _prompts = new UiPrompt[3];
        private BlueprintsListUI _list;
        private BlueprintFilterUI _filter;
        private SelectedBlueprintUI _selected;
        private TMP_InputField _search;
        private string _query = string.Empty;   // the Workshop's own search, over the page that is loaded
        private int _category = -1;
        private float _next;
        private long _seen;
        private bool _dirty = true, _sortOpen, _filtersOpen;
        private string _confirmTitle, _confirmBody;

        // What the chosen blueprint holds: its layout read once, off the game's own file, counted by the game's item types.
        private Blueprint _layoutOf;
        private Task<BusinessLayoutSet> _layout;
        private int _layoutState;   // 0 none yet, 1 loading, 2 counted, 3 not to be had
        private int _itemTotal;
        private readonly int[] _kinds = new int[BlueprintsRules.KindCount];
        private static Dictionary<string, int> _floors;

        public BlueprintsScreen(BlueprintsPanel panel, MiniMenu menu)
        {
            _panel = panel;
            _menu = menu;
        }

        public string Id => BlueprintsKits.BlueprintsId;

        /// <summary>The model last laid out, for the tests.</summary>
        public UiBlueprintsModel Model => _m;

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

        // The game's Cancel is taken by the kit while no question stands over it, and it does what the game's own does.
        public bool TakesBack => !UiBoxes.Up;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Read();
            _prompts[0] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
            _prompts[1] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") };
            _prompts[2] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") };
            _m.Prompts = _prompts;
            _m.PromptCount = _prompts.Length;
            UiBlueprints.Lay(s, _m, frame, dark, keys);
            _seen = Signature();
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- what the player does

        public bool Press(int command, int arg)
        {
            if (UiBoxes.Up)
            {
                return false;   // a question of the game's stands over the window: it is the question's key
            }
            _dirty = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return OpenTab(arg);
                case UiBlueprints.SortCommand:
                    _sortOpen = !_sortOpen;
                    _filtersOpen = false;
                    return true;
                case UiBlueprints.SortOptionCommand:
                    _sortOpen = false;
                    return arg >= 0 && arg < _sorts.Count && UiWidgets.Set(_sorts[arg], true);
                case UiBlueprints.FiltersCommand:
                    _filtersOpen = !_filtersOpen;
                    _sortOpen = false;
                    return true;
                case UiBlueprints.FilterRowCommand:
                    return FilterRow(arg);
                case UiBlueprints.CardCommand:
                    return Card(arg);
                case UiBlueprints.ActionCommand:
                    _filtersOpen = _sortOpen = false;
                    return arg >= 0 && arg < _actions.Count && UiWidgets.Press(_actions[arg]);
                case UiBlueprints.PageCommand:
                    return PageBy(arg);
                case UiBlueprints.ClearCommand:
                    Typed(string.Empty);
                    return true;
                case UiBlueprints.ConfirmCommand:
                    return Terms(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        // The game's Cancel, step by step as its own handler takes it: the Workshop terms, then the chosen blueprint, then the window.
        public bool Back()
        {
            _dirty = true;
            if (_sortOpen || _filtersOpen)
            {
                _sortOpen = _filtersOpen = false;
                return true;
            }
            if (_panel == null)
            {
                return false;
            }
            if (_panel.IsWorkshopConfirmOpen)
            {
                _panel.CloseWorkshopConfirm();
            }
            else if (_panel.IsBlueprintInfoOpen)
            {
                _panel.CloseBlueprintInfo();
            }
            else if (_menu != null)
            {
                _menu.CloseBlueprints();
            }
            else
            {
                _panel.Hide();
            }
            return true;
        }

        public void Tab(int step)
        {
            OpenTab(Math.Abs(_m.Tab - 1));
        }

        public bool Act(UiAction action)
        {
            if (UiBoxes.Up || _m.Actions.Length == 0)
            {
                return false;
            }
            int at = action == UiAction.Alt ? 0 : action == UiAction.Option ? 1 : -1;
            for (int i = 0; at >= 0 && i < _m.Actions.Length; i++)
            {
                if (_m.Actions[i].Key == action)
                {
                    _dirty = true;
                    return !_m.Actions[i].Disabled && i < _actions.Count && UiWidgets.Press(_actions[i]);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_m.FilterClip >= 0 && _filtersOpen)
            {
                float top = index * UiTokens.ControlSmall;
                _m.FilterOffset = Math.Min(_m.FilterOffset, top);
                _m.FilterOffset = Math.Max(_m.FilterOffset, top + UiTokens.ControlSmall - UiTokens.ControlSmall * 9f);
            }
            else
            {
                _m.Reveal = UiCatalogue.CardOf(_m.GridColumns, _m.GridList, list, index);
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_filtersOpen && clip == _m.FilterClip)
            {
                _m.FilterOffset = Math.Max(0f, _m.FilterOffset - notches * UiTokens.ControlSmall);
            }
            else
            {
                _m.Offset = Math.Max(0f, _m.Offset - notches * UiTokens.S16);
            }
            _dirty = true;
        }

        private bool OpenTab(int tab)
        {
            // The library's button is the game's second, the Workshop's (its gallery) the first, as the game lists its categories.
            int button = tab == 0 ? 1 : 0;
            _sortOpen = _filtersOpen = false;
            if (_m.Tab == tab)
            {
                return true;
            }
            return button < _tabs.Count && UiWidgets.Press(_tabs[button]);
        }

        private bool Card(int shown)
        {
            if (shown < 0 || shown >= _shown.Count)
            {
                return false;
            }
            BlueprintElementUI element = _elements[_shown[shown]];
            if (element == null)
            {
                return false;
            }
            _sortOpen = _filtersOpen = false;
            using (HotSpots.Game())
            {
                element.ShowElementInfo();   // the card's own click: the game's list opens its detail
            }
            return true;
        }

        private bool PageBy(int step)
        {
            Button b = Member(GameContract.BpListNext, GameContract.BpListPrevious, step) ;
            _sortOpen = _filtersOpen = false;
            return UiWidgets.Press(b);
        }

        private Button Member(GameMember next, GameMember previous, int step) => (step > 0 ? next : previous).Field?.GetValue(_list) as Button;

        private bool FilterRow(int row)
        {
            if (row < 0 || row >= _rowGroup.Count || _rowOption[row] < 0)
            {
                return false;
            }
            int g = _rowGroup[row], o = _rowOption[row];
            Toggle t = g < _filterToggles.Count && o < _filterToggles[g].Length ? _filterToggles[g][o] : null;
            return t != null && UiWidgets.Set(t, !t.isOn);
        }

        private bool Terms(int arg)
        {
            if (_selected == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                switch (arg)
                {
                    case 0:
                        _selected.UploadToSteamWorkshop();
                        break;
                    case 1:
                        _panel.CloseWorkshopConfirm();
                        break;
                    default:
                        _selected.ReadWorkshopTermsOfService();
                        return true;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => UiBlueprints.SearchCommand;

        public string TypingText => _category == MyLibrary && _search != null ? _search.text ?? string.Empty : _query;

        public void Typed(string text)
        {
            text = text ?? string.Empty;
            _dirty = true;
            if (_category == MyLibrary && _search != null)
            {
                UiWidgets.Type(_search, text, false);   // the game's own search: it reloads the library after its short wait
                return;
            }
            _query = text;
            _m.Offset = 0f;
        }

        // ------------------------------------------------------------------------------------------------- reading the game's panel

        private bool Wire()
        {
            if (_list != null)
            {
                return true;
            }
            _list = GameContract.BpPanelList.Field?.GetValue(_panel) as BlueprintsListUI;
            _filter = _list != null ? GameContract.BpListFilter.Field?.GetValue(_list) as BlueprintFilterUI : null;
            _selected = _list != null ? GameContract.BpListSelected.Field?.GetValue(_list) as SelectedBlueprintUI : null;
            _search = _filter != null ? GameContract.BpFilterSearch.Field?.GetValue(_filter) as TMP_InputField : null;
            return _list != null && _filter != null && _selected != null;
        }

        private int Category()
        {
            object value = GameContract.BpListCategory.Field?.GetValue(null);
            return value != null ? (int)value : -1;
        }

        // The game's page as it stands: its cards, its page, what is chosen and what the chosen can do, said as one number.
        private long Signature()
        {
            if (!Wire())
            {
                return -1;
            }
            Poll();
            int category = Category();
            if (category != _category)
            {
                _category = category;
                _query = string.Empty;   // a tab starts with its own search, as the game's panel does
                _sortOpen = _filtersOpen = false;
            }
            long h = category;
            h = h * 31 + (_panel.IsWorkshopConfirmOpen ? 1 : 0) + (_panel.IsBlueprintInfoOpen ? 2 : 0) + (LoadingSpinner.isLoading ? 4 : 0);
            h = h * 31 + Member(GameContract.BpListNext, GameContract.BpListPrevious, 1).Active() * 3 + Member(GameContract.BpListNext, GameContract.BpListPrevious, -1).Active();
            h = h * 31 + (int)(GameContract.BpListPage.Field?.GetValue(_list) ?? 0) * 7 + (int)(GameContract.BpListTotal.Field?.GetValue(_list) ?? 0);
            Transform template = GameContract.BpListTemplate.Field?.GetValue(_list) as Transform;
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == template || !c.gameObject.activeSelf)
                {
                    continue;
                }
                var e = c.GetComponent<BlueprintElementUI>();
                var bp = e != null ? GameContract.BpElementBlueprint.Field?.GetValue(e) as Blueprint : null;
                var image = e != null ? GameContract.BpElementPicture.Field?.GetValue(e) as Image : null;
                h = h * 31 + (bp != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(bp) : 0);
                h = h * 31 + (image != null && image.sprite != null ? image.sprite.GetInstanceID() : 0);
            }
            Blueprint chosen = Chosen();
            h = h * 31 + (chosen != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(chosen) : 0) * 5 + _layoutState;
            var author = GameContract.BpSelectedAuthor.Field?.GetValue(_selected) as Localizor.LanguageChangeEvent.TextLocalizationComponent;
            h = h * 31 + (author != null && author.TextContainer != null ? author.TextContainer.text.GetHashCode() : 0);
            h = h * 31 + SelectedButton(GameContract.BpSelectedAdd).Active() + SelectedButton(GameContract.BpSelectedUpload).Active() * 3
                + SelectedButton(GameContract.BpSelectedRemoveLibrary).Active() * 9
                + SelectedButton(GameContract.BpSelectedRemoveWorkshop).Active() * 27;
            h = h * 31 + (_search != null ? (_search.text ?? string.Empty).GetHashCode() : 0);
            BlueprintSortInfo info = _filter.SortInfo;
            h = h * 31 + (info != null ? (int)info.SortByOption * 11 + FilterHash(info.BuildingTypeFilter) + FilterHash(info.BuildingSizeFilter) * 3 + FilterHash(info.BusinessTypeFilter) * 5
                + FilterHash(info.BuildVersionFilter) * 7 : 0);
            var cache = LibraryPages();
            h = h * 31 + (cache != null ? cache.Count : -1) * 13 + SubscribedCount();
            return h;
        }

        private static int FilterHash(BlueprintFilter f)
        {
            if (f == null || f.filterOptions == null)
            {
                return 0;
            }
            int h = f.filterOptions.Count;
            for (int i = 0; i < f.filterOptions.Count; i++)
            {
                h = h * 3 + (f.filterOptions[i].toggled ? 1 : 0);
            }
            return h;
        }

        private Button SelectedButton(GameMember member) => member.Field?.GetValue(_selected) as Button;

        private static Dictionary<int, List<Blueprint>> LibraryPages()
        {
            BlueprintController library = BlueprintsListUI.LibraryController;
            return library != null ? GameContract.BpLibraryCache.Field?.GetValue(library) as Dictionary<int, List<Blueprint>> : null;
        }

        private static int SubscribedCount() => BlueprintsListUI.LibraryController != null ? BlueprintsListUI.LibraryController.workshopBlueprintsBySteamId.Count : -1;

        // The blueprint the game's detail shows now: none while its panel is closed.
        private Blueprint Chosen() => _selected != null && _selected.IsOpen ? GameContract.BpSelectedBlueprint.Field?.GetValue(_selected) as Blueprint : null;

        // The layout of the chosen blueprint, started once and counted when the game's own file read is done.
        private void Poll()
        {
            Blueprint chosen = Chosen();
            if (chosen != _layoutOf)
            {
                _layoutOf = chosen;
                _layout = null;
                _layoutState = 0;
                if (chosen != null && Local(chosen))
                {
                    try
                    {
                        _layout = chosen.GetLayout(false);
                        _layoutState = 1;
                    }
                    catch (Exception e)
                    {
                        UiDirector.Logger?.Warn("UI: the blueprints window could not read a layout (" + e.GetType().Name + ")");
                        _layoutState = 3;
                    }
                }
            }
            if (_layoutState == 1 && _layout != null && _layout.IsCompleted)
            {
                Count();
            }
        }

        // The layout's items counted by kind; a blueprint whose file could not be read says so rather than showing zeros.
        private void Count()
        {
            _itemTotal = 0;
            Array.Clear(_kinds, 0, _kinds.Length);
            BusinessLayoutSet set = !_layout.IsFaulted && !_layout.IsCanceled ? _layout.Result : null;
            _layout = null;
            if (set == null || set.Items == null)
            {
                _layoutState = 3;
                return;
            }
            for (int i = 0; i < set.Items.Count; i++)
            {
                BusinessLayoutSets.Item item = set.Items[i];
                BigAmbitions.Items.Item data = BigAmbitions.Items.ItemsGetter.GetByName(item.itemName, true);
                int kind = data != null ? BlueprintsRules.KindOf((long)data.type) : -1;
                _itemTotal++;
                if (kind >= 0)
                {
                    _kinds[kind]++;
                }
            }
            _layoutState = 2;
        }

        // A blueprint with its own files on this PC: only these have a layout to read.
        private static bool Local(Blueprint b)
        {
            BlueprintType t = b.metadata.blueprintType;
            return t == BlueprintType.SavedLocally || t == BlueprintType.SavedFromWorkshop || t == BlueprintType.UploadedToWorkshop;
        }

        // ------------------------------------------------------------------------------------------------- the model

        private void Read()
        {
            if (!Wire())
            {
                return;
            }
            _category = Category();
            Tabs();
            Cards();
            Toolbar();
            Page();
            Detail();
            _m.Title = UiText.T("common_blueprints");
            _m.Search = string.IsNullOrEmpty(TypingText) ? null : TypingText;
            _m.SearchPlaceholder = UiText.T("common_search");
            _m.SortOpen = _sortOpen;
            _m.FiltersOpen = _filtersOpen;
            _m.Confirm = _panel.IsWorkshopConfirmOpen;
            if (_m.Confirm)
            {
                ConfirmCard();
            }
            if (_m.Offset < 0f)
            {
                _m.Offset = 0f;
            }
        }

        private void Tabs()
        {
            _tabs.Clear();
            Transform template = GameContract.BpPanelTabs.Field?.GetValue(_panel) as Transform;
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                var b = c != template && c.gameObject.activeSelf ? c.GetComponent<Button>() : null;
                if (b != null)
                {
                    _tabs.Add(b);
                }
            }
            int libraryCount = 0;
            var pages = LibraryPages();
            bool known = pages != null && pages.Count > 0;
            if (known)
            {
                foreach (List<Blueprint> page in pages.Values)
                {
                    libraryCount += page.Count;
                }
            }
            int subscribed = SubscribedCount();
            string library = _tabs.Count > 1 ? UiWidgets.Label(_tabs[1]) : null, workshop = _tabs.Count > 0 ? UiWidgets.Label(_tabs[0]) : null;
            _m.Tabs = new[]
            {
                new UiNavEntry { Icon = UiIcon.Books, Label = library ?? UiText.T("reallife_ui_bp_library"), Count = known ? BlueprintsRules.Count(libraryCount) : null },
                new UiNavEntry { Icon = UiIcon.People, Label = workshop ?? UiText.T("reallife_ui_bp_workshop"), Count = subscribed > 0 ? BlueprintsRules.Count(subscribed) : null }
            };
            _m.Tab = _category == MyLibrary ? 0 : 1;
        }

        private void Cards()
        {
            _elements.Clear();
            _blueprints.Clear();
            _shown.Clear();
            Transform template = GameContract.BpListTemplate.Field?.GetValue(_list) as Transform;
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == template || !c.gameObject.activeSelf)
                {
                    continue;
                }
                var e = c.GetComponent<BlueprintElementUI>();
                var bp = e != null ? GameContract.BpElementBlueprint.Field?.GetValue(e) as Blueprint : null;
                if (bp != null)
                {
                    _elements.Add(e);
                    _blueprints.Add(bp);
                }
            }
            bool local = _category == Gallery && !string.IsNullOrWhiteSpace(_query);
            for (int i = 0; i < _elements.Count; i++)
            {
                if (!local || BlueprintsRules.Matches(_blueprints[i].name, _query))
                {
                    _shown.Add(i);
                }
            }
            Blueprint chosen = Chosen();
            _m.CardCount = _shown.Count;
            if (_m.Cards.Length < _shown.Count)
            {
                _m.Cards = new UiBlueprintCard[Math.Max(_shown.Count, BlueprintsRules.PageSize)];
            }
            _m.Chosen = -1;
            for (int i = 0; i < _shown.Count; i++)
            {
                int at = _shown[i];
                _m.Cards[i] = CardOf(_blueprints[at], _elements[at]);
                if (chosen != null && ReferenceEquals(chosen, _blueprints[at]))
                {
                    _m.Chosen = i;
                }
            }
            _m.SearchCount = local ? BlueprintsRules.CountLine(UiText.Words, _shown.Count, _elements.Count) : null;
        }

        private UiBlueprintCard CardOf(Blueprint b, BlueprintElementUI element)
        {
            var image = GameContract.BpElementPicture.Field?.GetValue(element) as Image;
            BuildingSizeInfo size = b.metadata.buildingSizeInfo;
            var card = new UiBlueprintCard { Name = b.name, Picture = image != null ? WorldPictures.Of(image.sprite) : null, Size = size != null ? size.ToString() : null };
            Look(b, ref card.State, ref card.StateTone);
            bool rated = BlueprintsRules.Rated(b.metadata.blueprintType != BlueprintType.SavedLocally, b.rating);
            card.Halves = rated ? BlueprintsRules.Halves(b.rating) : -1;
            card.Rating = rated ? BlueprintsRules.RatingFigure(b.rating) : null;
            card.Author = Author(b);
            card.Old = GameVersion.IsBuildFromOlderVersionGroup(b.metadata.buildNumber);
            return card;
        }

        // "by name" for what other players made; the player's own and the unnamed carry none on the card (their chip says whose).
        private static string Author(Blueprint b)
        {
            BlueprintType t = b.metadata.blueprintType;
            if (t == BlueprintType.SavedLocally || t == BlueprintType.UploadedToWorkshop || string.IsNullOrEmpty(b.author) || b.author == "common_unknown")
            {
                return null;
            }
            return UiText.Fill("reallife_ui_bp_by", "author", b.author);
        }

        // The chip over the picture: whose it is and where it lives, from the game's own types.
        private static void Look(Blueprint b, ref string state, ref UiRole tone)
        {
            switch (b.metadata.blueprintType)
            {
                case BlueprintType.SavedLocally:
                    state = UiText.T("reallife_ui_bp_yours");
                    tone = UiRole.Accent;
                    break;
                case BlueprintType.UploadedToWorkshop:
                    state = UiText.T("reallife_ui_bp_shared");
                    tone = UiRole.Ok;
                    break;
                case BlueprintType.SavedFromWorkshop:
                    state = UiText.T("reallife_ui_bp_subscribed");
                    tone = UiRole.Notice;
                    break;
                case BlueprintType.Workshop:
                    bool have = BlueprintsFolderLoader.IsWorkshopBlueprintInLibrary(b.metadata.itemId);
                    state = have ? UiText.T("reallife_ui_bp_in_library") : null;
                    tone = UiRole.Ok;
                    break;
                default:
                    state = null;
                    break;
            }
        }

        // The sort the game offers and the filters it holds, as the game's own widgets stand now.
        private void Toolbar()
        {
            _sorts.Clear();
            Transform sortTemplate = GameContract.BpFilterSorts.Field?.GetValue(_filter) as Transform;
            Transform sortParent = sortTemplate != null ? sortTemplate.parent : null;
            var options = new List<string>(3);
            int chosen = 0;
            for (int i = 0; sortParent != null && i < sortParent.childCount; i++)
            {
                Transform c = sortParent.GetChild(i);
                var t = c != sortTemplate && c.gameObject.activeSelf ? c.GetComponent<Toggle>() : null;
                if (t != null)
                {
                    chosen = t.isOn ? _sorts.Count : chosen;
                    _sorts.Add(t);
                    options.Add(UiWidgets.Label(t) ?? string.Empty);
                }
            }
            _m.SortOptions = options.ToArray();
            _m.SortChosen = chosen;
            _m.SortLabel = UiText.Fill("reallife_ui_bp_sort", "sort", chosen < options.Count ? options[chosen] : string.Empty);
            Filters();
        }

        private void Filters()
        {
            _filterToggles.Clear();
            _rowGroup.Clear();
            _rowOption.Clear();
            Transform template = GameContract.BpFilterTemplate.Field?.GetValue(_filter) as Transform;
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                Transform options = c != template && c.gameObject.activeSelf ? c.Find("Options") : null;
                if (options != null)
                {
                    _filterToggles.Add(options.GetComponentsInChildren<Toggle>(false));
                }
            }
            BlueprintSortInfo info = _filter.SortInfo;
            BlueprintFilter[] groups = info != null ? new[] { info.BuildingTypeFilter, info.BuildingSizeFilter, info.BusinessTypeFilter, info.BuildVersionFilter } : new BlueprintFilter[0];
            var rows = new List<UiBlueprintFilterRow>(96);
            int on = 0;
            for (int g = 0; g < groups.Length && g < _filterToggles.Count; g++)
            {
                BlueprintFilter f = groups[g];
                if (f == null || f.filterOptions == null || f.filterOptions.Count < 2)
                {
                    continue;
                }
                rows.Add(new UiBlueprintFilterRow { Label = f.localizeLabel && f.localizationKey != null ? UiText.T(f.localizationKey) : f.label ?? string.Empty, Group = true });
                _rowGroup.Add(g);
                _rowOption.Add(-1);
                on += f.allFilterToggle != null && !f.allFilterToggle.isOn ? 1 : 0;
                for (int o = 0; o < f.filterOptions.Count && o < _filterToggles[g].Length; o++)
                {
                    BlueprintFilterOption option = f.filterOptions[o];
                    rows.Add(new UiBlueprintFilterRow { Label = option.localizeText ? UiText.T(option.text) : option.text, On = _filterToggles[g][o].isOn });
                    _rowGroup.Add(g);
                    _rowOption.Add(o);
                }
            }
            _m.FilterRows = rows.ToArray();
            _m.FiltersOn = on;
            _m.FiltersLabel = on > 0 ? UiText.Fill("reallife_ui_bp_filters_on", "n", on.ToString(CultureInfo.InvariantCulture)) : UiText.T("reallife_ui_bp_filters");
        }

        private void Page()
        {
            int page = (int)(GameContract.BpListPage.Field?.GetValue(_list) ?? 1), total = (int)(GameContract.BpListTotal.Field?.GetValue(_list) ?? 0);
            _m.PageLine = BlueprintsRules.PageLine(UiText.Words, page, total);
            _m.PrevLabel = UiText.T("reallife_ui_bp_prev");
            _m.NextLabel = UiText.T("reallife_ui_bp_next");
            _m.CanPrev = Member(GameContract.BpListNext, GameContract.BpListPrevious, -1).Active() > 0;
            _m.CanNext = Member(GameContract.BpListNext, GameContract.BpListPrevious, 1).Active() > 0;
            _m.Loading = LoadingSpinner.isLoading && _elements.Count == 0;
            _m.LoadingText = UiText.T("reallife_ui_bp_loading");
            var none = GameContract.BpListNoResults.Field?.GetValue(_list) as Localizor.LanguageChangeEvent.TextLocalizationComponent;
            bool searching = !string.IsNullOrEmpty(TypingText);
            _m.EmptyTitle = UiText.T(searching ? "reallife_ui_bp_none_found" : "reallife_ui_bp_none");
            _m.EmptyBody = none != null && none.gameObject.activeSelf && !string.IsNullOrEmpty(none.Key) ? UiText.T(none.Key) : UiText.T("reallife_ui_bp_none_body");
            if (_shown.Count == 0 && _elements.Count > 0 && _category == Gallery)
            {
                _m.EmptyBody = UiText.T("reallife_ui_bp_none_found_body");   // the search holds back every card of the page that is loaded
            }
            _m.EmptyAction = searching ? UiText.T("reallife_ui_bp_clear") : null;
        }

        private void Detail()
        {
            Blueprint b = Chosen();
            _m.HasDetail = b != null;
            _m.DetailEmpty = UiText.T("reallife_ui_bp_pick");
            _actions.Clear();
            _m.Actions = new UiActionModel[0];
            if (b == null)
            {
                return;
            }
            _m.DetailName = b.name;
            _m.DetailPicture = _m.Chosen >= 0 ? _m.Cards[_m.Chosen].Picture : null;
            Look(b, ref _m.DetailState, ref _m.DetailStateTone);
            string kind = b.metadata.buildingType;
            _m.DetailKicker = string.IsNullOrEmpty(kind) ? null : kind.GetLocalization();
            var author = GameContract.BpSelectedAuthor.Field?.GetValue(_selected) as Localizor.LanguageChangeEvent.TextLocalizationComponent;
            _m.DetailAuthor = author != null ? UiWidgets.Text(author.TextContainer) : null;
            bool rated = BlueprintsRules.Rated(b.metadata.blueprintType != BlueprintType.SavedLocally, b.rating);
            _m.DetailHalves = rated ? BlueprintsRules.Halves(b.rating) : -1;
            _m.DetailRating = rated ? BlueprintsRules.RatingFigure(b.rating) : null;
            Facts(b);
            Inside(b);
            var warnings = new List<string>(2);
            if (GameVersion.IsBuildFromOlderVersionGroup(b.metadata.buildNumber))
            {
                warnings.Add(UiText.Fill("reallife_ui_bp_older", "version", GameVersion.GetVersionString(b.metadata.buildNumber, true)));
            }
            if (b.IsMissingMods())
            {
                warnings.Add(UiText.T("reallife_ui_bp_mods"));
            }
            _m.Warnings = warnings.ToArray();
            Actions();
        }

        private void Facts(Blueprint b)
        {
            var labels = new List<string>(8);
            var values = new List<string>(8);
            BuildingSizeInfo size = b.metadata.buildingSizeInfo;
            if (size != null)
            {
                labels.Add(UiText.T("common_building_size"));
                values.Add(BlueprintsRules.SizeLine(UiText.Words, size.ToString(), FloorsOf(size.buildingSize)));
            }
            labels.Add(UiText.T("reallife_ui_bp_cost"));
            values.Add(UiText.Money(b.metadata.FullPriceWithInstallation));
            if (b.metadata.blueprintType != BlueprintType.SavedLocally && b.downloads > 0f)
            {
                labels.Add(UiText.T("reallife_ui_bp_subscribers"));
                values.Add(BlueprintsRules.Subscribers(b.downloads));
            }
            if (b.metadata.blueprintType != BlueprintType.SavedLocally && b.releaseDate > DateTime.MinValue)
            {
                labels.Add(UiText.T("reallife_ui_bp_released"));
                values.Add(b.releaseDate.ToShortDateString());
            }
            for (int i = 0; b.metadata.otherData != null && i < b.metadata.otherData.Count; i++)
            {
                BlueprintDataElement d = b.metadata.otherData[i];
                labels.Add(UiText.T(d.dataElement.GetLocalizeKey()));
                values.Add(d.dataElement == DataElement.BusinessTypeName ? d.value.GetLocalization() : d.value);
            }
            if (_layoutState == 2)
            {
                labels.Add(UiText.T("reallife_ui_bp_items"));
                values.Add(UiText.Whole(_itemTotal));
            }
            _m.FactsTitle = UiText.T("reallife_ui_bp_facts");
            _m.FactLabels = labels.ToArray();
            _m.FactValues = values.ToArray();
        }

        // What the layout holds: a line a kind that is there, or the sentence that says why there is no list.
        private void Inside(Blueprint b)
        {
            var labels = new List<string>(BlueprintsRules.KindCount);
            var values = new List<string>(BlueprintsRules.KindCount);
            if (_layoutState == 2)
            {
                for (int k = 0; k < BlueprintsRules.KindCount; k++)
                {
                    if (_kinds[k] > 0)
                    {
                        labels.Add(UiText.T(BlueprintsRules.KindKeys[k]));
                        values.Add(UiText.Whole(_kinds[k]));
                    }
                }
            }
            _m.InsideTitle = UiText.T("reallife_ui_bp_inside");
            _m.InsideLabels = labels.ToArray();
            _m.InsideValues = values.ToArray();
            string note = null;
            if (_layoutState == 1)
            {
                note = UiText.T("reallife_ui_bp_counting");
            }
            else if (!Local(b))
            {
                note = UiText.T("reallife_ui_bp_not_here");
            }
            else if (_layoutState == 3)
            {
                note = UiText.T("reallife_ui_bp_no_list");
            }
            else if (labels.Count == 0)
            {
                note = UiText.T("reallife_ui_bp_nothing");
            }
            _m.InsideNote = note;
        }

        // The game's own buttons for it, in the order it lays them out; the first that adds or uploads is the main action, the next the second.
        private void Actions()
        {
            var actions = new List<UiActionModel>(4);
            int main = 0;
            Add(actions, GameContract.BpSelectedAdd, UiIcon.Download, ref main, false);
            Button upload = SelectedButton(GameContract.BpSelectedUpload);
            Add(actions, GameContract.BpSelectedUpload, UiIcon.Upload, ref main, false);
            Add(actions, GameContract.BpSelectedRemoveLibrary, UiIcon.Trash, ref main, true);
            Add(actions, GameContract.BpSelectedRemoveWorkshop, UiIcon.Trash, ref main, true);
            _m.Actions = actions.ToArray();
            _m.ActionNote = upload != null && upload.gameObject.activeSelf && !upload.interactable ? UiText.T("reallife_ui_bp_upload_held") : null;
        }

        private void Add(List<UiActionModel> into, GameMember member, UiIcon icon, ref int main, bool remove)
        {
            Button b = SelectedButton(member);
            if (b == null || !b.gameObject.activeSelf)
            {
                return;
            }
            UiAction key = remove ? UiAction.Count : main == 0 ? UiAction.Alt : main == 1 ? UiAction.Option : UiAction.Count;
            main += remove ? 0 : 1;
            _actions.Add(b);
            into.Add(new UiActionModel
            {
                Label = UiWidgets.Label(b) ?? string.Empty, Icon = icon, Key = key, Command = UiBlueprints.ActionCommand, Arg = into.Count, Disabled = !b.interactable,
                Kind = remove ? UiButtonKind.Secondary : main == 1 ? UiButtonKind.Primary : UiButtonKind.Secondary
            });
        }

        // The Workshop's terms before an upload, as the game words them where its card has words, else in the mod's own.
        private void ConfirmCard()
        {
            GameObject card = GameContract.BpListConfirm.Field?.GetValue(_list) as GameObject;
            string title = null, body = null;
            if (card != null)
            {
                foreach (TMP_Text t in card.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (string.IsNullOrWhiteSpace(t.text) || t.GetComponentInParent<UnityEngine.UI.Button>() != null)
                    {
                        continue;
                    }
                    if (title == null)
                    {
                        title = UiWidgets.Text(t);
                    }
                    else if (body == null)
                    {
                        body = UiWidgets.Text(t);
                    }
                }
            }
            _confirmTitle = title ?? UiText.T("reallife_ui_bp_terms_title");
            _confirmBody = body ?? UiText.T("reallife_ui_bp_terms_body");
            _m.ConfirmTitle = _confirmTitle;
            _m.ConfirmBody = _confirmBody;
            _m.ConfirmYes = UiText.T("reallife_ui_bp_terms_yes");
            _m.ConfirmNo = UiText.T("reallife_ui_bp_terms_no");
            _m.ConfirmTerms = UiText.T("reallife_ui_bp_terms_read");
        }

        private static int FloorsOf(string buildingSize)
        {
            if (_floors == null)
            {
                _floors = new Dictionary<string, int>(32);
                foreach (BuildingSizeData d in BuildingSizeHelper.GetAllBuildingSizes())
                {
                    _floors[d.buildingSize] = d.wallHeights != null ? d.wallHeights.Length : 1;
                }
            }
            return buildingSize != null && _floors.TryGetValue(buildingSize, out int n) ? n : 1;
        }
    }

    /// <summary>A piece of the screen's signature: whether a game button is there to press.</summary>
    internal static class BlueprintsReads
    {
        /// <summary>1 when a button is there to press, else 0: a signature's piece.</summary>
        public static int Active(this Button b) => b != null && b.gameObject.activeSelf && b.interactable ? 1 : 0;
    }
}
