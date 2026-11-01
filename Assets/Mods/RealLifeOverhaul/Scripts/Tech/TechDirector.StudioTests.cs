using System;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio's entries in Options, Tests, each on the player's own studio and for real: start a game at once (the
    /// studio's draft pitched as planned), finish its production now, launch it now, and a year of sales for every game on
    /// sale (its year lived at once, the takings booked today). Each says in a notice why it cannot run.
    /// </summary>
    public sealed partial class TechDirector
    {
        // The player's first game studio, or null with a notice why.
        private FirmPlace TestStudio()
        {
            if (!_studioOn || Save == null)
            {
                Notifications.Show(NotificationType.Warning, StudioWanted ? "reallife_studio_test_not_ready" : "reallife_studio_test_off", null, 8f);
                return null;
            }
            for (int i = 0; i < _firms.Count; i++)
            {
                if (_firms[i].File.K == FirmKind.Games)
                {
                    return _firms[i];
                }
            }
            Notifications.Show(NotificationType.Warning, "reallife_studio_test_no_studio", null, 8f);
            return null;
        }

        // The studio's first game that is not over, or null with a notice why.
        private GameDev StudioTestGame(FirmPlace firm, bool building)
        {
            for (int i = 0; firm != null && i < _studio.Games.Count; i++)
            {
                GameDev g = _studio.Games[i];
                if (g.Place == firm.File.Place && g.Phase != (int)GamePhase.Ended && (!building || g.Phase <= (int)GamePhase.Ready))
                {
                    return g;
                }
            }
            if (firm != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_studio_test_no_game", null, 8f);
            }
            return null;
        }

        /// <summary>A game started at once at the first studio: its draft (or a new one) pitched as planned, self-published, with no DRM.</summary>
        public void TestStudioStart()
        {
            try
            {
                FirmPlace firm = TestStudio();
                if (firm == null)
                {
                    return;
                }
                if (FirmsRules.Room(_firmsFile, firm.File.Place) != FirmsRules.RoomYes)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_studio_session_full", null, 8f);
                    return;
                }
                StudioDraft(firm.File.Place);
                int[] choices = { StudioRules.AsPlanned, StudioRules.SelfPublish, StudioRules.NoDrm };
                bool done = StudioSession(StudioRules.SessionPitch, firm.File.Place, firm.File.Name, 1000, choices, out string key, out string[] pairs);
                Notifications.Show(done ? NotificationType.Success : NotificationType.Warning, key, Data(pairs), 10f);
                Logger?.Info("Tech: test: a game " + (done ? "started" : "not started (" + key + ")") + " at " + firm.File.Name + ".");
            }
            catch (Exception e)
            {
                Fault("the studio's test", e);
            }
        }

        /// <summary>The first game being made at the first studio finished now: its work and every part built to plan with its team's craft so far, ready for its day.</summary>
        public void TestStudioFinish()
        {
            try
            {
                FirmPlace firm = TestStudio();
                GameDev g = firm != null ? StudioTestGame(firm, true) : null;
                if (g == null)
                {
                    return;
                }
                if (g.Phase <= (int)GamePhase.Polish)
                {
                    for (int p = 0; p < (int)Part.Count; p++)
                    {
                        long need = g.Plan * StudioCatalogue.PartNeed[p] / 1000;
                        long craft = g.PartDone[p] > 0 ? g.PartCraft[p] / g.PartDone[p] : StudioCatalogue.CraftBase + 30;
                        if (g.PartDone[p] < need)
                        {
                            g.PartCraft[p] += (need - g.PartDone[p]) * craft;
                            g.PartDone[p] = need;
                        }
                    }
                    g.Done = g.Work;
                    g.Phase = (int)GamePhase.Polish;
                    StudioRules.Advance(g, Save.Day);
                }
                g.Milestone = -1;
                g.CertUntil = Save.Day;
                g.ReleaseDay = Save.Day + 1;   // held for a day, so the launch test or tomorrow's turn launches it
                SyncProducts();
                ChangedTech();
                Notifications.Show(NotificationType.Success, "reallife_studio_test_finished", Data("game", NameOf(g.Id), "quality", StudioRules.Quality(g).ToString(Inv)), 10f);
            }
            catch (Exception e)
            {
                Fault("the studio's test", e);
            }
        }

        /// <summary>The first game of the first studio launched now: finished first if it was still being made.</summary>
        public void TestStudioLaunch()
        {
            try
            {
                FirmPlace firm = TestStudio();
                GameDev g = firm != null ? StudioTestGame(firm, true) : null;
                if (g == null)
                {
                    return;
                }
                if (g.Phase != (int)GamePhase.Ready)
                {
                    TestStudioFinish();
                }
                g.CertUntil = Save.Day;
                LaunchNow(g, Save.Day);
                ChangedTech();
                if (!g.OnSale)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_studio_test_no_game", null, 8f);
                }
            }
            catch (Exception e)
            {
                Fault("the studio's test", e);
            }
        }

        /// <summary>
        /// A year of sales at once for every game on sale at the player's studios: each lives its next year of the save as if it
        /// had launched a year earlier (its days moved back first, so the windows and half-lives fall right), the year's
        /// takings booked today at each studio, and what it came to told.
        /// </summary>
        public void TestStudioYear()
        {
            try
            {
                if (TestStudio() == null)
                {
                    return;
                }
                int day = Save.Day, year = GameCalendar.DaysPerYear, games = 0, level = Prices.Level;
                long copies = 0, kept = 0;
                for (int i = 0; i < _studio.Games.Count; i++)
                {
                    GameDev g = _studio.Games[i];
                    Product p = _firmsFile.ProductOf(g.Id);
                    FirmPlace firm = FirmOf(g.Place);
                    if (!g.OnSale || p == null || firm == null)
                    {
                        continue;
                    }
                    Shift(g, -year);
                    long net = 0, sold = 0;
                    for (int d = day - year + 1; d <= day; d++)
                    {
                        StudioRules.GenreAppeals(_studio, d, _genreAppeal);
                        GameSales r = StudioRules.SellDay(g, _studio, d, _genreAppeal[g.Genre], _platforms, 1000, StudioRules.Season(d, GameCalendar.DaysPerYear));
                        StudioRules.ReportDay(g);
                        net += r.Payout;
                        sold += r.Units;
                    }
                    FirmsGame.Takings(net * level / 1000, firm.Reg, firm.File.Name, null);
                    games++;
                    copies += sold;
                    kept += net;
                }
                SyncProducts();
                ChangedTech();
                Notifications.Show(games > 0 ? NotificationType.Success : NotificationType.Warning, games > 0 ? "reallife_studio_test_year_done" : "reallife_studio_test_none_on_sale",
                    Data("games", games.ToString(Inv), "copies", TechText.Whole(copies), "amount", TechText.Cents(kept * level / 1000)), 12f);
                Logger?.Info("Tech: test: a year of sales for " + games + " game(s): " + copies.ToString(Inv) + " copies, " + kept.ToString(Inv) + " cents kept at the start's prices.");
            }
            catch (Exception e)
            {
                Fault("the studio's test", e);
            }
        }

        // A game's days moved by so many (back: as if launched earlier), so a year lived at once falls on the right ages.
        private static void Shift(GameDev g, int by)
        {
            g.Launched += by;
            g.WindowStart = g.WindowStart >= 0 ? g.WindowStart + by : g.WindowStart;
            g.AddOnStart = g.AddOnStart >= 0 ? g.AddOnStart + by : g.AddOnStart;
            g.SaleUntil = Math.Max(0, g.SaleUntil + by);
            g.LastLeadsPay += by;
        }
    }
}
