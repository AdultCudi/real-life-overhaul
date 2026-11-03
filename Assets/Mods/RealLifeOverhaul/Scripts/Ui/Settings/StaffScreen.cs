using System;
using System.Collections.Generic;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Staffing;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Staff and roles on the kit: businesses and roles from A to Z in the sidebar, search at the head, and the
    /// chosen entry as a reader page whose cross links open the entry they name. The list is built when the search
    /// changes. Nothing per frame.
    /// </summary>
    public sealed class StaffScreen : IUiScreen, IUiTyping, IUiPaging
    {
        public const string ScreenId = "staff_roles";

        private readonly UiReaderModel _page = new UiReaderModel();
        private readonly List<StaffEntry> _all = new List<StaffEntry>(96);
        private readonly List<int> _shown = new List<int>(96);
        private UiNavEntry[] _nav = new UiNavEntry[0];
        private StaffStyle _style;
        private float _navOffset;
        private int _at;
        private int _built = -2;
        private string _query = string.Empty;
        private bool _ready;
        private bool _dirty = true;

        public static IUiScreen Make()
        {
            if (_one == null)
            {
                _one = new StaffScreen();
            }
            return _one;
        }

        private static StaffScreen _one;

        /// <summary>The entry on screen, so a report can name it.</summary>
        public static string Place { get; private set; }

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
            s.Begin("staff", frame.Width, frame.Height, dark);
            frame.Icon = UiIcon.People;
            float offset = _navOffset;
            UiAt at = UiAppWindow.ShellFooted(s, frame, T("reallife_staff_app"), UiIcon.People, _nav, ShownAt(), ref offset, null, 0, keys,
                out float x, out float y, out float w, out float bottom);
            _navOffset = offset;
            if (_shown.Count == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Search, T("reallife_staff_none"), T("reallife_staff_none_body"));
            }
            else
            {
                UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), T("reallife_staff_search"), _query, null, UiSettingsList.SearchCommand);
                float top = y + UiTokens.Control + UiTokens.S4;
                Build();
                UiReader.Lay(s, at, _page, x, top, w, bottom - top, keys);
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
                _at = _shown[arg];
                _built = -2;
                _page.Offset = 0f;
                _dirty = true;
                return true;
            }
            if (command == UiReader.AnchorCommand)
            {
                return OpenLink(arg);
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
            int at = ShownAt();
            if (at < 0)
            {
                return;
            }
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
            if (_nav.Length == 0)
            {
                return;
            }
            UiAppWindow.MeasureGroups(_nav);
            _navOffset = UiAppWindow.GroupBlock(_nav, Math.Max(0, Math.Min(index, _nav.Length - 1)));
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _page.Offset -= notches * UiTokens.S10;
            _dirty = true;
        }

        public void Page(int step)
        {
            _page.Offset += step * UiTokens.S16 * 8f;
            _dirty = true;
        }

        private void Ensure()
        {
            if (_all.Count == 0)
            {
                _style = StaffPages.GuideStyle(T, Money, true);
                List<StaffEntry> places = StaffIndex.Sorted(true, T);
                List<StaffEntry> roles = StaffIndex.Sorted(false, T);
                for (int i = 0; i < places.Count; i++)
                {
                    _all.Add(places[i]);
                }
                for (int i = 0; i < roles.Count; i++)
                {
                    _all.Add(roles[i]);
                }
            }
            if (_ready)
            {
                return;
            }
            _shown.Clear();
            string placeGroup = T("reallife_staff_businesses");
            string roleGroup = T("reallife_staff_roles");
            int count = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                string name = T(_all[i].NameKey);
                if (!string.IsNullOrWhiteSpace(_query) && StaffIndex.Match(name, _query) < 0)
                {
                    continue;
                }
                _shown.Add(i);
                count++;
            }
            if (_nav.Length != count)
            {
                _nav = new UiNavEntry[count];
            }
            for (int s = 0; s < _shown.Count; s++)
            {
                StaffEntry entry = _all[_shown[s]];
                _nav[s] = new UiNavEntry
                {
                    Icon = entry.IsPlace ? UiIcon.Store : UiIcon.Person,
                    Label = T(entry.NameKey),
                    Group = entry.IsPlace ? placeGroup : roleGroup
                };
            }
            if (ShownAt() < 0 && _shown.Count > 0)
            {
                _at = _shown[0];
                _built = -2;
            }
            _ready = true;
        }

        private int ShownAt()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i] == _at)
                {
                    return i;
                }
            }
            return -1;
        }

        private void Build()
        {
            if (_built == _at || _at < 0 || _at >= _all.Count)
            {
                return;
            }
            _built = _at;
            StaffEntry entry = _all[_at];
            List<StaffBlock> blocks = StaffPages.Page(entry, _style);
            var reader = new UiReaderBlock[blocks.Count];
            var facts = new List<UiPairModel>(4);
            for (int i = 0; i < blocks.Count; i++)
            {
                StaffBlock block = blocks[i];
                UiReaderBlock row = new UiReaderBlock { Text = block.Text };
                if (block.Kind == StaffBlockKind.Heading)
                {
                    row.Kind = UiReaderKind.Heading;
                    if (facts.Count < 4)
                    {
                        facts.Add(new UiPairModel { Key = block.Text, Value = (facts.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) });
                    }
                }
                else if (block.Kind == StaffBlockKind.Bullet)
                {
                    row.Kind = UiReaderKind.Bullet;
                }
                else if (block.Kind == StaffBlockKind.Link)
                {
                    row.Kind = UiReaderKind.Link;
                    row.Target = IndexOf(block.Slug);
                }
                else
                {
                    row.Kind = UiReaderKind.Paragraph;
                }
                reader[i] = row;
            }
            _page.Title = T(entry.NameKey);
            _page.Blocks = reader;
            _page.Picture = entry.IsPlace ? UiIcon.Store : UiIcon.Person;
            _page.FactsTitle = T("reallife_guide_on_page");
            _page.Facts = facts.ToArray();
            _page.RelatedTitle = T("reallife_guide_next");
            _page.Related = new string[0];
            _page.Offset = 0f;
            Place = _page.Title;
        }

        private int IndexOf(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey))
            {
                return -1;
            }
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].NameKey == nameKey)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool OpenLink(int anchor)
        {
            if (_page.Blocks == null)
            {
                return false;
            }
            int seen = 0;
            for (int i = 0; i < _page.Blocks.Length; i++)
            {
                if (_page.Blocks[i].Kind != UiReaderKind.Heading && _page.Blocks[i].Kind != UiReaderKind.Link)
                {
                    continue;
                }
                if (seen == anchor && _page.Blocks[i].Kind == UiReaderKind.Link && _page.Blocks[i].Target >= 0)
                {
                    _at = _page.Blocks[i].Target;
                    _built = -2;
                    _query = string.Empty;
                    _ready = false;
                    _dirty = true;
                    return true;
                }
                seen++;
            }
            _page.Reveal = anchor;
            _dirty = true;
            return true;
        }

        private static string Money(long cents)
        {
            return Guide.Money(Prices.At((int)Math.Min(int.MaxValue / 4, cents / 100)));
        }

        private static string T(string key) => UiText.T(key);
    }
}
