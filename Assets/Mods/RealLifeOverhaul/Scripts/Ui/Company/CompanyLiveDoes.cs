using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the Company screen asks of the director when a button is pressed: the calls of the old app's buttons, by the director's own names. The director implements it; the
    /// checks hand in a recording one, so that every action can be run through the very mapping the live screen uses (<see cref="CompanyLiveDoes.Run"/>) and each be seen to reach the
    /// right call with the right figures. Game-free.
    /// </summary>
    internal interface ICompanyDoes
    {
        int Index { get; }
        string Say(string key, string name, string value);   // a word of the game's language (the name a new plan starts with)
        void HierarchySettle(int id, int how);
        void HierarchyDismiss(int id);
        bool LeaseHaggle(string place);
        void LeaseAnswer(string place, bool accept);
        void HierarchyTryNow(int id);
        void HierarchyPass(int id);
        void AnswerRaise(string id, RaiseAnswer answer);
        void GiveRaise(string id);
        void AnswerOffer(string id, bool match);
        void SettleStrike(string place, StrikeAnswer how);
        void AnswerOvertime(string place, bool agree);
        void AnswerAllRaises(RaiseAnswer answer);
        void SetSickPolicy(int policy);
        void BuyCover(string place, int kind, int insurer);
        void CancelCover(string place, int kind);
        void FileClaim(int id);
        void BookCampaign(string place, int kind);
        void FileReturn();
        void Hire(int tier);
        void Dismiss();
        void SetHide(string place, int percent);
        void BuyLicence(string place, int kind);
        void SetAutoRenew(string place, int kind, bool on);
        void ReserveSet(int amount);
        void ReserveRelease();
        void ReserveSetAuto(bool on);
        void RotaChoose(string place, int mode);
        void FillRota(string place, int rung);
        void RotaBack(string place);
        bool RotaPlanNew(string place, string name);
        RotaPlanSet PlansOf(string place);
        bool RotaPlanCopy(string place, int id, string name);
        void RotaPlanUse(string place, int id);
        bool RotaPlanDelete(string place, int id);
        void RotaPlanRename(string place, int id, string name);
        void RotaPlanChange(string place, int id, int field, int value, int at);
        void ManagerHandDown(string place, bool on);
        void ManagerTaskSet(string place, ManagerTask task, bool on);
        void ManagerLimit(string place, int limit, int step);
        void ManagerStandDown(string place, bool region);
        void ManagerAppoint(string place, string id);
        void ManagerHandBack(string place);
        void ManagerUndo(int id);
        void HierarchyDeep(bool on);
        void PolicySetRule(int scopeKind, string scope, PolicyField field, int step);
        void PolicyClear(int scopeKind, string scope);
        void PolicySaveTemplate(int scopeKind, string scope);
        void PolicyApplyTemplate(int scopeKind, string scope, int builtIn, int own);
        void PolicyDeleteTemplate(int number);
        void HierarchyFillNow(string place);
        void HierarchyRaiseNow(string place);
        void HierarchyPickArea(string place, int number);
        void HierarchyFillRegion(string area);
        void HierarchyRaiseRegion(string area);
        void HierarchyReallocate(string area);
        void ManagerAppointRegion(string district, string id);
        void HierarchyAppointRegion(string area);
        void HierarchyAppointExecutive(int seat, string id);
        void HierarchyStandDownExecutive(int seat);
    }

    /// <summary>The other apps and the game's own screens a press opens (the game's side of the screen).</summary>
    internal interface ICompanyOpens
    {
        bool BizMan(string place);
        void Sale(string place);
        void Cleaning();
        void Bank();
        void Stocks();
        void Options(string section);
        void Quiet(string place, int matter, int figure);
    }

    /// <summary>The map from a press (<see cref="CompanyAct"/>) to the director's call, game-free. The live screen runs exactly this.</summary>
    internal static class CompanyLiveDoes
    {
        // The director's numbers the actions carry (CompanyDirector.AnswerAgree and the rest); the live screen pins them equal at compile time (CompanyLive.Do.cs).
        public const int Agree = 0, Haggle = 4;
        public const int PlanName = 0;
        public const int LimitWage = 0, LimitStock = 1, LimitPriceLow = 2, LimitPriceHigh = 3, LimitBudget = 4, LimitKeep = 5, LimitMarketing = 11, LimitLease = 12;

        /// <summary>
        /// Does what a press stands for. False for a kind that is the screen's own (moving about) or has nothing to act on (a plan that is not there); the director says why in the
        /// game's own notice. A typed figure comes in C.
        /// </summary>
        public static bool Run(ICompanyDoes d, ICompanyOpens open, CompanyAct a)
        {
            switch (a.Do)
            {
                // Other apps and the game's own screens.
                case CompanyDo.BizMan:
                    return open.BizMan(a.S);
                case CompanyDo.SellBusiness:
                    open.Sale(a.S);
                    return true;
                case CompanyDo.OpenCleaning:
                    open.Cleaning();
                    return true;
                case CompanyDo.OpenBank:
                    open.Bank();
                    return true;
                case CompanyDo.OpenStocks:
                    open.Stocks();
                    return true;
                case CompanyDo.OpenOptions:
                    open.Options(string.IsNullOrEmpty(a.S) ? "company" : a.S);
                    return true;
                case CompanyDo.Quiet:
                    open.Quiet(a.S, a.A, a.B);
                    return true;
                // Questions waiting on the owner.
                case CompanyDo.Settle:
                    d.HierarchySettle(a.A, a.B);
                    return true;
                case CompanyDo.Dismiss:
                    d.HierarchyDismiss(a.A);
                    return true;
                case CompanyDo.LeaseAnswer:
                    if (a.A == Haggle)
                    {
                        d.LeaseHaggle(a.S);
                    }
                    else
                    {
                        d.LeaseAnswer(a.S, a.A == Agree);
                    }
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
                    return d.RotaPlanNew(a.S, d.Say("reallife_rota_plan_n", "number", NextPlanNumber(d, a.S).ToString()));
                case CompanyDo.PlanCopy:
                {
                    RotaPlanSet set = d.PlansOf(a.S);
                    RotaPlanFile from = set != null ? set.Find(a.A > 0 ? a.A : set.Active) : null;
                    return from != null && d.RotaPlanCopy(a.S, from.Id, d.Say("reallife_rota_plan_copy_of", "name", from.Name));
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
                    d.ManagerLimit(a.S, a.A, a.C > 0 ? LimitStepFor(d, a.A, a.C, a.B) : a.B);
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
        private static int NextPlanNumber(ICompanyDoes d, string place)
        {
            RotaPlanSet set = d.PlansOf(place);
            return set != null ? set.Plans.Count + 1 : 1;
        }

        // One figure of a plan (the director's PlanXxx fields): the value is in C; a block's place in the plan is in S2. A place with no plan of its own gets its standard week first.
        private static bool PlanChange(ICompanyDoes d, CompanyAct a)
        {
            RotaPlanSet set = d.PlansOf(a.S);
            if (set == null)
            {
                return false;
            }
            int id = a.A > 0 ? a.A : set.Active;
            if (a.B == PlanName)
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
        private static int LimitStepFor(ICompanyDoes d, int limit, int typed, int fallback)
        {
            int index = d.Index;
            switch (limit)
            {
                case LimitWage: return Nearest(ManagerCatalogue.WageBands, typed * 10);
                case LimitBudget: return NearestAt(ManagerCatalogue.Budgets, typed, index);
                case LimitMarketing: return NearestAt(ManagerCatalogue.MarketingSteps, typed, index);
                case LimitLease: return Nearest(ManagerCatalogue.LeaseRises, typed * 10);
                case LimitKeep: return Math.Max(0, Math.Min(ManagerCatalogue.KeepMost, typed));
                case LimitStock: return Nearest(ManagerCatalogue.StockDays, typed);
                case LimitPriceLow: return Nearest(ManagerCatalogue.PriceLows, typed * 10);
                case LimitPriceHigh: return Nearest(ManagerCatalogue.PriceHighs, typed * 10);
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
