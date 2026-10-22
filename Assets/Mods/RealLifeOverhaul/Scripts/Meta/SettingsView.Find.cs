using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app's search: the help screen's own box at the top left. From two letters on, the page shows every row
    /// that has each word typed (as the Mods tab's box finds them, OptionsFind), under its group and section, each with its
    /// own control, and the list keeps only the pages with a match, open, as the help's list does while it is searched.
    /// Each keystroke is one pass over texts made on the first one after the app opened; nothing a frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private readonly List<SettingsLine> _all = new List<SettingsLine>(800);   // every line of every page, in order
        private readonly List<int> _pageOfLine = new List<int>(800);
        private readonly List<string> _lineTexts = new List<string>(800);
        private readonly List<string> _sectionTexts = new List<string>(800);
        private readonly List<string> _groupTexts = new List<string>(800);
        private readonly List<string> _words = new List<string>(4);
        private readonly List<int> _found = new List<int>(128);
        private bool[] _hits;                     // the pages with a match, while a search is on
        private string _query = string.Empty;
        private bool _textsMade;

        private void TakeSearch()
        {
            _hits = new bool[_pages.Count];
            for (int p = 0; p < _pages.Count; p++)
            {
                foreach (SettingsLine line in _pages[p].Lines)
                {
                    _all.Add(line);
                    _pageOfLine.Add(p);
                }
            }
            UiClone.Key(_frame.Search.placeholder, "reallife_set_search_hint");
            _frame.Search.onValueChanged.AddListener(typed => UiClone.Safe(() => Typed(typed)));
            _frame.Search.onSelect.AddListener(Guard.Listener<string>(Selected));   // these two have no try of their own: the guard's
            _frame.Search.onDeselect.AddListener(Guard.Listener<string>(Deselected));
            if (_frame.Clear != null)
            {
                UiClone.Rewire(_frame.Clear, () => _frame.Search.text = string.Empty);
            }
        }

        // The mod's keys are off while the box has the keyboard; the game leaves its shortcuts alone itself (GameManager.HasInputSelected).
        private void Selected(string typed)
        {
            Keys.Suspended = true;
        }

        private void Deselected(string typed)
        {
            Keys.Suspended = MetaUi.AnyOpen;
        }

        private void SearchClosed()
        {
            if (_frame != null && _frame.Search.isFocused)
            {
                _frame.Search.DeactivateInputField();
            }
            Keys.Suspended = MetaUi.AnyOpen;
        }

        // The box emptied: its text and its search, without writing the page (the caller does).
        private void ClearSearch()
        {
            if (_mode == ModeFound)
            {
                _mode = ModePage;
            }
            if (_frame != null && _frame.Search.text.Length > 0)
            {
                _frame.Search.SetTextWithoutNotify(string.Empty);
                if (_frame.Clear != null)
                {
                    _frame.Clear.gameObject.SetActive(false);
                }
            }
            _query = string.Empty;
        }

        private void Typed(string typed)
        {
            if (_frame.Clear != null)
            {
                _frame.Clear.gameObject.SetActive(!string.IsNullOrEmpty(typed));
            }
            OptionsFind.Split(typed, _words);
            if (_words.Count == 0)
            {
                if (_mode == ModeFound)
                {
                    _mode = ModePage;
                    _query = string.Empty;
                    Refresh();
                }
                return;
            }
            _query = typed.Trim();
            MakeTexts();
            SettingsRules.Search(_words, _all, _lineTexts, _sectionTexts, _groupTexts, _found);
            for (int p = 0; p < _hits.Length; p++)
            {
                _hits[p] = false;
            }
            for (int f = 0; f < _found.Count; f++)
            {
                _hits[_pageOfLine[_found[f]]] = true;
            }
            _mode = ModeFound;
            Refresh();
        }

        // The lower-case texts the search looks in, made on the first letters typed after the app opened (the language or the rows shown may have changed).
        private void MakeTexts()
        {
            if (_textsMade)
            {
                return;
            }
            _textsMade = true;
            _lineTexts.Clear();
            _sectionTexts.Clear();
            _groupTexts.Clear();
            string testWords = "reallife_meta_title_tests".GetLocalization().ToLowerInvariant();
            for (int i = 0; i < _all.Count; i++)
            {
                SettingsLine line = _all[i];
                bool shown = OptionsLayout.AppShown(line);
                _lineTexts.Add(shown ? OptionsLayout.AppSearchText(line) : string.Empty);
                string section = line.Section != null ? line.Section.TitleKey.GetLocalization().ToLowerInvariant() + " " + line.Section.Words : string.Empty;
                _sectionTexts.Add(shown ? (line.Kind == LineKind.Test ? section + " " + testWords : section) : string.Empty);
                _groupTexts.Add(shown ? OptionsTree.Groups[line.Tab].TitleKey.GetLocalization().ToLowerInvariant() : string.Empty);
            }
        }

        // The matches in the order of the pages, under the group and section they stand in, at most SettingsRules.MostFound of them.
        private void WriteFound()
        {
            int count = 0;
            for (int f = 0; f < _found.Count; f++)
            {
                count += OptionsLayout.AppShown(_all[_found[f]]) ? 1 : 0;
            }
            Topic(-1, T("reallife_set_search"), count > 0 ? T("reallife_set_headline_found", "count", count.ToString(), "query", _query) : T("reallife_set_empty_found"), null);
            if (count == 0)
            {
                Paragraph(T("reallife_set_box_found_none"));
                return;
            }
            int shown = 0, lastPage = -1;
            for (int f = 0; f < _found.Count && shown < SettingsRules.MostFound; f++)
            {
                SettingsLine line = _all[_found[f]];
                if (!OptionsLayout.AppShown(line))
                {
                    continue;
                }
                int page = _pageOfLine[_found[f]];
                if (page != lastPage)
                {
                    lastPage = page;
                    Small(Where(line));
                }
                Put(line);
                shown++;
            }
            if (count > shown)
            {
                Paragraph(T("reallife_set_found_more", "count", shown.ToString()));
            }
        }
    }
}
