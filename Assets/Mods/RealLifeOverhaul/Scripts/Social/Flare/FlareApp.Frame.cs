using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The frame of Flare's screens: the rail on the left, the column's header and its list, the side on the right, and
    /// the sheet layer over all. Made once when the app's panel is built; laid out again when its size changes; painted
    /// again when the look changes (light, dark).
    /// </summary>
    public sealed partial class FlareApp
    {
        private const float RailWidth = 470f, SideWidth = 760f, ColumnMost = 1500f, Margin = 34f, HeaderHeight = 150f, NavHeight = 92f;
        private const float SideFrom = 2700f;   // the frame's width from which the side shows

        private static readonly FlareGlyph[] NavGlyphs = { FlareGlyph.Home, FlareGlyph.Explore, FlareGlyph.Bell, FlareGlyph.Mail, FlareGlyph.Store, FlareGlyph.Person };
        private static readonly string[] NavKeys =
        {
            "reallife_flare_nav_home", "reallife_flare_nav_explore", "reallife_flare_nav_notices", "reallife_flare_nav_messages", "reallife_flare_nav_business", "reallife_flare_nav_profile"
        };
        private static readonly FlareScreen[] NavScreens = { FlareScreen.Home, FlareScreen.Explore, FlareScreen.Notices, FlareScreen.Messages, FlareScreen.Business, FlareScreen.Profile };

        private Image _backdrop;
        private RectTransform _rail, _column, _side, _header;
        private TextMeshProUGUI _logo, _title, _railName, _railHandle, _railTier;
        private Image _logoMark, _postButton, _headerLine, _backButton;
        private TextMeshProUGUI _postLabel;
        private Avatar _railAvatar;
        private float _railAvatarY;
        private readonly Image[] _navIcons = new Image[6];
        private readonly TextMeshProUGUI[] _navLabels = new TextMeshProUGUI[6];
        private readonly Image[] _navBadges = new Image[6];
        private readonly TextMeshProUGUI[] _navCounts = new TextMeshProUGUI[6];
        private readonly Image[] _navHits = new Image[6];
        private readonly TextMeshProUGUI[] _tabLabels = new TextMeshProUGUI[3];
        private readonly Image[] _tabBars = new Image[3];
        private readonly Image[] _tabHits = new Image[3];
        private FlareList _list;
        private float _laidWidth = -1f;

        // ---------------------------------------------------------------- building

        private void BuildFrame()
        {
            _backdrop = FlareKit.Box(_root, "Back", Color.white, FlareKit.Radius, false, true);
            FlareKit.Stretch(_backdrop.rectTransform, 0f, 0f, 0f, 0f);

            _rail = FlareKit.Rect(_root, "Rail");
            _logoMark = FlareKit.Box(_rail, "Mark", Color.white, 0f, true);
            _logoMark.sprite = FlareIcons.Get(FlareGlyph.HeartFull);
            _logoMark.type = Image.Type.Simple;
            _logo = FlareKit.Text(_rail, "Logo", FlareKit.Logo, Color.white, FontStyles.Bold);
            _logo.text = "flare";
            for (int i = 0; i < NavKeys.Length; i++)
            {
                int n = i;
                _navHits[i] = FlareKit.Box(_rail, "Nav" + i, new Color(1f, 1f, 1f, 0f), NavHeight / 2f, false, true);
                FlareKit.Click(_navHits[i], () => Nav(n));
                _navIcons[i] = FlareKit.Box(_navHits[i].transform, "Icon", Color.white);
                _navIcons[i].sprite = FlareIcons.Get(NavGlyphs[i]);
                _navLabels[i] = FlareKit.Text(_navHits[i].transform, "Label", FlareKit.Nav, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                _navLabels[i].text = FlareText.T(NavKeys[i]);
                _navBadges[i] = FlareKit.Box(_navHits[i].transform, "Badge", Color.white, 0f, true);
                _navCounts[i] = FlareKit.Text(_navBadges[i].transform, "Count", 22f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(_navCounts[i].rectTransform, 0f, 0f, 0f, 0f);
            }
            _postButton = FlareKit.Box(_rail, "Post", Color.white, 0f, true, true);
            FlareKit.Click(_postButton, () => OpenCompose(-1, -1));
            _postLabel = FlareKit.Text(_postButton.transform, "Label", 36f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            FlareKit.Stretch(_postLabel.rectTransform, 0f, 0f, 0f, 0f);
            _postLabel.text = FlareText.T("reallife_flare_post_button");
            _railAvatar = new Avatar(_rail, 84f, () => Open(FlareScreen.Profile, 0, -1, "me", true));
            _railName = FlareKit.Text(_rail, "Name", FlareKit.Name, Color.white, FontStyles.Bold);
            _railHandle = FlareKit.Text(_rail, "Handle", FlareKit.Small, Color.white);
            _railTier = FlareKit.Text(_rail, "Tier", FlareKit.Small, Color.white);

            _column = FlareKit.Rect(_root, "Column");
            _header = FlareKit.Rect(_column, "Header");
            _backButton = FlareKit.Box(_header, "BackArrow", Color.white, 0f, false, true);
            _backButton.sprite = FlareIcons.Get(FlareGlyph.Back);
            FlareKit.Click(_backButton, Back);
            _title = FlareKit.Text(_header, "Title", FlareKit.Title, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                int n = i;
                _tabHits[i] = FlareKit.Box(_header, "Tab" + i, new Color(1f, 1f, 1f, 0f), 0f, false, true);
                FlareKit.Click(_tabHits[i], () => Tab(n));
                _tabLabels[i] = FlareKit.Text(_tabHits[i].transform, "Label", 32f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                FlareKit.Stretch(_tabLabels[i].rectTransform, 0f, 0f, 0f, 10f);
                _tabBars[i] = FlareKit.Box(_tabHits[i].transform, "Bar", Color.white, 3f);
            }
            _headerLine = FlareKit.Box(_header, "Line", Color.white);
            _list = new FlareList(_column, MakeCell, Estimate);

            _side = FlareKit.Rect(_root, "Side");
            BuildSide();
            BuildSheet();
        }

        // ---------------------------------------------------------------- laying it out

        // Where each part stands for the frame's size now: laid out again only when that size changes.
        private void Lay()
        {
            float w = _root.rect.width, h = _root.rect.height;
            if (w < 100f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_shell.Root.transform);
                w = _root.rect.width;
                h = _root.rect.height;
            }
            if (Mathf.Abs(w - _laidWidth) < 1f || w < 100f)
            {
                return;
            }
            _laidWidth = w;
            _heights.Clear();
            bool side = w >= SideFrom;
            float railW = RailWidth;
            float sideW = side ? SideWidth : 0f;
            float colW = Mathf.Min(ColumnMost, w - railW - sideW - Margin * (side ? 4f : 3f));
            float colX = railW + Margin + Mathf.Max(0f, (w - railW - sideW - Margin * (side ? 4f : 3f) - colW) / 2f);
            FlareKit.Place(_rail, Margin, Margin, railW - Margin, h - Margin * 2f);
            FlareKit.Place(_column, colX, Margin, colW, h - Margin * 2f);
            FlareKit.Show(_side, side);
            if (side)
            {
                FlareKit.Place(_side, w - sideW - Margin, Margin, sideW, h - Margin * 2f);
            }
            float rw = railW - Margin;
            FlareKit.Place(_logoMark.rectTransform, 10f, 18f, 64f, 64f);
            FlareKit.Place(_logo.rectTransform, 88f, 6f, rw - 90f, 90f);
            for (int i = 0; i < NavKeys.Length; i++)
            {
                float y = 130f + i * (NavHeight + 8f);
                FlareKit.Place(_navHits[i].rectTransform, 0f, y, rw - 20f, NavHeight);
                FlareKit.Place(_navIcons[i].rectTransform, 22f, (NavHeight - 48f) / 2f, 48f, 48f);
                FlareKit.Place(_navLabels[i].rectTransform, 96f, 0f, rw - 200f, NavHeight);
                FlareKit.Place(_navBadges[i].rectTransform, 52f, 12f, 40f, 32f);
            }
            float postY = 130f + NavKeys.Length * (NavHeight + 8f) + 24f;
            FlareKit.Place(_postButton.rectTransform, 0f, postY, rw - 20f, 96f);
            float h2 = h - Margin * 2f;
            _railAvatarY = h2 - 120f;   // (placed when the rail is written: Avatar.Show)
            FlareKit.Place(_railName.rectTransform, 108f, h2 - 126f, rw - 120f, 42f);
            FlareKit.Place(_railHandle.rectTransform, 108f, h2 - 84f, rw - 120f, 34f);
            FlareKit.Place(_railTier.rectTransform, 108f, h2 - 48f, rw - 120f, 34f);
            FlareKit.Place(_header, 0f, 0f, colW, HeaderHeight);
            FlareKit.Place(_list.Root, 0f, HeaderHeight + 12f, colW, h2 - HeaderHeight - 12f);
            FlareKit.Place(_headerLine.rectTransform, 0f, HeaderHeight - 2f, colW, 2f);
            LaySide(side ? sideW : 0f, h2);
            LaySheet(w, h);
        }

        // ---------------------------------------------------------------- painting

        private void Paint()
        {
            FlareLook l = _look;
            _backdrop.color = l.Back;
            _logoMark.color = l.Accent;
            _logo.color = l.Accent;
            for (int i = 0; i < NavKeys.Length; i++)
            {
                _navBadges[i].color = l.Accent;
                _navCounts[i].color = Color.white;
            }
            _postButton.color = l.Accent;
            _postLabel.color = Color.white;
            _railName.color = l.Text;
            _railHandle.color = l.Muted;
            _railTier.color = l.Accent;
            _railAvatar.Letters.color = Color.white;
            _title.color = l.Text;
            _backButton.color = l.Text;
            _headerLine.color = l.Line;
            for (int i = 0; i < _tabBars.Length; i++)
            {
                _tabBars[i].color = l.Accent;
            }
            PaintSide();
            PaintSheet();
            _list.Paint(l);
        }

        // ---------------------------------------------------------------- the rail

        private void Nav(int n)
        {
            CloseSheet();
            if (NavScreens[n] == FlareScreen.Profile)
            {
                Open(FlareScreen.Profile, 0, -1, "me", true);
            }
            else
            {
                Open(NavScreens[n], 0, -1, null, true);
            }
        }

        private void WriteRail(FlareFile file)
        {
            FlareScreen on = _at.Screen == FlareScreen.Tag ? FlareScreen.Explore : _at.Screen == FlareScreen.Conversation ? FlareScreen.Messages
                : _at.Screen == FlareScreen.Profile && _at.Key != "me" ? (FlareScreen)(-1) : _at.Screen == FlareScreen.Thread ? (FlareScreen)(-1) : _at.Screen;
            int notices = file != null ? _director.Unread : 0, messages = 0;
            if (file != null)
            {
                long now = _director.Now;
                for (int i = 0; i < file.Net.Dms.Count; i++)
                {
                    FlareDm m = file.Net.Dms[i];
                    messages += !m.Read && !m.Mine && m.At <= now ? 1 : 0;
                }
            }
            for (int i = 0; i < NavKeys.Length; i++)
            {
                bool selected = NavScreens[i] == on;
                _navLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                _navLabels[i].color = selected ? _look.Accent : _look.Text;
                _navIcons[i].color = selected ? _look.Accent : _look.Text;
                _navHits[i].color = selected ? _look.AccentSoft : new Color(1f, 1f, 1f, 0f);
                int count = i == 2 ? notices : i == 3 ? messages : 0;
                FlareKit.Show(_navBadges[i], count > 0);
                if (count > 0)
                {
                    FlareKit.Set(_navCounts[i], count > 99 ? "99" : count.ToString());
                }
            }
            string name = _director.MyName;
            FlareKit.Set(_railName, name);
            FlareKit.Set(_railHandle, _director.MyHandle);
            FlareKit.Set(_railTier, file != null ? FlareText.Fill("reallife_flare_rail_followers", "followers", FlareText.N(file.Followers), "tier", FlareText.Tier(file.Tier)) : string.Empty);
            _railAvatar.Show(this, "me", name, 6f, _railAvatarY, 84f);
        }

        // ---------------------------------------------------------------- the header

        private static readonly string[] HomeTabs = { "reallife_flare_tab_foryou", "reallife_flare_tab_following" };
        private static readonly string[] ExploreTabs = { "reallife_flare_tab_trending", "reallife_flare_tab_accounts" };
        private static readonly string[] ProfileTabs = { "reallife_flare_tab_posts", "reallife_flare_tab_replies", "reallife_flare_tab_pictures" };

        private void WriteHeader(FlareFile file)
        {
            string[] tabs = null;
            string title;
            switch (_at.Screen)
            {
                case FlareScreen.Home: title = FlareText.T("reallife_flare_nav_home"); tabs = HomeTabs; break;
                case FlareScreen.Explore: title = FlareText.T("reallife_flare_nav_explore"); tabs = ExploreTabs; break;
                case FlareScreen.Tag: title = FlareText.Tag(_at.Key); break;
                case FlareScreen.Notices: title = FlareText.T("reallife_flare_nav_notices"); break;
                case FlareScreen.Messages: title = FlareText.T("reallife_flare_nav_messages"); break;
                case FlareScreen.Conversation: title = _director.AccountName(_at.Key); break;
                case FlareScreen.Profile: title = _at.Key == "me" ? _director.MyName : _director.AccountName(_at.Key); tabs = ProfileTabs; break;
                case FlareScreen.Thread: title = FlareText.T("reallife_flare_title_post"); break;
                default: title = FlareText.T("reallife_flare_nav_business"); break;
            }
            bool back = _back.Count > 0;
            FlareKit.Show(_backButton, back);
            float x = back ? 80f : 0f;
            FlareKit.Place(_backButton.rectTransform, 6f, 22f, 52f, 52f);
            float colW = _header.rect.width;
            FlareKit.Place(_title.rectTransform, x, 6f, colW - x, 84f);
            FlareKit.Set(_title, title);
            int count = tabs != null ? tabs.Length : 0;
            float tabW = count > 0 ? Mathf.Min(320f, colW / count) : 0f;
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                bool shown = i < count;
                FlareKit.Show(_tabHits[i], shown);
                if (!shown)
                {
                    continue;
                }
                FlareKit.Place(_tabHits[i].rectTransform, i * tabW, 90f, tabW, 60f);
                FlareKit.Set(_tabLabels[i], FlareText.T(tabs[i]));
                bool on = _at.Tab == i;
                _tabLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                _tabLabels[i].color = on ? _look.Text : _look.Muted;
                FlareKit.Show(_tabBars[i], on);
                FlareKit.Place(_tabBars[i].rectTransform, tabW * 0.25f, 54f, tabW * 0.5f, 6f);
            }
        }

        /// <summary>A name's initials, for its avatar: "AB".</summary>
        internal static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }
            name = name.TrimStart('@');
            char a = '\0', b = '\0';
            bool next = true;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (next)
                    {
                        if (a == '\0')
                        {
                            a = c;
                        }
                        else if (b == '\0')
                        {
                            b = c;
                        }
                    }
                    next = false;
                }
                else
                {
                    next = true;
                }
            }
            if (a == '\0')
            {
                return "?";
            }
            return b == '\0' ? char.ToUpperInvariant(a).ToString() : new string(new[] { char.ToUpperInvariant(a), char.ToUpperInvariant(b) });
        }

        // The heights of the rows measured so far, by what they show, so that a screen written again keeps its place.
        private readonly Dictionary<long, float> _heights = new Dictionary<long, float>(256);
    }
}
