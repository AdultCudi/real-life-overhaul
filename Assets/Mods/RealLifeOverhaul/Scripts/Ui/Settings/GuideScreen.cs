using System;
using System.Collections.Generic;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Guide on the kit: its pages grouped down the sidebar, one page in the reader, with search, the headings
    /// on the page and what to read next. What's new carries the day each version went out. A page is built when it
    /// is opened. Nothing per frame.
    /// </summary>
    public sealed class GuideScreen : IUiScreen, IUiTyping, IUiPaging
    {
        public const string ScreenId = "guide";

        private readonly UiReaderModel _page = new UiReaderModel();
        private readonly List<GuideTopic> _topics = new List<GuideTopic>(128);
        private readonly List<string> _body = new List<string>(128);
        private readonly List<int> _shown = new List<int>(128);
        private readonly List<string> _words = new List<string>(4);
        private UiNavEntry[] _nav = new UiNavEntry[0];
        private float _navOffset;
        private int _navClip = -1;   // the sidebar's scroll region in the last layout, so the wheel scrolls the pane under the pointer
        private int _pageAt;
        private int _built = -1;
        private string _query = string.Empty;
        private bool _ready;
        private bool _dirty = true;

        public static IUiScreen Make()
        {
            if (_one == null)
            {
                _one = new GuideScreen();
            }
            return _one;
        }

        private static GuideScreen _one;

        /// <summary>The page on screen, so a report can name it.</summary>
        public static string Place { get; private set; }

        /// <summary>Opens on a topic the next time the screen is shown. Used when settings asks for a section.</summary>
        public static void Want(string topicId)
        {
            _want = topicId;
            if (_one != null)
            {
                _one._ready = false;
                _one._built = -1;
                _one._dirty = true;
            }
        }

        private static string _want;

        public string Id => ScreenId;

        public bool Dirty => _dirty;

        public int TypingCommand => UiSettingsList.SearchCommand;

        public string TypingText => _query ?? string.Empty;

        public void Typed(string text)
        {
            string next = text ?? string.Empty;
            if (next == _query)
            {
                return;
            }
            _query = next;
            _ready = false;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Ensure();
            s.Begin("guide", frame.Width, frame.Height, dark);
            frame.Icon = UiIcon.Info;
            float offset = _navOffset;
            _navClip = s.ClipCount;   // the sidebar's clip is the first the footed window adds
            UiAt at = UiAppWindow.ShellFooted(s, frame, T("reallife_guide_app"), UiIcon.Info, _nav, ShownIndex(), ref offset, null, 0, keys,
                out float x, out float y, out float w, out float bottom);
            _navOffset = offset;
            if (_shown.Count == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Search, T("reallife_guide_none"), T("reallife_guide_none_body"));
            }
            else
            {
                UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), T("reallife_guide_find"), _query, null, UiSettingsList.SearchCommand);
                float top = y + UiTokens.Control + UiTokens.S4;
                BuildPage();
                UiReader.Lay(s, at, _page, x, top, w, bottom - top, keys);
                Place = _page.Title;
            }
            s.AddPrompt(UiAction.Move, T("reallife_ui_prompt_move"));
            s.AddPrompt(UiAction.Confirm, T("reallife_ui_prompt_select"));
            s.AddPrompt(UiAction.Back, T("reallife_ui_prompt_back"));
            UiDraw.PromptBar(s, frame, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                if (arg < 0 || arg >= _shown.Count)
                {
                    return false;
                }
                _pageAt = _shown[arg];
                _built = -1;
                _page.Offset = 0f;
                _dirty = true;
                return true;
            }
            if (command == UiReader.RelatedCommand)
            {
                return OpenRelated(arg);
            }
            if (command == UiReader.AnchorCommand)
            {
                _page.Reveal = arg;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (!string.IsNullOrEmpty(_query))
            {
                _query = string.Empty;
                _ready = false;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_shown.Count == 0)
            {
                return;
            }
            int at = ShownIndex();
            int next = at + (step < 0 ? -1 : 1);
            if (next < 0)
            {
                next = _shown.Count - 1;
            }
            else if (next >= _shown.Count)
            {
                next = 0;
            }
            Press(UiAppWindow.NavCommand, next);
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (index < 0 || index >= _shown.Count)
            {
                _page.Reveal = index;
            }
            else
            {
                UiAppWindow.MeasureGroups(_nav);
                _navOffset = UiAppWindow.GroupBlock(_nav, index);
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (clip == _navClip)
            {
                _navOffset = Math.Max(0f, _navOffset - notches * UiTokens.S16);   // the sidebar, under the pointer, selected or not
            }
            else
            {
                _page.Offset -= notches * UiTokens.S10;
            }
            _dirty = true;
        }

        public void Page(int step)
        {
            _page.Offset += step * UiTokens.S16 * 8f;
            _dirty = true;
        }

        private void Ensure()
        {
            if (_topics.Count == 0)
            {
                List<GuideTopic> topics = Guide.Topics();
                for (int i = 0; i < topics.Count; i++)
                {
                    _topics.Add(topics[i]);
                    _body.Add(null);
                }
            }
            if (!string.IsNullOrEmpty(_want))
            {
                for (int i = 0; i < _topics.Count; i++)
                {
                    if (_topics[i].Id == _want)
                    {
                        _pageAt = i;
                        _built = -1;
                        break;
                    }
                }
                _want = null;
                _ready = false;
            }
            if (_ready)
            {
                return;
            }
            _shown.Clear();
            bool searching = _query.Trim().Length >= OptionsFind.ShortestQuery;
            if (searching)
            {
                OptionsFind.Split(_query, _words);
            }
            string group = null;
            int navCount = 0;
            for (int i = 0; i < _topics.Count; i++)
            {
                if (searching && !Hit(i))
                {
                    continue;
                }
                _shown.Add(i);
                navCount++;
            }
            if (_nav.Length != navCount)
            {
                _nav = new UiNavEntry[navCount];
            }
            for (int s = 0; s < _shown.Count; s++)
            {
                GuideTopic topic = _topics[_shown[s]];
                string label = topic.Group == null ? T("reallife_guide_own") : T(topic.Group);
                _nav[s] = new UiNavEntry
                {
                    Icon = IconOf(topic),
                    Label = topic.Title ?? T(topic.TitleKey),
                    Group = label != group ? label : group,
                    Count = null
                };
                if (label != group)
                {
                    group = label;
                    _nav[s].Group = label;
                }
                else
                {
                    _nav[s].Group = label;
                }
            }
            if (ShownIndex() < 0 && _shown.Count > 0)
            {
                _pageAt = _shown[0];
                _built = -1;
            }
            _ready = true;
        }

        private bool Hit(int index)
        {
            GuideTopic topic = _topics[index];
            string title = (topic.Title ?? T(topic.TitleKey)).ToLowerInvariant();
            if (_body[index] == null && topic.Body != null)
            {
                _body[index] = topic.Body() ?? string.Empty;
            }
            string body = _body[index] == null ? string.Empty : _body[index].ToLowerInvariant();
            return OptionsFind.Matches(_words, title, body, topic.Group);
        }

        private int ShownIndex()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i] == _pageAt)
                {
                    return i;
                }
            }
            return -1;
        }

        private void BuildPage()
        {
            if (_built == _pageAt || _pageAt < 0 || _pageAt >= _topics.Count)
            {
                return;
            }
            GuideTopic topic = _topics[_pageAt];
            _built = _pageAt;
            if (topic.Id == "whatsnew")
            {
                BuildNews();
                return;
            }
            if (_body[_pageAt] == null && topic.Body != null)
            {
                _body[_pageAt] = topic.Body() ?? string.Empty;
            }
            _page.Title = topic.Title ?? T(topic.TitleKey);
            _page.Blocks = UiReaderRules.Parse(_body[_pageAt] ?? string.Empty);
            _page.Picture = IconOf(topic);
            _page.PictureCaption = null;
            _page.FactsTitle = T("reallife_guide_on_page");
            _page.Facts = Headings(_page.Blocks);
            _page.RelatedTitle = T("reallife_guide_next");
            _page.Related = Next(2);
            _page.Offset = 0f;
            Place = _page.Title;
        }

        private void BuildNews()
        {
            Changelog.Entry[] all = Changelog.All;
            var blocks = new List<UiReaderBlock>(all.Length * 2);
            var facts = new List<UiPairModel>(4);
            bool unread = Welcome.Unread;
            for (int i = 0; i < all.Length; i++)
            {
                string title = T(all[i].TitleKey);
                string day = ReleaseDates.Words(all[i].Order, T);
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Heading, Text = string.IsNullOrEmpty(day) ? title : day, Chip = title });
                if (i == 0 && unread)
                {
                    blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Note, Text = T("reallife_guide_new_since"), Icon = UiIcon.Spark });
                }
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = T(all[i].TextKey) });
                if (facts.Count < 4)
                {
                    facts.Add(new UiPairModel { Key = title, Value = day ?? title });
                }
            }
            _page.Title = T("reallife_guide_whatsnew_title");
            _page.Blocks = blocks.ToArray();
            _page.Picture = UiIcon.Newspaper;
            _page.PictureCaption = null;
            _page.FactsTitle = T("reallife_guide_on_page");
            _page.Facts = facts.ToArray();
            _page.RelatedTitle = T("reallife_guide_next");
            _page.Related = Next(2);
            _page.Offset = 0f;
        }

        private UiPairModel[] Headings(UiReaderBlock[] blocks)
        {
            var facts = new List<UiPairModel>(4);
            if (blocks == null)
            {
                return facts.ToArray();
            }
            for (int i = 0; i < blocks.Length && facts.Count < 4; i++)
            {
                if (blocks[i].Kind == UiReaderKind.Heading)
                {
                    facts.Add(new UiPairModel { Key = blocks[i].Text, Value = (facts.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) });
                }
            }
            return facts.ToArray();
        }

        private string[] Next(int count)
        {
            var names = new List<string>(count);
            for (int i = _pageAt + 1; i < _topics.Count && names.Count < count; i++)
            {
                names.Add(_topics[i].Title ?? T(_topics[i].TitleKey));
            }
            return names.ToArray();
        }

        private bool OpenRelated(int arg)
        {
            int index = _pageAt + 1 + arg;
            if (index < 0 || index >= _topics.Count)
            {
                return false;
            }
            _pageAt = index;
            _built = -1;
            _query = string.Empty;
            _ready = false;
            _dirty = true;
            return true;
        }

        private static UiIcon IconOf(GuideTopic topic)
        {
            if (topic.Id == "whatsnew")
            {
                return UiIcon.Newspaper;
            }
            if (topic.Id == "staff")
            {
                return UiIcon.People;
            }
            if (topic.Id == "trouble")
            {
                return UiIcon.Wrench;
            }
            if (topic.Group != null && topic.Group.IndexOf("money", StringComparison.Ordinal) >= 0)
            {
                return UiIcon.Coin;
            }
            return UiIcon.Info;
        }

        private static string T(string key) => UiText.T(key);
    }
}
