using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's director, the player's own: posts, likes, shares, invitations, messages and notifications.</summary>
    public sealed partial class FlareDirector
    {
        /// <summary>The player posts from the compose sheet; false, with the reason's key, only for a post that is not whole.</summary>
        public bool Compose(FlareCompose c, out string why)
        {
            why = null;
            if (!Running || Save == null || c == null)
            {
                why = "reallife_flare_off_title";
                return false;
            }
            try
            {
                int day = Save.Day, minute = FlareGame.MinuteOfDay;
                string text = FlareLiveRules.Clip(c.Text);
                FlarePost p;
                switch (c.Kind)
                {
                    case FlareSource.Reply:
                        p = Reply(c, text, day, out why);
                        break;
                    case FlareSource.Report:
                        p = Report(c, text, day, out why);
                        break;
                    default:
                        p = Built(c, day, minute, out why);
                        break;
                }
                if (p == null)
                {
                    Changed();
                    return false;
                }
                p.Text = text;
                p.Pic = c.Pic ?? p.Pic;
                p.Mention = c.Mention;
                p.Minute = minute % 60;
                if (p.District == null)
                {
                    p.District = c.District ?? FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
                }
                p.Topic = FlareLiveRules.TagsOf(text, Join(c.Tags, BuiltTag(p)));
                PostLive(p);
                Logger?.Info("Flare: the player posts (" + p.Source + (p.Place != null ? ", " + p.Name : string.Empty) + (text != null ? ", " + text.Length + " characters typed" : string.Empty)
                             + (p.Pic != null ? ", a picture" : string.Empty) + (p.Topic != null ? ", #" + p.Topic.Replace(" ", " #") : string.Empty) + ").");
                Changed();
                return true;
            }
            catch (Exception e)
            {
                Fault("a post", e);
                return false;
            }
        }

        private static string Join(string a, string b)
        {
            return string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " " + b;
        }

        private static string BuiltTag(FlarePost p)
        {
            switch (p.Source)
            {
                case FlareSource.Report: return FlareLiveRules.ReportTag((FlareReport)FlareRules.Clamp(p.Arg, 0, (int)FlareReport.Utilities));
                case FlareSource.Review: return "review";
                case FlareSource.Announce: return p.Arg == (int)FlareAnnounce.Event ? "events" : "shoplocal";
                default: return null;
            }
        }

        // A moment, a review or an announcement: a post by the account's own rules.
        private FlarePost Built(FlareCompose c, int day, int minute, out string why)
        {
            why = null;
            FlareDraft d = c.Draft >= 0 ? _file.Draft(c.Draft) : null;
            if (c.Kind == FlareSource.Review && (c.Place == null || c.Arg < 1 || c.Arg > 5))
            {
                why = "reallife_flare_compose_need_place";
                return null;
            }
            if (c.Kind == FlareSource.Review && FlareGame.Business(c.Place) != null)
            {
                why = "reallife_flare_compose_own_review";
                return null;
            }
            if (c.Kind == FlareSource.Announce && (c.Place == null || FlareGame.Business(c.Place) == null))
            {
                why = "reallife_flare_compose_need_mine";
                return null;
            }
            if (d == null)
            {
                d = new FlareDraft { Id = -1, Source = c.Kind, Day = day, Hour = Save.Hour, Place = c.Place, Name = c.Name, Arg = c.Arg };
            }
            FlareTone tone = c.ToneChosen ? c.Tone : FlareRules.BestTone(d.Source);
            ReadPeople();
            bool tag = c.Tag && d.Person >= 0;
            bool counts = _testViral || FlareRules.Counts(_file, day);
            FlarePost p = FlareRules.Post(_file, d, tone, tag ? d.Person : -1, _people, day, Save.Hour, minute, _testViral);
            _testViral = false;
            if (!counts)
            {
                Logger?.Info("Flare: post " + FlareRules.PostsToday(_file, day) + " of the day: past the first " + FlareRules.PostsCounted + ", seen and liked, the account as it was.");
            }
            if (d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie)
            {
                p.Pic = !string.IsNullOrEmpty(d.Name) ? "f:" + d.Name : null;
            }
            p.When = c.When;
            if (d.Id >= 0)
            {
                Influence(p, d);
                CityInfluence(p, d);
            }
            else if (p.Viral)
            {
                Viral(p, day);
            }
            if (p.Viral && FlareRules.MayNotify(_file, day, Settings.SocialMediaNotices))
            {
                Tell(NotificationType.Success, "reallife_flare_note_viral", _openFeed, "followers", FlareText.N(p.Gained));
            }
            return p;
        }

        // A report to the city: a post that gains nothing, and a case; the day's first three weigh in the council.
        private FlarePost Report(FlareCompose c, string text, int day, out string why)
        {
            why = null;
            string district = c.District ?? FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            if (district == null)
            {
                why = "reallife_flare_compose_need_district";
                return null;
            }
            if (c.Arg == (int)FlareReport.Business && c.Place == null)
            {
                why = "reallife_flare_compose_need_place";
                return null;
            }
            int id = _file.NextId++;
            FlareTone reportTone = c.ToneChosen ? c.Tone : FlareTone.Honest;
            int reach = Math.Max(1, FlareRules.Reach(_file.Followers, FlareRules.Quality(FlareSource.Report, false, false), FlareRules.ToneFit(FlareSource.Report, reportTone)));
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = Save.Hour, Author = FlareAuthor.Player, Source = FlareSource.Report, Tone = reportTone, Arg = c.Arg,
                Place = c.Place, Name = c.Name, District = district, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = reach,
                Likes = reach * FlareRules.LikeShare / 1000, Replies = reach / FlareRules.ReplyPer
            };
            _file.Posts.Add(p);
            _file.TotalPosts++;
            float x, z;
            FlareLiveGame.PlayerPoint(out x, out z);
            if (!Following)
            {
                FlareCase cs = FileCase(p, "me", c.District == null ? x : 0f, c.District == null ? z : 0f);
                if (cs != null && (cs.Kind == FlareReport.Road || cs.Kind == FlareReport.Crime) && FlareBridges.Issue != null && FlareLiveRules.Weighs(_file.Net, day))
                {
                    FlareBridges.Issue(cs.Kind == FlareReport.Crime ? (int)FlareIssue.Safety : (int)FlareIssue.Roads, FlareLiveRules.ReportsWeigh * FlareRules.IssueWeight(_file.Followers) / 1000);
                }
            }
            return p;
        }

        // A reply: typed words under a post; an honest answer to a poor review of the player's business wins a little back.
        private FlarePost Reply(FlareCompose c, string text, int day, out string why)
        {
            why = null;
            FlarePost parent = c.ReplyTo >= 0 ? _file.Post(c.ReplyTo) : null;
            if (parent == null)
            {
                why = "reallife_flare_compose_gone";
                return null;
            }
            if (text == null)
            {
                why = "reallife_flare_compose_need_words";
                return null;
            }
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = Save.Hour, Author = FlareAuthor.Player, Source = FlareSource.Reply, Tone = c.ToneChosen ? c.Tone : FlareTone.Honest, ReplyTo = parent.Id,
                Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = Math.Max(1, parent.Reach / 20), Place = parent.Place, Name = parent.Name
            };
            p.Likes = p.Reach / 15;
            _file.Posts.Add(p);
            if (parent.Source == FlareSource.Review && parent.Arg <= 2 && parent.Place != null && FlareGame.Business(parent.Place) != null && p.Tone != FlareTone.Cheeky
                && !_file.Net.Applied.Contains(-parent.Id))
            {
                _file.Net.Applied.Add(-parent.Id);   // once for each review
                Buzz(parent.Place, 1, FlareLiveRules.ReviewDays, day, FlareGame.Business(parent.Place));
                Logger?.Info("Flare: the player answered a poor review of " + parent.Name + " politely: +1% customers for " + FlareLiveRules.ReviewDays + " days.");
            }
            return p;
        }

        // After any post of the player's: who reacts and when, the city's replies, the business, a mention's answer, the session.
        private void PostLive(FlarePost p)
        {
            ReadPeople();
            Names(FlareBridges.Staff, _staff);
            int shift = FlareBridges.Ask(FlareBridges.OnShift, -1);
            string employer;
            int career = FlareBridges.Career != null ? FlareBridges.Career(out employer) : -1;
            bool boss = career >= 0 && FlareRules.BossFollows(_file.Seed, career, _file.Tier) && shift < 0;
            p.Reactors = FlareLiveRules.Reactors(p, _people, _staff, boss, _line);
            bool hostAnswers = FlareLiveRules.HostAnswers(Following, Settings.FlareSharePosts, p.Source);   // the host's replies come back with the post
            if (p.Source != FlareSource.Reply)
            {
                if (!hostAnswers)
                {
                    FlareLiveRules.CityReplies(_file, p, null);
                }
                if (FlareLiveRules.PileOn(p))
                {
                    bool paper = FlareLiveRules.StoryOnce(_file.Net, p.Day + 1, "reallife_flare_news_pileon", "name", MyName, "handle", MyHandle);
                    Logger?.Info("Flare: the post went badly and was seen by " + p.Reach + ": a pile-on of angry replies"
                                 + (paper ? ", and tomorrow's City Ledger has it." : " (the City Ledger has today's already)."));
                    if (FlareRules.MayNotify(_file, p.Day, Settings.SocialMediaNotices))
                    {
                        Tell(NotificationType.Warning, "reallife_flare_note_pileon", _openFeed);
                    }
                }
            }
            if (p.Source == FlareSource.Review || p.Source == FlareSource.Announce)
            {
                BusinessEffect(p, true);
            }
            if (p.Source == FlareSource.Announce && _tycoons.Count == 0)
            {
                ReadTycoons();
            }
            if (p.Source == FlareSource.Announce && _tycoons.Count > 0 && FlareRules.Roll(_file.Seed, p.Id, 870) < 400)
            {
                p.Mention = p.Mention ?? "t:" + _tycoons[FlareRules.Roll(_file.Seed, p.Id, 871) % _tycoons.Count];
            }
            if (!hostAnswers)
            {
                MentionAnswer(p);
            }
            FlareRules.Prune(_file);
            ShareOwn(p);
            TextsFriends(p);   // the friends' replies and the city's: their words asked now
            TextsAhead();
        }

        public void Like(int postId)
        {
            FlarePost p = Running ? _file.Post(postId) : null;
            if (p == null)
            {
                return;
            }
            p.Liked = !p.Liked;
            ShareLike(p, p.Liked);
            Changed();
        }

        /// <summary>The player likes a friend's reply under their post, or takes it back: kept by post and place; sends nothing.</summary>
        public void LikeReply(int parentId, int slot)
        {
            if (!Running)
            {
                return;
            }
            FlareRules.ToggleReplyLike(_file, parentId, slot);
            Changed();
        }

        public bool IsReplyLiked(int parentId, int slot) => Running && FlareRules.ReplyLiked(_file, parentId, slot);

        /// <summary>The player likes a comment under another's post, or takes it back: kept by post, slot and writer; sends nothing.</summary>
        public void LikeComment(int postId, int slot, char kind, int who)
        {
            if (!Running)
            {
                return;
            }
            FlareCommentRules.ToggleLike(_file, postId, slot, kind, who);
            Changed();
        }

        public bool IsCommentLiked(int postId, int slot, char kind, int who) => Running && FlareCommentRules.Liked(_file, postId, slot, kind, who);

        /// <summary>The player shares a post with their followers, once.</summary>
        public void Repost(int postId)
        {
            FlarePost p = Running ? _file.Post(postId) : null;
            if (p == null || p.Reposted)
            {
                return;
            }
            p.Reposted = true;
            p.Reposts++;
            ShareRepost(p);
            Changed();
        }

        /// <summary>The player goes to an invitation, or not: a notification when it starts, with the way there.</summary>
        public void Going(int postId)
        {
            FlarePost p = Running ? _file.Post(postId) : null;
            if (p == null)
            {
                return;
            }
            if (_file.Net.Going.Remove(postId))
            {
                Changed();
                return;
            }
            if (_file.Net.Going.Count < FlareNet.GoingMost)
            {
                _file.Net.Going.Add(postId);
                ShareGoing(p);
            }
            Changed();
        }

        public bool IsGoing(int postId) => Running && _file.Net.Going.Contains(postId);

        /// <summary>The player writes a message: a city person answers within an hour or two, an invitation by the game's rules; a session player gets it as written.</summary>
        public bool SendDm(string with, string text)
        {
            text = FlareLiveRules.Clip(text, FlareLiveRules.DmTextMost);
            if (!Running || Save == null || with == null || text == null)
            {
                return false;
            }
            long now = Now;
            FlareLiveRules.Dm(_file.Net, with, AccountName(with), now, true, text, null, null);
            if (with.StartsWith("p:", StringComparison.Ordinal))
            {
                ShareDm(with.Substring(2), text);
            }
            else
            {
                string what = with.StartsWith("i:", StringComparison.Ordinal) ? "collab" : with.StartsWith("o:", StringComparison.Ordinal) ? "office" : "chat";
                FlareDm answer = Invite(with, text, now)
                                 ?? FlareLiveRules.Dm(_file.Net, with, AccountName(with), now + 30 + FlareRules.Roll(_file.Seed, _file.Net.NextDm, 844) % 90, false, null,
                                                      FlareText.DmKey("answer_" + what), null);
                TextsDm(answer, text);   // its words asked now
            }
            Changed();
            return true;
        }

        public void ReadDms(string with)
        {
            if (!Running)
            {
                return;
            }
            long now = Now;
            for (int i = 0; i < _file.Net.Dms.Count; i++)
            {
                FlareDm m = _file.Net.Dms[i];
                if (m.With == with && m.At <= now)
                {
                    m.Read = true;
                }
            }
            Recount();
            Badge();
        }

        public void NoticesSeen()
        {
            if (Running)
            {
                _file.Net.NoticeSeen = Now;
                Recount();
                Badge();
            }
        }

        /// <summary>The Notifications screen's lines, newest first: the director's list, filled again on each call.</summary>
        public List<FlareNotice> Notices()
        {
            if (!Running)
            {
                _notices.Clear();
                return _notices;
            }
            Recount();
            return _notices;
        }

        private void Recount()
        {
            if (_file == null || Save == null)
            {
                _unread = 0;
                return;
            }
            if (_myPlaces.Count == 0)
            {
                List<BuildingRegistration> mine = FlareGame.Businesses();
                for (int i = 0; i < mine.Count; i++)
                {
                    _myPlaces.Add(FlareGame.Place(mine[i]));
                }
            }
            long now = Now;
            FlareLiveRules.Notices(_file, now, MeKey, _myPlaces, _reactions, _notices, NoticesMost);
            int unread = FlareLiveRules.Unread(_notices, _file.Net);
            for (int i = 0; i < _file.Net.Dms.Count; i++)
            {
                FlareDm m = _file.Net.Dms[i];
                unread += !m.Read && !m.Mine && m.At <= now && m.At <= _file.Net.NoticeSeen ? 1 : 0;   // a newer message is counted with the notifications already
            }
            _unread = unread;
        }

        /// <summary>Mutes a player of the session (no notifications or messages from them), or not.</summary>
        public void Mute(string player, bool on)
        {
            if (Running && player != null)
            {
                Toggle(_file.Net.Muted, player, on);
                Logger?.Info("Flare: a player of the session is " + (on ? "muted" : "no longer muted") + ".");
                Changed();
            }
        }

        /// <summary>Hides a player of the session (nothing of theirs shown), or not.</summary>
        public void Hide(string player, bool on)
        {
            if (Running && player != null)
            {
                Toggle(_file.Net.Hidden, player, on);
                Logger?.Info("Flare: a player of the session is " + (on ? "hidden" : "shown again") + ".");
                Changed();
            }
        }

        private static void Toggle(List<string> list, string item, bool on)
        {
            if (on && !list.Contains(item) && list.Count < FlareNet.MutedMost)
            {
                list.Add(item);
            }
            else if (!on)
            {
                list.Remove(item);
            }
        }

        /// <summary>The stories Flare has for a morning's paper (the City Ledger): the same for the same day however often asked.</summary>
        public void Stories(int day, List<FlareStory> into)
        {
            into.Clear();
            for (int i = 0; Running && i < _file.Net.Stories.Count; i++)
            {
                if (_file.Net.Stories[i].Day == day)
                {
                    into.Add(_file.Net.Stories[i]);
                }
            }
        }
    }
}
