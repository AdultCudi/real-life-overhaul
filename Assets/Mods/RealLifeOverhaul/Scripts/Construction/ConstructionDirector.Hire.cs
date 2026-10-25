using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the player as a client: a building of theirs (a plot on their lot or a rebuild of a building they
    /// own, its plans drawn and its permission granted) put up by one of the city's eight builders under contract, with or
    /// without a company of their own. Each builder quotes as it bids on a tender (CommissionRules, the week's quotes the same on every
    /// look), its days and its due day from its site's own schedule in the city's working day (SiteRules.HandoverDay); the one
    /// hired takes a deposit, sets the site out in the city's programme and works it at its own pace through the same three
    /// inspections, booking the inspector itself the hour each falls due and paid at each one passed, and the player hears of
    /// each stage, booking and verdict, and of the day it runs late; at the handover the retention is paid less a penalty for
    /// each day late, and the building is the player's as their company's own builds are. On a press, a site's working hour,
    /// the day's pass and the handover; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly TenderBid[] _quotes = new TenderBid[BuildCatalogue.RivalCount];
        private readonly int[] _busy = new int[BuildCatalogue.RivalCount];

        /// <summary>True for a project a builder of the city's can be hired for: a plot or a rebuild of the player's, not yet started.</summary>
        public static bool HireKind(ProjectFile p)
        {
            return p != null && (p.Kind == ProjectKind.Plot || p.Kind == ProjectKind.Rebuild) && p.Contractor < 0;
        }

        /// <summary>What stands in the way of hiring a builder for a project now, as a text key; null when one can be hired.</summary>
        public string HireBlock(ProjectFile p)
        {
            if (!HireKind(p))
            {
                return "reallife_bld_answer_hire_kind";
            }
            if (p.Stage != ProjectStage.Ready)
            {
                return p.Stage == ProjectStage.Building ? "reallife_bld_answer_building" : "reallife_bld_answer_no_permission";
            }
            if (p.Estimate <= 0)
            {
                return "reallife_bld_answer_hire_no_plans";
            }
            if (SiteCount() >= BuildCatalogue.SitesMost)
            {
                return "reallife_bld_answer_city_full";
            }
            if (p.Model == ProjectKind.Plot && !Lot(p.Lot, out _))
            {
                return "reallife_bld_answer_no_lot";
            }
            if (p.Kind == ProjectKind.Rebuild && !ConstructionGame.Owns(p.Street, p.Number))
            {
                return "reallife_bld_answer_not_yours";
            }
            return null;
        }

        /// <summary>
        /// Every builder's quote for a project this week, into the caller's array (one for each of the city's builders): its price
        /// (a bid on the plans' estimate and a client's margin), its working days to the handover by its site's own schedule at the
        /// pace its bid needs, the inspections' waits in them, and its score as a client weighs it. The count.
        /// </summary>
        public int Quotes(ProjectFile p, TenderBid[] into)
        {
            int count = Bids(p, into);
            if (count == 0)
            {
                return 0;
            }
            int hours = SiteHours(p), permits = Permits(), usual = CommissionRules.UsualDays(hours, permits);
            for (int r = 0; r < count; r++)
            {
                into[r].Days = CommissionRules.Days(p.Model, hours, CommissionRules.Promised(usual, into[r].Days), permits);
            }
            return count;
        }

        // Every builder's bid as on a tender, its days of works drawn round the usual days of the hours its site is really set out
        // with, in the city's working day (a tender's ten hours a day knew nothing of the building code).
        private int Bids(ProjectFile p, TenderBid[] into)
        {
            if (p == null || into == null)
            {
                return 0;
            }
            for (int r = 0; r < _busy.Length; r++)
            {
                _busy[r] = Busy(r);   // a builder with the city's works on its hands asks more
            }
            int week = FirmRules.Week(ConstructionGame.Day);
            int days = CommissionRules.UsualDays(SiteHours(p), Permits());
            return CommissionRules.Bids(_state.Seed, CommissionRules.Salt(p.Id, week), CommissionRules.Value(p.Estimate), days, CommissionRules.NeedOf(p.Kind, p.Use), _busy, into);
        }

        /// <summary>
        /// One of the city's builders hired for a project at this week's quote: the deposit now, the site set out at once in the
        /// city's programme, the builder's crews at work from the next working hour, and its due day the day its site's schedule
        /// hands the building over at the pace its quote needs. A test's contract (free) moves no money. Returns a text key.
        /// </summary>
        public string HireBuilder(ProjectFile p, int rival, bool free)
        {
            string block = HireBlock(p);
            if (block != null)
            {
                return block;
            }
            int count = Bids(p, _quotes);
            int r = BuildCatalogue.Clamp(rival, 0, count - 1);
            TenderBid bid = _quotes[r];   // its days of works as it bid them; the price as its quote shows it
            long price = CommissionRules.Price(bid);
            long deposit = CommissionRules.Deposit(price);
            string name = ProjectName(p);
            if (!free && !ConstructionGame.Take(deposit, "reallife_transaction_bld_builder", name, true))
            {
                return "reallife_bld_answer_no_money";
            }
            FreeProject(p);   // the company's crews, plant and site manager are not wanted on a builder's site
            int site = StartSite(p, r, out string why);
            if (site <= 0)
            {
                if (!free)
                {
                    ConstructionGame.Give(deposit, "reallife_transaction_bld_builder_back", name);   // nothing started: the deposit comes back
                }
                Logger?.Info("Construction: project " + p.Id + "'s builder could not set its site out: " + why + ".");
                return "reallife_bld_answer_site_refused";
            }
            int day = ConstructionGame.Day, permits = Permits();
            int usual = CommissionRules.UsualDays(p.Hours, permits), promised = CommissionRules.Promised(usual, bid.Days);
            long first = (long)day * 24 + ConstructionGame.Hour + 1;   // the programme works the site from the next hour
            p.Contractor = r;
            p.ContractorPrice = price;
            p.HireDays = CommissionRules.Days(p.Model, p.Hours, promised, permits);   // the working days it quoted, to the handover
            p.HirePace = CommissionRules.Pace(_state.Seed, p.Id, r, bid.Days, usual, _state.Rivals[r].Reputation);
            p.HireStages = 0;
            p.HirePenalty = 0;
            p.HireFree = free;
            p.HirePaid = deposit;
            if (!free)
            {
                Spend(p, deposit);
            }
            p.SiteId = site;
            p.Stage = ProjectStage.Building;
            p.StartDay = day;
            p.DueDay = CommissionRules.Due(p.Model, p.Hours, promised, permits, (int)(first / 24), (int)(first % 24));   // its site's own schedule at the pace it quoted
            p.StageSeen = -1;
            p.WaitHours = 0;
            if (ClosesShop(p))
            {
                HoldShut(p);
            }
            LoanDraw(p, 0);
            _state.Rivals[r].Won++;
            if (!GoingPublic.InSession)
            {
                RivalNews(r, RivalTenderWon, 2);   // a contract won, as a client's tender would be: the stock market hears it on one machine only outside a session
            }
            Logger?.Info("Construction: project " + p.Id + " hired builder " + r + " for " + price + ": " + bid.Days + " working day(s) of works on the site's " + p.Hours
                         + " working hours, the " + p.HireDays + " working day(s) of its quote to the handover at the " + promised + " in 1000 of the city's pace it needs (permits "
                         + permits + "), its crews at " + p.HirePace + ", site " + site + ", due on day " + p.DueDay
                         + (free ? " (a test's contract: no money moves)" : ", deposit " + deposit + " paid") + ".");
            Changed();
            return "reallife_bld_answer_hired_builder";
        }

        /// <summary>The projects a builder of the city's has built or is building for the player.</summary>
        public int HiredBy(int rival)
        {
            int n = 0;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                n += _state.Projects[i].Hired && _state.Projects[i].Contractor == rival && _state.Projects[i].Stage != ProjectStage.Abandoned ? 1 : 0;
            }
            return n;
        }

        /// <summary>Opens the app on a project's quotes.</summary>
        public void OpenQuotes(int project)
        {
            if (_app != null && _view != null && Running)
            {
                _view.WantQuotes(project);
                _app.Open();
            }
        }

        // ------------------------------------------------------------------ the works

        // A new stage on a builder's site: its own materials, plant and crews, so nothing is ordered or hired by the player.
        private void HiredStage(ProjectFile p, WorkStage stage)
        {
            p.StageSeen = (int)stage;
            p.Visited = false;
            p.PaceSum = 0;
            p.PaceDays = 0;
            Array.Clear(p.Need, 0, p.Need.Length);
            Array.Clear(p.Have, 0, p.Have.Length);
            Logger?.Info("Construction: project " + p.Id + " (" + ProjectName(p) + ") starts its " + stage + " stage, builder " + p.Contractor + "'s crews at " + p.HirePace + " in 1000.");
            TellHire(NotificationType.Info, "reallife_bld_notice_hire_begun", p, 0L, BuildCatalogue.StageKeys[BuildCatalogue.Clamp((int)stage, 0, BuildCatalogue.StageKeys.Length - 1)]);
        }

        // The builder books the inspector the hour (day and hour) the inspection falls due, for the next working day, and pays the fee itself.
        private void HiredBooks(ProjectFile p, int inspection, int day, int hour)
        {
            p.Insp[inspection] = InspectionState.Booked;
            p.InspDay[inspection] = SiteRules.InspectorDay(day, hour);
            Logger?.Info("Construction: builder " + p.Contractor + " booked project " + p.Id + "'s " + (Inspection)inspection + " inspection for day " + p.InspDay[inspection]
                         + " (ready on day " + day + " at " + hour.ToString("00", CultureInfo.InvariantCulture) + ":00).");
            TellHire(NotificationType.Info, "reallife_bld_notice_hire_booked", p, 0L, BuildCatalogue.InspectionKeys[inspection], p.InspDay[inspection]);
        }

        // A builder's site that an older version held at a stage's end for good goes on from where it stands: a hired builder never
        // waits for materials, so the hours its project counts as lost are those it stood still. Told once; its due day stays as it
        // was signed, so a builder that stood past it gives back its days late as the Guide says, and no inspection or payment is
        // skipped or made twice (they come as its site reaches them, each stage paid once).
        private void HireResumed(ProjectFile p, double progress, int day)
        {
            int percent = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, progress)) * 100.0);
            int late = Math.Max(0, day - p.DueDay);
            long perDay = CommissionRules.Penalty(p.ContractorPrice, 1), most = CommissionRules.Penalty(p.ContractorPrice, 1000);
            Logger?.Info("Construction: project " + p.Id + " (" + ProjectName(p) + "): builder " + p.Contractor + "'s site at " + percent + "% stood still for " + p.WaitHours
                         + " working hour(s) at the end of its " + (WorkStage)Math.Max(0, p.StageSeen) + " stage, held there by an older version; it goes on from where it stands, with "
                         + HireWords(p) + ", every inspection and stage payment still to come in turn; due on day " + p.DueDay
                         + (late > 0 ? ", " + late + " day(s) late now: " + perDay + " a day off its price at the handover, " + most + " at most." : "."));
            p.WaitHours = 0;
            TellHire(NotificationType.Info, late > 0 ? "reallife_bld_notice_hire_resumed_late" : "reallife_bld_notice_hire_resumed", p, perDay, null, p.DueDay, most, percent);
            Changed();
        }

        // The day's pass: a builder whose due day went by since the last pass (the day before this one, or before a gap the clock
        // jumped) and has not handed its building over is late; the player hears of it once, with what it costs the builder.
        private void HiresDay(int day, int before)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (!p.Hired || p.Stage != ProjectStage.Building || p.DueDay >= day || p.DueDay < before)
                {
                    continue;
                }
                long perDay = CommissionRules.Penalty(p.ContractorPrice, 1), most = CommissionRules.Penalty(p.ContractorPrice, 1000);
                SiteFacts site = SiteOf(p.SiteId);
                int percent = site.Known && site.Hours > 0f ? (int)Math.Round(Math.Min(1.0, site.Done / (double)site.Hours) * 100.0) : 0;
                Logger?.Info("Construction: builder " + p.Contractor + " is late with project " + p.Id + " (" + ProjectName(p) + "): due on day " + p.DueDay + ", its site at " + percent
                             + "% in its " + (WorkStage)Math.Max(0, p.StageSeen) + " stage; " + perDay + " a day comes off its price at the handover, " + most + " at most.");
                TellHire(NotificationType.Warning, "reallife_bld_notice_hire_overdue", p, perDay, null, p.DueDay, most, percent);
            }
        }

        // An inspection passed on a builder's site: its stage's payment falls due, through the game's own money call.
        private void HireStagePaid(ProjectFile p, int inspection)
        {
            if (inspection < 0 || inspection >= BuildCatalogue.HireStagePermille.Length || (p.HireStages & (1 << inspection)) != 0)
            {
                return;
            }
            p.HireStages |= 1 << inspection;
            long pay = CommissionRules.StagePay(p.ContractorPrice, inspection);
            if (!p.HireFree)
            {
                ConstructionGame.Force(pay, "reallife_transaction_bld_builder", ProjectName(p), true);   // a contract's payment runs on as the game's own loan interest does
                Spend(p, pay);
            }
            p.HirePaid += pay;
            Logger?.Info("Construction: project " + p.Id + " passed its " + (Inspection)inspection + " inspection; builder " + p.Contractor + " paid " + pay + " (" + p.HirePaid + " of "
                         + p.ContractorPrice + ").");
            TellHire(NotificationType.Success, "reallife_bld_notice_hire_stage", p, pay, BuildCatalogue.InspectionKeys[inspection]);
        }

        // The handover's settling up: the retention, less a penalty for each day past the day due; the builder's record.
        private void HireSettled(ProjectFile p, int day)
        {
            for (int k = 0; k < BuildCatalogue.HireStagePermille.Length; k++)
            {
                HireStagePaid(p, k);   // a stage the handover overtook is paid with it
            }
            int late = Math.Max(0, day - p.DueDay);
            long penalty = CommissionRules.Penalty(p.ContractorPrice, late);
            long pay = CommissionRules.Settle(p.ContractorPrice, p.HirePaid, penalty, out long back);
            if (!p.HireFree)
            {
                ConstructionGame.Force(pay, "reallife_transaction_bld_builder", ProjectName(p), true);
                Spend(p, pay);
                ConstructionGame.Give(back, "reallife_transaction_bld_builder_back", ProjectName(p));
                p.Spent = Math.Max(0L, p.Spent - back);
            }
            p.HirePaid += pay - back;
            p.HirePenalty = penalty;
            RivalFile rival = _state.Rivals[p.Contractor];
            rival.Built++;
            rival.Reputation = FirmRules.Moved(rival.Reputation, late > 0 ? -BuildCatalogue.RepFailed : 1);
            if (!GoingPublic.InSession)
            {
                RivalNews(p.Contractor, late > 0 ? RivalLate : RivalFinished, 2);
            }
            Logger?.Info("Construction: builder " + p.Contractor + " handed project " + p.Id + " over on day " + day + " (due " + p.DueDay + ", " + late + " day(s) late): paid " + pay
                         + " at the handover, " + back + " back to the client, a penalty of " + penalty + "; " + p.HirePaid + " paid of " + p.ContractorPrice + ".");
        }

        // A notice about a builder's works: the project, the builder, an amount, what was inspected or begun, a day, the most a
        // penalty comes to and how far the site stands.
        private void TellHire(NotificationType type, string key, ProjectFile p, long amount, string whatKey = null, int day = 0, long most = 0L, int percent = 0)
        {
            var data = new Dictionary<string, string>(8)
            {
                { "name", ProjectName(p) },
                { "builder", ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(p.Contractor, 0, BuildCatalogue.RivalCount - 1)]) },
                { "amount", ConstructionText.Money(amount) },
                { "what", whatKey != null ? ConstructionText.T(whatKey) : string.Empty },
                { "day", day > 0 ? ConstructionText.Day(day) : string.Empty },
                { "most", ConstructionText.Money(most) },
                { "percent", ConstructionText.N(percent) }
            };
            int id = p.Id;
            ConstructionGame.Tell(type, key, data, () => Open(TabProjects, id));
        }

        /// <summary>A builder's contract in words for the log and the tests: who, the price, the days and what is paid.</summary>
        internal static string HireWords(ProjectFile p)
        {
            return "builder " + p.Contractor + ", " + p.ContractorPrice.ToString(CultureInfo.InvariantCulture) + " in " + p.HireDays + " working day(s), paid "
                   + p.HirePaid.ToString(CultureInfo.InvariantCulture) + (p.HirePenalty > 0 ? ", a penalty of " + p.HirePenalty.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }
    }
}
