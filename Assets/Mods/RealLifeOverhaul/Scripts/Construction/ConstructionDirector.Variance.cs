using System;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The height variance hearing: plans that go above the floors the building code allows on their plot can ask City Hall's council to lift the cap, for the zoning fee twice over,
    /// instead of being refused outright at the planning decision. The council hears it at its next sitting the way it hears a change of zoning (the mod's one council, through
    /// the Council bridge, at the sitting and without the player's vote), with a suit worked out by <see cref="VarianceRules.Suit"/>; the city's builders ask by the same rule
    /// when they extend a building to the top of the code. A granted variance stands for the floors it was asked for. The day's pass hears what is due; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>True for a project whose floors the code caps: a building on a plot, a rebuild or an extension, the company's own or built by its hired builder.</summary>
        public static bool VarianceKind(ProjectFile p)
        {
            return p != null && (p.Kind == ProjectKind.Plot || p.Kind == ProjectKind.Rebuild || p.Kind == ProjectKind.Extension) && p.Grow <= 0 && (p.Contractor < 0 || p.Hired);
        }

        /// <summary>True when a granted variance covers the floors the plans have now.</summary>
        public static bool VarianceCovers(ProjectFile p) => p != null && p.Variance == VarianceState.Granted && p.VarianceFloors >= p.Storeys;

        /// <summary>The floors the plans are over the code's limit, whatever a variance says; 0 when they are within it.</summary>
        public static int VarianceOver(ProjectFile p) => VarianceKind(p) ? VarianceRules.Over(p.Storeys, HeightLimitOf(p)) : 0;

        /// <summary>The limit the planning decision holds the plans to: the code's, or the floors a granted variance covers.</summary>
        public static int LimitFor(ProjectFile p) => VarianceCovers(p) ? Math.Max(HeightLimitOf(p), p.Storeys) : HeightLimitOf(p);

        /// <summary>What the hearing costs the player's company.</summary>
        public static long VarianceFee => VarianceRules.Fee(ZoneFee);

        /// <summary>Why a variance cannot be asked now (a text key), or null.</summary>
        public string VarianceBlock(ProjectFile p)
        {
            if (!VarianceKind(p) || !p.Live || p.Stage > ProjectStage.Planning)
            {
                return "reallife_ui_cons_answer_variance_none";
            }
            if (p.Variance == VarianceState.Pending)
            {
                return "reallife_ui_cons_answer_variance_waits";
            }
            if (!p.PlansDone)
            {
                return "reallife_ui_cons_answer_variance_plans";
            }
            if (p.Planning == PlanningState.Submitted || p.Planning == PlanningState.Appealed)
            {
                return "reallife_bld_answer_planning_waits";
            }
            int over = VarianceOver(p);
            if (over <= 0 || VarianceCovers(p))
            {
                return "reallife_ui_cons_answer_variance_none";
            }
            return VarianceRules.Askable(over) ? null : "reallife_ui_cons_answer_variance_tall";
        }

        /// <summary>The floors above the code put to the council: its fee now, its hearing at the next sitting. Returns a text key.</summary>
        public string AskVariance(ProjectFile p)
        {
            string block = VarianceBlock(p);
            if (block != null)
            {
                return block;
            }
            long fee = VarianceFee;
            if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, fee);
            int day = ConstructionGame.Day;
            p.Variance = VarianceState.Pending;
            p.VarianceDay = CouncilDay(day);
            p.VarianceFloors = p.Storeys;
            p.VarianceAyes = p.VarianceNoes = -1;
            Logger?.Info("Construction: project " + p.Id + " asked the council to allow " + p.Storeys + " floors where the code allows " +
                HeightLimitOf(p) + " (fee " + fee + "), heard on day " + p.VarianceDay + ".");
            Changed();
            return "reallife_ui_cons_answer_variance_asked";
        }

        // The day: each variance whose sitting has come is heard.
        private void VarianceDayPass(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Variance == VarianceState.Pending && day >= p.VarianceDay)
                {
                    HearVariance(p, day);
                }
            }
        }

        private void HearVariance(ProjectFile p, int day)
        {
            if (!p.Live)
            {
                p.Variance = VarianceState.None;
                return;
            }
            int over = VarianceRules.Over(p.VarianceFloors, HeightLimitOf(p));
            string place = !string.IsNullOrEmpty(p.Lot) ? p.Lot : p.Street + " " + p.Number;
            int lotClass = Lot(p.Lot, out LotView lot) ? lot.Class : 0;
            int district = lot.District;
            int suit = VarianceRules.Suit(Math.Max(1, over), p.Quality, _state.Firm.Reputation, _state.Firm.Level, Permits(), string.IsNullOrEmpty(p.Lot) ? -1 : DemandFor(p.Use, district));
            bool carried = over <= 0 || VarianceHearing(VarianceRules.Matter(place, p.VarianceFloors), lotClass, p.Use, suit, _state.Firm.Reputation, out p.VarianceAyes, out p.VarianceNoes);
            p.Variance = carried ? VarianceState.Granted : VarianceState.Refused;
            Logger?.Info("Construction: the council heard project " + p.Id + "'s variance for " + p.VarianceFloors + " floors on day " + day +
                (p.VarianceAyes >= 0 ? " (" + p.VarianceAyes + " to " + p.VarianceNoes + ")" : " (the planners' odds " + suit + ")")
                         + ": " + (carried ? "allowed" : "refused") + ".");
            Tell(carried ? NotificationType.Success : NotificationType.Warning, carried ? "reallife_ui_cons_notice_variance_yes" :
                "reallife_ui_cons_notice_variance_no", ProjectName(p), null, TabProjects, p.Id,
                p.VarianceFloors);
            Changed();
        }

        /// <summary>
        /// The one hearing of a variance, for the player's company and the city's builders alike: the council's members vote on it as on a change of zoning (decided at the sitting, never
        /// before the player's meeting), and where no council answers the planners vote at the same suit. <paramref name="ayes"/> and <paramref name="noes"/> are -1 for the planners' vote.
        /// </summary>
        internal bool VarianceHearing(string matter, int lotClass, BuildUse use, int suit, int reputation, out int ayes, out int noes)
        {
            int vote = CouncilVote(matter, lotClass, use, suit, reputation, true, out ayes, out noes);
            if (vote == -2 || vote == -1)
            {
                ayes = noes = -1;
                return VarianceRules.Planners(_state.Seed, matter, suit);
            }
            return vote == 1;
        }

        // The city's builders extend a building to the top of the code and, now and then, ask for one floor more; the council hears them by the same rule as the player's company.
        private void CityVariance(RedevFile r, int limit, int day)
        {
            RivalFile rival = _state.Rivals[BuildCatalogue.Clamp(r.Rival, 0, BuildCatalogue.RivalCount - 1)];
            int level = LevelRules.Rival(rival.Built, rival.Reputation);
            if (r.Storeys < limit || limit >= RedevelopRules.StoreysMost || !LevelRules.Allows(level, r.Storeys + 1) || RedevelopRules.PickOf(_state.Seed, day, 0x52, 2) != 0)
            {
                return;
            }
            string matter = VarianceRules.Matter(r.Lot, r.Storeys + 1);
            int suit = VarianceRules.Suit(1, Math.Min(95, rival.Reputation + 10), rival.Reputation, level, Permits(), DemandFor(r.Use, r.Lot));
            rival.VarianceAsked++;
            bool carried = VarianceHearing(matter, 0, r.Use, suit, rival.Reputation, out int ayes, out int noes);
            if (carried)
            {
                r.Storeys++;
                rival.VarianceGranted++;
            }
            Paper(carried ? "reallife_ui_cons_paper_variance_yes" : "reallife_ui_cons_paper_variance_no", CityName(r), r.Storeys + (carried ? 0 : 1));
            Logger?.Info("Construction: " + DeveloperName(r) + " asked the council for " + (r.Storeys + (carried ? 0 : 1)) + " floors on " + r.Lot + " where the code allows " + limit + ": "
                         + (carried ? "allowed" : "refused") + (ayes >= 0 ? " (" + ayes + " to " + noes + ")" : " (the planners' odds " + suit + ")") + ".");
        }
    }
}
