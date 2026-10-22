using System;
using System.Collections.Generic;
using System.Diagnostics;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app's page: the game's help screen (F1) in the mod's dark colours (GameUi.HelpFrame). The groups of the
    /// options page are the categories of its list on the left and their sections its pages; a page shows each option's
    /// name, a short description under it and, at the right, the game's own control of its kind (GameUi.OptionRows): a
    /// switch, a slider with its number, the game's drop-down, a button for what is done rather than set, the key itself
    /// for a key. Every value goes through OptionsLayout's rows (OptionsLayout.Book.cs), saved under the key the Mods tab
    /// saves it under. Built when the app is first opened, a page written when it is shown, a row when it changes; nothing per frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private const int ModePage = 0, ModeFound = 1, ModeDiffer = 2;
        private const int MostHistory = 40;     // pages the back button remembers
        private const float ScrollMargin = 24f; // above a row scrolled to, so that it does not stand on the page's edge

        /// <summary>The pictures the app's pages ask for, named before the one look through the game's sprites (GameSprites).</summary>
        internal static readonly string[] Glyphs =
        {
            "Icon-Info", "Icon-PersonManage", "Icon-Building", "Icon-Route", "Icon-OpeningHours", "Icon-Coin", "Icon-Shop", "Icon-Edit", "Icon-Condition", "Icon-Security",
            "Icon-Notifications"
        };

        private HelpFrame _frame;
        private OptionRows _kit;
        private List<SettingsPage> _pages;
        private HelpGroup[] _groups;            // one per category: the groups, then the test tools
        private bool[] _open;                   // which categories the player has open
        private int[] _perCategory;             // pages of each category the list shows now
        private HelpLink[] _links;              // one per page
        private int _page = -1;                 // the page chosen in the list
        private int _chosen = -1;               // the link drawn as chosen
        private int _mode;
        private readonly List<int> _history = new List<int>(MostHistory);
        private int _at = -1;
        private string _want;
        private bool _ready, _writing, _wrote;
        private Action<KeyAction, bool> _keyShown;

        /// <summary>The app's panel, asleep: the help screen's copy centred in it, with the list built. Null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            _pages = OptionsLayout.AppPages;
            if (tabs == null || _pages == null)
            {
                return null;
            }
            var root = new GameObject(SettingsApp.AppId, typeof(RectTransform));
            root.transform.SetParent(dormant, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            Transform templates = UiClone.Dormant(root.transform, "Templates");
            _frame = HelpFrame.Make(templates, "Settings Window");
            _kit = _frame != null ? OptionRows.Take(templates) : null;
            if (_frame == null || _kit == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                _frame = null;
                return null;
            }
            _frame.Root.transform.SetParent(root.transform, false);   // the window keeps the help's size, at the middle of the menu
            UiClone.Key(_frame.Headline, "reallife_set_app_title");
            UiClone.Words(_frame.Status, string.Empty);
            UiClone.Rewire(_frame.Close, () => menu.CloseFullMenu());
            UiClone.Rewire(_frame.Back, () => Step(-1));
            UiClone.Rewire(_frame.Forward, () => Step(1));
            TakeSearch();
            BuildList(tabs);
            _keyShown = (action, waiting) => UiClone.Safe(() => KeyShown(action, waiting));
            return root;
        }

        // The list on the left: a category per group and one for the test tools, a link per page, all made once.
        private void BuildList(SettingsTab[] tabs)
        {
            int categories = SettingsRules.TestsCategory(tabs) + 1;
            _groups = new HelpGroup[categories];
            _open = new bool[categories];
            _perCategory = new int[categories];
            for (int c = 0; c < categories; c++)
            {
                bool tests = c == categories - 1;
                HelpGroup group = _frame.AddGroup(tests ? "Tests" : tabs[c].Group.Id);
                UiClone.Key(group.Name, tests ? "reallife_meta_title_tests" : tabs[c].Group.TitleKey);
                int category = c;
                UiClone.Rewire(group.Button, () => Fold(category));
                _groups[c] = group;
            }
            _links = new HelpLink[_pages.Count];
            for (int i = 0; i < _pages.Count; i++)
            {
                SettingsPage page = _pages[i];
                HelpLink link = _groups[page.Category].Add("Page " + i);
                if (page.IsTests || page.Heading.Section == null)
                {
                    UiClone.Words(link.Name, OptionsLayout.AppLabel(page.Heading));
                }
                else
                {
                    UiClone.Key(link.Name, page.Heading.Section.TitleKey);
                }
                int index = i;
                UiClone.Rewire(link.Button, () => Pick(index));
                _links[i] = link;
            }
        }

        private bool Showing => _frame != null && _frame.Root != null && _frame.Root.activeInHierarchy;

        public void Opened()
        {
            if (_frame == null)
            {
                return;
            }
            _ready = true;
            _textsMade = false;   // the language or the rows shown may have changed while it was closed
            Keys.RowShown = _keyShown;
            if (_want != null)
            {
                GoTo(_want);
                _want = null;
                return;
            }
            if (_page < 0)
            {
                Pick(FirstShown(), true);
                return;
            }
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
            if (Keys.RowShown == _keyShown)
            {
                Keys.RowShown = null;
            }
            CloseChoices();
            SearchClosed();
            if (_wrote)
            {
                _wrote = false;
                UiClone.Safe(UnityEngine.PlayerPrefs.Save);   // on disk now, not only when the game quits: a crash later keeps the change
            }
        }

        /// <summary>The section ("s:id") or option (its id) shown when the app next opens; null keeps what was shown.</summary>
        public void Want(string target)
        {
            _want = target;
            if (Showing && _ready && target != null)
            {
                GoTo(target);
                _want = null;
            }
        }

        /// <summary>The options were read again (a preset, an import, a reset): every row shown is written again if the page is showing.</summary>
        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                RefillAll();
            }
        }

        // A section or an option asked for from elsewhere: its page, scrolled to the option.
        private void GoTo(string target)
        {
            bool section = target.StartsWith("s:", StringComparison.Ordinal);
            SettingsLine line = section ? OptionsLayout.AppHeading(target.Substring(2)) : OptionsLayout.AppFind(target);
            int page = line != null ? PageOf(line) : -1;
            if (page < 0)
            {
                MetaLog.Warn("the Settings app was asked to open on '" + target + "', which the options page does not have.");
                if (_page < 0)
                {
                    Pick(FirstShown(), true);
                }
                else
                {
                    Refresh();
                }
                return;
            }
            ClearSearch();
            Pick(page, true);
            if (!section)
            {
                ScrollTo(line);
            }
        }

        private int PageOf(SettingsLine line)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                SettingsPage page = _pages[i];
                if (page.Heading == line || page.Lines.Contains(line))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FirstShown()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                if (PageShown(_pages[i]))
                {
                    return i;
                }
            }
            return 0;
        }

        // A link clicked: its page, remembered for the back button. While a search is on, the search is left first.
        private void Pick(int index)
        {
            ClearSearch();
            Pick(index, true);
        }

        private void Pick(int index, bool remember)
        {
            if (index < 0 || index >= _pages.Count)
            {
                return;
            }
            _page = index;
            _mode = ModePage;
            _open[_pages[index].Category] = true;
            if (remember)
            {
                Remember(index);
            }
            Refresh();
        }

        private void Remember(int index)
        {
            if (_at >= 0 && _at < _history.Count && _history[_at] == index)
            {
                return;
            }
            if (_at < _history.Count - 1)
            {
                _history.RemoveRange(_at + 1, _history.Count - _at - 1);
            }
            if (_history.Count == MostHistory)
            {
                _history.RemoveAt(0);
            }
            _history.Add(index);
            _at = _history.Count - 1;
        }

        // The round buttons at the top left: back and forward through the pages shown, as the help's own (HistoryBack, HistoryForward).
        private void Step(int by)
        {
            if (_mode == ModeFound)
            {
                ClearSearch();
                Refresh();
                return;
            }
            int to = _mode == ModeDiffer && by < 0 ? _at : _at + by;   // the differences are no page of the list: back goes to the page they came from
            if (to < 0 || to >= _history.Count)
            {
                return;
            }
            _at = to;
            Pick(_history[to], false);
        }

        // A category's button: it opens and closes as the help's does.
        private void Fold(int category)
        {
            _open[category] = !_open[category];
            _groups[category].SetOpen(_open[category] || _mode == ModeFound);
        }

        // Which categories and pages the list shows: a page with no row shown is left out, the test tools only while asked for, and during a search only pages with a match.
        private void ShowList()
        {
            Array.Clear(_perCategory, 0, _perCategory.Length);
            for (int i = 0; i < _pages.Count; i++)
            {
                SettingsPage page = _pages[i];
                bool shown = PageShown(page) && (_mode != ModeFound || _hits[i]);
                _links[i].Show(shown);
                _perCategory[page.Category] += shown ? 1 : 0;
            }
            for (int c = 0; c < _groups.Length; c++)
            {
                _groups[c].Show(_perCategory[c] > 0);
                _groups[c].SetOpen(_mode == ModeFound || _open[c]);
            }
            int chosen = _mode == ModePage ? _page : -1;
            if (chosen != _chosen)
            {
                if (_chosen >= 0 && _chosen < _links.Length)
                {
                    _links[_chosen].SetChosen(false);
                }
                if (chosen >= 0)
                {
                    _links[chosen].SetChosen(true);
                }
                _chosen = chosen;
            }
            _frame.Back.interactable = _mode != ModePage || _at > 0;
            _frame.Forward.interactable = _mode == ModePage && _at < _history.Count - 1;
        }

        private static bool PageShown(SettingsPage page)
        {
            if (page.IsTests && !Settings.ShowTestTools)
            {
                return false;
            }
            for (int i = 0; i < page.Lines.Count; i++)
            {
                if (OptionsLayout.AppShown(page.Lines[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // The whole of what shows: the list, the top bar's status and the page in its mode.
        private void Refresh()
        {
            if (_frame == null || !_ready || _writing)
            {
                return;
            }
            _writing = true;
            long started = Stopwatch.GetTimestamp();
            try
            {
                ClearPage();
                Compare();
                WriteStatus();
                ShowList();
                if (_mode == ModeFound)
                {
                    WriteFound();
                }
                else if (_mode == ModeDiffer)
                {
                    WriteDiffer();
                }
                else
                {
                    WritePage(_pages[_page]);
                }
                ScrollTop();
                if (Dev.Verbose)
                {
                    double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                    string what = _mode == ModeFound ? "search '" + _query + "'" : _mode == ModeDiffer ? "differences" : "page '" + OptionsLayout.AppLabel(_pages[_page].Heading) + "'";
                    MetaLog.Info("Meta check (settings app): " + what + ", " + CountShown() + ", written in " + ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                 + " ms (wanted under 20): " + (ms < 20.0 ? "PASS" : "SLOW") + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // The header over a page: the group's picture in the game's blue square, its name in small capitals, the page's name and a note at the right.
        private void Topic(int category, string group, string name, string note)
        {
            if (_frame.TopicGlyph != null)
            {
                Sprite glyph = GameSprites.Get(GlyphOf(category));
                if (glyph != null)
                {
                    _frame.TopicGlyph.sprite = glyph;
                }
            }
            SetText(_frame.TopicCategory, group);
            SetText(_frame.TopicName, name);
            SetText(_frame.TopicNote, note ?? string.Empty);
        }

        // A category's picture: the groups in the tree's order, then the test tools; one the game is sure to have in memory (docs/game-ui.md, Pictures).
        private static string GlyphOf(int category)
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            if (category < 0 || tabs == null || category >= tabs.Length)
            {
                return "Icon-Notifications";
            }
            switch (tabs[category].Group.Id)
            {
                case OptionsTree.StartId: return "Icon-Info";
                case "you": return "Icon-PersonManage";
                case "city": return "Icon-Building";
                case "around": return "Icon-Route";
                case "work": return "Icon-OpeningHours";
                case "money": return "Icon-Coin";
                case "business": return "Icon-Shop";
                case "controls": return "Icon-Edit";
                case "performance": return "Icon-Condition";
                default: return "Icon-Security";
            }
        }

        private void ScrollTop()
        {
            RectTransform content = _frame.Page;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            _frame.PageScroll.velocity = Vector2.zero;
        }

        // The page laid out at once, then moved so that the option's row stands at the top (or as near as the page allows).
        private void ScrollTo(SettingsLine line)
        {
            Slot slot = SlotOf(line);
            if (slot == null)
            {
                return;
            }
            RectTransform content = _frame.Page;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var row = (RectTransform)slot.Row.Root.transform;
            float top = -(row.anchoredPosition.y + row.rect.height * (1f - row.pivot.y)) - ScrollMargin;
            RectTransform view = _frame.PageScroll.viewport != null ? _frame.PageScroll.viewport : (RectTransform)_frame.PageScroll.transform;
            float most = Mathf.Max(0f, content.rect.height - view.rect.height);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(top, 0f, most));
            _frame.PageScroll.velocity = Vector2.zero;
        }

        /// <summary>A text with its names filled: T("reallife_set_count", "on", "4", "total", "6").</summary>
        private static string T(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }

        // A text written only when its words change: TextMeshPro builds its mesh again on every write.
        private static void SetText(TMP_Text text, string words)
        {
            words = words ?? string.Empty;
            if (text != null && !string.Equals(text.text, words, StringComparison.Ordinal))
            {
                UiClone.Words(text, words);
            }
        }
    }
}
