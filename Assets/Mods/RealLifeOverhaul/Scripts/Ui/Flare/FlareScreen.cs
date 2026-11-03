using System;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face over the phone app. It reads the account when the account changes and lays the social shell.
    /// Typing goes through the host's text box. Cost: nothing per frame; a layout when the account or the page changes.
    /// </summary>
    public sealed class FlareScreen : IUiScreen, IUiTypingSubmit
    {
        private readonly FlareDirector _flare;
        private readonly FlareFaceModel _model = new FlareFaceModel();
        private readonly System.Collections.Generic.List<FlarePost> _shown = new System.Collections.Generic.List<FlarePost>(8);
        private int _revision = -1;
        private int _page;
        private string _draft = string.Empty;
        private bool _dirty = true;

        public FlareScreen(FlareDirector flare)
        {
            _flare = flare;
            if (flare != null && flare.KitPage >= 0)
            {
                _page = flare.KitPage;
                flare.KitPage = -1;
            }
        }

        public string Id => "flare";

        public bool Dirty => _dirty || (_flare != null && _flare.Revision != _revision);

        public int TypingCommand => _page == 0 || _page == 7 || _page == 3 ? FlareFace.FieldCommand : -1;

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
            if (_page == 7 || _page == 0)
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
                _page = arg == 8 ? 10 : arg;
                _dirty = true;
                return true;
            }
            if (command == FlareFace.PostCommand)
            {
                _page = 7;
                _dirty = true;
                return true;
            }
            if (command == FlareFace.ActionCommand && _page == 7)
            {
                return Send();
            }
            if (command == FlareFace.RowCommand && _page == 2 && _flare != null && _flare.File != null)
            {
                return AnswerShown(arg);
            }
            return command == FlareFace.ChipCommand || command == FlareFace.StoryCommand || command == UiFeed.AuthorCommand || command == UiFeed.ReactCommand
                || command == UiMessageList.RowCommand || command == FlareFace.FieldCommand;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_page != 0)
            {
                _page = 0;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int n = 9;
            _page = (_page + (step < 0 ? n - 1 : 1)) % n;
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

        private bool Send()
        {
            if (_flare == null || string.IsNullOrEmpty(_draft))
            {
                return false;
            }
            var compose = new FlareCompose { Kind = FlareSource.Life, Text = _draft, Tone = FlareTone.Honest, ToneChosen = true };
            if (_flare.File != null && _flare.File.Shops.Count > 0)
            {
                compose.Place = _flare.File.Shops[0].Place;
                compose.Name = _flare.File.Shops[0].Place;
            }
            _flare.Preview(compose);
            string why;
            if (!_flare.Compose(compose, out why))
            {
                return false;
            }
            _draft = string.Empty;
            _page = 0;
            _dirty = true;
            return true;
        }

        private bool AnswerShown(int arg)
        {
            FlareFile file = _flare.File;
            if (file == null || arg < 0 || arg >= file.Answers.Count)
            {
                return false;
            }
            return _flare.AnswerReview(file.Answers[arg].Key, file.Answers[arg].Stars, _draft.Length > 0 ? _draft : file.Answers[arg].Answer);
        }

        private void Fill()
        {
            _model.Page = _page;
            _model.NavAt = _page == 10 ? 8 : _page;
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
            _model.Chips = new[] { UiText.T("reallife_flare_chip_photo"), UiText.T("reallife_flare_chip_place"), UiText.T("reallife_flare_chip_tag"), UiText.T("reallife_flare_chip_promote") };
            _model.EmptyTitle = UiText.T("reallife_flare_empty_feed");
            _model.EmptyBody = UiText.T("reallife_flare_empty_following");
            _model.Action = _page == 7 ? _model.Post : null;
            FlareFile file = _flare != null ? _flare.File : null;
            if (file == null)
            {
                _model.Feed = new UiFeedModel { EmptyTitle = _model.EmptyTitle, EmptyBody = _model.EmptyBody, Profile = Self(null) };
                return;
            }
            if (_page == 0 || _page == 1)
            {
                _model.Feed = FeedOf(file);
                _model.Story = Stories(file);
            }
            else if (_page == 3)
            {
                _model.Messages = new UiMessageListModel { SearchPlaceholder = UiText.T("reallife_flare_field"), EmptyTitle = _model.EmptyTitle, EmptyBody = _model.EmptyBody };
            }
            else
            {
                Rows(file);
            }
        }

        private UiFeedProfile Self(FlareFile file)
        {
            int followers = file != null ? file.Followers : 0;
            return new UiFeedProfile
            {
                Name = UiText.T("reallife_flare_you"),
                Handle = "@me",
                Hue = 28f,
                FactLabels = new[] { UiText.T("reallife_flare_followers"), UiText.T("reallife_flare_posts") },
                FactValues = new[] { UiText.Whole(followers), file != null ? UiText.Whole(file.TotalPosts) : "0" }
            };
        }

        private UiFeedModel FeedOf(FlareFile file)
        {
            _shown.Clear();
            for (int i = file.Posts.Count - 1; i >= 0 && _shown.Count < 4; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.ReplyTo < 0)
                {
                    _shown.Add(p);
                }
            }
            var posts = new UiFeedPost[_shown.Count];
            for (int i = 0; i < _shown.Count; i++)
            {
                FlarePost p = _shown[i];
                FlareBoost boost = BoostOf(file, p.Id);
                int visits = 0;
                int cents = 0;
                if (boost != null)
                {
                    visits = boost.GivenVisitors > 0 ? boost.GivenVisitors : boost.Visitors;
                    cents = boost.GivenCents > 0 ? boost.GivenCents : boost.Cents;
                }
                string caption = visits > 0 || cents > 0
                    ? UiText.Fill("reallife_flare_result", "visits", UiText.Whole(visits), "money", UiText.Whole(cents / 100))
                    : null;
                posts[i] = new UiFeedPost
                {
                    Author = string.IsNullOrEmpty(p.Name) ? UiText.T("reallife_flare_you") : p.Name,
                    Handle = "@me",
                    Time = UiText.Whole(p.Hour),
                    Text = string.IsNullOrEmpty(p.Text) ? UiText.T("reallife_flare_field") : p.Text,
                    Picture = string.IsNullOrEmpty(p.Pic) ? UiIcon.None : UiIcon.Image,
                    Caption = caption,
                    Likes = UiText.Whole(p.Likes),
                    Comments = UiText.Whole(p.Replies),
                    Shares = UiText.Whole(p.Reposts),
                    Liked = p.Liked,
                    Hue = 28f
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

        private string[] Stories(FlareFile file)
        {
            int n = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && n < 6; i--)
            {
                n += file.Posts[i].ReplyTo < 0 ? 1 : 0;
            }
            var names = new string[n];
            int at = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && at < n; i--)
            {
                if (file.Posts[i].ReplyTo < 0)
                {
                    names[at++] = string.IsNullOrEmpty(file.Posts[i].Name) ? UiText.T("reallife_flare_you") : file.Posts[i].Name;
                }
            }
            _model.StoryHue = new float[n];
            for (int i = 0; i < n; i++)
            {
                _model.StoryHue[i] = 20f + i * 30f;
            }
            return names;
        }

        private void Rows(FlareFile file)
        {
            int followers = file.Followers;
            int next = FlareUseRules.NextPerkAt(followers);
            FlareExpect ex = _flare != null ? _flare.LastExpect : default(FlareExpect);
            if (_page == 7)
            {
                _model.Head = UiText.T("reallife_flare_expect");
                _model.Label = new[]
                {
                    UiText.T("reallife_flare_visits"), UiText.T("reallife_flare_takings"), UiText.T("reallife_flare_hours"),
                    UiText.T("reallife_flare_cost"), UiText.T("reallife_flare_gain")
                };
                _model.Value = new[]
                {
                    UiText.Whole(ex.Visitors), UiText.Whole(ex.Cents / 100), UiText.Whole(ex.Hours),
                    UiText.Whole(ex.Cost / 100), UiText.Whole(ex.Gain / 100)
                };
            }
            else if (_page == 6 || _page == 10)
            {
                int reach = file.ReachDays[file.ReachDays.Length - 1];
                int visits = 0;
                int cents = 0;
                for (int i = 0; i < file.Boosts.Count; i++)
                {
                    visits += file.Boosts[i].GivenVisitors > 0 ? file.Boosts[i].GivenVisitors : file.Boosts[i].Visitors;
                    cents += file.Boosts[i].GivenCents > 0 ? file.Boosts[i].GivenCents : file.Boosts[i].Cents;
                }
                _model.Head = UiText.T("reallife_flare_nav_city");
                _model.Label = new[]
                {
                    UiText.T("reallife_flare_followers"), UiText.T("reallife_flare_city_fig_reach"), UiText.T("reallife_flare_visits"),
                    UiText.T("reallife_flare_takings"), UiText.T("reallife_flare_trend"), UiText.T("reallife_flare_tier")
                };
                _model.Value = new[]
                {
                    UiText.Whole(followers), UiText.Whole(reach), UiText.Whole(visits), UiText.Whole(cents / 100),
                    UiText.Whole(FlareUseRules.TrendDemand(file.Posts.Count)), next < 0 ? UiText.T("reallife_flare_perk_vip") : UiText.Whole(next)
                };
            }
            else
            {
                _model.Head = null;
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
            _model.Links = 0;
        }
    }
}
