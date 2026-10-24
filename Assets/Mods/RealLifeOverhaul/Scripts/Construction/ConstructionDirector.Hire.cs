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
    /// look); the one hired takes a deposit, sets the site out in the city's programme and works it at its own pace through the
    /// same three inspections, booking the inspector itself and paid at each one passed; at the handover the retention is paid
    /// less a penalty for each day late, and the building is the player's as their company's own builds are. On a press, the
    /// hour's inspections and the handover; nothing per frame.
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
        /// (a bid on the plans' estimate and a client's margin), its working days and its score as a client weighs it. The count.
        /// </summary>
        public int Quotes(ProjectFile p, TenderBid[] into)
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
            int days = TenderRules.StandardDays(SiteHours(p));
            return CommissionRules.Bids(_state.Seed, CommissionRules.Salt(p.Id, week), CommissionRules.Value(p.Estimate), days, CommissionRules.NeedOf(p.Kind, p.Use), _busy, into);
        }

        /// <summary>
        /// One of the city's builders hired for a project at this week's quote: the deposit now, the site set out at once in the
        /// city's programme, and the builder's crews at work from the next working hour. A test's contract (free) moves no money.
        /// Returns a text key.
        /// </summary>
        public string HireBuilder(ProjectFile p, int rival, bool free)
        {
            string block = HireBlock(p);
            if (block != null)
            {
                return block;
            }
            int count = Quotes(p, _quotes);
            int r = BuildCatalogue.Clamp(rival, 0, count - 1);
            TenderBid bid = _quotes[r];
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
            int day = ConstructionGame.Day;
            p.Contractor = r;
            p.ContractorPrice = price;
            p.HireDays = Math.Max(1, bid.Days);
            p.HirePace = CommissionRules.Pace(_state.Seed, p.Id, r, p.HireDays, TenderRules.StandardDays(p.Hours), _state.Rivals[r].Reputation);
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
            p.DueDay = PlanningRules.AfterWorkingDays(day, p.HireDays + 2);   // its quoted days and a day for each inspection's wait, as the company's own
            p.StageSeen = -1;
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
            Logger?.Info("Construction: project " + p.Id + " hired builder " + r + " for " + price + " in " + p.HireDays + " working day(s) at " + p.HirePace
                         + " in 1000 of the city's pace, site " + site + ", due on day " + p.DueDay + (free ? " (a test's contract: no money moves)" : ", deposit " + deposit + " paid") + ".");
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
        }

        // The builder books the inspector for the next working day and pays the fee itself.
        private void HiredBooks(ProjectFile p, int inspection)
        {
            p.Insp[inspection] = InspectionState.Booked;
            p.InspDay[inspection] = SiteRules.InspectorDay(ConstructionGame.Day, ConstructionGame.Hour);
            Logger?.Info("Construction: builder " + p.Contractor + " booked project " + p.Id + "'s " + (Inspection)inspection + " inspection for day " + p.InspDay[inspection] + ".");
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

        // A notice about a builder's works: the project, the builder, an amount and what was inspected.
        private void TellHire(NotificationType type, string key, ProjectFile p, long amount, string whatKey = null)
        {
            var data = new Dictionary<string, string>(4)
            {
                { "name", ProjectName(p) },
                { "builder", ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(p.Contractor, 0, BuildCatalogue.RivalCount - 1)]) },
                { "amount", ConstructionText.Money(amount) },
                { "what", whatKey != null ? ConstructionText.T(whatKey) : string.Empty }
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
