using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// Every action of the Company screen run through the map the live screen uses (CompanyLiveDoes.Run) against a recording director: each kind reaches the one director call it stands
// for, with the figures it carries, and the kinds that are the screen's own reach nothing. (Review 2: the live half was only a text test.)
internal static partial class Program
{
    private sealed class DoesRecorder : ICompanyDoes, ICompanyOpens
    {
        public readonly List<string> Calls = new List<string>();
        public bool HavePlans = true;
        public int Index => 120;
        public string Say(string key, string name, string value) => key + ":" + value;

        private static string J(params object[] a) => string.Join(",", a);
        private void Log(string name, params object[] a) => Calls.Add(name + "(" + J(a) + ")");

        public void HierarchySettle(int id, int how) => Log("HierarchySettle", id, how);
        public void HierarchyDismiss(int id) => Log("HierarchyDismiss", id);
        public bool LeaseHaggle(string place) { Log("LeaseHaggle", place); return true; }
        public void LeaseAnswer(string place, bool accept) => Log("LeaseAnswer", place, accept);
        public void HierarchyTryNow(int id) => Log("HierarchyTryNow", id);
        public void HierarchyPass(int id) => Log("HierarchyPass", id);
        public void AnswerRaise(string id, RaiseAnswer answer) => Log("AnswerRaise", id, answer);
        public void GiveRaise(string id) => Log("GiveRaise", id);
        public void AnswerOffer(string id, bool match) => Log("AnswerOffer", id, match);
        public void SettleStrike(string place, StrikeAnswer how) => Log("SettleStrike", place, how);
        public void AnswerOvertime(string place, bool agree) => Log("AnswerOvertime", place, agree);
        public void AnswerAllRaises(RaiseAnswer answer) => Log("AnswerAllRaises", answer);
        public void SetSickPolicy(int policy) => Log("SetSickPolicy", policy);
        public void BuyCover(string place, int kind, int insurer) => Log("BuyCover", place, kind, insurer);
        public void CancelCover(string place, int kind) => Log("CancelCover", place, kind);
        public void FileClaim(int id) => Log("FileClaim", id);
        public void BookCampaign(string place, int kind) => Log("BookCampaign", place, kind);
        public void FileReturn() => Log("FileReturn");
        public void Hire(int tier) => Log("Hire", tier);
        public void Dismiss() => Log("Dismiss");
        public void SetHide(string place, int percent) => Log("SetHide", place, percent);
        public void BuyLicence(string place, int kind) => Log("BuyLicence", place, kind);
        public void SetAutoRenew(string place, int kind, bool on) => Log("SetAutoRenew", place, kind, on);
        public void ReserveSet(int amount) => Log("ReserveSet", amount);
        public void ReserveRelease() => Log("ReserveRelease");
        public void ReserveSetAuto(bool on) => Log("ReserveSetAuto", on);
        public void RotaChoose(string place, int mode) => Log("RotaChoose", place, mode);
        public void FillRota(string place, int rung) => Log("FillRota", place, rung);
        public void RotaBack(string place) => Log("RotaBack", place);
        public bool RotaPlanNew(string place, string name) { Log("RotaPlanNew", place, name); return true; }

        public RotaPlanSet PlansOf(string place)
        {
            if (!HavePlans)
            {
                return null;
            }
            var set = new RotaPlanSet { Place = place };
            set.Plans.Add(new RotaPlanFile { Id = 3, Name = "Standard week" });
            set.Plans.Add(new RotaPlanFile { Id = 5, Name = "Holiday week" });
            set.Active = 3;
            return set;
        }

