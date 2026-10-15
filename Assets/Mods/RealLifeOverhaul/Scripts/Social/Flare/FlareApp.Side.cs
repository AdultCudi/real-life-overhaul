using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The side of Flare's screens: the player's account at a glance (followers, tier, reputation, the week as bars, posts
    /// today), Trending in the city (the day's tags, a click shows their posts), Who to follow, and in a Going Public session
    /// the players of the session, each with mute and hide. Fixed parts made once, written on a refresh.
    /// </summary>
    public sealed partial class FlareApp
    {
        private const int TrendRows = 5, FollowRows = 3, PlayerRows = 4;

        private Image _accountCard, _trendCard, _followCard, _sessionCard;
        private TextMeshProUGUI _accountTitle, _accountBig, _accountLine, _accountWeek, _trendTitle, _followTitle, _sessionTitle;
        private readonly Image[] _bars = new Image[FlareFile.HistoryDays];
        private readonly Image[] _trendHits = new Image[TrendRows];
        private readonly TextMeshProUGUI[] _trendTags = new TextMeshProUGUI[TrendRows], _trendCounts = new TextMeshProUGUI[TrendRows];
        private readonly Image[] _followAvatars = new Image[FollowRows], _followButtons = new Image[FollowRows];
        private readonly TextMeshProUGUI[] _followInitials = new TextMeshProUGUI[FollowRows], _followNames = new TextMeshProUGUI[FollowRows], _followHandles = new TextMeshProUGUI[FollowRows],
            _followLabels = new TextMeshProUGUI[FollowRows];
        private readonly Image[] _followHits = new Image[FollowRows];
        private readonly string[] _followKeys = new string[FollowRows];
        private readonly TextMeshProUGUI[] _playerNames = new TextMeshProUGUI[PlayerRows];
        private readonly Image[] _muteButtons = new Image[PlayerRows], _hideButtons = new Image[PlayerRows];
        private readonly string[] _players = new string[PlayerRows];
        private readonly string[] _trendKeys = new string[TrendRows];
        private readonly List<string> _tags = new List<string>(8);
        private readonly List<int> _tagCounts = new List<int>(8);
        private readonly List<string> _keys = new List<string>(16);

        private void BuildSide()
        {
            _accountCard = FlareKit.Box(_side, "Account", Color.white, FlareKit.Radius);
            _accountTitle = FlareKit.Text(_accountCard.transform, "Title", 30f, Color.white, FontStyles.Bold);
            _accountBig = FlareKit.Text(_accountCard.transform, "Followers", 56f, Color.white, FontStyles.Bold);
            _accountLine = FlareKit.Text(_accountCard.transform, "Line", FlareKit.Small, Color.white);
            _accountWeek = FlareKit.Text(_accountCard.transform, "Week", 22f, Color.white);
            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i] = FlareKit.Box(_accountCard.transform, "Bar" + i, Color.white, 6f);
            }
            _trendCard = FlareKit.Box(_side, "Trending", Color.white, FlareKit.Radius);
            _trendTitle = FlareKit.Text(_trendCard.transform, "Title", 34f, Color.white, FontStyles.Bold);
            for (int i = 0; i < TrendRows; i++)
            {
                int n = i;
                _trendHits[i] = FlareKit.Box(_trendCard.transform, "Row" + i, new Color(1f, 1f, 1f, 0f), 14f, false, true);
                FlareKit.Click(_trendHits[i], () => TrendClicked(n));
                _trendTags[i] = FlareKit.Text(_trendHits[i].transform, "Tag", 32f, Color.white, FontStyles.Bold);
                _trendCounts[i] = FlareKit.Text(_trendHits[i].transform, "Count", 24f, Color.white);
            }
            _followCard = FlareKit.Box(_side, "Follow", Color.white, FlareKit.Radius);
            _followTitle = FlareKit.Text(_followCard.transform, "Title", 34f, Color.white, FontStyles.Bold);
            for (int i = 0; i < FollowRows; i++)
            {
                int n = i;
                _followHits[i] = FlareKit.Box(_followCard.transform, "Row" + i, new Color(1f, 1f, 1f, 0f), 14f, false, true);
                FlareKit.Click(_followHits[i], () => FollowRowClicked(n));
                _followAvatars[i] = FlareKit.Box(_followHits[i].transform, "Avatar", Color.white, 0f, true);
                _followInitials[i] = FlareKit.Text(_followAvatars[i].transform, "Initials", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(_followInitials[i].rectTransform, 0f, 0f, 0f, 0f);
                _followNames[i] = FlareKit.Text(_followHits[i].transform, "Name", 28f, Color.white, FontStyles.Bold);
                _followHandles[i] = FlareKit.Text(_followHits[i].transform, "Handle", 22f, Color.white);
                _followButtons[i] = FlareKit.Box(_followHits[i].transform, "Follow", Color.white, 0f, true, true);
                FlareKit.Click(_followButtons[i], () => FollowClicked(n));
                _followLabels[i] = FlareKit.Text(_followButtons[i].transform, "Label", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(_followLabels[i].rectTransform, 0f, 0f, 0f, 0f);
            }
            _sessionCard = FlareKit.Box(_side, "Session", Color.white, FlareKit.Radius);
            _sessionTitle = FlareKit.Text(_sessionCard.transform, "Title", 34f, Color.white, FontStyles.Bold);
            for (int i = 0; i < PlayerRows; i++)
            {
                int n = i;
                _playerNames[i] = FlareKit.Text(_sessionCard.transform, "Player" + i, 28f, Color.white);
                _muteButtons[i] = FlareKit.Box(_sessionCard.transform, "Mute" + i, Color.white, 0f, false, true);
                _muteButtons[i].sprite = FlareIcons.Get(FlareGlyph.Mute);
                FlareKit.Click(_muteButtons[i], () => MuteClicked(n));
                _hideButtons[i] = FlareKit.Box(_sessionCard.transform, "Hide" + i, Color.white, 0f, false, true);
                _hideButtons[i].sprite = FlareIcons.Get(FlareGlyph.Hide);
                FlareKit.Click(_hideButtons[i], () => HideClicked(n));
            }
        }

        private void LaySide(float w, float h)
        {
            if (w <= 0f)
            {
                return;
            }
            float y = 0f, p = 30f;
            FlareKit.Place(_accountCard.rectTransform, 0f, y, w, 330f);
            FlareKit.Place(_accountTitle.rectTransform, p, p, w - p * 2f, 40f);
            FlareKit.Place(_accountBig.rectTransform, p, p + 44f, w - p * 2f, 70f);
            FlareKit.Place(_accountLine.rectTransform, p, p + 118f, w - p * 2f, 70f);
            float barW = (w - p * 2f - 6f * 12f) / 7f;
            for (int i = 0; i < _bars.Length; i++)
            {
                FlareKit.Place(_bars[i].rectTransform, p + i * (barW + 12f), 290f - 60f, barW, 60f);
            }
            FlareKit.Place(_accountWeek.rectTransform, p, 296f, w - p * 2f, 30f);
            y += 330f + 24f;
            FlareKit.Place(_trendCard.rectTransform, 0f, y, w, 90f + TrendRows * 88f);
            FlareKit.Place(_trendTitle.rectTransform, p, p, w - p * 2f, 46f);
            for (int i = 0; i < TrendRows; i++)
            {
                FlareKit.Place(_trendHits[i].rectTransform, 12f, 86f + i * 88f, w - 24f, 84f);
                FlareKit.Place(_trendTags[i].rectTransform, p - 12f, 8f, w - p * 2f, 40f);
                FlareKit.Place(_trendCounts[i].rectTransform, p - 12f, 48f, w - p * 2f, 30f);
            }
            y += 90f + TrendRows * 88f + 24f;
            FlareKit.Place(_followCard.rectTransform, 0f, y, w, 90f + FollowRows * 100f);
            FlareKit.Place(_followTitle.rectTransform, p, p, w - p * 2f, 46f);
            for (int i = 0; i < FollowRows; i++)
            {
                FlareKit.Place(_followHits[i].rectTransform, 12f, 86f + i * 100f, w - 24f, 96f);
                FlareKit.Place(_followAvatars[i].rectTransform, p - 12f, 12f, 72f, 72f);
                FlareKit.Place(_followNames[i].rectTransform, p + 76f, 14f, w - p * 2f - 260f, 38f);
                FlareKit.Place(_followHandles[i].rectTransform, p + 76f, 52f, w - p * 2f - 260f, 30f);
                FlareKit.Place(_followButtons[i].rectTransform, w - 24f - 190f - p + 12f, 22f, 190f, 56f);
            }
            y += 90f + FollowRows * 100f + 24f;
            FlareKit.Place(_sessionCard.rectTransform, 0f, y, w, Mathf.Max(0f, Mathf.Min(90f + PlayerRows * 70f, h - y)));
            FlareKit.Place(_sessionTitle.rectTransform, p, p, w - p * 2f, 46f);
            for (int i = 0; i < PlayerRows; i++)
            {
                FlareKit.Place(_playerNames[i].rectTransform, p, 92f + i * 70f, w - p * 2f - 140f, 50f);
                FlareKit.Place(_muteButtons[i].rectTransform, w - p - 120f, 94f + i * 70f, 46f, 46f);
                FlareKit.Place(_hideButtons[i].rectTransform, w - p - 50f, 94f + i * 70f, 46f, 46f);
            }
        }

        private void PaintSide()
        {
            FlareLook l = _look;
            _accountCard.color = _trendCard.color = _followCard.color = _sessionCard.color = l.Card;
            _accountTitle.color = _trendTitle.color = _followTitle.color = _sessionTitle.color = l.Text;
            _accountBig.color = l.Text;
            _accountLine.color = l.Muted;
            _accountWeek.color = l.Muted;
            for (int i = 0; i < TrendRows; i++)
            {
                _trendTags[i].color = l.Text;
                _trendCounts[i].color = l.Muted;
            }
            for (int i = 0; i < FollowRows; i++)
            {
                _followNames[i].color = l.Text;
                _followHandles[i].color = l.Muted;
                _followInitials[i].color = Color.white;
            }
            for (int i = 0; i < PlayerRows; i++)
            {
                _playerNames[i].color = l.Text;
            }
        }

        private void WriteSide(FlareFile file)
        {
            if (!_side.gameObject.activeSelf || file == null)
            {
                return;
            }
            int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            FlareKit.Set(_accountTitle, FlareText.T("reallife_flare_side_account"));
            FlareKit.Set(_accountBig, FlareText.N(file.Followers));
            int next = FlareRules.NextPostMinute(file, day);
            FlareKit.Set(_accountLine, FlareText.Fill("reallife_flare_side_line", "tier", FlareText.Tier(file.Tier), "reputation", FlareText.Reputation(file.Reputation),
                "count", (file.PostsDay == day ? file.PostsToday : 0).ToString(), "most", FlareRules.PostsPerDay.ToString())
                + "\n" + (next < 0 ? FlareText.T("reallife_flare_wait_day") : next <= FlareGame.MinuteOfDay ? FlareText.T("reallife_flare_side_can_post")
                    : FlareText.Fill("reallife_flare_wait_soon", "time", FlareText.Clock(next))));
            int most = 1;
            for (int i = 0; i < file.History.Length; i++)
            {
                most = Mathf.Max(most, file.History[i]);
            }
            int least = most;
            for (int i = 0; i < file.History.Length; i++)
            {
                least = Mathf.Min(least, file.History[i]);
            }
            for (int i = 0; i < _bars.Length && i < file.History.Length; i++)
            {
                float share = most == least ? 0.6f : 0.25f + 0.75f * (file.History[i] - least) / (float)(most - least);
                RectTransform r = _bars[i].rectTransform;
                FlareKit.Place(r, r.anchoredPosition.x, 290f - 60f * share, r.sizeDelta.x, 60f * share);
                _bars[i].color = i == _bars.Length - 1 ? _look.Accent : _look.AccentSoft;
            }
            FlareKit.Set(_accountWeek, FlareText.T("reallife_flare_side_week"));

            FlareKit.Set(_trendTitle, FlareText.T("reallife_flare_side_trending"));
            FlareLiveRules.Trending(file.Posts, _director.Now, TrendRows, _tags, _tagCounts);
            for (int i = 0; i < TrendRows; i++)
            {
                bool shown = i < _tags.Count;
                FlareKit.Show(_trendHits[i], shown);
                _trendKeys[i] = shown ? _tags[i] : null;
                if (shown)
                {
                    FlareKit.Set(_trendTags[i], FlareText.Tag(_tags[i]));
                    FlareKit.Set(_trendCounts[i], FlareText.Fill("reallife_flare_side_posts", "rank", (i + 1).ToString(), "count", FlareText.N(_tagCounts[i] * 37)));
                }
            }

            FlareKit.Set(_followTitle, FlareText.T("reallife_flare_side_follow"));
            WhoToFollow(file);
            for (int i = 0; i < FollowRows; i++)
            {
                bool shown = _followKeys[i] != null;
                FlareKit.Show(_followHits[i], shown);
                if (!shown)
                {
                    continue;
                }
                string name = _director.AccountName(_followKeys[i]);
                FlareKit.Set(_followNames[i], name);
                FlareKit.Set(_followHandles[i], Handle(_followKeys[i], name));
                _followAvatars[i].color = FlareLook.AvatarOf(_followKeys[i]);
                FlareKit.Set(_followInitials[i], Initials(name));
                bool following = FlareCast.Followed(file, _followKeys[i]);
                _followButtons[i].color = following ? _look.Chip : _look.Text;
                _followLabels[i].color = following ? _look.ChipText : _look.Card;
                FlareKit.Set(_followLabels[i], FlareText.T(following ? "reallife_flare_follow_on" : "reallife_flare_follow"));
            }

            bool session = GoingPublic.InSession;
            FlareKit.Show(_sessionCard, session);
            if (session)
            {
                FlareKit.Set(_sessionTitle, FlareText.T("reallife_flare_side_session"));
                Players(file);
                for (int i = 0; i < PlayerRows; i++)
                {
                    bool shown = _players[i] != null;
                    FlareKit.Show(_playerNames[i], shown);
                    FlareKit.Show(_muteButtons[i], shown);
                    FlareKit.Show(_hideButtons[i], shown);
                    if (shown)
                    {
                        FlareKit.Set(_playerNames[i], _players[i]);
                        _muteButtons[i].color = file.Net.IsMuted(_players[i]) ? _look.Accent : _look.Muted;
                        _hideButtons[i].color = file.Net.IsHidden(_players[i]) ? _look.Accent : _look.Muted;
                    }
                }
            }
        }

        // Three accounts the player does not follow yet: an official account, then the best-followed influencers, then a brand.
        private void WhoToFollow(FlareFile file)
        {
            _keys.Clear();
            for (int i = 0; i <= (int)FlareOfficial.Bank && _keys.Count < 1; i++)
            {
                if (!FlareCast.Followed(file, "o:" + i))
                {
                    _keys.Add("o:" + i);
                }
            }
            for (int pass = 0; pass < 2 && _keys.Count < FollowRows; pass++)
            {
                FlareInfluencer best = null;
                for (int i = 0; i < file.Influencers.Count; i++)
                {
                    FlareInfluencer inf = file.Influencers[i];
                    string key = "i:" + inf.Id;
                    if (inf.Active && !FlareCast.Followed(file, key) && !_keys.Contains(key) && (best == null || inf.Followers > best.Followers))
                    {
                        best = inf;
                    }
                }
                if (best != null)
                {
                    _keys.Add("i:" + best.Id);
                }
            }
            IReadOnlyList<string> brands = _director.BrandAccounts;
            for (int i = 0; i < brands.Count && _keys.Count < FollowRows; i++)
            {
                if (!FlareCast.Followed(file, "b:" + brands[i]))
                {
                    _keys.Add("b:" + brands[i]);
                }
            }
            for (int i = 0; i < FollowRows; i++)
            {
                _followKeys[i] = i < _keys.Count ? _keys[i] : null;
            }
        }

        // The players of the session seen in the feed (the newest first), this one left out.
        private void Players(FlareFile file)
        {
            for (int i = 0; i < PlayerRows; i++)
            {
                _players[i] = null;
            }
            int n = 0;
            string me = GoingPublicLink.MyId;
            for (int i = file.Posts.Count - 1; i >= 0 && n < PlayerRows; i--)
            {
                string who = file.Posts[i].Author == FlareAuthor.Network ? file.Posts[i].AuthorId : null;
                if (who == null || who == me)
                {
                    continue;
                }
                bool seen = false;
                for (int k = 0; k < n; k++)
                {
                    seen |= _players[k] == who;
                }
                if (!seen)
                {
                    _players[n++] = who;
                }
            }
        }

        private void TrendClicked(int n)
        {
            if (_trendKeys[n] != null)
            {
                Open(FlareScreen.Tag, 0, -1, _trendKeys[n]);
            }
        }

        private void FollowRowClicked(int n)
        {
            if (_followKeys[n] != null)
            {
                Open(FlareScreen.Profile, 0, -1, _followKeys[n]);
            }
        }

        private void FollowClicked(int n)
        {
            FlareFile file = _director.File;
            if (file != null && _followKeys[n] != null)
            {
                _director.Follow(_followKeys[n], !FlareCast.Followed(file, _followKeys[n]));
            }
        }

        private void MuteClicked(int n)
        {
            FlareFile file = _director.File;
            if (file != null && _players[n] != null)
            {
                _director.Mute(_players[n], !file.Net.IsMuted(_players[n]));
            }
        }

        private void HideClicked(int n)
        {
            FlareFile file = _director.File;
            if (file != null && _players[n] != null)
            {
                _director.Hide(_players[n], !file.Net.IsHidden(_players[n]));
            }
        }

        /// <summary>An account's handle, as shown under its name.</summary>
        private string Handle(string key, string name)
        {
            if (key == null)
            {
                return string.Empty;
            }
            int n;
            if (key.StartsWith("o:", System.StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n))
            {
                return FlareText.OfficialHandle(n);
            }
            if (key.Length > 1 && key[0] == 'c' && key[1] != ':' && int.TryParse(key.Substring(1), out n) && n >= 0 && n < _director.Citizens.Count)
            {
                return _director.Citizens[n].Handle;
            }
            if (key.StartsWith("i:", System.StringComparison.Ordinal))
            {
                return name;
            }
            return FlareText.HandleOf(name);
        }
    }
}
