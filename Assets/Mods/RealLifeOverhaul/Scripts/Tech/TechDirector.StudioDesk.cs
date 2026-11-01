using System;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio's decisions: the four desk sessions' callbacks (the concept pitch, the milestone review, the
    /// playtest, the post-launch review; held at the studio's desk or by its manager, Wiring/Wiring.StudioLinks), the Tech
    /// app's choices and buttons on a game's page and the concept, and the screens those pages draw. Run on a click or a
    /// session's end, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        // the notices of a marketing beat done, by StudioRules.Beat*
        private static readonly string[] BeatNotes =
        {
            "reallife_studio_note_beat_0", "reallife_studio_note_beat_1", "reallife_studio_note_beat_2", "reallife_studio_note_beat_3", "reallife_studio_note_beat_4"
        };

        // ---------------------------------------------------------------- the desk sessions

        /// <summary>Why a kind of the studio's sessions cannot be held at a studio now (a key), or null.</summary>
        internal string StudioBlocked(int kind, string place)
        {
            if (!_studioOn || _firmsFile == null)
            {
                return "reallife_studio_session_off";
            }
            switch (kind)
            {
                case StudioRules.SessionPitch:
                    if (_studio.DraftAt(place) == null)
                    {
                        return "reallife_studio_session_no_draft";
                    }
                    return FirmsRules.Room(_firmsFile, place) != FirmsRules.RoomYes ? "reallife_studio_session_full" : null;
                case StudioRules.SessionMilestone:
                    return MilestoneGame(place) == null ? "reallife_studio_session_no_milestone" : null;
                case StudioRules.SessionPlaytest:
                    return TestGame(place) == null ? "reallife_studio_session_no_build" : null;
                default:
                    return LiveGame(place) == null ? "reallife_studio_session_no_live" : null;
            }
        }

        private GameDev MilestoneGame(string place)
        {
            for (int i = 0; i < _studio.Games.Count; i++)
            {
                if (_studio.Games[i].Place == place && _studio.Games[i].Milestone >= 0)
                {
                    return _studio.Games[i];
                }
            }
            return null;
        }

        // A game being built far enough to play, with enough bugs hiding for a playtest to be worth the time.
        private GameDev TestGame(string place)
        {
            for (int i = 0; i < _studio.Games.Count; i++)
            {
                GameDev g = _studio.Games[i];
                if (g.Place == place && g.Building && StudioRules.Progress(g) >= StudioCatalogue.PhaseShare[0] && g.Hidden > StudioRules.BugRoom(g) / 10)
                {
                    return g;
                }
            }
            return null;
        }

        // The studio's game on sale that most needs looking after: the most bugs reported, else the newest out within a market year.
        private GameDev LiveGame(string place)
        {
            GameDev best = null;
            int day = Save != null ? Save.Day : 0;
            for (int i = 0; i < _studio.Games.Count; i++)
            {
                GameDev g = _studio.Games[i];
                if (g.Place != place || !g.Live || g.Building || day - g.Launched > StudioCatalogue.MarketYear * 2
                    || g.Reported == 0 && (g.Project != StudioRules.ProjectNone || g.Asks == 0) && !(g.Drm && !g.DrmRemoved))
                {
                    continue;   // nothing to look after: no bugs reported, no request open while the team is free, no DRM to take off
                }
                if (best == null || g.Reported > best.Reported || g.Reported == best.Reported && g.Launched > best.Launched)
                {
                    best = g;
                }
            }
            return best;
        }

        /// <summary>The sound choice at a step of a kind, as a manager judges it at a studio.</summary>
        internal int StudioPick(int kind, string place, int step)
        {
            if (!_studioOn || Save == null)
            {
                return 0;
            }
            switch (kind)
            {
                case StudioRules.SessionPitch:
                    GameDraft d = _studio.DraftAt(place);
                    bool big = d != null && StudioRules.ScopeOf(StudioRules.BudgetAt(d.Budget)) == (int)Scope.Big;
                    return step == 0 ? StudioRules.AsPlanned : step == 1 ? (big ? StudioRules.Publisher : StudioRules.SelfPublish) : StudioRules.NoDrm;   // a big game's risk shared
                case StudioRules.SessionMilestone:
                    GameDev m = MilestoneGame(place);
                    return m != null ? StudioRules.PickMilestone(m, step, Save.Day, StudioPointsAt(place)) : StudioRules.Hold;
                case StudioRules.SessionPlaytest:
                    GameDev t = TestGame(place);
                    return t != null ? StudioRules.PickPlaytest(t, step) : 0;
                default:
                    GameDev l = LiveGame(place);
                    return l != null ? StudioRules.PickPostLaunch(l, step, Save.Day) : 0;
            }
        }

        /// <summary>
        /// A kind of the studio's sessions held at a studio with its choices, by the owner or a manager (their strength,
        /// thousandths): the game's state changed and its money booked. False with nothing changed (it stays due); the key and
        /// pairs say the result in a notice either way.
        /// </summary>
        internal bool StudioSession(int kind, string place, string business, int strength, int[] choices, out string key, out string[] pairs)
        {
            key = "reallife_studio_session_off";
            pairs = new[] { "business", business ?? string.Empty };
            string why = StudioBlocked(kind, place);
            FirmPlace firm = FirmOf(place);
            if (why != null || firm == null || Save == null)
            {
                key = why ?? key;
                return false;
            }
            int day = Save.Day;
            switch (kind)
            {
                case StudioRules.SessionPitch:
                    return Pitch(firm, business, choices, day, out key, out pairs);
                case StudioRules.SessionMilestone:
                    GameDev m = MilestoneGame(place);
                    StudioRules.ApplyMilestone(m, StudioRules.Choice(choices, 0, 3, StudioRules.Hold), StudioRules.Choice(choices, 1, 3, StudioRules.Normal), day);
                    key = "reallife_studio_milestone_done";
                    pairs = new[] { "game", NameOf(m.Id) };
                    break;
                case StudioRules.SessionPlaytest:
                    GameDev t = TestGame(place);
                    int found = StudioRules.ApplyPlaytest(t, StudioRules.Choice(choices, 0, 3, StudioRules.ClosedBeta), StudioRules.Choice(choices, 1, 3, StudioRules.FixWorst),
                        strength, _studio.Seed, day, out int hype);
                    key = "reallife_studio_playtest_done";
                    pairs = new[] { "game", NameOf(t.Id), "found", found.ToString(Inv), "hype", (hype / 10).ToString(Inv) + "%" };
                    break;
                default:
                    GameDev l = LiveGame(place);
                    int fixedNow = StudioRules.ApplyPostLaunch(l, StudioRules.Choice(choices, 0, 3, StudioRules.LeaveBugs), StudioRules.Choice(choices, 1, 3, StudioRules.Listen),
                        StudioRules.Choice(choices, 2, 3, StudioRules.HoldPrice), strength, day);
                    key = "reallife_studio_postlaunch_done";
                    pairs = new[] { "game", NameOf(l.Id), "fixed", fixedNow.ToString(Inv), "bugs", l.Reported.ToString(Inv) };
                    break;
            }
            SyncProducts();
            ChangedTech();
            return true;
        }

        // The concept pitched: the draft becomes a product of the firm and a game, its engine's fee booked, a publisher's advance and any crowdfunding paid in.
        private bool Pitch(FirmPlace firm, string business, int[] choices, int day, out string key, out string[] pairs)
        {
            string place = firm.File.Place;
            GameDraft d = _studio.DraftAt(place);
            FirmsRules.TrimWithdrawn(_firmsFile);
            long budget = StudioRules.BudgetAt(d.Budget);
            string name = string.IsNullOrEmpty(d.Name) ? FirmsRules.Name(_firmsFile.Seed, _firmsFile.NextId) : d.Name;
            Product p = FirmsRules.NewProduct(_firmsFile, place, ProductKind.Game, Segment.Consumer, StudioRules.ScopeOf(budget), name);
            p.Launched = day;
            _firmsFile.Products.Add(p);
            StudioEngine e = _studio.EngineAt(place);
            GameDev g = StudioRules.Greenlight(d, p.Id, choices, e != null && e.Ready, day);
            _studio.Games.Add(g);
            _studio.Drafts.Remove(d);
            int level = Prices.Level;
            if (g.Engine == (int)GameEngine.Licensed && StudioCatalogue.EngineFee[g.Scope] > 0)
            {
                FirmsGame.Cost(StudioCatalogue.EngineFee[g.Scope] * level / 1000, StudioGame.EngineKey, firm.Reg, firm.File.Name, null);
            }
            if (g.Published)
            {
                StudioGame.Advance(g.Advance * level / 1000, firm.Reg, firm.File.Name);
            }
            if (StudioBridges.CanCrowdfund)
            {
                g.Pledged = StudioBridges.CrowdfundNow(place, budget * StudioCatalogue.AdvanceShare / 1000 / 5);   // a tenth of the budget asked of the studio's followers
                if (g.Pledged > 0)
                {
                    StudioGame.Crowdfund(g.Pledged * level / 1000, firm.Reg, firm.File.Name);
                    StudioRules.MoveHype(g, StudioCatalogue.HypeAnnounce, day);   // its backers talk it up
                }
            }
            int days = StudioRules.DaysLeft(g.Work, StudioPointsAt(place) > 0 ? StudioPointsAt(place) : firm.Crew.Engineering + firm.Crew.Design + firm.Crew.Product + firm.Crew.Testing);
            key = days > 0 ? "reallife_studio_pitch_done" : "reallife_studio_pitch_done_nobody";
            pairs = new[] { "game", name, "business", business ?? firm.File.Name, "genre", TechText.T(StudioViews.GenreKey(g.Genre)), "days", days.ToString(Inv) };
            if (Verbose)
            {
                Logger?.Info("Tech check (studio pitch): " + name + " at " + firm.File.Name + ": " + (Genre)g.Genre + " on " + (Platform)StudioRules.FirstPlatform(g) + ", budget "
                             + g.Budget.ToString(Inv) + " cents (" + (Scope)g.Scope + "), " + g.Work.ToString(Inv) + " points of work, " + days + " day(s) at today's pace: PASS");
            }
            return true;
        }

        private string NameOf(int id)
        {
            Product p = _firmsFile != null ? _firmsFile.ProductOf(id) : null;
            return p != null ? p.Name : string.Empty;
        }

        // ---------------------------------------------------------------- the pages' choices and buttons

        /// <summary>A studio's concept draft: its own, or a new one made from what its team knows best and kept.</summary>
        internal GameDraft StudioDraft(string place)
        {
            if (!_studioOn || place == null)
            {
                return null;
            }
            GameDraft d = _studio.DraftAt(place);
            if (d == null)
            {
                d = new GameDraft { Place = place };
                _studio.Drafts.Add(d);
            }
            return d;
        }

        /// <summary>
        /// A choice or a button of the studio's pages (StudioAct): the concept's draft changed, a marketing beat, the
        /// campaign, the release date, the bonus and the leads' royalty, the share of the studio's time, launch now, early
        /// access, a project after launch, withdraw, the studio's own engine, or the way to the desk.
        /// </summary>
        internal void StudioDo(int act, string place, int id, int value)
        {
            if (!_studioOn || Save == null)
            {
                return;
            }
            int day = Save.Day;
            GameDraft d = act >= StudioAct.Genre && act <= StudioAct.Price ? StudioDraft(place) : null;
            GameDev g = id > 0 ? _studio.GameOf(id) : null;
            switch (act)
            {
                case StudioAct.Genre:
                    if (d.Genre != value)
                    {
                        d.Genre = StudioRules.Clamp(value, 0, (int)Genre.Count - 1);
                        d.Setting = 0;
                    }
                    break;
                case StudioAct.Setting: d.Setting = StudioRules.Clamp(value, 0, 3); break;
                case StudioAct.Platform: d.Platform = StudioRules.Clamp(value, 0, StudioCatalogue.GamePlatforms - 1); break;
                case StudioAct.Model: d.Model = StudioRules.Clamp(value, 0, (int)GameModel.Count - 1); break;
                case StudioAct.Engine: d.Engine = StudioRules.Clamp(value, 0, 1); break;
                case StudioAct.Sequel: d.Sequel = StudioViews.SequelAt(_studio, place, value); break;
                case StudioAct.Budget: d.Budget = StudioRules.Clamp(value, 0, StudioCatalogue.Budgets.Length - 1); break;
                case StudioAct.Price: d.Price = StudioRules.Clamp(value, 0, StudioCatalogue.PriceSteps.Length - 1); break;
                case StudioAct.Desk: ToDesk(place); break;
                case StudioAct.Pitch:
                    StudioDraft(place);   // the concept as the page shows it is kept for the pitch
                    ToDesk(place);
                    break;
                case StudioAct.Greenlight: GreenlightHere(place); break;
                case StudioAct.Beat: Beat(g, value, day); break;
                case StudioAct.Campaign:
                    if (g != null)
                    {
                        g.Campaign = StudioRules.Clamp(value, 0, StudioCatalogue.CampaignShare.Length - 1);
                    }
                    break;
                case StudioAct.Release:
                    if (g != null)
                    {
                        g.ReleaseDay = value > day ? value : 0;
                    }
                    break;
                case StudioAct.Bonus:
                    if (g != null && !g.BonusDone)
                    {
                        g.Bonus = StudioRules.Clamp(value, 0, StudioCatalogue.BonusPool.Length - 1);
                    }
                    break;
                case StudioAct.Leads:
                    if (g != null)
                    {
                        g.Leads = StudioRules.Clamp(value, 0, StudioCatalogue.LeadRoyalty.Length - 1);
                    }
                    break;
                case StudioAct.Share: SetProduct(id, -1, -1, StudioRules.Clamp(value, 0, 4) * 25); break;
                case StudioAct.LaunchNow: LaunchNow(g, day); break;
                case StudioAct.OpenEarly: OpenEarly(g, day); break;
                case StudioAct.Project: Project(g, value); break;
                case StudioAct.Withdraw: Withdraw(id); break;
                case StudioAct.BuildEngine:
                    if (_studio.EngineAt(place) == null)
                    {
                        _studio.Engines.Add(new StudioEngine { Place = place, Building = true });
                    }
                    break;
            }
            ChangedTech();
        }

        // The way to the studio's desk: the marker set, and what to click said.
        private void ToDesk(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null)
            {
                return;
            }
            bool marked = StudioGame.Route(place);
            StudioGame.TellDesk(marked ? "reallife_studio_note_desk" : "reallife_studio_note_desk_here", Data("studio", firm.File.Name));
        }

        private void Beat(GameDev g, int beat, int day)
        {
            Product p = g != null ? _firmsFile.ProductOf(g.Id) : null;
            FirmPlace firm = g != null ? FirmOf(g.Place) : null;
            if (p == null || firm == null || StudioRules.BeatWhy(g, beat, day, StudioRules.YearPart(day, GameCalendar.DaysPerYear)) != null)
            {
                return;
            }
            long cost = StudioRules.BeatCost(g, beat);
            if (cost > 0 && !FirmsGame.Pay(cost * Prices.Level / 1000, FirmsGame.MarketingKey, firm.Reg, firm.File.Name))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            int devlog = beat == StudioRules.BeatDevlog ? StudioBridges.DevlogNow(g.Place, p.Name) : 0;
            int moved = StudioRules.Beat(g, beat, devlog, day);
            if (beat != StudioRules.BeatDevlog)
            {
                StudioBridges.Say((StudioNews)beat, g.Place, p.Name, g.Hype);   // the beats 0 to 3 are the news of the same number; a devlog is Flare's own post
            }
            if (beat == StudioRules.BeatAnnounce)
            {
                Story("reallife_studio_news_announced", "game", p.Name, "firm", firm.File.Name);
            }
            TellTech(moved < 0 ? NotificationType.Warning : NotificationType.Info, BeatNotes[StudioRules.Clamp(beat, 0, BeatNotes.Length - 1)],
                Data("game", p.Name, "hype", (moved / 10).ToString(Inv) + "%"), TabProducts, TechApp.ProductRow + g.Id);
        }

        private void LaunchNow(GameDev g, int day)
        {
            Product p = g != null ? _firmsFile.ProductOf(g.Id) : null;
            FirmPlace firm = g != null ? FirmOf(g.Place) : null;
            if (p == null || firm == null || !StudioRules.CanLaunch(g, day))
            {
                return;
            }
            StudioRules.GenreAppeals(_studio, day, _genreAppeal);
            StudioRelease(firm, g, p, day);
            SyncProducts();
        }

        private void OpenEarly(GameDev g, int day)
        {
            Product p = g != null ? _firmsFile.ProductOf(g.Id) : null;
            FirmPlace firm = g != null ? FirmOf(g.Place) : null;
            if (p == null || firm == null || !StudioRules.CanOpenEarly(g))
            {
                return;
            }
            StudioRules.GenreAppeals(_studio, day, _genreAppeal);
            GameDev prior = g.Sequel > 0 ? _studio.GameOf(g.Sequel) : null;
            StudioRules.OpenEarly(g, _studio, day, _genreAppeal[g.Genre], _platforms, StudioRules.FansOf(prior));
            p.Stage = 1;
            p.Launched = day;
            Story("reallife_studio_news_early", "game", p.Name, "firm", firm.File.Name);
            StudioBridges.Say(StudioNews.Launched, g.Place, p.Name, g.Quality);
            TellTech(NotificationType.Success, "reallife_studio_note_early", Data("game", p.Name), TabProducts, TechApp.ProductRow + g.Id);
            SyncProducts();
        }

        // A port to a platform (its index) or localisation (the index past the platforms) started on a game on sale.
        private void Project(GameDev g, int pick)
        {
            int project = pick < StudioCatalogue.GamePlatforms ? StudioRules.ProjectPort : StudioRules.ProjectLocalise;
            if (g == null || StudioRules.ProjectWhy(g, project, pick) != null)
            {
                return;
            }
            StudioRules.StartProject(g, project, pick);
        }

        // A game withdrawn: off sale and out of the studio's work, as every product.
        private void WithdrawGame(int id)
        {
            GameDev g = _studioOn ? _studio.GameOf(id) : null;
            if (g != null)
            {
                g.Phase = (int)GamePhase.Ended;
                g.Milestone = -1;
                g.Project = StudioRules.ProjectNone;
            }
        }

        // ---------------------------------------------------------------- the screens

        /// <summary>What the screens know of a studio: its name, its points a day, its engine, room for a game and its reputation.</summary>
        internal StudioPlace StudioPlaceOf(FirmPlace firm)
        {
            var s = new StudioPlace();
            if (firm == null)
            {
                return s;
            }
            s.Place = firm.File.Place;
            s.Name = firm.File.Name;
            long points = StudioPointsAt(s.Place);
            s.PointsADay = points > 0 ? points : firm.Crew.Engineering + firm.Crew.Design + firm.Crew.Product + firm.Crew.Testing;
            s.People = firm.People;
            StudioEngine e = _studioOn ? _studio.EngineAt(s.Place) : null;
            s.OwnEngine = e != null && e.Ready;
            s.Room = _firmsFile != null && FirmsRules.Room(_firmsFile, s.Place) == FirmsRules.RoomYes;
            s.Reputation = firm.File.Reputation;
            s.Desks = TechBridges.DesksNow();
            s.Tickets = StudioTicketsAt(s.Place);
            s.Answered = (long)firm.Crew.Support * FirmsCatalogue.TicketsPerPerson;
            return s;
        }

        internal StudioScreen StudioConcept(FirmPlace firm, IStudioFormat fmt)
        {
            if (!_studioOn || firm == null || Save == null)
            {
                return null;
            }
            GameDraft d = _studio.DraftAt(firm.File.Place) ?? new GameDraft { Place = firm.File.Place };   // only shown: kept once a choice is made or it goes to the desk
            return StudioViews.Concept(_studio, d, StudioPlaceOf(firm), Save.Day, fmt);
        }

        // With desk sessions off, the concept is greenlit on its page as a pitch would make it: as planned, self-published, with no DRM.
        private void GreenlightHere(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null || TechBridges.DesksNow() || FirmsRules.Room(_firmsFile, place) != FirmsRules.RoomYes)
            {
                return;
            }
            StudioDraft(place);
            int[] choices = { StudioRules.AsPlanned, StudioRules.SelfPublish, StudioRules.NoDrm };
            bool done = StudioSession(StudioRules.SessionPitch, place, firm.File.Name, 1000, choices, out string key, out string[] pairs);
            TellTech(done ? NotificationType.Success : NotificationType.Warning, key, Data(pairs), TabProducts, null);
        }

        internal StudioScreen StudioGameScreen(Product p, int beatPick, int projectPick, IStudioFormat fmt)
        {
            GameDev g = _studioOn && p != null ? _studio.GameOf(p.Id) : null;
            return g == null || Save == null ? null
                : StudioViews.Game(_studio, g, p.Name, StudioPlaceOf(FirmOf(p.Place)), StudioShareOf(p), p.Share, Save.Day, GameCalendar.DaysPerYear, beatPick, projectPick, fmt);
        }

        internal StudioScreen StudioFirmScreen(FirmPlace firm, IStudioFormat fmt)
        {
            return !_studioOn || firm == null || firm.File.K != FirmKind.Games || Save == null ? null : StudioViews.Studio(_studio, StudioPlaceOf(firm), Save.Day, fmt);
        }

        // A product's share of its studio's time among the studio's live products, thousandths.
        private int StudioShareOf(Product p)
        {
            int shares = 0;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product q = _firmsFile.Products[i];
                shares += q.Place == p.Place && q.Stage < 2 ? Math.Max(0, q.Share) : 0;
            }
            return shares > 0 ? (int)((long)Math.Max(0, p.Share) * 1000 / shares) : 0;
        }
    }
}
