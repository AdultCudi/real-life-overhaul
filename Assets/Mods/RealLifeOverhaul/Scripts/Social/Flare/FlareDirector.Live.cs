using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What the player is writing in Flare's compose sheet: the built part, the typed text, the picture.</summary>
    public sealed class FlareCompose
    {
        public FlareSource Kind = FlareSource.Life;   // Life: a moment of now; a draft's source; Report, Review, Announce or Reply
        public int Draft = -1;           // a moment from a draft (what happened today)
        public int Arg;                  // a report's kind, a review's stars, an announcement's kind
        public string Place, Name;       // the business it is about
        public string District;          // where (a report's, a moment's)
        public FlareTone Tone = FlareTone.Cheerful;
        public bool ToneChosen;          // the player picked the tone (else the one that suits it best)
        public bool Tag = true;          // a draft's friend tagged
        public string Mention;           // an account called on: "o:0", "b:name", "p:player"
        public string Text;              // typed
        public string Pic;               // "f:" a picture of the player's
        public int ReplyTo = -1;         // a reply's post
        public int When = -1;            // an event's hour (day * 24 + hour)
        public string Tags;              // tags picked from the chips, a space between each

        public void Reset()
        {
            Kind = FlareSource.Life;
            Draft = -1;
            Arg = 0;
            Place = Name = District = Mention = Text = Pic = Tags = null;
            Tone = FlareTone.Cheerful;
            ToneChosen = false;
            Tag = true;
            ReplyTo = -1;
            When = -1;
        }
    }

    /// <summary>
    /// Flare as a living network (FlareLiveRules): the city's posts each morning from what really happened, the reactions
    /// that come in over the hours, the City Office's cases and what it does about them, reviews, complaints and
    /// announcements that move a business's customers, invitations, the city's people's messages, the stories for the
    /// paper and the badge. Going Public is FlareDirector.Share.cs.
    ///
    /// COST: nothing per frame. The day: the city's posts (a few dozen), one pass over the buildings for a sample of the
    /// city's businesses, the watches. The hour: the cases due (a few), the invitations due, the badge (one walk over some
    /// two hundred posts). A post: its reactions and the city's replies (a dozen posts at most), a case.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly List<FlareCitizen> _citizens = new List<FlareCitizen>(FlareLiveRules.Citizens);
        private readonly FlareCityFacts _facts = new FlareCityFacts();
        private readonly List<FlarePost> _made = new List<FlarePost>(32);
        private readonly List<FlareCase> _due = new List<FlareCase>(8);
        private readonly List<string> _staff = new List<string>(8);
        private readonly List<string> _tycoons = new List<string>(4);              // their names, for the accounts called on
        private readonly List<FlareTycoon> _tycoonsIn = new List<FlareTycoon>(4);
        private readonly List<FlareReaction> _reactions = new List<FlareReaction>(16);
        private readonly List<FlareNotice> _notices = new List<FlareNotice>(64);
        private readonly List<string> _myPlaces = new List<string>(8);
        private readonly StringBuilder _line = new StringBuilder(256);
        private int _citizensSeed;
        private int _unread;
        private int _caseNoteDay = -1, _caseNotes;
        private int _hiringDay = -100;

        public const int NoticesMost = 60;
        private const int CaseNotesPerDay = 3;     // notifications of an answer to the player's report, a day at most

        /// <summary>The city's people on Flare, by their number.</summary>
        public IReadOnlyList<FlareCitizen> Citizens => _citizens;

        /// <summary>Game minutes since day 0 now.</summary>
        public long Now => Save != null ? FlareLiveRules.Now(Save.Day, Save.Hour, FlareGame.MinuteOfDay % 60) : 0;

        /// <summary>The player as a post's mention names them: "me" alone, "p:" and their id in a session.</summary>
        public string MeKey => GoingPublic.InSession && GoingPublicLink.MyId.Length > 0 ? "p:" + GoingPublicLink.MyId : "me";

        /// <summary>The player's name as Flare shows it.</summary>
        public string MyName
        {
            get
            {
                string name = FlareBridges.PlayerName != null ? FlareBridges.Ask(FlareBridges.PlayerName, null) : null;
                return string.IsNullOrEmpty(name) ? FlareText.T("reallife_flare_you") : name;
            }
        }

        /// <summary>The player's handle: their name in lower case, no spaces.</summary>
        public string MyHandle => FlareText.HandleOf(MyName);

        /// <summary>Some of the city's businesses, for a review's choice (the day's sample; made now if the day has not made it yet).</summary>
        public IReadOnlyList<FlareBiz> CityShops
        {
            get
            {
                if (_facts.Shops.Count == 0 && Running)
                {
                    FlareLiveGame.CityShops(_facts.Shops);
                }
                return _facts.Shops;
            }
        }

        /// <summary>Today, in the save's days; 0 without a save.</summary>
        public int CurrentDay => Save != null ? Save.Day : 0;

        /// <summary>Notifications and messages not yet seen: the badge.</summary>
        public int Unread => _unread;

        // ---------------------------------------------------------------- starting

        private void LiveStart(FlareFile file)
        {
            if (file.Net.CitySeed == 0)
            {
                file.Net.CitySeed = file.CastSeed != 0 ? file.CastSeed : file.Seed;
            }
            EnsureCitizens(file);
        }

        // The city's people for the seed in use (the host's while a guest follows it).
        private void EnsureCitizens(FlareFile file)
        {
            int seed = FlareLiveRules.CitySeed(file);
            if (seed == _citizensSeed && _citizens.Count > 0)
            {
                return;
            }
            FlareGame.Districts(_districts);
            FlareLiveRules.CitizensOf(seed, _districts, _citizens);
            _citizensSeed = seed;
        }

        /// <summary>True while this machine is a guest that follows the host's feed: the city's posts and the City Office are the host's.</summary>
        public bool Following => SharedCity.Follows(FlareNetShare.Part);

        // ---------------------------------------------------------------- the day

        // After the account's own daily round: the watches, the city's posts of the day, the city's people's message.
        private void LiveDay(int day)
        {
            EnsureCitizens(_file);
            if (FlareLiveRules.WatchDay(_file.Net, day))
            {
                Shared_WatchesChanged();
            }
            for (int i = _file.Net.Stories.Count - 1; i >= 0; i--)
            {
                if (day - _file.Net.Stories[i].Day > 3)
                {
                    _file.Net.Stories.RemoveAt(i);   // a piece for a paper that has come and gone
                }
            }
            for (int i = _file.Net.Going.Count - 1; i >= 0; i--)
            {
                FlarePost invite = _file.Post(_file.Net.Going[i]);
                if (invite == null || invite.When < day * 24 - 24)
                {
                    _file.Net.Going.RemoveAt(i);
                }
            }
            ReadFacts(day);
            bool newDay = _file.Net.CityDay != day;
            int made = FlareLiveRules.CityDay(_file, _facts, _citizens, _made);
            for (int i = 0; i < _made.Count; i++)
            {
                CityMade(_made[i]);
            }
            if (newDay)
            {
                Tycoons(day);
            }
            CityDm(day);
            if (made > 0)
            {
                Logger?.Info("Flare: the city's posts of day " + day + ": " + made + " (" + _made.Count + " reports and reviews among them), at its own minute each; the feed's busyness " + _facts.Busy + ".");
                Shared_Changed(null);
            }
            Recount();
        }

        // What the city is like today, for its posts.
        private void ReadFacts(int day)
        {
            FlareCityFacts f = _facts;
            f.Day = day;
            f.Rain = f.Rained = false;
            f.News = _file.NewsDay >= day - 1 ? _file.NewsToday : 0;
            f.NewsDistrict = f.EventToday = f.EventTomorrow = f.SiteDistrict = f.Policy = f.Mover = null;
            f.EventTodayFrom = f.EventTomorrowFrom = -1;
            f.PriceTrend = f.LateSites = f.MoverBp = f.RateMove = 0;
            f.Election = f.LitterPolicy = false;
            f.Candidates.Clear();
            f.Busy = FlareRules.Clamp(Settings.FlareBusy, 0, 2);
            f.Following = Following;
            FlareGame.Districts(f.Districts);
            f.Mine.Clear();
            _myPlaces.Clear();
            List<BuildingRegistration> mine = FlareGame.Businesses();
            for (int i = 0; i < mine.Count; i++)
            {
                f.Mine.Add(FlareLiveGame.Biz(mine[i], true));
                _myPlaces.Add(FlareGame.Place(mine[i]));
            }
            if (!f.Following)
            {
                FlareLiveGame.CityShops(f.Shops);
            }
            else
            {
                f.Shops.Clear();
            }
            if (_moverName != null && _moverDay >= day - 1)
            {
                f.Mover = _moverName;
                f.MoverBp = _moverBp;
            }
            try
            {
                FlareBridges.CityFacts?.Invoke(f);
            }
            catch (Exception e)
            {
                Logger?.Warn("Flare: the city's facts for the day could not all be read (" + e.Message + "); the posts go by what Flare knows.");
            }
        }

        // One of the city's people's reports or reviews of the day, as it goes up: a report is a case of the City Office's, a review moves its business's customers.
        private void CityMade(FlarePost p)
        {
            if (p.Source == FlareSource.Report)
            {
                FileCase(p, "c" + p.Who, 0f, 0f);
            }
            else if (p.Source == FlareSource.Review)
            {
                BusinessEffect(p, false);
            }
        }

        // The rival tycoons of the end game (Rivals): one of them posts in their own voice, now and then, about their latest move if they have one.
        private void Tycoons(int day)
        {
            ReadTycoons();
            if (Following)
            {
                return;
            }
            FlarePost p = FlareLiveRules.TycoonPost(_file, _tycoonsIn, day);
            if (p != null)
            {
                _file.Posts.Add(p);
                Logger?.Info("Flare: " + p.Name + " posts on day " + day + (p.Arg >= FlareLiveRules.TycoonMoveArg ? " about their move (" + (p.Arg - FlareLiveRules.TycoonMoveArg) + ")." : "."));
            }
        }

        private void ReadTycoons()
        {
            _tycoonsIn.Clear();
            _tycoons.Clear();
            try
            {
                FlareBridges.Tycoons?.Invoke(_tycoonsIn);
            }
            catch (Exception e)
            {
                Logger?.Warn("Flare: the rival tycoons could not be read (" + e.Message + ").");
                _tycoonsIn.Clear();
            }
            for (int i = 0; i < _tycoonsIn.Count; i++)
            {
                _tycoons.Add(_tycoonsIn[i].Name);
            }
        }

        // A tycoon's voice by their name (their place in the catalogue); -1 for a name not in play.
        private int TycoonVoice(string name)
        {
            for (int i = 0; i < _tycoonsIn.Count; i++)
            {
                if (_tycoonsIn[i].Name == name)
                {
                    return _tycoonsIn[i].Index;
                }
            }
            return -1;
        }

        // One of the city's people writes to the player, now and then.
        private void CityDm(int day)
        {
            bool hiring = day - _hiringDay <= 7 || FlareBridges.Ask(FlareBridges.Hiring, false);
            string with, what;
            if (!FlareLiveRules.CityDm(_file, day, hiring, out with, out what))
            {
                return;
            }
            long at = FlareLiveRules.Now(day, 10 + FlareRules.Roll(_file.Seed, day, 842) % 10, FlareRules.Roll(_file.Seed, day, 843) % 60);
            string name = AccountName(with);
            FlareLiveRules.Dm(_file.Net, with, name, at, false, null, FlareText.DmKey(what), null);
            Logger?.Info("Flare: " + name + " writes to the player (" + what + ") on day " + day + ".");
        }

        // ---------------------------------------------------------------- the hour

        private void LiveHour(int day, int hour)
        {
            long now = FlareLiveRules.Now(day, hour, 0);
            if (!Following)
            {
                FlareLiveRules.Due(_file.Net, now + 59, _due);   // (the cases of this hour: the answer is dated at its own minute)
                for (int i = 0; i < _due.Count; i++)
                {
                    Answer(_due[i], day);
                }
                Works(day);
            }
            Invitations(day, hour);
            Watched(day);
            Recount();
        }

        // The police and the cleaners where the player stands, for the hour.
        private void Watched(int day)
        {
            if (FlareBridges.Watched == null)
            {
                return;
            }
            string here = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            bool patrol = here != null && _file.Net.WatchIn(here, day, FlareOrder.Patrol) != FlareOrder.None;
            bool clean = here != null && _file.Net.WatchIn(here, day, FlareOrder.Cleanup) != FlareOrder.None;
            FlareBridges.Watched(patrol, clean);
        }

        // Works the City Office sent: done once the council's site is handed over.
        private void Works(int day)
        {
            for (int i = 0; i < _file.Net.Cases.Count; i++)
            {
                FlareCase c = _file.Net.Cases[i];
                if (c.State == FlareCaseState.Acting && c.Order == FlareOrder.Works && c.Ref >= 0 && FlareBridges.WorksDone != null && FlareBridges.WorksDone(c.Ref))
                {
                    c.State = FlareCaseState.Done;
                    Logger?.Info("Flare: the City Office's works for case " + c.Id + " (" + c.Kind + " in " + FlareGame.DistrictName(c.District) + ") are done.");
                    Shared_CaseChanged(c);
                }
            }
        }

        // An invitation the player goes to: a notification as it starts, with the way there.
        private void Invitations(int day, int hour)
        {
            int at = day * 24 + hour;
            for (int i = 0; i < _file.Net.Going.Count; i++)
            {
                FlarePost p = _file.Post(_file.Net.Going[i]);
                if (p != null && p.When == at && Settings.SocialMediaNotices)
                {
                    string place = p.Place;
                    Tell(NotificationType.Info, "reallife_flare_note_invite_now", () => GoTo(place), "event", FlareText.InviteName(p));
                }
            }
        }

        private static void GoTo(string place)
        {
            if (place != null)
            {
                FlareBridges.Destination?.Invoke(place);
            }
            else
            {
                FlareBridges.Ask(FlareBridges.EventDestination, false);
            }
        }

        // ---------------------------------------------------------------- the City Office

        // A report reaches the City Office: a case (or the case there is already), and the tally of the week's complaints.
        private FlareCase FileCase(FlarePost p, string reporter, float x, float z)
        {
            if (Following || p.District == null)
            {
                return null;
            }
            bool again;
            var kind = (FlareReport)FlareRules.Clamp(p.Arg, 0, (int)FlareReport.Utilities);
            FlareCase c = FlareLiveRules.File(_file.Net, kind, p.District, p.Place, p.Name, x, z, Now, reporter, p.Id, FlareLiveRules.CitySeed(_file), out again);
            p.Case = c.Id;
            if (again)
            {
                FlareLiveRules.Complained(_file.Net, kind, p.District, Save.Day, c.State == FlareCaseState.Acting);
                Escalation(c, Save.Day);
            }
            Shared_CaseChanged(c);
            return c;
        }

        // A case's answer is due: what the City Office does, its answer in character, the tally, the mood about the city's running.
        private void Answer(FlareCase c, int day)
        {
            bool siteNear = c.Kind == FlareReport.Unsafe && FlareBridges.SiteNear != null && (c.X != 0f || c.Z != 0f) && FlareBridges.SiteNear(c.X, c.Z, 120f);
            FlareAnswer answer;
            FlareOrder order = FlareLiveRules.Decide(_file.Net, c, c.ReplyAt, Settings.FlareCityOffice, siteNear, out answer);
            int site = -1, days = 0;
            string address = null;
            if (order == FlareOrder.Works)
            {
                if (FlareBridges.Works == null || !FlareBridges.Works(c.Kind, c.District, c.X, c.Z, FlareLiveRules.CitySeed(_file) + c.Id, out site, out days, out address))
                {
                    order = FlareOrder.Queued;
                    answer = FlareAnswer.Busy;
                }
            }
            else if (order == FlareOrder.SiteCheck)
            {
                FlareBridges.SiteReported?.Invoke(c.X, c.Z);
            }
            FlareLiveRules.Act(_file.Net, c, order, day, site);
            if (order == FlareOrder.Inspection)
            {
                Inspected(c, day);
            }
            bool acted = order != FlareOrder.Queued && order != FlareOrder.Answered;
            FlareLiveRules.Complained(_file.Net, c.Kind, c.District, day, acted);
            if (acted)
            {
                FlareLiveRules.Answered(_file.Net, c.Kind, c.District);
            }
            _file.MayorMood = FlareRules.Clamp(_file.MayorMood + (acted ? FlareLiveRules.MoodActed : order == FlareOrder.Queued ? FlareLiveRules.MoodQueued : 0), -100, 100);
            FlarePost reply = AnswerPost(c, answer, address, days);
            Logger?.Info("Flare: the City Office answers case " + c.Id + " (" + c.Kind + " in " + FlareGame.DistrictName(c.District) + ", reported by " + c.Reporter + "): " + answer + ", " + order
                         + (order == FlareOrder.Works ? " (site " + site + " at " + address + ", about " + days + " days)" : c.Until >= 0 ? " until day " + c.Until : string.Empty)
                         + "; the feed's mood " + _file.MayorMood + ".");
            if (c.Reporter == "me" && reply != null && Settings.SocialMediaNotices && CaseNote(day))
            {
                int id = reply.Id;
                Tell(NotificationType.Info, "reallife_flare_note_case", () => OpenPost(id), "account", FlareText.OfficialName(reply.Who));
            }
            Escalation(c, day);
            Shared_CaseChanged(c);
        }

        private bool CaseNote(int day)
        {
            if (_caseNoteDay != day)
            {
                _caseNoteDay = day;
                _caseNotes = 0;
            }
            return _caseNotes++ < CaseNotesPerDay;
        }

        // The answer in character, a reply to the report: by the City Office, the police, the transit company or the utilities office.
        private FlarePost AnswerPost(FlareCase c, FlareAnswer answer, string address, int days)
        {
            FlarePost report = c.Post >= 0 ? _file.Post(c.Post) : null;
            bool reportedByMe = c.Reporter == "me" || (c.Reporter != null && c.Reporter.StartsWith("p:", StringComparison.Ordinal));
            if (report == null && !reportedByMe)
            {
                return null;
            }
            if (!reportedByMe && answer != FlareAnswer.Works && answer != FlareAnswer.Patrol && answer != FlareAnswer.Cleanup && FlareRules.Roll(c.Id, c.Repeats, 860) >= 350)
            {
                return null;   // the city's people's complaints are answered when something is done, and now and then otherwise
            }
            FlareOfficial who = c.Kind == FlareReport.Crime ? FlareOfficial.Police : c.Kind == FlareReport.Transit ? FlareOfficial.Transit : c.Kind == FlareReport.Utilities ? FlareOfficial.Utilities
                : c.Kind == FlareReport.Unsafe && answer == FlareAnswer.Patrol ? FlareOfficial.Police : FlareOfficial.CityOffice;
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Official, Who = (int)who, Source = FlareSource.Answer, Arg = (int)answer, ReplyTo = report != null ? report.Id : -1, Case = c.Id,
                Name = address ?? c.Name, District = c.District, When = days, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29),
                Reach = 400 + FlareRules.Roll(_file.Seed, id, 5), Likes = 3 + FlareRules.Roll(_file.Seed, id, 6) % 40, Topic = FlareLiveRules.ReportTag(c.Kind)
            };
            FlareLiveRules.SetAt(p, Math.Max(c.ReplyAt, report != null ? report.At + 5 : c.ReplyAt));
            _file.Posts.Add(p);
            c.Answer = id;
            FlareRules.Prune(_file);
            Shared_Changed(p);
            return p;
        }

        // A business complained about and inspected: the talk turns against it for a few days, at the owner's game alone.
        private void Inspected(FlareCase c, int day)
        {
            if (c.Place == null)
            {
                return;
            }
            BuildingRegistration reg = FlareLiveGame.AnyBusiness(c.Place);
            if (reg == null || !FlareLiveRules.EffectHere(FlareSource.Report, GoingPublic.Foreign(reg), GoingPublic.Mine(reg), false))
            {
                return;   // another player's: their own game applies it when the case comes to it (FlareDirector.Share.cs)
            }
            Buzz(c.Place, FlareLiveRules.ComplaintBuzz, FlareLiveRules.InspectionDays, day, reg);
            Logger?.Info("Flare: an inspector visits " + c.Name + " after a complaint: " + FlareLiveRules.ComplaintBuzz + "% customers for " + FlareLiveRules.InspectionDays + " days.");
        }

        // The week's ignored complaints of a kind in a district: three, and it is in the paper, the mood falls, and the City Office acts after all.
        private void Escalation(FlareCase c, int day)
        {
            if (!FlareLiveRules.Escalate(_file.Net, c.Kind, c.District, day, Settings.FlareCityOffice))
            {
                return;
            }
            FlareLiveRules.Story(_file.Net, day + 1, "reallife_flare_news_ignored", "problem", FlareText.ReportWord(c.Kind), "district", FlareGame.DistrictName(c.District),
                "count", FlareLiveRules.Tally(_file.Net, c.Kind, c.District, day).ToString());
            _file.MayorMood = FlareRules.Clamp(_file.MayorMood + FlareLiveRules.MoodEscalated, -100, 100);
            Logger?.Info("Flare: " + FlareLiveRules.Tally(_file.Net, c.Kind, c.District, day) + " complaints about " + c.Kind + " in " + FlareGame.DistrictName(c.District)
                         + " this week, none acted on: in tomorrow's City Ledger; the feed's mood " + _file.MayorMood + ".");
            // Under the paper's eye a crew is found after all, if one of the week's is free.
            FlareOrder order = FlareLiveRules.EscalationOrder(_file.Net, c, Now, Settings.FlareCityOffice);
            if (order == FlareOrder.None)
            {
                return;
            }
            int site = -1, days = 0;
            string address = null;
            if (order == FlareOrder.Works && (FlareBridges.Works == null || !FlareBridges.Works(c.Kind, c.District, c.X, c.Z, FlareLiveRules.CitySeed(_file) + c.Id, out site, out days, out address)))
            {
                return;
            }
            FlareLiveRules.Act(_file.Net, c, order, day, site);
            if (order == FlareOrder.Inspection)
            {
                Inspected(c, day);
            }
            FlareLiveRules.Answered(_file.Net, c.Kind, c.District);
            AnswerPost(c, FlareAnswer.Pressure, address, days);
        }

        // ---------------------------------------------------------------- what a post does to a business

        /// <summary>
        /// A review, an announcement or a complaint about a business: its customers move, at the game of the business's
        /// owner alone (the player's own here; another player's in a session at theirs; the city's at every machine
        /// alike, as every machine has its own). 'mine' is a post of the player's own (an announcement is only for the
        /// player's own business).
        /// </summary>
        private void BusinessEffect(FlarePost p, bool mine)
        {
            if (p.Place == null || Save == null)
            {
                return;
            }
            BuildingRegistration reg = FlareLiveGame.AnyBusiness(p.Place);
            if (reg == null || !FlareLiveRules.EffectHere(p.Source, GoingPublic.Foreign(reg), GoingPublic.Mine(reg), mine))
            {
                return;
            }
            int day = Save.Day;
            if (p.Source == FlareSource.Review)
            {
                int buzz = FlareLiveRules.ReviewBuzz(p.Arg, p.Reach);
                if (buzz != 0)
                {
                    Buzz(p.Place, buzz, FlareLiveRules.ReviewDays, day, reg);
                    Logger?.Info("Flare: a " + p.Arg + "-star review of " + p.Name + " by " + FlareLiveRules.KeyOf(p) + ": " + (buzz > 0 ? "+" : string.Empty) + buzz + "% customers for "
                                 + FlareLiveRules.ReviewDays + " days.");
                }
                if (p.Author != FlareAuthor.Player && GoingPublic.Mine(reg) && Settings.SocialMediaNotices && p.Arg <= 2 && FlareRules.MayNotify(_file, day, true))
                {
                    int id = p.Id;
                    Tell(NotificationType.Warning, "reallife_flare_note_review", () => OpenPost(id), "business", p.Name, "stars", p.Arg.ToString());
                }
                if (FlareLiveGame.City(reg) && p.Author == FlareAuthor.Player)
                {
                    BusinessAnswer(p);
                }
            }
            else if (p.Source == FlareSource.Announce)
            {
                int buzz = FlareLiveRules.AnnounceBuzz(p.Reach);
                int days = p.Arg == (int)FlareAnnounce.Event && p.When > 0 ? Math.Max(1, p.When / 24 - day + 1) : FlareLiveRules.AnnounceDays;
                Buzz(p.Place, buzz + (p.Arg == (int)FlareAnnounce.Event ? FlareLiveRules.EventCrowd - 1 : 0), days, day, reg);
                if (p.Arg == (int)FlareAnnounce.Hiring)
                {
                    _hiringDay = day;
                }
            }
        }

        // Talk about a business for some days, inside Flare's cap, written into its customers by business pressure's own writer.
        private void Buzz(string place, int percent, int days, int day, BuildingRegistration reg)
        {
            FlareRules.AddBuzz(_file, place, percent, day, days);
            if (FlareBridges.Footfall != null && reg != null)
            {
                FlareBridges.Footfall(reg, false);
            }
        }

        // A business of the city's answers a review of the player's, in character: thanks, or sorry.
        private void BusinessAnswer(FlarePost review)
        {
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Business, Source = FlareSource.Answer, Arg = (int)(review.Arg >= 4 ? FlareAnswer.Thanks : FlareAnswer.Sorry), ReplyTo = review.Id,
                Place = review.Place, Name = review.Name, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 200, Likes = FlareRules.Roll(_file.Seed, id, 3) % 20
            };
            FlareLiveRules.SetAt(p, review.At + 40 + FlareRules.Roll(_file.Seed, id, 4) % 200);
            _file.Posts.Add(p);
        }

        // An account called on in a post answers it in character: an official account, a brand, a rival tycoon.
        private void MentionAnswer(FlarePost p)
        {
            string m = p.Mention;
            if (m == null || m.Length < 3 || (m[0] != 'o' && m[0] != 'b' && m[0] != 't') || m[1] != ':')
            {
                return;
            }
            int id = _file.NextId++;
            var a = new FlarePost
            {
                Id = id, Source = FlareSource.Answer, ReplyTo = p.Id, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 300, Likes = FlareRules.Roll(_file.Seed, id, 3) % 30
            };
            if (m[0] == 'o')
            {
                int who;
                int.TryParse(m.Substring(2), out who);
                a.Author = FlareAuthor.Official;
                a.Who = FlareRules.Clamp(who, 0, (int)FlareOfficial.Bank);
                a.Arg = (int)FlareAnswer.Mention;
            }
            else
            {
                a.Author = m[0] == 'b' ? FlareAuthor.Brand : FlareAuthor.Tycoon;
                a.Name = m.Substring(2);
                a.Arg = (int)(m[0] == 'b' ? FlareAnswer.Brand : FlareAnswer.Tycoon);
                a.Who = m[0] == 'b' ? -1 : TycoonVoice(a.Name);   // a tycoon answers in their own voice
            }
            FlareLiveRules.SetAt(a, p.At + 30 + FlareRules.Roll(_file.Seed, id, 4) % 90);
            _file.Posts.Add(a);
            Shared_Changed(a);
        }

        // ---------------------------------------------------------------- the player's own

        /// <summary>
        /// The player posts from the compose sheet: the built part (a moment, a report, a review, an announcement or a reply),
        /// the typed text (it decides nothing: the built part does), the picture, the tags and the account called on. False,
        /// with the reason's key, when it cannot go now (the limits on posting, a report a day for each thing, a reply's
        /// words missing).
        /// </summary>
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

        // A moment, a review or an announcement: a post by the account's own rules (followers, reputation, a viral moment, the friends' answers).
        private FlarePost Built(FlareCompose c, int day, int minute, out string why)
        {
            why = null;
            if (FlareRules.CanPost(_file, day, minute) != FlareRules.Wait.None)
            {
                why = "reallife_flare_wait_soon";
                return null;
            }
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
            FlarePost p = FlareRules.Post(_file, d, tone, tag ? d.Person : -1, _people, day, Save.Hour, minute, _testViral);
            _testViral = false;
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

        // A report to the city: a post of its own (it gains nothing), a case of the City Office's.
        private FlarePost Report(FlareCompose c, string text, int day, out string why)
        {
            why = null;
            FlareNet net = _file.Net;
            if (net.ReportsDay != day)
            {
                net.ReportsDay = day;
                net.ReportsToday = 0;
            }
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
            if (net.ReportsToday >= FlareLiveRules.ReportsPerDay || ReportedToday(day, c.Arg, district, c.Place))
            {
                why = "reallife_flare_compose_reported";
                return null;
            }
            net.ReportsToday++;
            int id = _file.NextId++;
            int reach = Math.Max(1, FlareRules.Reach(_file.Followers, FlareRules.Quality(FlareSource.Report, false, false), FlareRules.ToneFit(FlareSource.Report, c.ToneChosen ? c.Tone : FlareTone.Honest)));
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = Save.Hour, Author = FlareAuthor.Player, Source = FlareSource.Report, Tone = c.ToneChosen ? c.Tone : FlareTone.Honest, Arg = c.Arg,
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
                if (cs != null && (cs.Kind == FlareReport.Road || cs.Kind == FlareReport.Crime) && FlareBridges.Issue != null)
                {
                    FlareBridges.Issue(cs.Kind == FlareReport.Crime ? (int)FlareIssue.Safety : (int)FlareIssue.Roads, FlareLiveRules.ReportsWeigh * FlareRules.IssueWeight(_file.Followers) / 1000);
                }
            }
            return p;
        }

        private bool ReportedToday(int day, int kind, string district, string place)
        {
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.Author == FlareAuthor.Player && p.Source == FlareSource.Report && p.Day == day && p.Arg == kind && p.District == district && p.Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        // A reply: the typed words under a post (they decide nothing); an honest answer to a poor review of the player's business wins a little back.
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
            FlareNet net = _file.Net;
            if (net.RepliesDay != day)
            {
                net.RepliesDay = day;
                net.RepliesToday = 0;
            }
            if (net.RepliesToday >= FlareLiveRules.RepliesPerDay)
            {
                why = "reallife_flare_compose_replies";
                return null;
            }
            net.RepliesToday++;
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
                _file.Net.Applied.Add(-parent.Id);   // (once for each review)
                Buzz(parent.Place, 1, FlareLiveRules.ReviewDays, day, FlareGame.Business(parent.Place));
                Logger?.Info("Flare: the player answered a poor review of " + parent.Name + " politely: +1% customers for " + FlareLiveRules.ReviewDays + " days.");
            }
            return p;
        }

        // After any post of the player's: who reacts and when, the city's replies, what it does to a business, an answer to a mention, the session.
        private void PostLive(FlarePost p)
        {
            ReadPeople();
            Names(FlareBridges.Staff, _staff);
            int shift = FlareBridges.Ask(FlareBridges.OnShift, -1);
            string employer;
            int career = FlareBridges.Career != null ? FlareBridges.Career(out employer) : -1;
            bool boss = career >= 0 && FlareRules.BossFollows(_file.Seed, career, _file.Tier) && shift < 0;
            p.Reactors = FlareLiveRules.Reactors(p, _people, _staff, boss, _line);
            bool hostAnswers = FlareLiveRules.HostAnswers(Following, Settings.FlareSharePosts, p.Source);   // (the host's replies come back with the post)
            if (p.Source != FlareSource.Reply)
            {
                if (!hostAnswers)
                {
                    FlareLiveRules.CityReplies(_file, p, null);
                }
                if (FlareLiveRules.PileOn(p))
                {
                    FlareLiveRules.Story(_file.Net, p.Day + 1, "reallife_flare_news_pileon", "name", MyName, "handle", MyHandle);
                    Logger?.Info("Flare: the post went badly and was seen by " + p.Reach + ": a pile-on of angry replies, and tomorrow's City Ledger has it.");
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
            Shared_Own(p);
        }

        /// <summary>The player likes a post, or takes the like back.</summary>
        public void Like(int postId)
        {
            FlarePost p = Running ? _file.Post(postId) : null;
            if (p == null)
            {
                return;
            }
            p.Liked = !p.Liked;
            Shared_Like(p, p.Liked);
            Changed();
        }

        /// <summary>The player shares a post with their followers (once).</summary>
        public void Repost(int postId)
        {
            FlarePost p = Running ? _file.Post(postId) : null;
            if (p == null || p.Reposted)
            {
                return;
            }
            p.Reposted = true;
            p.Reposts++;
            Shared_Repost(p);
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
                Shared_Going(p);
            }
            Changed();
        }

        /// <summary>True if the player goes to an invitation.</summary>
        public bool IsGoing(int postId) => Running && _file.Net.Going.Contains(postId);

        /// <summary>
        /// The player writes a message. One of the city's people answers within the hour or two in character (what they say
        /// never depends on what was typed); a player of a session gets it as written, through the host.
        /// </summary>
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
                Shared_Dm(with.Substring(2), text);
            }
            else
            {
                string what = with.StartsWith("i:", StringComparison.Ordinal) ? "collab" : with.StartsWith("o:", StringComparison.Ordinal) ? "office" : "chat";
                FlareLiveRules.Dm(_file.Net, with, AccountName(with), now + 30 + FlareRules.Roll(_file.Seed, _file.Net.NextDm, 844) % 90, false, null, FlareText.DmKey("answer_" + what), null);
            }
            Changed();
            return true;
        }

        /// <summary>A conversation has been read.</summary>
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

        /// <summary>The Notifications screen has been seen: everything up to now is read.</summary>
        public void NoticesSeen()
        {
            if (Running)
            {
                _file.Net.NoticeSeen = Now;
                Recount();
                Badge();
            }
        }

        /// <summary>The Notifications screen's lines, newest first (the list is the director's, filled again on each call).</summary>
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

        // The notifications worked out again, and the badge's count.
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
                unread += !m.Read && !m.Mine && m.At <= now && m.At <= _file.Net.NoticeSeen ? 1 : 0;   // (a message newer than that is counted with the notifications already)
            }
            _unread = unread;
        }

        /// <summary>Mutes a player of the session (no notifications, no messages from them), or not.</summary>
        public void Mute(string player, bool on)
        {
            if (Running && player != null)
            {
                Toggle(_file.Net.Muted, player, on);
                Logger?.Info("Flare: a player of the session is " + (on ? "muted" : "no longer muted") + ".");
                Changed();
            }
        }

        /// <summary>Hides a player of the session (nothing of theirs shown, their pictures neither), or not.</summary>
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

        /// <summary>The stories Flare has for a morning's paper (the City Ledger, through Wiring/FlareLinks): the same for the same day however often asked.</summary>
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

        // ---------------------------------------------------------------- names

        /// <summary>An account's name as shown, by its key ("c12", "o:0", "i:3", "b:name", "k:name", "p:player", "f3", "me").</summary>
        public string AccountName(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "?";
            }
            if (key == "me")
            {
                return MyName;
            }
            int n;
            switch (key[0])
            {
                case 'c':
                    return int.TryParse(key.Substring(1), out n) && n >= 0 && n < _citizens.Count ? _citizens[n].First + " " + _citizens[n].Last : "?";
                case 'f':
                case 'p' when key.Length > 1 && key[1] != ':':
                    return int.TryParse(key.Substring(1), out n) ? PersonName(n) ?? "?" : "?";
                case 'o':
                    return int.TryParse(key.Substring(2), out n) ? FlareText.OfficialName(n) : "?";
                case 'i':
                    FlareInfluencer inf = _file != null && int.TryParse(key.Substring(2), out n) ? _file.Influencer(n) : null;
                    return inf != null ? inf.Handle : "?";
                case 's':
                    return key.Length > 2 ? key.Substring(2) : "?";
                case 'b':
                    return key.Length == 1 ? FlareText.T("reallife_flare_your_boss") : key.Substring(2);
                case 'l':
                    return FlareText.T("reallife_flare_ledger_name");
                default:
                    return key.Length > 2 ? key.Substring(2) : key;
            }
        }

        /// <summary>The app opens on a post, its thread.</summary>
        public void OpenPost(int id)
        {
            if (_app != null && _view != null && Running)
            {
                _view.WantPost(id);
                _app.Open();
            }
        }

        // ---------------------------------------------------------------- the camera and pictures

        /// <summary>
        /// The camera button: the phone's own camera (the Camera app's, Photos/CameraMode), whose first photo comes back to the
        /// compose sheet; with the Camera off, a shot of the screen as it is, without the phone (FlareDirector.Camera.cs).
        /// </summary>
        public void CameraPressed()
        {
            if (FlareBridges.OpenCamera == null || !FlareBridges.OpenCamera())
            {
                TakeShot();
            }
        }

        /// <summary>The selfie button: the phone is put away and photo mode starts as a selfie; the picture saved comes back to the compose sheet.</summary>
        public void SelfiePressed()
        {
            if (FlareBridges.TakeSelfie == null)
            {
                return;
            }
            _awaitSelfie = UnityEngine.Time.unscaledTime + 600f;
            FlareLiveGame.CloseMenu();
            FlareBridges.TakeSelfie();
        }

        private float _awaitSelfie = -1f;
    }
}
