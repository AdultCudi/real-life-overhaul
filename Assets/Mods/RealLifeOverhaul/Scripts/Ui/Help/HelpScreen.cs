using System;
using System.Collections;
using System.Collections.Generic;
using Localizor;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's help as the kit's window (UiHelp): the game's own list of pages (read after the game built it, the mod's Staff and
    /// roles lists among them, narrowed by the game's own search), the page the game has open and the pages it has seen. A page, a
    /// link, the way back and forward and the search are all pressed through the game itself (HelpSystem.OpenLink, HistoryBack,
    /// HistoryForward, its search field), so its history, its analytics, the map links and the tutorial's page checks stay the game's.
    /// The page's words come from the game's own table (its markdown turned into the reader's blocks, HelpRules); a page with a
    /// picture or a video opens the game's own view of it. Cost: a look at the game's list four times a second (a signature of it) and a
    /// page built when another is shown; nothing a frame.
    /// </summary>
    public sealed class HelpScreen : IUiScreen, IUiTyping, IUiGameBack, IUiPaging
    {
        private const float Every = 0.25f;
        private const int RelatedMost = 3;

        private readonly HelpSystem _help;
        private readonly UiHelpModel _m = new UiHelpModel();
        private readonly List<HelpPageLink> _navLinks = new List<HelpPageLink>(128);   // the pages the sidebar lists, in step with its entries
        private readonly List<string> _targets = new List<string>(16);                  // the pages the open page links to, as its link blocks number them
        private readonly List<string> _related = new List<string>(RelatedMost);
        private readonly UiPrompt[] _prompts = new UiPrompt[3];
        private string _slug;       // the page built in the model
        private int _stamp;         // the list the game built it from, so a language change builds it again
        private long _seen;
        private float _next;
        private bool _dirty = true, _media;

        public HelpScreen(HelpSystem help)
        {
            _help = help;
        }

        public string Id => HelpKits.HelpId;

        /// <summary>The model last laid out, for the tests.</summary>
        public UiHelpModel Model => _m;

        /// <summary>The pages the open page links to, for the tests.</summary>
        internal IList<string> Targets => _targets;

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

        // The game's Cancel is the kit's while no question stands over the help; it steps out of a search first, then closes the help as the game's does.
        public bool TakesBack => !UiBoxes.Up;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Read();
            _prompts[0] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
            _prompts[1] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") };
            _prompts[2] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") };
            _m.Prompts = _prompts;
            _m.PromptCount = _prompts.Length;
            UiHelp.Lay(s, _m, frame, dark, keys);
            _seen = Signature();
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- what the player does

        public bool Press(int command, int arg)
        {
            if (UiBoxes.Up)
            {
                return false;
            }
            _dirty = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return arg >= 0 && arg < _navLinks.Count && Open(_navLinks[arg].linkSlug);
                case UiReader.RelatedCommand:
                    return arg >= 0 && arg < _related.Count && Open(_related[arg]);
                case UiReader.AnchorCommand:
                    return Anchor(arg);
                case UiHelp.BackCommand:
                    return Pages(true);
                case UiHelp.ForwardCommand:
                    return Pages(false);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            _dirty = true;
            TMP_InputField field = _help.searchField;
            if (field != null && !string.IsNullOrEmpty(field.text))
            {
                UiWidgets.Type(field, string.Empty, false);   // out of the search first: the game's own list is whole again
                return true;
            }
            using (HotSpots.Game())
            {
                _help.Toggle(false);   // as the game's Cancel and its close do
            }
            return true;
        }

        public void Tab(int step)
        {
            int at = _m.Selected + (step < 0 ? -1 : 1);
            if (_navLinks.Count == 0)
            {
                return;
            }
            at = at < 0 ? _navLinks.Count - 1 : at >= _navLinks.Count ? 0 : at;
            Press(UiAppWindow.NavCommand, at);
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (index < 0 || index >= _m.Nav.Length)
            {
                _m.Page.Reveal = index;
            }
            else
            {
                UiAppWindow.MeasureGroups(_m.Nav);
                _m.NavOffset = UiAppWindow.GroupBlock(_m.Nav, index);
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (clip == _m.NavClip)
            {
                _m.NavOffset = Math.Max(0f, _m.NavOffset - notches * UiTokens.S16);   // the sidebar, under the pointer, selected or not
            }
            else
            {
                _m.Page.Offset -= notches * UiTokens.S10;
            }
            _dirty = true;
        }

        public void Page(int step)
        {
            _m.Page.Offset += step * UiTokens.S16 * 8f;
            _dirty = true;
        }

        // A page or a place by the game's own link, as its text's links open them.
        private bool Open(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return false;
            }
            if (slug == HelpRules.MediaLink)
            {
                HelpKits.Classic();
                return true;
            }
            using (HotSpots.Game())
            {
                _help.OpenLink(slug);
            }
            _m.Page.Offset = 0f;
            return true;
        }

        // A heading or a link of the page, by its number among them: a link goes where it says, a heading is brought into view.
        private bool Anchor(int arg)
        {
            int at = 0;
            UiReaderBlock[] blocks = _m.Page.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                UiReaderKind kind = blocks[i].Kind;
                if (kind != UiReaderKind.Heading && kind != UiReaderKind.Link)
                {
                    continue;
                }
                if (at++ == arg)
                {
                    if (kind == UiReaderKind.Link)
                    {
                        int target = blocks[i].Target;
                        return target >= 0 && target < _targets.Count && Open(_targets[target]);
                    }
                    _m.Page.Reveal = arg;
                    return true;
                }
            }
            return false;
        }

        private bool Pages(bool back)
        {
            if (back ? !_m.CanBack : !_m.CanForward)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                if (back)
                {
                    _help.HistoryBack();
                }
                else
                {
                    _help.HistoryForward();
                }
            }
            _m.Page.Offset = 0f;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => UiHelp.SearchCommand;

        public string TypingText => _help.searchField != null ? _help.searchField.text ?? string.Empty : string.Empty;

        public void Typed(string text)
        {
            _dirty = true;
            if (_help.searchField != null)
            {
                UiWidgets.Type(_help.searchField, text ?? string.Empty, false);   // the game's own search hides the pages that do not match
            }
        }

        // ------------------------------------------------------------------------------------------------- reading the game's list

        private List<HelpCategory> Categories() => GameContract.HelpGenerated.Field?.GetValue(_help) as List<HelpCategory>;

        private static int Count(object stack) => stack is ICollection c ? c.Count : 0;

        // The game's list as it stands: its pages shown, the page open, the search and the history, said as one number.
        private long Signature()
        {
            List<HelpCategory> cats = Categories();
            long h = cats != null ? cats.Count : -1;
            string slug = _help.CurrentSlug;
            h = h * 31 + (slug != null ? slug.GetHashCode() : 0);
            string search = TypingText;
            h = h * 31 + search.GetHashCode();
            h = h * 31 + Count(GameContract.HelpBackPages.Field?.GetValue(_help)) * 7 + Count(GameContract.HelpForwardPages.Field?.GetValue(_help));
            for (int c = 0; cats != null && c < cats.Count; c++)
            {
                HelpCategory cat = cats[c];
                if (cat == null)
                {
                    continue;
                }
                h = h * 31 + (c == 0 ? cat.GetInstanceID() : 0) + (cat.gameObject.activeSelf ? 1 : 0);
                for (int i = 0; cat.gameObject.activeSelf && i < cat.links.Count; i++)
                {
                    h = h * 3 + (cat.links[i] != null && cat.links[i].gameObject.activeSelf ? 1 : 0);
                }
            }
            return h;
        }

        private void Read()
        {
            List<HelpCategory> cats = Categories();
            _navLinks.Clear();
            var nav = new List<UiNavEntry>(_navLinks.Capacity);
            HelpPageLink open = null;
            HelpCategory openIn = null;
            string slug = _help.CurrentSlug;
            for (int c = 0; cats != null && c < cats.Count; c++)
            {
                HelpCategory cat = cats[c];
                if (cat == null)
                {
                    continue;
                }
                string name = UiWidgets.Text(cat.categoryNameLabel.TextContainer) ?? string.Empty;
                UiIcon icon = HelpRules.IconOf(cat.categoryNameLabel.Key);
                for (int i = 0; i < cat.links.Count; i++)
                {
                    HelpPageLink link = cat.links[i];
                    if (link == null)
                    {
                        continue;
                    }
                    if (open == null && link.linkSlug == slug)
                    {
                        open = link;
                        openIn = cat;
                    }
                    if (cat.gameObject.activeSelf && link.gameObject.activeSelf)
                    {
                        _navLinks.Add(link);
                        nav.Add(new UiNavEntry { Icon = icon, Label = link.rawTranslatedKey ?? link.linkSlug, Group = name });
                    }
                }
            }
            _m.Nav = nav.ToArray();
            _m.Selected = -1;
            for (int i = 0; i < _navLinks.Count; i++)
            {
                if (_navLinks[i].linkSlug == slug)
                {
                    _m.Selected = i;
                    break;
                }
            }
            string search = TypingText;
            _m.Title = UiText.T("reallife_ui_help_title");
            _m.Search = string.IsNullOrEmpty(search) ? null : search;
            _m.SearchPlaceholder = UiText.T("common_search");
            _m.SearchCount = string.IsNullOrWhiteSpace(search) ? null : UiText.Fill("reallife_ui_help_found", "n", UiText.Whole(_navLinks.Count));
            _m.BackLabel = UiText.T("reallife_ui_help_back");
            _m.ForwardLabel = UiText.T("reallife_ui_help_forward");
            _m.CanBack = Count(GameContract.HelpBackPages.Field?.GetValue(_help)) > 1;
            _m.CanForward = Count(GameContract.HelpForwardPages.Field?.GetValue(_help)) > 0;
            bool noMatch = !string.IsNullOrWhiteSpace(search) && _navLinks.Count == 0;
            _m.HasPage = !noMatch && !string.IsNullOrEmpty(slug);
            _m.EmptyTitle = UiText.T("reallife_ui_help_none");
            _m.EmptyBody = UiText.Fill("reallife_ui_help_none_body", "search", search ?? string.Empty);
            int stamp = cats != null && cats.Count > 0 && cats[0] != null ? cats[0].GetInstanceID() : 0;
            if (_m.HasPage && (slug != _slug || stamp != _stamp))
            {
                Build(slug, open, openIn);
                _slug = slug;
                _stamp = stamp;
            }
        }

        // The open page: its words from the game's table under the key its entry names, the way on to the pages after it in its category.
        private void Build(string slug, HelpPageLink open, HelpCategory category)
        {
            string prefix = open != null && open.languageChangeEvent != null && !string.IsNullOrEmpty(open.languageChangeEvent.Key) ? open.languageChangeEvent.Key : slug;
            string title = open != null && !string.IsNullOrEmpty(open.rawTranslatedKey) ? open.rawTranslatedKey : slug.GetLocalization();
            string markdown = ("help_" + prefix + "_content").GetLocalization();
            UiReaderBlock[] blocks = HelpRules.Parse(markdown, _targets, out _media);
            if (_media)
            {
                // The kit draws words, not pictures or video: the page's own view is the game's.
                var more = new UiReaderBlock[blocks.Length + 2];
                more[0] = new UiReaderBlock { Kind = UiReaderKind.Note, Text = UiText.T("reallife_ui_help_media") };
                more[1] = new UiReaderBlock { Kind = UiReaderKind.Link, Text = UiText.T("reallife_ui_help_media_open"), Target = _targets.Count };
                Array.Copy(blocks, 0, more, 2, blocks.Length);
                _targets.Add(HelpRules.MediaLink);
                blocks = more;
            }
            UiReaderModel page = _m.Page;
            page.Title = title;
            page.Blocks = blocks;
            page.Offset = 0f;
            page.Reveal = -1;
            page.Picture = category != null ? HelpRules.IconOf(category.categoryNameLabel.Key) : UiIcon.Info;
            page.PictureCaption = category != null ? UiWidgets.Text(category.categoryNameLabel.TextContainer) : null;
            page.FactsTitle = null;
            page.Facts = new UiPairModel[0];
            _related.Clear();
            var names = new List<string>(RelatedMost);
            if (category != null && open != null)
            {
                int at = category.links.IndexOf(open);
                for (int i = at + 1; at >= 0 && i < category.links.Count && names.Count < RelatedMost; i++)
                {
                    HelpPageLink next = category.links[i];
                    if (next != null)
                    {
                        _related.Add(next.linkSlug);
                        names.Add(next.rawTranslatedKey ?? next.linkSlug);
                    }
                }
            }
            page.RelatedTitle = UiText.T("reallife_ui_help_next");
            page.Related = names.ToArray();
        }
    }
}