        public bool RotaPlanCopy(string place, int id, string name) { Log("RotaPlanCopy", place, id); return true; }
        public void RotaPlanUse(string place, int id) => Log("RotaPlanUse", place, id);
        public bool RotaPlanDelete(string place, int id) { Log("RotaPlanDelete", place, id); return true; }
        public void RotaPlanRename(string place, int id, string name) => Log("RotaPlanRename", place, id, name);
        public void RotaPlanChange(string place, int id, int field, int value, int at) => Log("RotaPlanChange", place, id, field, value, at);
        public void ManagerHandDown(string place, bool on) => Log("ManagerHandDown", place, on);
        public void ManagerTaskSet(string place, ManagerTask task, bool on) => Log("ManagerTaskSet", place, task, on);
        public void ManagerLimit(string place, int limit, int step) => Log("ManagerLimit", place, limit, step);
        public void ManagerStandDown(string place, bool region) => Log("ManagerStandDown", place, region);
        public void ManagerAppoint(string place, string id) => Log("ManagerAppoint", place, id);
        public void ManagerHandBack(string place) => Log("ManagerHandBack", place);
        public void ManagerUndo(int id) => Log("ManagerUndo", id);
        public void HierarchyDeep(bool on) => Log("HierarchyDeep", on);
        public void PolicySetRule(int scopeKind, string scope, PolicyField field, int step) => Log("PolicySetRule", scopeKind, scope, field, step);
        public void PolicyClear(int scopeKind, string scope) => Log("PolicyClear", scopeKind, scope);
        public void PolicySaveTemplate(int scopeKind, string scope) => Log("PolicySaveTemplate", scopeKind, scope);
        public void PolicyApplyTemplate(int scopeKind, string scope, int builtIn, int own) => Log("PolicyApplyTemplate", scopeKind, scope, builtIn, own);
        public void PolicyDeleteTemplate(int number) => Log("PolicyDeleteTemplate", number);
        public void HierarchyFillNow(string place) => Log("HierarchyFillNow", place);
        public void HierarchyRaiseNow(string place) => Log("HierarchyRaiseNow", place);
        public void HierarchyPickArea(string place, int number) => Log("HierarchyPickArea", place, number);
        public void HierarchyFillRegion(string area) => Log("HierarchyFillRegion", area);
        public void HierarchyRaiseRegion(string area) => Log("HierarchyRaiseRegion", area);
        public void HierarchyReallocate(string area) => Log("HierarchyReallocate", area);
        public void ManagerAppointRegion(string district, string id) => Log("ManagerAppointRegion", district, id);
        public void HierarchyAppointRegion(string area) => Log("HierarchyAppointRegion", area);
        public void HierarchyAppointExecutive(int seat, string id) => Log("HierarchyAppointExecutive", seat, id);
        public void HierarchyStandDownExecutive(int seat) => Log("HierarchyStandDownExecutive", seat);

        public bool BizMan(string place) { Log("OpenBizMan", place); return true; }
        public void Sale(string place) => Log("OpenSale", place);
        public void Cleaning() => Log("OpenCleaning");
        public void Bank() => Log("OpenBank");
        public void Stocks() => Log("OpenStocks");
        public void Options(string section) => Log("OpenOptions", section);
        public void Parties() => Log("OpenParties");
    }

