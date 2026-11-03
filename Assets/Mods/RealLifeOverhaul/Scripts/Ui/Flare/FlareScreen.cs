using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face over the phone app. It reads the account when the account changes and lays the social shell.
    /// A chip, a story, a post and a page each do the thing they name. Typing goes through the host's text box.
    /// Cost: nothing per frame; a layout when the account or the page changes.
    /// </summary>
    public sealed class FlareScreen : IUiScreen, IUiTypingSubmit
    {
        private const int FeedMost = 40;

        private readonly FlareDirector _flare;
        private readonly FlareFaceModel _model = new FlareFaceModel();
        private readonly List<FlarePost> _shown = new List<FlarePost>(16);
        private readonly List<string> _pics = new List<string>(8);
        private readonly List<string> _tags = new List<string>(8);
        private readonly List<string> _stories = new List<string>(6);
        private readonly List<string> _withs = new List<string>(12);
        private FlareUseRules.FlarePick _pick = FlareUseRules.FlarePick.Fresh();
        private int _revision = -1;
        private int _page;
        private int _nav;
        private int _replyTo = -1;
        private string _draft = string.Empty;
        private string _focus;
        private string _with;
        private int _hire = -1;
        private bool _dirty = true;

        public FlareScreen(FlareDirector flare)
        {
            _flare = flare;
            if (flare != null && flare.KitPage >= 0)
            {
                _page = flare.KitPage;
                _nav = _page == 8 ? 7 : _page == 10 ? 8 : _page == 7 ? 0 : _page;
                flare.KitPage = -1;
            }
        }

        public string Id => "flare";

        public bool Dirty => _dirty || (_flare != null && _flare.Revision != _revision);

        public int TypingCommand
        {
            get
            {
                if (_page == 3)
                {
                    return _with == null ? UiMessageList.SearchCommand : FlareFace.FieldCommand;
                }
                return _page == 0 || _page == 7 ? FlareFace.FieldCommand : -1;
            }
        }

        public string TypingText => _draft ?? string.Empty;

        public int TypingLimit => 240;

        public void Typed(string text)
        {
            _draft = text ?? string.Empty;
            _dirty = true;
        }

        public void Submitted(string text)
        {
            _draft = text ?? string.Empty;
            if (_page == 3 && _with != null)
            {
                SendMessage();
            }
            else if (_page == 7 || _page == 0)
            {
                Send();
            }
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Fill();
            FlareFace.Lay(s, frame, dark, keys, UiText.Words, _model);
            _dirty = false;
            if (_flare != null)
            {
                _revision = _flare.Revision;
            }
        }

        public bool Press(int command, int arg)
        {
            if (command == FlareFace.NavCommand && arg >= 0 && arg < 9)
            {
                _nav = arg;
                _page = arg == 7 ? 8 : arg == 8 ? 10 : arg;
                _with = null;
                _focus = null;
                _dirty = true;
                return true;
            }
            if (command == FlareFace.PostCommand)
            {
                if (!string.IsNullOrEmpty(_draft))
                {
                    return Send();
                }
                _page = 7;
                _dirty = true;
                return true;
            }
            if (command == FlareFace.ActionCommand && _page == 7)
            {
                return Send();
            }
            if (command == FlareFace.ChipCommand)
            {
                return Chip(arg);
            }
            if (command == FlareFace.StoryCommand)
            {
                return OpenStory(arg);
            }
            if (command == UiFeed.AuthorCommand)
            {
                return OpenAuthor(arg);
            }
            if (command == UiFeed.ReactCommand)
            {
                return React(arg);
            }
            if (command == UiFeed.PersonCommand)
            {
                return FollowShown(arg);
            }
            if (command == UiFeed.ListCommand)
            {
                return OpenTag(arg);
            }
            if (command == UiMessageList.RowCommand)
            {
                return OpenMessage(arg);
            }
            if (command == UiMessageList.SearchCommand || command == UiMessageList.ClearCommand)
            {
                if (command == UiMessageList.ClearCommand)
                {
                    _draft = string.Empty;
                }
                _dirty = true;
                return true;
            }
            if (command == FlareFace.FieldCommand)
            {
                return Field();
            }
            if (command == FlareFace.RowCommand)
            {
                return Row(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_with != null)
            {
                _with = null;
                _dirty = true;
                return true;
            }
            if (_focus != null)
            {
                _focus = null;
                _dirty = true;
                return true;
            }
            if (_page != 0)
            {
                _page = 0;
                _nav = 0;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int n = 9;
            _nav = (_nav + (step < 0 ? n - 1 : 1)) % n;
            _page = _nav == 7 ? 8 : _nav == 8 ? 10 : _nav;
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return Press(FlareFace.PostCommand, 0);
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_model.Feed != null)
            {
                _model.Feed.Reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_model.Feed != null)
            {
                _model.Feed.Offset -= notches * UiTokens.Control;
            }
            _dirty = true;
        }

        private bool Chip(int arg)
        {
            FlareFile file = _flare != null ? _flare.File : null;
            Collect(file);
            FlareUseRules.Chip(ref _pick, arg, file != null ? file.Shops.Count : 0, _pics.Count, _tags.Count);
            _pick.Pic = _pick.Photo >= 0 && _pick.Photo < _pics.Count ? _pics[_pick.Photo] : null;
            _pick.Tags = _pick.Tag >= 0 && _pick.Tag < _tags.Count ? _tags[_pick.Tag] : null;
            if (_flare != null && file != null)
            {
                _flare.Preview(DraftOf(file));
            }
            _dirty = true;
            return true;
        }

        private bool OpenStory(int arg)
        {
            if (arg < 0 || arg >= _stories.Count)
            {
                return false;
            }
            _focus = _stories[arg];
            _page = 0;
            _nav = 0;
            _dirty = true;
            return true;
        }

        private bool OpenAuthor(int arg)
        {
            if (arg < 0 || arg >= _shown.Count)
            {
                return false;
            }
            string name, handle;
            AuthorOf(_flare != null ? _flare.File : null, _shown[arg], out name, out handle);
            _focus = name;
            _page = 4;
            _nav = 4;
            _dirty = true;
            return true;
        }

        private bool React(int arg)
        {
            int index = arg / 3;
            int kind = arg % 3;
            if (_flare == null || index < 0 || index >= _shown.Count)
            {
                return false;
            }
            FlarePost p = _shown[index];
            if (kind == 1)
            {
                _replyTo = p.Id;
                _page = 7;
            }
            else if (kind == 2)
            {
                _flare.Repost(p.Id);
            }
            else
            {
                _flare.Like(p.Id);
            }
            _dirty = true;
            return true;
        }

        private bool FollowShown(int arg)
        {
            FlareFile file = _flare != null ? _flare.File : null;
            if (_flare == null || file == null || arg < 0)
            {
                return false;
            }
            int at = 0;
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf == null || !inf.Active || FlareCast.Followed(file, "i:" + inf.Id))
                {
                    continue;
                }
                if (at == arg)
                {
                    _flare.Follow("i:" + inf.Id, true);
                    _dirty = true;
                    return true;
                }
                at++;
                if (at >= 4)
                {
                    break;
                }
            }
            return false;
        }

        private bool OpenTag(int arg)
        {
            if (arg < 0 || arg >= _tags.Count)
            {
                return false;
            }
            _pick.Tag = arg;
            _pick.Tags = _tags[arg];
            _focus = null;
            _page = 1;
            _nav = 1;
            _dirty = true;
            return true;
        }

        private bool OpenMessage(int arg)
        {
            if (arg < 0 || arg >= _withs.Count)
            {
                return false;
            }
            _with = _withs[arg];
            if (_flare != null)
            {
                _flare.ReadDms(_with);
            }
            _page = 3;
            _dirty = true;
            return true;
        }

        private bool Field()
        {
            if (_page == 3 && _with != null && !string.IsNullOrEmpty(_draft))
            {
                return SendMessage();
            }
            FlareFile file = _flare != null ? _flare.File : null;
            if (_flare != null && file != null)
            {
                _flare.Preview(DraftOf(file));
            }
            _dirty = true;
            return true;
        }

        private bool Row(int arg)
        {
            FlareFile file = _flare != null ? _flare.File : null;
            if (file == null || arg < 0)
            {
                return false;
            }
            if (_page == 5)
            {
                if (arg >= file.Shops.Count)
                {
                    return false;
                }
                _pick.Shop = arg;
                _dirty = true;
                return true;
            }
            if (_page == 8)
            {
                int at = 0;
                for (int i = 0; i < file.Influencers.Count; i++)
                {
                    FlareInfluencer inf = file.Influencers[i];
                    if (inf == null || !inf.Active)
                    {
                        continue;
                    }
                    if (at == arg)
                    {
                        _hire = i;
                        _flare.HireExpect(inf, FlareUseRules.ChosenPlace(file.Shops, _pick.Shop));
                        _dirty = true;
                        return true;
                    }
                    at++;
                }
                return false;
            }
            if (_page == 2)
            {
                return AnswerNotice(file, arg);
            }
            if (_page == 4 && arg < _shown.Count)
            {
                _replyTo = _shown[arg].Id;
                _page = 7;
                _dirty = true;
                return true;
            }
            return false;
        }

        private bool AnswerNotice(FlareFile file, int arg)
        {
            if (_flare == null || string.IsNullOrEmpty(_draft))
            {
                return false;
            }
            List<FlareNotice> notes = _flare.Notices();
            if (notes == null || arg >= notes.Count || notes[arg].Kind != FlareNoticeKind.Review)
            {
                return false;
            }
            FlarePost p = file.Post(notes[arg].Post);
            if (p == null || string.IsNullOrEmpty(p.Place))
            {
                return false;
            }
            bool answered = _flare.AnswerReview(p.Place, p.Arg, _draft);
            if (answered)
            {
                _draft = string.Empty;
            }
            _dirty = true;
            return answered;
        }

        private bool Send()
        {
            if (_flare == null || string.IsNullOrEmpty(_draft))
            {
                return false;
            }
            FlareCompose compose = DraftOf(_flare.File);
            _flare.Preview(compose);
            string why;
            if (!_flare.Compose(compose, out why))
            {
                return false;
            }
            _draft = string.Empty;
            _replyTo = -1;
            _page = 0;
            _nav = 0;
            _dirty = true;
            return true;
        }

        private bool SendMessage()
        {
            if (_flare == null || _with == null || string.IsNullOrEmpty(_draft))
            {
                return false;
            }
            bool sent = _flare.SendDm(_with, _draft);
            if (sent)
            {
                _draft = string.Empty;
            }
            _dirty = true;
            return sent;
        }

        private FlareCompose DraftOf(FlareFile file)
        {
            var compose = new FlareCompose
            {
                Kind = _replyTo >= 0 ? FlareSource.Reply : FlareSource.Life,
                Text = _draft,
                Tone = FlareTone.Honest,
                ToneChosen = true,
                ReplyTo = _replyTo,
                PromoteDays = 1
            };
            FlareUseRules.ApplyPick(compose, file != null ? file.Shops : null, _pick);
            return compose;
        }

        private void Fill()
        {
            _model.Page = _page;
            _model.NavAt = _nav;
            _model.Post = UiText.T("reallife_flare_post");
            _model.Field = string.IsNullOrEmpty(_draft) ? UiText.T("reallife_flare_field") : _draft;
            _model.Nav = new[]
            {
                UiText.T("reallife_flare_nav_home"), UiText.T("reallife_flare_nav_explore"), UiText.T("reallife_flare_nav_notices"),
                UiText.T("reallife_flare_nav_messages"), UiText.T("reallife_flare_nav_profile"), UiText.T("reallife_flare_nav_business"),
                UiText.T("reallife_flare_nav_city"), UiText.T("reallife_flare_nav_hire"), UiText.T("reallife_flare_nav_studio")
            };
            _model.NavIcon = new[]
            {
                UiIcon.Home, UiIcon.Search, UiIcon.Bell, UiIcon.Message, UiIcon.Person, UiIcon.Store, UiIcon.Building, UiIcon.People, UiIcon.Chart
            };
            _model.Chips = new[]
            {
                UiText.T("reallife_flare_chip_photo"), UiText.T("reallife_flare_chip_place"),
                UiText.T("reallife_flare_chip_tag"), UiText.T("reallife_flare_chip_promote")
            };
            _model.EmptyTitle = UiText.T("reallife_flare_empty_feed");
            _model.EmptyBody = UiText.T("reallife_flare_empty_following");
            _model.Action = _page == 7 ? _model.Post : null;
            _model.Hint = null;
            _model.Messages = null;
            _model.Feed = null;
            _model.Head = null;
            _model.Links = 0;
            FlareFile file = _flare != null ? _flare.File : null;
            if (file == null)
            {
                _model.Feed = new UiFeedModel { EmptyTitle = _model.EmptyTitle, EmptyBody = _model.EmptyBody, Profile = Self(null) };
                return;
            }
            if (_page == 0 || _page == 1)
            {
                _model.Feed = FeedOf(file, _page == 1);
                Stories(file);
                Hint(file);
            }
            else if (_page == 3)
            {
                _model.Messages = Messages(file);
            }
            else if (_page == 4)
            {
                Profile(file);
            }
            else if (_page == 2)
            {
                Notices(file);
            }
            else if (_page == 5)
            {
                Business(file);
            }
            else if (_page == 8)
            {
                Hire(file);
            }
            else if (_page == 6)
            {
                City(file);
            }
            else if (_page == 10)
            {
                Studio(file);
            }
            else
            {
                Expect(file);
            }
        }

        private void Hint(FlareFile file)
        {
            if (_pick.Shop < 0 && _pick.Photo < 0 && _pick.Tag < 0 && _pick.Promote <= 0)
            {
                return;
            }
            FlareExpect ex = _flare.Preview(DraftOf(file));
            _model.Hint = UiText.Fill("reallife_flare_preview", "visits", UiText.Whole(ex.Visitors), "money", UiText.Whole(ex.Cents / 100), "hours", UiText.Whole(ex.Hours));
        }

        private UiFeedProfile Self(FlareFile file)
        {
            int followers = file != null ? file.Followers : 0;
            string name = _flare != null ? _flare.MyName : UiText.T("reallife_flare_you");
            string handle = _flare != null ? _flare.MyHandle : FlareText.HandleOf(name);
            int reach = file != null && file.ReachDays.Length > 0 ? file.ReachDays[file.ReachDays.Length - 1] : 0;
            return new UiFeedProfile
            {
                Name = name,
                Handle = handle,
                Hue = HueOf(name),
                About = file != null ? file.Bio : null,
                FactLabels = new[] { UiText.T("reallife_flare_followers"), UiText.T("reallife_flare_city_fig_reach"), UiText.T("reallife_flare_rating") },
                FactValues = new[] { UiText.Whole(followers), UiText.Whole(reach), RatingText(file) },
                ListTitle = UiText.T("reallife_flare_trend"),
                List = Topics(file),
                PeopleTitle = UiText.T("reallife_flare_nav_hire"),
                People = People(file)
            };
        }

        private UiFeedModel FeedOf(FlareFile file, bool others)
        {
            _shown.Clear();
            for (int i = file.Posts.Count - 1; i >= 0 && _shown.Count < FeedMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p == null || p.ReplyTo >= 0)
                {
                    continue;
                }
                if (others && p.Author == FlareAuthor.Player)
                {
                    continue;
                }
                string name, handle;
                AuthorOf(file, p, out name, out handle);
                if (_focus != null && name != _focus)
                {
                    continue;
                }
                _shown.Add(p);
            }
            var posts = new UiFeedPost[_shown.Count];
            int minute = NowMinute();
            for (int i = 0; i < _shown.Count; i++)
            {
                FlarePost p = _shown[i];
                string name, handle;
                AuthorOf(file, p, out name, out handle);
                posts[i] = new UiFeedPost
                {
                    Author = name,
                    Handle = handle,
                    Time = UiText.Whole(p.Day) + " " + UiText.Whole(p.Hour),
                    Text = string.IsNullOrEmpty(p.Text) ? UiText.T("reallife_flare_field") : p.Text,
                    Picture = string.IsNullOrEmpty(p.Pic) ? UiIcon.None : UiIcon.Image,
                    Caption = ResultOf(file, p.Id, minute),
                    Likes = UiText.Whole(p.Likes),
                    Comments = UiText.Whole(p.Replies),
                    Shares = UiText.Whole(p.Reposts),
                    Liked = p.Liked,
                    Hue = HueOf(name),
                    Icon = p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Brand ? UiIcon.Store : UiIcon.None
                };
            }
            return new UiFeedModel
            {
                Posts = posts,
                PostCount = posts.Length,
                EmptyTitle = _model.EmptyTitle,
                EmptyBody = _model.EmptyBody,
                Profile = Self(file)
            };
        }

        private string ResultOf(FlareFile file, int id, int minute)
        {
            FlareBoost boost = BoostOf(file, id);
            if (boost == null || !FlareUseRules.HourRan(boost, minute))
            {
                return null;
            }
            int visits, cents;
            FlareUseRules.Credited(boost, minute, out visits, out cents);
            return UiText.Fill("reallife_flare_result", "visits", UiText.Whole(visits), "money", UiText.Whole(cents / 100));
        }

        private static FlareBoost BoostOf(FlareFile file, int id)
        {
            for (int i = 0; i < file.Boosts.Count; i++)
            {
                if (file.Boosts[i].Post == id)
                {
                    return file.Boosts[i];
                }
            }
            return null;
        }

        private void Stories(FlareFile file)
        {
            _stories.Clear();
            for (int i = file.Posts.Count - 1; i >= 0 && _stories.Count < 6; i--)
            {
                if (file.Posts[i] == null || file.Posts[i].ReplyTo >= 0)
                {
                    continue;
                }
                string name, handle;
                AuthorOf(file, file.Posts[i], out name, out handle);
                if (!_stories.Contains(name))
                {
                    _stories.Add(name);
                }
            }
            var names = new string[_stories.Count];
            _model.StoryHue = new float[_stories.Count];
            for (int i = 0; i < _stories.Count; i++)
            {
                names[i] = _stories[i];
                _model.StoryHue[i] = HueOf(_stories[i]);
            }
            _model.Story = names;
        }

        private void AuthorOf(FlareFile file, FlarePost p, out string name, out string handle)
        {
            if (p.Author == FlareAuthor.Player)
            {
                name = _flare != null ? _flare.MyName : UiText.T("reallife_flare_you");
                handle = _flare != null ? _flare.MyHandle : FlareText.HandleOf(name);
                return;
            }
            if (p.Author == FlareAuthor.Influencer && file != null)
            {
                FlareInfluencer inf = file.Influencer(p.Who);
                if (inf != null && !string.IsNullOrEmpty(inf.Handle))
                {
                    handle = inf.Handle;
                    name = handle.Length > 1 && handle[0] == '@' ? handle.Substring(1) : handle;
                    return;
                }
            }
            if (p.Author == FlareAuthor.Ledger)
            {
                name = UiText.T("reallife_flare_ledger_name");
                handle = "@cityledger";
                return;
            }
            name = !string.IsNullOrEmpty(p.AuthorName) ? p.AuthorName : !string.IsNullOrEmpty(p.Name) ? p.Name : UiText.T("reallife_flare_you");
            handle = p.Author == FlareAuthor.Network ? FlareText.HandleOf(p.AuthorId ?? name) : FlareText.HandleOf(name);
        }

        private string RatingText(FlareFile file)
        {
            if (file == null || _flare == null)
            {
                return UiText.Whole(0);
            }
            FlareStarRow row = _flare.StarsFor(FlareUseRules.ChosenPlace(file.Shops, _pick.Shop));
            if (row == null)
            {
                for (int i = 0; i < file.Shops.Count && row == null; i++)
                {
                    row = _flare.StarsFor(file.Shops[i].Place);
                }
            }
            if (row == null || row.Total <= 0)
            {
                return UiText.Whole(0);
            }
            int sum = row.One + row.Two * 2 + row.Three * 3 + row.Four * 4 + row.Five * 5;
            return UiText.Fill("reallife_flare_stars", "stars", UiText.Whole(sum / row.Total));
        }

        private string[] Topics(FlareFile file)
        {
            _tags.Clear();
            if (file == null)
            {
                return new string[0];
            }
            for (int i = file.Posts.Count - 1; i >= 0 && _tags.Count < 6; i--)
            {
                string topic = file.Posts[i].Topic;
                if (string.IsNullOrEmpty(topic))
                {
                    continue;
                }
                int sp = topic.IndexOf(' ');
                string one = sp < 0 ? topic : topic.Substring(0, sp);
                if (one.Length > 0 && !_tags.Contains(one))
                {
                    _tags.Add(one);
                }
            }
            var list = new string[_tags.Count];
            for (int i = 0; i < _tags.Count; i++)
            {
                list[i] = _tags[i];
            }
            return list;
        }

        private UiPersonRow[] People(FlareFile file)
        {
            if (file == null)
            {
                return new UiPersonRow[0];
            }
            int n = 0;
            for (int i = 0; i < file.Influencers.Count && n < 4; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf != null && inf.Active && !FlareCast.Followed(file, "i:" + inf.Id))
                {
                    n++;
                }
            }
            var rows = new UiPersonRow[n];
            int at = 0;
            for (int i = 0; i < file.Influencers.Count && at < n; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf == null || !inf.Active || FlareCast.Followed(file, "i:" + inf.Id))
                {
                    continue;
                }
                string handle = string.IsNullOrEmpty(inf.Handle) ? UiText.T("reallife_flare_nav_hire") : inf.Handle;
                rows[at++] = new UiPersonRow
                {
                    Name = handle,
                    Hue = HueOf(handle),
                    Role = UiText.Whole(inf.Followers),
                    RoleIcon = UiIcon.Person
                };
            }
            return rows;
        }

        private UiMessageListModel Messages(FlareFile file)
        {
            _withs.Clear();
            var rows = new List<UiMessageRow>(12);
            if (_with != null)
            {
                _withs.Add(_with);
                for (int i = 0; i < file.Net.Dms.Count && rows.Count < 12; i++)
                {
                    FlareDm m = file.Net.Dms[i];
                    if (m == null || m.With != _with)
                    {
                        continue;
                    }
                    rows.Add(new UiMessageRow
                    {
                        Name = m.Mine ? UiText.T("reallife_flare_you") : (string.IsNullOrEmpty(m.Name) ? m.With : m.Name),
                        Preview = DmWords(m),
                        Time = When(m.At),
                        Hue = HueOf(m.Name),
                        Unread = !m.Read && !m.Mine ? 1 : 0
                    });
                }
            }
            else
            {
                for (int i = file.Net.Dms.Count - 1; i >= 0 && _withs.Count < 12; i--)
                {
                    FlareDm m = file.Net.Dms[i];
                    if (m == null || string.IsNullOrEmpty(m.With) || _withs.Contains(m.With))
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(_draft) && !Holds(m.Name, _draft) && !Holds(m.With, _draft) && !Holds(DmWords(m), _draft))
                    {
                        continue;
                    }
                    _withs.Add(m.With);
                    int unread = 0;
                    FlareDm newest = m;
                    for (int k = 0; k < file.Net.Dms.Count; k++)
                    {
                        FlareDm d = file.Net.Dms[k];
                        if (d == null || d.With != m.With)
                        {
                            continue;
                        }
                        if (!d.Read && !d.Mine)
                        {
                            unread++;
                        }
                        if (d.At >= newest.At)
                        {
                            newest = d;
                        }
                    }
                    rows.Add(new UiMessageRow
                    {
                        Name = string.IsNullOrEmpty(newest.Name) ? newest.With : newest.Name,
                        Preview = DmWords(newest),
                        Time = When(newest.At),
                        Hue = HueOf(newest.Name),
                        Unread = unread,
                        Section = rows.Count == 0 ? UiText.T("reallife_flare_nav_messages") : null
                    });
                }
            }
            var built = new UiMessageRow[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                built[i] = rows[i];
            }
            return new UiMessageListModel
            {
                SearchPlaceholder = UiText.T("reallife_flare_field"),
                Query = _with == null ? _draft : null,
                Rows = built,
                RowCount = built.Length,
                EmptyTitle = _model.EmptyTitle,
                EmptyBody = _model.EmptyBody
            };
        }

        private static string DmWords(FlareDm m)
        {
            if (m == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(m.Text))
            {
                return m.Text;
            }
            return !string.IsNullOrEmpty(m.Key) ? UiText.T(m.Key) : string.Empty;
        }

        private void Notices(FlareFile file)
        {
            List<FlareNotice> notes = _flare.Notices();
            int n = 0;
            if (notes != null)
            {
                for (int i = 0; i < notes.Count && n < 8; i++)
                {
                    n++;
                }
            }
            _model.Head = UiText.T("reallife_flare_nav_notices");
            if (n == 0)
            {
                _model.Label = new string[0];
                _model.Value = new string[0];
                return;
            }
            _model.Label = new string[n];
            _model.Value = new string[n];
            _model.Links = n;
            for (int i = 0; i < n; i++)
            {
                FlareNotice note = notes[i];
                string who = string.IsNullOrEmpty(note.Who) ? UiText.T("reallife_flare_you") : note.Who;
                _model.Label[i] = UiText.Fill(NoticeKey(note.Kind), "name", who);
                _model.Value[i] = When(note.At);
            }
        }

        private static string NoticeKey(FlareNoticeKind kind)
        {
            switch (kind)
            {
                case FlareNoticeKind.Reply: return "reallife_flare_notice_reply";
                case FlareNoticeKind.Repost: return "reallife_flare_notice_repost";
                case FlareNoticeKind.Mention: return "reallife_flare_notice_mention";
                case FlareNoticeKind.Case: return "reallife_flare_notice_case";
                case FlareNoticeKind.Dm: return "reallife_flare_notice_dm";
                case FlareNoticeKind.Going: return "reallife_flare_notice_going";
                case FlareNoticeKind.Review: return "reallife_flare_notice_review";
                default: return "reallife_flare_notice_like";
            }
        }

        private void Profile(FlareFile file)
        {
            _shown.Clear();
            for (int i = file.Posts.Count - 1; i >= 0 && _shown.Count < 6; i--)
            {
                FlarePost p = file.Posts[i];
                if (p == null || p.ReplyTo >= 0)
                {
                    continue;
                }
                string name, handle;
                AuthorOf(file, p, out name, out handle);
                bool mine = _focus == null ? p.Author == FlareAuthor.Player : name == _focus;
                if (mine)
                {
                    _shown.Add(p);
                }
            }
            int rows = 3 + _shown.Count;
            _model.Head = _focus ?? (_flare != null ? _flare.MyName : UiText.T("reallife_flare_you"));
            _model.Label = new string[rows];
            _model.Value = new string[rows];
            for (int i = 0; i < _shown.Count; i++)
            {
                _model.Label[i] = string.IsNullOrEmpty(_shown[i].Text) ? UiText.T("reallife_flare_field") : _shown[i].Text;
                _model.Value[i] = UiText.Whole(_shown[i].Likes);
            }
            _model.Label[_shown.Count] = UiText.T("reallife_flare_followers");
            _model.Value[_shown.Count] = UiText.Whole(file.Followers);
            _model.Label[_shown.Count + 1] = UiText.T("reallife_flare_posts");
            _model.Value[_shown.Count + 1] = UiText.Whole(file.OwnPosts);
            _model.Label[_shown.Count + 2] = UiText.T("reallife_flare_rating");
            _model.Value[_shown.Count + 2] = RatingText(file);
            _model.Links = _shown.Count;
        }

        private void Business(FlareFile file)
        {
            _model.Head = UiText.T("reallife_flare_nav_business");
            if (_pick.Shop < 0)
            {
                int n = file.Shops.Count > 8 ? 8 : file.Shops.Count;
                _model.Label = new string[n];
                _model.Value = new string[n];
                _model.Links = n;
                for (int i = 0; i < n; i++)
                {
                    FlareShopSeen shop = file.Shops[i];
                    string shopPlace = shop != null ? shop.Place : null;
                    _model.Label[i] = string.IsNullOrEmpty(shopPlace) ? UiText.T("reallife_flare_nav_business") : shopPlace;
                    _model.Value[i] = RatingTextAt(shopPlace);
                }
                return;
            }
            string place = FlareUseRules.ChosenPlace(file.Shops, _pick.Shop);
            FlareStarRow row = _flare.StarsFor(place);
            _model.Label = new string[7];
            _model.Value = new string[7];
            _model.Label[0] = UiText.T("reallife_flare_rating");
            _model.Value[0] = RatingTextAt(place);
            for (int s = 1; s <= 5; s++)
            {
                _model.Label[s] = UiText.Fill("reallife_flare_star_line", "stars", UiText.Whole(6 - s));
                _model.Value[s] = UiText.Whole(row != null ? row.Count(6 - s) : 0);
            }
            _model.Label[6] = UiText.T("reallife_flare_visits");
            _model.Value[6] = UiText.Whole(CreditedAt(file, place));
        }

        private string RatingTextAt(string place)
        {
            FlareStarRow row = _flare != null ? _flare.StarsFor(place) : null;
            if (row == null || row.Total <= 0)
            {
                return UiText.Whole(0);
            }
            int sum = row.One + row.Two * 2 + row.Three * 3 + row.Four * 4 + row.Five * 5;
            return UiText.Fill("reallife_flare_stars", "stars", UiText.Whole(sum / row.Total));
        }

        private static int CreditedAt(FlareFile file, string place)
        {
            int minute = NowMinute();
            int visits = 0;
            for (int i = 0; i < file.Boosts.Count; i++)
            {
                FlareBoost b = file.Boosts[i];
                if (b == null || b.Place != place)
                {
                    continue;
                }
                int v, c;
                FlareUseRules.Credited(b, minute, out v, out c);
                visits += v;
            }
            return visits;
        }

        private void Hire(FlareFile file)
        {
            int n = 0;
            for (int i = 0; i < file.Influencers.Count && n < 8; i++)
            {
                if (file.Influencers[i] != null && file.Influencers[i].Active)
                {
                    n++;
                }
            }
            _model.Head = UiText.T("reallife_flare_nav_hire");
            _model.Label = new string[n];
            _model.Value = new string[n];
            _model.Links = n;
            int at = 0;
            for (int i = 0; i < file.Influencers.Count && at < n; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf == null || !inf.Active)
                {
                    continue;
                }
                string line = UiText.Whole(inf.Followers);
                if (i == _hire)
                {
                    FlareExpect ex = _flare.LastExpect;
                    line = UiText.Whole(ex.Visitors) + " " + UiText.Whole(ex.Cents / 100);
                }
                _model.Label[at] = string.IsNullOrEmpty(inf.Handle) ? UiText.T("reallife_flare_nav_hire") : inf.Handle;
                _model.Value[at] = line;
                at++;
            }
        }

        private void City(FlareFile file)
        {
            int minute = NowMinute();
            int visits = 0;
            int cents = 0;
            for (int i = 0; i < file.Boosts.Count; i++)
            {
                int v, c;
                FlareUseRules.Credited(file.Boosts[i], minute, out v, out c);
                visits += v;
                cents += c;
            }
            int reach = file.ReachDays.Length > 0 ? file.ReachDays[file.ReachDays.Length - 1] : 0;
            _model.Head = UiText.T("reallife_flare_nav_city");
            _model.Label = new[]
            {
                UiText.T("reallife_flare_followers"), UiText.T("reallife_flare_city_fig_reach"), UiText.T("reallife_flare_visits"),
                UiText.T("reallife_flare_takings"), UiText.T("reallife_flare_trend"), UiText.T("reallife_flare_terms")
            };
            _model.Value = new[]
            {
                UiText.Whole(file.Followers), UiText.Whole(reach), UiText.Whole(visits), UiText.Whole(cents / 100),
                UiText.Whole(_flare.PostDemand()), UiText.Whole(_flare.TrendRows + _flare.OfferRows)
            };
        }

        private void Studio(FlareFile file)
        {
            int followers = file.Followers;
            int next = FlareUseRules.NextPerkAt(followers);
            _model.Head = UiText.T("reallife_flare_nav_studio");
            _model.Label = new[]
            {
                UiText.T("reallife_flare_followers"), UiText.T("reallife_flare_tier"), UiText.T("reallife_flare_perk_sponsor"),
                UiText.T("reallife_flare_perk_job"), UiText.T("reallife_flare_perk_raise"), UiText.T("reallife_flare_perk_investor")
            };
            _model.Value = new[]
            {
                UiText.Whole(followers), next < 0 ? UiText.T("reallife_flare_perk_vip") : UiText.Whole(next),
                FlareUseRules.Sponsorships(followers) ? UiText.T("reallife_flare_yes") : UiText.T("reallife_flare_no"),
                UiText.Whole(FlareUseRules.JobExtra(followers)), UiText.Whole(FlareUseRules.RaiseBp(followers)),
                FlareUseRules.InvestorChance(followers) > 0 ? UiText.T("reallife_flare_yes") : UiText.T("reallife_flare_no")
            };
        }

        private void Expect(FlareFile file)
        {
            FlareExpect ex = _flare.Preview(DraftOf(file));
            _model.Head = UiText.T("reallife_flare_expect");
            _model.Label = new[]
            {
                UiText.T("reallife_flare_visits"), UiText.T("reallife_flare_takings"), UiText.T("reallife_flare_hours"),
                UiText.T("reallife_flare_cost"), UiText.T("reallife_flare_gain"), UiText.T("reallife_flare_city_fig_reach")
            };
            _model.Value = new[]
            {
                UiText.Whole(ex.Visitors), UiText.Whole(ex.Cents / 100), UiText.Whole(ex.Hours),
                UiText.Whole(ex.Cost / 100), UiText.Whole(ex.Gain / 100), UiText.Whole(FlareUseRules.PromoteOf(_pick.Promote))
            };
        }

        private void Collect(FlareFile file)
        {
            _pics.Clear();
            if (file != null)
            {
                for (int i = file.Posts.Count - 1; i >= 0 && _pics.Count < 8; i--)
                {
                    string pic = file.Posts[i].Pic;
                    if (!string.IsNullOrEmpty(pic) && !_pics.Contains(pic))
                    {
                        _pics.Add(pic);
                    }
                }
                for (int i = 0; i < file.Drafts.Count && _pics.Count < 8; i++)
                {
                    FlareDraft d = file.Drafts[i];
                    if (d != null && (d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name))
                    {
                        string pic = "f:" + d.Name;
                        if (!_pics.Contains(pic))
                        {
                            _pics.Add(pic);
                        }
                    }
                }
            }
            try
            {
                string folder = FlarePictures.Folder;
                if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
                {
                    string[] files = System.IO.Directory.GetFiles(folder, "*.jpg");
                    for (int i = 0; i < files.Length && _pics.Count < 8; i++)
                    {
                        string pic = "f:" + System.IO.Path.GetFileName(files[i]);
                        if (!_pics.Contains(pic))
                        {
                            _pics.Add(pic);
                        }
                    }
                }
            }
            catch (System.IO.IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            if (_tags.Count == 0 && file != null)
            {
                Topics(file);
            }
            if (_tags.Count == 0)
            {
                string place = file != null ? FlareUseRules.ChosenPlace(file.Shops, _pick.Shop) : null;
                string tag = Letters(place);
                if (!string.IsNullOrEmpty(tag))
                {
                    _tags.Add(tag);
                }
            }
        }

        private static string Letters(string place)
        {
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            var b = new System.Text.StringBuilder(place.Length);
            for (int i = 0; i < place.Length && b.Length < 24; i++)
            {
                char c = place[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    b.Append(c);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        private static bool Holds(string text, string part)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string When(long at)
        {
            if (at > int.MaxValue)
            {
                at = int.MaxValue;
            }
            if (at < 0)
            {
                at = 0;
            }
            return UiText.Whole((int)at);
        }

        private static int NowMinute()
        {
            return FlareGame.Day * 1440 + FlareGame.Hour * 60;
        }

        private static float HueOf(string name)
        {
            int h = 0;
            if (name != null)
            {
                for (int i = 0; i < name.Length; i++)
                {
                    h = h * 31 + name[i];
                }
            }
            int m = h % 360;
            return m < 0 ? m + 360 : m;
        }
    }
}
