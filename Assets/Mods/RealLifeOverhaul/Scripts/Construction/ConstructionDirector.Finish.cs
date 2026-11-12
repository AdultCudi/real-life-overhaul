using System;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The fit-out finish of a project (<see cref="FinishRules"/>): chosen on the project's page from the plans until the site is set out or the window City Hall's permission
    /// opens (ten days) closes, the standard finish taken for a company that did not choose. It changes the materials the site asks for and the estimate (one rule with the hedge's
    /// exposure), the working hours the site and the builders' quotes are set out with, and the rent the finished building's deed asks. The city's builders pick a tier by the same
    /// rule (<see cref="FinishRules.ByMeans"/>) for their own developments, which then take that tier's days and clear that tier's gain. Nothing per frame: a press and the day's pass.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>
        /// True for a project that has a finish to choose: the company's own building on a plot or a rebuild (its builder hired or its own crews),
        /// not Let it grow's, which has its tier.
        /// </summary>
        public static bool FinishKind(ProjectFile p)
        {
            return p != null && (p.Kind == ProjectKind.Plot || p.Kind == ProjectKind.Rebuild) && p.Grow <= 0 && (p.Contractor < 0 || p.Hired);
        }

        /// <summary>The tier a project is finished to now: Let it grow's own tier, else the one chosen (the standard finish while none is).</summary>
        public static int FinishTier(ProjectFile p)
        {
            return p != null && p.Grow > 0 ? FinishRules.Clamp(p.Grow - 1) : FinishRules.TierOf(p != null ? p.Finish : 0);
        }

        /// <summary>True while the finish may still be chosen: before the site is set out and before the window closes.</summary>
        public static bool FinishOpen(ProjectFile p)
        {
            return FinishKind(p) && p.Live && !p.FinishFixed && p.Stage <= ProjectStage.Ready;
        }

        /// <summary>Why the finish cannot be chosen now (a text key), or null.</summary>
        public string FinishBlock(ProjectFile p)
        {
            if (!FinishKind(p))
            {
                return "reallife_ui_cons_answer_finish_none";
            }
            return FinishOpen(p) ? null : "reallife_ui_cons_answer_finish_locked";
        }

        /// <summary>A project's finish chosen: its estimate follows the new finish's cost, the site's hours and the rent follow it later. Returns a text key.</summary>
        public string SetFinish(ProjectFile p, int tier)
        {
            string block = FinishBlock(p);
            if (block != null)
            {
                return block;
            }
            int t = FinishRules.Clamp(tier), was = p.Finish;
            if (FinishRules.TierOf(was) == t && was > 0)
            {
                return "reallife_ui_cons_answer_finish_set";
            }
            if (p.Estimate > 0L)
            {
                p.Estimate = FinishRules.Estimate(p.Estimate, was, t);
            }
            p.Finish = t + 1;
            Logger?.Info("Construction: project " + p.Id + "'s finish chosen: " + (GrowTier)t + ", its estimate now " + p.Estimate + ", its site " + SiteHours(p) + " working hours.");
            Changed();
            return "reallife_ui_cons_answer_finish_set";
        }

        /// <summary>What a project would cost at a tier: its estimate under that finish (the plans' estimate while they are not drawn).</summary>
        public long FinishEstimate(ProjectFile p, int tier)
        {
            long basis = p.PlansDone && p.Estimate > 0L ? p.Estimate : Preview(p, out _, out _, out _, out _).Total;
            return FinishRules.Estimate(basis, p.Finish, tier);
        }

        /// <summary>The working days a tier adds to the standard finish's for a project (negative: it saves them).</summary>
        public int FinishDays(ProjectFile p, int tier)
        {
            int standard = p.Model != ProjectKind.Contract || p.Hours <= 0 ? PlansRules.Hours(p.Use, p.Area, p.Storeys, p.Kind) : p.Hours;
            return FinishRules.DaysMore(standard, tier);
        }

        // The day: the window opens when a project is ready to build and closes ten days later with the standard finish taken for a company that did not choose; a finished building's
        // deed asks the finish's rent once it is written.
        private void FinishDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (FinishKind(p) && p.Live && !p.FinishFixed && p.Stage == ProjectStage.Ready)
                {
                    if (p.FinishBy <= 0)
                    {
                        p.FinishBy = FinishRules.Deadline(day);
                        Tell(NotificationType.Info, "reallife_ui_cons_notice_finish_open", ProjectName(p), null, TabProjects, p.Id, p.FinishBy);
                        Changed();
                    }
                    else if (day > p.FinishBy)
                    {
                        p.FinishFixed = true;
                        Logger?.Info("Construction: project " + p.Id + "'s window to choose a finish closed on day " + p.FinishBy + ": " + (GrowTier)FinishTier(p) + " it is.");
                        Tell(NotificationType.Info, "reallife_ui_cons_notice_finish_default", ProjectName(p), GrowRules.TierKeys[FinishTier(p)], TabProjects, p.Id);
                        Changed();
                    }
                }
                if (p.Stage == ProjectStage.Finished && FinishKind(p) && !p.FinishRented && p.Handed > 0)
                {
                    FinishRentDay(p, day);
                }
            }
        }

        // A finished building of the player's asks the rent of its finish once its deed is written; the standard finish asks the market's
        // and is left alone, and a block whose flats are sold has no rent.

        private void FinishRentDay(ProjectFile p, int day)
        {
            int tier = FinishTier(p);
            if (tier == FinishRules.Standard || p.After == AfterUse.SellFlats && p.Use == BuildUse.Homes)
            {
                p.FinishRented = true;
                return;
            }
            string street = p.Street;
            int number = p.Number;
            if (p.Kind == ProjectKind.Plot && Space(p.Lot, out LotView lot))
            {
                street = lot.Street;
                number = lot.Number;
            }
            if (!string.IsNullOrEmpty(street) && ConstructionGame.DeedRent(street, number, (GrowTier)tier, out float rent))
            {
                p.FinishRented = true;
                Logger?.Info("Construction: project " + p.Id + "'s building at " + street + " " + number + " asks " + rent.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                             + " a square metre a day (" + (GrowTier)tier + " finish).");
                Tell(NotificationType.Success, "reallife_ui_cons_notice_finish_rent", ProjectName(p), GrowRules.TierKeys[tier], TabProjects, p.Id);
                Changed();
            }
            else if (day > p.Handed + GrowRules.PatienceDays)
            {
                p.FinishRented = true;   // no deed came: the market's rent stands
            }
        }

        // The city's builders: the tier a builder picks for its own development by what it can afford, one rule with the company's default.
        internal GrowTier BuilderFinish(int rival, long estimate, int demand)
        {
            RivalFile r = _state.Rivals[BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1)];
            long worth = AuctionRules.Worth(r.Reputation, r.Built, r.Won, ConstructionGame.Prices, r.Gained);
            long cash = AuctionRules.Cash(worth, LandHeldBy(rival, string.Empty, out _));
            return (GrowTier)FinishRules.ByMeans(cash, estimate, demand);
        }
    }
}