    private static void CompanyDoesChecks()
    {
        int before = _run, failedBefore = _failed;
        // Each kind, with the figures it carries, and the one call it must reach.
        var table = new List<KeyValuePair<CompanyAct, string>>
        {
            Pair(CompanyAct.Of(CompanyDo.BizMan, "Elm~7"), "OpenBizMan(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.SellBusiness, "Elm~7"), "OpenSale(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.OpenCleaning), "OpenCleaning()"),
            Pair(CompanyAct.Of(CompanyDo.OpenParties), "OpenParties()"),
            Pair(CompanyAct.Of(CompanyDo.OpenBank), "OpenBank()"),
            Pair(CompanyAct.Of(CompanyDo.OpenStocks), "OpenStocks()"),
            Pair(CompanyAct.Of(CompanyDo.OpenOptions, "rota"), "OpenOptions(rota)"),
            Pair(CompanyAct.Of(CompanyDo.OpenOptions), "OpenOptions(company)"),
            Pair(CompanyAct.Of(CompanyDo.Settle, null, 12, 3), "HierarchySettle(12,3)"),
            Pair(CompanyAct.Of(CompanyDo.Dismiss, null, 12), "HierarchyDismiss(12)"),
            Pair(CompanyAct.Of(CompanyDo.TryNow, null, 14), "HierarchyTryNow(14)"),
            Pair(CompanyAct.Of(CompanyDo.Pass, null, 15), "HierarchyPass(15)"),
            Pair(CompanyAct.Of(CompanyDo.LeaseAnswer, "Elm~7", CompanyLiveDoes.Agree), "LeaseAnswer(Elm~7,True)"),
            Pair(CompanyAct.Of(CompanyDo.LeaseAnswer, "Elm~7", 1), "LeaseAnswer(Elm~7,False)"),
            Pair(CompanyAct.Of(CompanyDo.LeaseAnswer, "Elm~7", CompanyLiveDoes.Haggle), "LeaseHaggle(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.RaiseAnswer, "e4", (int)RaiseAnswer.Bonus), "AnswerRaise(e4,Bonus)"),
            Pair(CompanyAct.Of(CompanyDo.RaiseGive, "e4"), "GiveRaise(e4)"),
            Pair(CompanyAct.Of(CompanyDo.OfferAnswer, "e4", 1), "AnswerOffer(e4,True)"),
            Pair(CompanyAct.Of(CompanyDo.OfferAnswer, "e4", 0), "AnswerOffer(e4,False)"),
            Pair(CompanyAct.Of(CompanyDo.StrikeAnswer, "Elm~7", (int)StrikeAnswer.Payment), "SettleStrike(Elm~7,Payment)"),
            Pair(CompanyAct.Of(CompanyDo.OvertimeAnswer, "Elm~7", 1), "AnswerOvertime(Elm~7,True)"),
            Pair(CompanyAct.Of(CompanyDo.RaisesAll, null, 0), "AnswerAllRaises(Agree)"),
            Pair(CompanyAct.Of(CompanyDo.RaisesAll, null, 1), "AnswerAllRaises(Refuse)"),
            Pair(CompanyAct.Of(CompanyDo.SickPolicy, null, 2), "SetSickPolicy(2)"),
            Pair(CompanyAct.Of(CompanyDo.CoverBuy, "Elm~7", 1, 2), "BuyCover(Elm~7,1,2)"),
            Pair(CompanyAct.Of(CompanyDo.CoverCancel, "Elm~7", 1), "CancelCover(Elm~7,1)"),
            Pair(CompanyAct.Of(CompanyDo.ClaimFile, null, 7), "FileClaim(7)"),
            Pair(CompanyAct.Of(CompanyDo.CampaignBook, "Elm~7", 2), "BookCampaign(Elm~7,2)"),
            Pair(CompanyAct.Of(CompanyDo.ReturnFile), "FileReturn()"),
            Pair(CompanyAct.Of(CompanyDo.AccountantHire, null, 2), "Hire(2)"),
            Pair(CompanyAct.Of(CompanyDo.AccountantDismiss), "Dismiss()"),
            Pair(CompanyAct.Of(CompanyDo.HideSet, "Elm~7", 4), "SetHide(Elm~7,4)"),
            Pair(CompanyAct.Of(CompanyDo.LicenceBuy, "Elm~7", 5), "BuyLicence(Elm~7,5)"),
            Pair(CompanyAct.Of(CompanyDo.LicenceAuto, "Elm~7", 5, 1), "SetAutoRenew(Elm~7,5,True)"),
            Pair(CompanyAct.Of(CompanyDo.ReserveSet, null, 900), "ReserveSet(900)"),
            Pair(CompanyAct.Of(CompanyDo.ReserveSet, null, 900, 0, 1250), "ReserveSet(1250)"),
            Pair(CompanyAct.Of(CompanyDo.ReserveRelease), "ReserveRelease()"),
            Pair(CompanyAct.Of(CompanyDo.ReserveAuto, null, 1), "ReserveSetAuto(True)"),
            Pair(CompanyAct.Of(CompanyDo.RotaMode, "Elm~7", 2), "RotaChoose(Elm~7,2)"),
            Pair(CompanyAct.Of(CompanyDo.RotaFill, "Elm~7", -1), "FillRota(Elm~7,-1)"),
            Pair(CompanyAct.Of(CompanyDo.RotaBack, "Elm~7"), "RotaBack(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.PlanNew, "Elm~7"), "RotaPlanNew(Elm~7,reallife_rota_plan_n:3)"),
            Pair(CompanyAct.Of(CompanyDo.PlanCopy, "Elm~7", 5), "RotaPlanCopy(Elm~7,5)"),
            Pair(CompanyAct.Of(CompanyDo.PlanCopy, "Elm~7"), "RotaPlanCopy(Elm~7,3)"),
            Pair(CompanyAct.Of(CompanyDo.PlanUse, "Elm~7", 5), "RotaPlanUse(Elm~7,5)"),
            Pair(CompanyAct.Of(CompanyDo.PlanDelete, "Elm~7", 5), "RotaPlanDelete(Elm~7,5)"),
            Pair(CompanyAct.Of(CompanyDo.PlanChange, "Elm~7", 5, CompanyLiveDoes.PlanName, 0, "Lean week"), "RotaPlanRename(Elm~7,5,Lean week)"),
            Pair(CompanyAct.Of(CompanyDo.PlanChange, "Elm~7", 0, CompanyDirectorPlanHours, 40, "2"), "RotaPlanChange(Elm~7,3," + CompanyDirectorPlanHours + ",40,2)"),
            Pair(CompanyAct.Of(CompanyDo.TaskSet, "Elm~7", (int)ManagerTask.Leases, 1), "ManagerTaskSet(Elm~7,Leases,True)"),
            Pair(CompanyAct.Of(CompanyDo.TaskSet, "Elm~7", -1, 1), "ManagerHandDown(Elm~7,True)"),
            Pair(CompanyAct.Of(CompanyDo.LimitSet, "Elm~7", CompanyLiveDoes.LimitLease, 3), "ManagerLimit(Elm~7,12,3)"),
            Pair(CompanyAct.Of(CompanyDo.LimitSet, "Elm~7", CompanyLiveDoes.LimitKeep, 0, 9), "ManagerLimit(Elm~7,5,9)"),
            Pair(CompanyAct.Of(CompanyDo.LimitSet, "Elm~7", CompanyLiveDoes.LimitWage, 0, 12), "ManagerLimit(Elm~7,0," + NearestStep(ManagerCatalogue.WageBands, 120) + ")"),
            Pair(CompanyAct.Of(CompanyDo.StandDown, "Elm~7", 1), "ManagerStandDown(Elm~7,True)"),
            Pair(CompanyAct.Of(CompanyDo.Appoint, "e4", 0, 0, 0, "Elm~7"), "ManagerAppoint(Elm~7,e4)"),
            Pair(CompanyAct.Of(CompanyDo.HandBack, "Elm~7"), "ManagerHandBack(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.Undo, null, 31), "ManagerUndo(31)"),
            Pair(CompanyAct.Of(CompanyDo.DeepMode, null, 1), "HierarchyDeep(True)"),
            Pair(CompanyAct.Of(CompanyDo.RuleSet, "Midtown", (int)PolicyField.RaiseMost, 2, 1), "PolicySetRule(1,Midtown," + PolicyField.RaiseMost + ",2)"),
            Pair(CompanyAct.Of(CompanyDo.RuleClear, "Midtown", 0, 0, 1), "PolicyClear(1,Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.TemplateSave, "Midtown", 0, 0, 1), "PolicySaveTemplate(1,Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.TemplateApply, "Midtown", 2, 3, 1), "PolicyApplyTemplate(1,Midtown,2,3)"),
            Pair(CompanyAct.Of(CompanyDo.TemplateDelete, null, 4), "PolicyDeleteTemplate(4)"),
            Pair(CompanyAct.Of(CompanyDo.StoreFill, "Elm~7"), "HierarchyFillNow(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.StoreRaise, "Elm~7"), "HierarchyRaiseNow(Elm~7)"),
            Pair(CompanyAct.Of(CompanyDo.StoreArea, "Elm~7", 2), "HierarchyPickArea(Elm~7,2)"),
            Pair(CompanyAct.Of(CompanyDo.RegionFill, "Midtown"), "HierarchyFillRegion(Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.RegionRaise, "Midtown"), "HierarchyRaiseRegion(Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.RegionMove, "Midtown"), "HierarchyReallocate(Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.RegionAppoint, "Midtown", 0, 0, 0, "e9"), "ManagerAppointRegion(Midtown,e9)"),
            Pair(CompanyAct.Of(CompanyDo.RegionAppoint, "Midtown"), "HierarchyAppointRegion(Midtown)"),
            Pair(CompanyAct.Of(CompanyDo.SeatAppoint, "x7", 2), "HierarchyAppointExecutive(2,x7)"),
            Pair(CompanyAct.Of(CompanyDo.SeatStandDown, null, 2), "HierarchyStandDownExecutive(2)"),
        };
        int reached = 0;
        string wrong = string.Empty;
        var covered = new HashSet<CompanyDo>();
        foreach (var one in table)
        {
            var rec = new DoesRecorder();
            bool done = CompanyLiveDoes.Run(rec, rec, one.Key);
            covered.Add(one.Key.Do);
            bool ok = done && rec.Calls.Count == 1 && rec.Calls[0].StartsWith(one.Value, StringComparison.Ordinal);
            reached += ok ? 1 : 0;
            wrong += ok ? string.Empty : " " + one.Key.Do + "->" + string.Join("|", rec.Calls) + " (wanted " + one.Value + ")";
        }
        Check(reached == table.Count, "company presses: every action reaches the one call it stands for, with its figures (" + reached + " of " + table.Count + ")" + wrong);
        string missing = string.Empty;
        foreach (CompanyDo kind in Enum.GetValues(typeof(CompanyDo)))
        {
            if (kind != CompanyDo.None && kind != CompanyDo.Go && !covered.Contains(kind))
            {
                missing += " " + kind;
            }
        }
        Check(missing.Length == 0, "company presses: every kind of action of the screen is pressed here" + (missing.Length > 0 ? ", not these:" + missing : string.Empty));
        // The screen's own (moving about) and a kind with nothing to act on reach no call.
        var none = new DoesRecorder();
        bool go = CompanyLiveDoes.Run(none, none, CompanyAct.Of(CompanyDo.Go, null, 3)), nothing = CompanyLiveDoes.Run(none, none, CompanyAct.Of(CompanyDo.None));
        none.HavePlans = false;
        bool noPlan = CompanyLiveDoes.Run(none, none, CompanyAct.Of(CompanyDo.PlanChange, "Elm~7", 5, 1, 4)) || CompanyLiveDoes.Run(none, none, CompanyAct.Of(CompanyDo.PlanCopy, "Elm~7", 5));
        Check(!go && !nothing && !noPlan && none.Calls.Count == 0, "company presses: moving about is the screen's own and reaches no call, and a plan that is not there is not acted on");
        Console.WriteLine("  the company's presses through the live map: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private const int CompanyDirectorPlanHours = 4;   // CompanyDirector.PlanHours (RotaPlanChange's field), pinned in CompanyLive.Do.cs

    private static KeyValuePair<CompanyAct, string> Pair(CompanyAct a, string call) => new KeyValuePair<CompanyAct, string>(a, call);

    private static int NearestStep(int[] values, int wanted)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
        {
            best = Math.Abs(values[i] - wanted) < Math.Abs(values[best] - wanted) ? i : best;
        }
        return best;
    }
}
