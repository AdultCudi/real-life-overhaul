using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        /// <summary>
        /// Does what a press on the page stands for, through the director that already owns it: the same calls the old app's buttons made. Moving about is the screen's own and never
        /// comes here. A typed figure comes in C. False when it could not be done (nothing to act on, the part off); the director says why in the game's own notice.
        /// </summary>
        public bool Do(CompanyAct a, CompanyView view)
        {
            CompanyDirector d = D;
            if (d == null || !d.Running)
            {
                return false;
            }
            try
            {
                bool done = Run(d, a);
                if (done)
                {
                    Touch();
                }
                return done;
            }
            catch (Exception e)
            {
                CompanyDirector.Logger?.Error(e);
                return false;
            }
        }

        private static bool Run(CompanyDirector d, CompanyAct a)
        {
            switch (a.Do)
            {
                // Other apps and the game's own screens.
                case CompanyDo.BizMan:
                {
                    CompanyShop shop = d.ShopAt(a.S);
                    return shop != null && CompanyGame.OpenBizMan(shop.Reg);
                }
                case CompanyDo.SellBusiness:
                    CompanyGame.OpenSale(a.S);
                    return true;
                case CompanyDo.OpenCleaning:
                    CompanyGame.OpenCleaning();
                    return true;
                case CompanyDo.OpenBank:
                    CompanyGame.OpenBank();
                    return true;
                case CompanyDo.OpenStocks:
                    CompanyGame.OpenListing();
                    return true;
                // Questions waiting on the owner.
                case CompanyDo.Settle:
                    d.HierarchySettle(a.A, a.B);
                    return true;
                case CompanyDo.Dismiss:
                    d.HierarchyDismiss(a.A);
                    return true;
                case CompanyDo.TryNow:
                    d.HierarchyTryNow(a.A);
                    return true;
                case CompanyDo.Pass:
                    d.HierarchyPass(a.A);
                    return true;
                // Staff.
                case CompanyDo.RaiseAnswer:
                    d.AnswerRaise(a.S, (RaiseAnswer)a.A);
                    return true;
                case CompanyDo.RaiseGive:
                    d.GiveRaise(a.S);
                    return true;
                case CompanyDo.OfferAnswer:
                    d.AnswerOffer(a.S, a.A != 0);
                    return true;
                case CompanyDo.StrikeAnswer:
                    d.SettleStrike(a.S, (StrikeAnswer)a.A);
                    return true;
                case CompanyDo.OvertimeAnswer:
                    d.AnswerOvertime(a.S, a.A != 0);
                    return true;
                case CompanyDo.RaisesAll:
                    d.AnswerAllRaises(a.A == 0 ? RaiseAnswer.Agree : RaiseAnswer.Refuse);
                    return true;
                case CompanyDo.SickPolicy:
                    d.SetSickPolicy(a.A);
                    return true;
                // Cover and marketing.
                case CompanyDo.CoverBuy:
                    d.BuyCover(a.S, a.A, a.B);
                    return true;
                case CompanyDo.CoverCancel:
                    d.CancelCover(a.S, a.A);
                    return true;
                case CompanyDo.ClaimFile:
                    d.FileClaim(a.A);
                    return true;
                case CompanyDo.CampaignBook:
                    d.BookCampaign(a.S, a.A);
                    return true;
                // Tax and licences.
                case CompanyDo.ReturnFile:
                    d.FileReturn();
                    return true;
                case CompanyDo.AccountantHire:
                    d.Hire(a.A);
                    return true;
                case CompanyDo.AccountantDismiss:
                    d.Dismiss();
                    return true;
                case CompanyDo.HideSet:
                    d.SetHide(a.S, a.A);
                    return true;
                case CompanyDo.LicenceBuy:
                    d.BuyLicence(a.S, a.A);
                    return true;
                case CompanyDo.LicenceAuto:
                    d.SetAutoRenew(a.S, a.A, a.B != 0);
                    return true;
                case CompanyDo.ReserveSet:
                    d.ReserveSet(a.C > 0 ? a.C : a.A);
                    return true;
                case CompanyDo.ReserveRelease:
                    d.ReserveRelease();
                    return true;
                case CompanyDo.ReserveAuto:
                    d.ReserveSetAuto(a.A != 0);
                    return true;
                // The rota.
                case CompanyDo.RotaMode:
                    d.RotaChoose(a.S, a.A);
                    return true;
                case CompanyDo.RotaFill:
                    d.FillRota(a.S, a.A);
                    return true;
                case CompanyDo.RotaBack:
                    d.RotaBack(a.S);
                    return true;
                case CompanyDo.PlanNew:
                    return d.RotaPlanNew(a.S, CompanyText.T("reallife_rota_plan_n", "number", NextPlanNumber(d, a.S).ToString()));
                case CompanyDo.PlanCopy:
                {
                    RotaPlanSet set = d.RotaPlansOf(a.S, true);
                    RotaPlanFile from = set != null ? set.Find(a.A > 0 ? a.A : set.Active) : null;
                    return from != null && d.RotaPlanCopy(a.S, from.Id, CompanyText.T("reallife_rota_plan_copy_of", "name", from.Name));
                }
                case CompanyDo.PlanUse:
                    d.RotaPlanUse(a.S, a.A);
                    return true;
                case CompanyDo.PlanDelete:
                    return d.RotaPlanDelete(a.S, a.A);
                case CompanyDo.PlanChange:
                    return PlanChange(d, a);
                // Managers.
                case CompanyDo.TaskSet:
                    if (a.A < 0)
                    {
                        d.ManagerHandDown(a.S, a.B != 0);   // where a question the manager cannot decide goes
                        return true;
                    }
                    d.ManagerTaskSet(a.S, (ManagerTask)a.A, a.B != 0);
                    return true;
                case CompanyDo.LimitSet:
                    d.ManagerLimit(a.S, a.A, a.C > 0 ? LimitStepFor(d, a.S, a.A, a.C, a.B) : a.B);
                    return true;
                case CompanyDo.StandDown:
                    d.ManagerStandDown(a.S, a.A == 1);
                    return true;
                case CompanyDo.Appoint:
                    d.ManagerAppoint(a.S2, a.S);
                    return true;
                case CompanyDo.HandBack:
                    d.ManagerHandBack(a.S);
                    return true;
                case CompanyDo.Undo:
                    d.ManagerUndo(a.A);
                    return true;
                case CompanyDo.DeepMode:
                    d.HierarchyDeep(a.A != 0);
                    return true;
                // The policy centre, dashboards and seats.
                case CompanyDo.RuleSet:
                    d.PolicySetRule(a.C, a.S, (PolicyField)a.A, a.B);
                    return true;
                case CompanyDo.RuleClear:
                    d.PolicyClear(a.C, a.S);
                    return true;
                case CompanyDo.TemplateSave:
                    d.PolicySaveTemplate(a.C, a.S);
                    return true;
                case CompanyDo.TemplateApply:
                    d.PolicyApplyTemplate(a.C, a.S, a.A, a.B);
                    return true;
                case CompanyDo.TemplateDelete:
                    d.PolicyDeleteTemplate(a.A);
                    return true;
                case CompanyDo.StoreFill:
                    d.HierarchyFillNow(a.S);
                    return true;
                case CompanyDo.StoreRaise:
                    d.HierarchyRaiseNow(a.S);
                    return true;
                case CompanyDo.StoreArea:
                    d.HierarchyPickArea(a.S, a.A);
                    return true;
                case CompanyDo.RegionFill:
                    d.HierarchyFillRegion(a.S);
                    return true;
                case CompanyDo.RegionRaise:
                    d.HierarchyRaiseRegion(a.S);
                    return true;
                case CompanyDo.RegionMove:
                    d.HierarchyReallocate(a.S);
                    return true;
                case CompanyDo.RegionAppoint:
                    if (a.S2 != null)
                    {
                        d.ManagerAppointRegion(a.S, a.S2);   // the one picked from the offices
                    }
                    else
                    {
                        d.HierarchyAppointRegion(a.S);       // the hierarchy's own pick
                    }
                    return true;
                case CompanyDo.SeatAppoint:
                    d.HierarchyAppointExecutive(a.A, a.S);
                    return true;
                case CompanyDo.SeatStandDown:
                    d.HierarchyStandDownExecutive(a.A);
                    return true;
                default:
                    return false;
            }
        }

        // The name a new plan starts with: the next number past the plans there are.
        private static int NextPlanNumber(CompanyDirector d, string place)
        {
            RotaPlanSet set = d.RotaPlansOf(place, true);
            return set != null ? set.Plans.Count + 1 : 1;
        }

        // One figure of a plan (the director's PlanXxx fields): the value is in C; a block's place in the plan is in S2. A place with no plan of its own gets its standard week first.
        private static bool PlanChange(CompanyDirector d, CompanyAct a)
        {
            RotaPlanSet set = d.RotaPlansOf(a.S, true);
            if (set == null)
            {
                return false;
            }
            int id = a.A > 0 ? a.A : set.Active;
            if (a.B == CompanyDirector.PlanName)
            {
                d.RotaPlanRename(a.S, id, a.S2 ?? string.Empty);
                return true;
            }
            int at = 0;
            if (a.S2 != null)
            {
                int.TryParse(a.S2, out at);
            }
            d.RotaPlanChange(a.S, id, a.B, a.C, at);
            return true;
        }

        // A limit typed in whole units (a per cent, a sum, days, people): the step nearest to it.
        private static int LimitStepFor(CompanyDirector d, string place, int limit, int typed, int fallback)
        {
            int index = d.Index;
            switch (limit)
            {
                case CompanyDirector.LimitWage: return Nearest(ManagerCatalogue.WageBands, typed * 10);
                case CompanyDirector.LimitBudget: return NearestAt(ManagerCatalogue.Budgets, typed, index);
                case CompanyDirector.LimitMarketing: return NearestAt(ManagerCatalogue.MarketingSteps, typed, index);
                case CompanyDirector.LimitLease: return Nearest(ManagerCatalogue.LeaseRises, typed * 10);
                case CompanyDirector.LimitKeep: return Math.Max(0, Math.Min(ManagerCatalogue.KeepMost, typed));
                case CompanyDirector.LimitStock: return Nearest(ManagerCatalogue.StockDays, typed);
                case CompanyDirector.LimitPriceLow: return Nearest(ManagerCatalogue.PriceLows, typed * 10);
                case CompanyDirector.LimitPriceHigh: return Nearest(ManagerCatalogue.PriceHighs, typed * 10);
                default: return fallback;
            }
        }

        private static int Nearest(int[] values, int wanted)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                best = Math.Abs(values[i] - wanted) < Math.Abs(values[best] - wanted) ? i : best;
            }
            return best;
        }

        private static int NearestAt(int[] values, int wanted, int index)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                best = Math.Abs(CompanyCatalogue.AtPrices(values[i], index) - wanted) < Math.Abs(CompanyCatalogue.AtPrices(values[best], index) - wanted) ? i : best;
            }
            return best;
        }
    }
}
