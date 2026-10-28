using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Staffing;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Staff and roles app's page: the game's help screen (F1) in the mod's dark colours (GameUi.HelpFrame), as the Settings app
    /// is. Two categories, the businesses and the roles, each from A to Z; the search box filters both as you type, with the match
    /// in bold, as the game's own help does; an entry's page is written by <see cref="StaffPages"/> from the table, with a button to each
    /// entry it names. Built when the app is first opened, a page written when it is shown; nothing per frame.
    /// </summary>
    public sealed class StaffView
    {
        private const int MostHistory = 40;     // pages the back button remembers

        /// <summary>The pictures the app's pages ask for, named before the one look through the game's sprites (GameSprites).</summary>
        internal static readonly string[] Glyphs = { "Icon-Shop", "Icon-PersonManage", "Icon-Info" };

        /// <summary>A button on the page: the game's row and what it does.</summary>
        private sealed class Slot
        {
            public OptionRow Row;
            public Action Run;
        }

        private HelpFrame _frame;
        private OptionRows _kit;
        private readonly HelpGroup[] _groups = new HelpGroup[2];        // businesses, roles
        private readonly List<StaffEntry>[] _order = { new List<StaffEntry>(32), new List<StaffEntry>(64) };
        private readonly List<HelpLink>[] _links = { new List<HelpLink>(32), new List<HelpLink>(64) };
        private readonly bool[] _open = { true, true };
        private readonly Dictionary<string, HelpLink> _linkOf = new Dictionary<string, HelpLink>(96);
        private readonly List<Slot> _shown = new List<Slot>(16);
        private readonly List<Slot> _free = new List<Slot>(16);
        private readonly List<TMP_Text> _texts = new List<TMP_Text>(24);
        private readonly List<TMP_Text> _freeParagraphs = new List<TMP_Text>(24);
        private readonly List<TMP_Text> _freeSmalls = new List<TMP_Text>(8);
        private readonly List<TMP_Text> _smalls = new List<TMP_Text>(8);
        private readonly List<string> _history = new List<string>(MostHistory);
        private StaffEntry _entry;              // the page shown; null for the home page
        private string _query = string.Empty;
        private string _want;
        private int _at = -1;
        private bool _ready, _writing;
        private string _language;               // the words the lists were last named in

        private static readonly StaffStyle Style = StaffPages.GuideStyle(key => key.GetLocalization(), Money, true);

        private static string Money(long cents)
        {
            return Guide.Money(Prices.At((int)Math.Min(int.MaxValue / 4, cents / 100)));
        }

        private bool Showing => _frame != null && _frame.Root != null && _frame.Root.activeInHierarchy;

        /// <summary>The app's panel, asleep: the help screen's copy centred in it, with both lists built. Null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            var root = new GameObject(StaffApp.AppId, typeof(RectTransform));
            root.transform.SetParent(dormant, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            Transform templates = UiClone.Dormant(root.transform, "Templates");
            _frame = HelpFrame.Make(templates, "Staff Window");
            _kit = _frame != null ? OptionRows.Take(templates) : null;
            if (_frame == null || _kit == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                _frame = null;
                return null;
            }
            _frame.Root.transform.SetParent(root.transform, false);
            UiClone.Key(_frame.Headline, "reallife_staff_app_title");
            UiClone.Words(_frame.Status, string.Empty);
            UiClone.Rewire(_frame.Close, () => menu.CloseFullMenu());
            UiClone.Rewire(_frame.Back, () => Step(-1));
            UiClone.Rewire(_frame.Forward, () => Step(1));
            UiClone.Key(_frame.Search.placeholder, "reallife_staff_search_hint");
            _frame.Search.onValueChanged.AddListener(typed => UiClone.Safe(() => Typed(typed)));
            _frame.Search.onSelect.AddListener(Guard.Listener<string>(Selected));
            _frame.Search.onDeselect.AddListener(Guard.Listener<string>(Deselected));
            if (_frame.Clear != null)
            {
                UiClone.Rewire(_frame.Clear, () => _frame.Search.text = string.Empty);
            }
            BuildList();
            return root;
        }

        // Both categories, with a link for every entry, made once; the order and the names are written when the app opens.
        private void BuildList()
        {
            string[] names = { "Businesses", "Roles" };
            string[] keys = { "reallife_staff_cat_places", "reallife_staff_cat_roles" };
            for (int c = 0; c < 2; c++)
            {
                HelpGroup group = _frame.AddGroup(names[c]);
                UiClone.Key(group.Name, keys[c]);
                int category = c;
                UiClone.Rewire(group.Button, () => Fold(category));
                _groups[c] = group;
                group.SetOpen(true);
            }
            StaffEntry[] all = StaffIndex.Entries;
            for (int i = 0; i < all.Length; i++)
            {
                StaffEntry entry = all[i];
                HelpLink link = _groups[entry.IsPlace ? 0 : 1].Add(entry.Key);
                UiClone.Rewire(link.Button, () => Pick(entry));
                _linkOf[entry.Key] = link;
            }
        }

        public void Opened()
        {
            if (_frame == null)
            {
                return;
            }
            _ready = true;
            string language = LocalizorManager.LoadedLocale;
            if (language != _language || _order[0].Count == 0)
            {
                _language = language;
                NameAndOrder();
            }
            if (_want != null)
            {
                GoTo(_want);
                _want = null;
                return;
            }
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
            if (_frame != null && _frame.Search.isFocused)
            {
                _frame.Search.DeactivateInputField();
            }
            Keys.Suspended = MetaUi.AnyOpen;
        }

        /// <summary>The entry asked for from elsewhere ("b:data_centre"), shown when the app opens; null keeps what was shown.</summary>
        public void Want(string key)
        {
            _want = key;
            if (Showing && _ready && key != null)
            {
                GoTo(key);
                _want = null;
            }
        }

        private void Selected(string typed)
        {
            Keys.Suspended = true;   // the mod's keys are off while the box has the keyboard
        }

        private void Deselected(string typed)
        {
            Keys.Suspended = MetaUi.AnyOpen;
        }

        // Every link written with its name in the language shown now, and each category put in A to Z order: a sort of two lists, once per language.
        private void NameAndOrder()
        {
            for (int c = 0; c < 2; c++)
            {
                _order[c] = StaffIndex.Sorted(c == 0, key => key.GetLocalization());
                for (int i = 0; i < _order[c].Count; i++)
                {
                    HelpLink link = _linkOf[_order[c][i].Key];
                    UiClone.Words(link.Name, _order[c][i].NameKey.GetLocalization());
                    link.Root.transform.SetSiblingIndex(i);
                }
            }
        }

        private void GoTo(string key)
        {
            StaffEntry entry = StaffIndex.Find(key);
            if (entry == null)
            {
                MetaLog.Warn("the Staff and roles app was asked to open on '" + key + "', which the table does not have.");
                Refresh();
                return;
            }
            ClearSearch();
            Pick(entry);
        }

        private void Pick(StaffEntry entry)
        {
            ClearSearch();
            Show(entry, true);
        }

        private void Show(StaffEntry entry, bool remember)
        {
            _entry = entry;
            if (remember && entry != null)
            {
                Remember(entry.Key);
            }
            Refresh();
        }

        private void Remember(string key)
        {
            if (_at >= 0 && _at < _history.Count && _history[_at] == key)
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
            _history.Add(key);
            _at = _history.Count - 1;
        }

        // The round buttons at the top left: back and forward through the pages shown, as the game's own help (HistoryBack, HistoryForward).
        private void Step(int by)
        {
            int to = _at + by;
            if (to < 0 || to >= _history.Count)
            {
                return;
            }
            _at = to;
            ClearSearch();
            Show(StaffIndex.Find(_history[to]), false);
        }

        private void Fold(int category)
        {
            _open[category] = !_open[category];
            _groups[category].SetOpen(_open[category] || _query.Length > 0);
        }

        private void ClearSearch()
        {
            _query = string.Empty;
            if (_frame != null && _frame.Search.text.Length > 0)
            {
                _frame.Search.SetTextWithoutNotify(string.Empty);
                if (_frame.Clear != null)
                {
                    _frame.Clear.gameObject.SetActive(false);
                }
            }
        }

        // Each keystroke filters both lists by any part of a name, as the game's help does, and writes the match in bold.
        private void Typed(string typed)
        {
            if (_frame.Clear != null)
            {
                _frame.Clear.gameObject.SetActive(!string.IsNullOrEmpty(typed));
            }
            _query = string.IsNullOrWhiteSpace(typed) ? string.Empty : typed.Trim();
            Refresh();
        }

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
                int found = ShowList();
                if (_query.Length > 0)
                {
                    WriteFound(found);
                }
                else if (_entry != null)
                {
                    WriteEntry(_entry);
                }
                else
                {
                    WriteHome();
                }
                _frame.Back.interactable = _at > 0;
                _frame.Forward.interactable = _at < _history.Count - 1;
                RectTransform content = _frame.Page;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
                _frame.PageScroll.velocity = Vector2.zero;
                if (Dev.Verbose)
                {
                    double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                    MetaLog.Info("Meta check (staff app): " + (_query.Length > 0 ? "search '" + _query + "', " + found + " found" : _entry != null ? "page '" + _entry.Key + "'" : "home")
                                 + ", written in " + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms (wanted under 20): " + (ms < 20.0 ? "PASS" : "SLOW") + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // The lists as they stand: while searching, only the entries with the typed word in their name, open, the match in bold. Returns how many are shown.
        private int ShowList()
        {
            int found = 0;
            for (int c = 0; c < 2; c++)
            {
                int shown = 0;
                for (int i = 0; i < _order[c].Count; i++)
                {
                    StaffEntry entry = _order[c][i];
                    HelpLink link = _linkOf[entry.Key];
                    string name = entry.NameKey.GetLocalization();
                    int at = StaffIndex.Match(name, _query);
                    link.Show(at >= 0);
                    if (at < 0)
                    {
                        continue;
                    }
                    shown++;
                    UiClone.Words(link.Name, _query.Length > 0 && at + _query.Length <= name.Length
                        ? name.Substring(0, at) + "<b>" + name.Substring(at, _query.Length) + "</b>" + name.Substring(at + _query.Length)
                        : name);
                    link.SetChosen(_query.Length == 0 && entry == _entry);
                }
                found += shown;
                _groups[c].Show(shown > 0);
                _groups[c].SetOpen(_query.Length > 0 || _open[c]);
            }
            return found;
        }

        // ---------------------------------------------------------------- the page

        private void WriteHome()
        {
            Topic(2, "reallife_staff_cat_places".GetLocalization(), "reallife_staff_home".GetLocalization(), string.Empty);
            Paragraph("reallife_staff_home_note".GetLocalization());
        }

        private void WriteFound(int count)
        {
            Topic(2, "reallife_staff_app".GetLocalization(), count > 0 ? "reallife_staff_headline_found".GetLocalization().Replace("{count}",
                count.ToString(CultureInfo.InvariantCulture)).Replace("{query}", _query)
                : "reallife_staff_empty_found".GetLocalization(), string.Empty);
            if (count == 0)
            {
                Paragraph("reallife_staff_found_none".GetLocalization());
            }
        }

        private void WriteEntry(StaffEntry entry)
        {
            Topic(entry.IsPlace ? 0 : 1, (entry.IsPlace ? "reallife_staff_cat_places" : "reallife_staff_cat_roles").GetLocalization(), entry.NameKey.GetLocalization(),
                (entry.IsPlace ? "reallife_staff_note_place" : "reallife_staff_note_role").GetLocalization());
            List<StaffBlock> blocks = StaffPages.Page(entry, Style);
            for (int i = 0; i < blocks.Count; i++)
            {
                StaffBlock b = blocks[i];
                switch (b.Kind)
                {
                    case StaffBlockKind.Heading:
                        Small(b.Text);
                        break;
                    case StaffBlockKind.Bullet:
                        Paragraph("-  " + b.Text);
                        break;
                    case StaffBlockKind.Link:
                        string slug = b.Slug;
                        Button(b.Text, () =>
                        {
                            StaffEntry target = StaffIndex.FindSlug(slug);
                            if (target != null)
                            {
                                Show(target, true);
                            }
                        });
                        break;
                    default:
                        Paragraph(b.Text);
                        break;
                }
            }
        }

        // The header over a page: a picture in the game's blue square, the category in small capitals, the entry's name and a note at the right.
        private void Topic(int glyph, string group, string name, string note)
        {
            if (_frame.TopicGlyph != null)
            {
                Sprite sprite = GameSprites.Get(Glyphs[Mathf.Clamp(glyph, 0, Glyphs.Length - 1)]);
                if (sprite != null)
                {
                    _frame.TopicGlyph.sprite = sprite;
                }
            }
            SetText(_frame.TopicCategory, group);
            SetText(_frame.TopicName, name);
            SetText(_frame.TopicNote, note);
        }

        private void Paragraph(string words)
        {
            TMP_Text text = Reuse(_freeParagraphs) ?? _frame.NewParagraph();
            Place(text, words);
        }

        private void Small(string words)
        {
            TMP_Text text = Reuse(_freeSmalls) ?? _frame.NewSmall();
            if (text == null)
            {
                Paragraph(words);
                return;
            }
            _smalls.Add(text);
            Place(text, words);
        }

        private static TMP_Text Reuse(List<TMP_Text> free)
        {
            if (free.Count == 0)
            {
                return null;
            }
            TMP_Text text = free[free.Count - 1];
            free.RemoveAt(free.Count - 1);
            return text;
        }

        private void Place(TMP_Text text, string words)
        {
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            SetText(text, words);
            _texts.Add(text);
        }

        // A button of the game's settings menu with the entry's name: it opens that entry.
        private void Button(string name, Action run)
        {
            Slot slot;
            if (_free.Count > 0)
            {
                slot = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
            }
            else
            {
                slot = new Slot { Row = _kit.Make(OptionRowKind.Button, _frame.Page) };
                Slot made = slot;
                UiClone.Rewire(slot.Row.Button, () => UiClone.Safe(() => made.Run?.Invoke()));
            }
            slot.Run = run;
            slot.Row.Show(true);
            slot.Row.Root.transform.SetAsLastSibling();
            SetText(slot.Row.Name, name);
            if (slot.Row.Note != null)
            {
                slot.Row.Note.gameObject.SetActive(false);
            }
            SetText(slot.Row.Caption, "reallife_opt_caption_open".GetLocalization());
            slot.Row.SetUsable(true);
            _shown.Add(slot);
        }

        private void ClearPage()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot slot = _shown[i];
                slot.Row.Show(false);
                slot.Run = null;
                _free.Add(slot);
            }
            _shown.Clear();
            for (int i = 0; i < _texts.Count; i++)
            {
                TMP_Text text = _texts[i];
                text.gameObject.SetActive(false);
                (_smalls.Contains(text) ? _freeSmalls : _freeParagraphs).Add(text);
            }
            _texts.Clear();
            _smalls.Clear();
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
