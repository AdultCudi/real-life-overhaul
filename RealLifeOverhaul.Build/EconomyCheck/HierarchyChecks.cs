using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using PolicyField = RealLifeOverhaul.Economy.PolicyField;

// Checks for the company's hierarchy (Scripts/Economy/Company: ExecutiveHiring, PolicyRules, EscalationRules,
// HierarchyRules, HierarchyState and HierarchyStore): the shared ranks and levels; the policy's catalogue and its starting
// steps deciding exactly as the managers did before there was a policy; a lower scope never past the one above, over every
// rule and step; templates; each escalation's trigger at its edges; who holds an escalation and who may settle it; the
// save's line over a round trip, a damaged line, a later version and its caps; the director going through these rules; the
// texts in every language and the Guide page.
internal static partial class Program
{
    private static void Hierarchy()
    {
        HierarchyRanks();
        HierarchyPolicyCatalogue();
        HierarchyStartIsBefore();
        HierarchyScopes();
        HierarchyTemplates();
        HierarchyTriggers();
        HierarchyRouting();
        HierarchyAreas();
        HierarchyAbove();   // the regional managers' replacing and the executives' proposals (step 2)
        HierarchyPay();     // everybody above the floor paid through the game's money, and their decisions logged (step 3)
        HierarchySave();
        HierarchyWiring();
        HierarchyWords();
        HierarchyTiming();
    }

    // The company's hierarchy names its rungs and seats from the one list both ladders share (Careers/Ladder/LadderRanks)
    // and keeps only who an executive is hired from beside it.
    private static void HierarchyRanks()
    {
        Check(EscalationRules.LevelRegion == (int)Rung.Regional && EscalationRules.LevelExecutive == (int)Rung.Executive && EscalationRules.LevelOwner == (int)Rung.Owner
              && LadderRanks.Of(EscalationRules.LevelOwner).Level == Rung.Owner && LadderRanks.Of(EscalationRules.LevelRegion).Key == "regional",
            "hierarchy: who holds an escalation is numbered by the careers' own rungs, regional manager, executive and owner");
        Check(ExecutiveHiring.Code(Rung.Frontline) == "L0" && ExecutiveHiring.Code(Rung.Supervisor) == "L0" && ExecutiveHiring.Code(Rung.StoreManager) == "L1"
              && ExecutiveHiring.Code(Rung.Regional) == "L2" && ExecutiveHiring.Code(Rung.Executive) == "L3" && ExecutiveHiring.Code(Rung.Owner) == "L4",
            "hierarchy: the rungs are coded L0 to L4 as the suggestion has them, the shift supervisor on the floor's L0");
        bool seats = ExecutiveHiring.Seats.Length == 3 && ExecutiveHiring.SeatRoles.Length == (int)ExecSeat.Cfo + 1 && ExecutiveHiring.SeatRoles[(int)ExecSeat.None].Length == 0;
        foreach (ExecSeat seat in ExecutiveHiring.Seats)
        {
            seats &= ExecutiveHiring.Real((int)seat) && ExecutiveHiring.SeatRoles[(int)seat].Length > 0;
            foreach (int role in ExecutiveHiring.SeatRoles[(int)seat])
            {
                seats &= role >= 0 && role < TalentCatalogue.RoleCount && ExecutiveHiring.Fits(seat, role, TalentCatalogue.Senior)
                         && !ExecutiveHiring.Fits(seat, role, TalentCatalogue.Mid) && TalentCatalogue.Role(role).MostLevel >= ExecutiveHiring.LeastLevel;
            }
        }
        Check(seats && !ExecutiveHiring.Real((int)ExecSeat.None) && !ExecutiveHiring.Fits(ExecSeat.None, TalentCatalogue.Accountant, TalentCatalogue.Principal),
            "hierarchy: each seat is hired from professions of the talent catalogue that reach senior, and nobody under senior fits");
        Check(TalentCatalogue.Role(25).Key == "hr_manager" && TalentCatalogue.Role(26).Key == "recruiter" && TalentCatalogue.Role(30).Key == "business_analyst"
              && !ExecutiveHiring.Fits(ExecSeat.Cfo, 25, TalentCatalogue.Principal) && !ExecutiveHiring.Fits(ExecSeat.Chro, TalentCatalogue.Accountant, TalentCatalogue.Principal),
            "hierarchy: the seats' professions are the talent catalogue's own by number, and one seat's profession is not another's");
        string hiring = ModSource(@"Economy\Company\ExecutiveHiring.cs");
        Check(hiring.Length > 0 && !hiring.Contains("enum ") && !hiring.Contains("\"frontline\"") && !ModSource(@"Economy\Company\HierarchyState.cs").Contains("enum Rung"),
            "hierarchy: no second list of ranks: the company's code keeps none of its own beside the careers' LadderRanks");
    }

    private static void HierarchyPolicyCatalogue()
    {
        bool ok = PolicyCatalogue.Defs.Length == (int)PolicyField.Count;
        for (int f = 0; ok && f < PolicyCatalogue.Defs.Length; f++)
        {
            PolicyDef d = PolicyCatalogue.Defs[f];
            ok &= (int)d.Field == f && d.Default >= 0 && d.Default < d.Steps.Length && d.Steps.Length >= 2;
            for (int s = 1; s < d.Steps.Length; s++)
            {
                ok &= d.Steps[s] > d.Steps[s - 1];   // the scopes' strict side is read by the step's number
            }
        }
        Check(ok, "policy: every rule is its own number, its steps rise, and its starting step is one of them (" + PolicyCatalogue.Defs.Length + " rules)");
        bool templates = PolicyCatalogue.Templates.Length == PolicyCatalogue.TemplateKeys.Length && PolicyCatalogue.Templates[0] == null;
        for (int t = 1; templates && t < PolicyCatalogue.Templates.Length; t++)
        {
            int[] steps = PolicyCatalogue.Templates[t];
            templates &= steps.Length == (int)PolicyField.Count;
            for (int f = 0; templates && f < steps.Length; f++)
            {
                templates &= steps[f] >= 0 && steps[f] < PolicyCatalogue.Defs[f].Steps.Length;
            }
        }
        Check(templates, "policy: every built-in template has a step for every rule, inside its steps, and the starting policy is the rules' own start");
        int[] cats = new int[(int)PolicyCategory.Count];
        foreach (PolicyDef d in PolicyCatalogue.Defs)
        {
            cats[(int)d.Category]++;
        }
        Check(Array.TrueForAll(cats, n => n > 0) && PolicyCatalogue.CategoryKeys.Length == (int)PolicyCategory.Count,
            "policy: the five headings of the policy centre each have rules: hiring, pay, demands, performance, escalation and reports");
        Check(PolicyCatalogue.Def(PolicyField.PayBand).Steps == ManagerCatalogue.WageBands && PolicyCatalogue.Def(PolicyField.PayBand).Default == ManagerCatalogue.WageBandDefault
              && PolicyCatalogue.Def(PolicyField.Budget).Steps.Length == ManagerCatalogue.Budgets.Length && PolicyCatalogue.Def(PolicyField.Budget).Default == ManagerCatalogue.BudgetDefault,
            "policy: the pay band and the budget are the managers' own steps, not a second copy of them");
    }

    // The starting policy decides exactly as the managers did before: the band alone for raises, offers and hires, every
    // demand answered, a report a day, nobody to approve past the band.
    private static void HierarchyStartIsBefore()
    {
        PolicySet company = new PolicySet();
        company.Start();
        int Start(PolicyField f) => PolicyRules.Effective(company, null, null, f);
        bool raises = true;
        var random = new Random(4401);
        for (int i = 0; i < 4000; i++)
        {
            int going = 800 + random.Next(4000), paid = random.Next(6000), band = random.Next(ManagerCatalogue.WageBands.Length);
            raises &= PolicyRules.RaiseCeiling(paid, going, band, Start(PolicyField.RaiseMost)) == ManagerRules.Ceiling(going, band)
                      && PolicyRules.HireCeiling(going, band, Start(PolicyField.HirePayCap)) == ManagerRules.Ceiling(going, band)
                      && PolicyRules.RaiseDue(random.Next(500), random.Next(500), Start(PolicyField.RaiseEvery))
                      && PolicyRules.ExceptionCeiling(going, band, Start(PolicyField.Approver), Start(PolicyField.ExceptionMargin)) == 0;
        }
        Check(raises, "policy: at its start a raise, an offer and a hire are held to the band alone, any day, with nobody to approve past it (4000 cases)");
        bool picks = true;
        for (int n = 0; n < 2000; n++)
        {
            int count = 1 + random.Next(8);
            var wages = new int[count];
            var goings = new int[count];
            var skills = new int[count];
            for (int i = 0; i < count; i++)
            {
                wages[i] = random.Next(3000);
                goings[i] = 500 + random.Next(2500);
                skills[i] = random.Next(101);
            }
            int band = random.Next(ManagerCatalogue.WageBands.Length), left = random.Next(3000);
            bool mistake = random.Next(2) == 0;
            picks &= PolicyRules.Pick(wages, goings, skills, count, band, Start(PolicyField.HirePayCap), Start(PolicyField.SkillFloor), left, mistake)
                     == ManagerRules.Pick(wages, goings, skills, count, band, left, mistake);
        }
        Check(picks, "policy: at its start a manager hires the very candidate they hired before (2000 job boards)");
        bool answers = Start(PolicyField.AutoRaises) == 1 && Start(PolicyField.AutoOffers) == 1 && Start(PolicyField.AutoOvertime) == 1 && Start(PolicyField.AutoClaims) == 1
                       && PolicyRules.SwitchOf(ManagerTask.Stock) < 0 && PolicyRules.SwitchOf(ManagerTask.Rota) < 0 && PolicyRules.SwitchOf(ManagerTask.Raises) == (int)PolicyField.AutoRaises;
        bool digest = true;
        for (int day = 1; day <= 28; day++)
        {
            digest &= HierarchyRules.DigestDue(Start(PolicyField.Reports), day);
        }
        Check(answers && digest, "policy: at its start every demand a manager answered is answered, the other kinds have no switch, and the day's notice goes out every day");
        PolicySet held = new PolicySet();
        held.Start();
        held.Steps[(int)PolicyField.SkillFloor] = 5;
        int[] w = { 1500, 1500 }, g = { 1500, 1500 }, sk = { 30, 45 };
        Check(PolicyRules.Pick(w, g, sk, 2, 3, held.Of(PolicyField.HirePayCap), held.Of(PolicyField.SkillFloor), 100000, false) == -1
              && PolicyRules.Pick(w, g, sk, 2, 3, held.Of(PolicyField.HirePayCap), 3, 100000, false) == 1
              && PolicyRules.Pick(new[] { 1200 }, new[] { 1000 }, new[] { 50 }, 1, 6, 2, 0, 100000, false) == -1,
            "policy: the skill floor passes over those under it and the hiring cap those paid past it, whatever the band");
        Check(PolicyRules.RaiseCeiling(1000, 2000, 6, 1) == 1050 && PolicyRules.RaiseCeiling(1000, 700, 6, 4) == ManagerRules.Ceiling(700, 6)
              && !PolicyRules.RaiseDue(10, 20, 3) && PolicyRules.RaiseDue(10, 40, 3) && PolicyRules.RaiseDue(0, 5, 5),
            "policy: the largest raise caps a raise on the pay now under the band, and the days between two raises hold one back");
        Check(PolicyRules.ExceptionCeiling(1000, 3, PolicyCatalogue.ApproveRegion, 1) == ManagerRules.Ceiling(1000, 3) + 100
              && PolicyRules.ExceptionCeiling(1000, 3, PolicyCatalogue.ApproveNobody, 3) == 0,
            "policy: an approver may go the margin past the band, and with nobody named nothing past it is agreed");
    }

    // Over every rule, every company step and every region and store step (or none): the step in force is the lowest
    // scope's own, held to the strict side of each scope above, and a rule only the company sets is the company's.
    private static void HierarchyScopes()
    {
        bool never = true, nearest = true, only = true;
        int cases = 0;
        var company = new PolicySet();
        var region = new PolicySet();
        var store = new PolicySet();
        for (int f = 0; f < (int)PolicyField.Count; f++)
        {
            PolicyDef d = PolicyCatalogue.Defs[f];
            var field = (PolicyField)f;
            for (int c = 0; c < d.Steps.Length; c++)
            {
                for (int r = -1; r < d.Steps.Length; r++)
                {
                    for (int s = -1; s < d.Steps.Length; s++)
                    {
                        company.Start();
                        company.Steps[f] = c;
                        region.Clear();
                        region.Steps[f] = r;
                        store.Clear();
                        store.Steps[f] = s;
                        int force = PolicyRules.Effective(company, region, store, field);
                        int above = PolicyRules.Effective(company, region, null, field);
                        cases++;
                        if (d.Way == PolicyWay.CompanyOnly)
                        {
                            only &= force == c;
                            continue;
                        }
                        never &= !PolicyRules.Past(d.Way, c, force) && !PolicyRules.Past(d.Way, above, force) && !PolicyRules.Past(d.Way, c, above);
                        int want = s >= 0 ? (PolicyRules.Past(d.Way, above, s) ? above : s) : above;
                        nearest &= force == want && (r < 0 || above == (PolicyRules.Past(d.Way, c, r) ? c : r));
                    }
                }
            }
        }
        Check(never, "policy scopes: the step in force is never looser than the region's or the company's (" + cases + " cases)");
        Check(nearest, "policy scopes: a store's own step is in force unless it is past the one above, then the one above; a region's likewise");
        Check(only, "policy scopes: a rule only the company sets is the company's whatever a region or a store says");
        Check(PolicyRules.Effective(null, null, null, PolicyField.GapDays) == PolicyCatalogue.Def(PolicyField.GapDays).Default
              && PolicyRules.Held(PolicyWay.LooseUp, 3, 5, 7) == 3 && PolicyRules.Held(PolicyWay.LooseDown, 3, 1, 8) == 3 && PolicyRules.Held(PolicyWay.LooseDown, 3, 5, 8) == 5
              && PolicyRules.Held(PolicyWay.LooseUp, 3, 99, 7) == 3 && PolicyRules.Held(PolicyWay.LooseUp, 9, 99, 7) == 6,
            "policy scopes: with no sets the start decides; a step is held to the strict side and to the rule's own steps");
        Check(PolicyRules.Allowed(PolicyWay.LooseUp, false, 3, 2) && !PolicyRules.Allowed(PolicyWay.LooseUp, false, 3, 4) && PolicyRules.Allowed(PolicyWay.LooseUp, true, 3, 6)
              && !PolicyRules.Allowed(PolicyWay.CompanyOnly, false, 1, 1) && PolicyRules.Allowed(PolicyWay.LooseDown, false, 3, 4),
            "policy scopes: a lower scope may pick only the strict side of the one above, the company any step, and nothing of a rule only the company sets");
    }

    private static void HierarchyTemplates()
    {
        var company = new PolicySet();
        company.Start();
        PolicyRules.Apply(PolicyCatalogue.Templates[1], company, null, true);
        bool all = true;
        for (int f = 0; f < (int)PolicyField.Count; f++)
        {
            all &= company.Steps[f] == PolicyCatalogue.Templates[1][f];
        }
        var above = new PolicySet();
        above.Start();
        above.Steps[(int)PolicyField.PayBand] = 1;
        var region = new PolicySet();
        PolicyRules.Apply(PolicyCatalogue.Templates[3], region, above, false);
        bool held = region.Of(PolicyField.PayBand) == 1 && !region.Sets(PolicyField.Approver) && !region.Sets(PolicyField.Reports) && !region.Sets(PolicyField.ProfitWeight)
                    && region.Of(PolicyField.SkillFloor) == Math.Max(above.Of(PolicyField.SkillFloor), PolicyCatalogue.Templates[3][(int)PolicyField.SkillFloor]);
        var back = new PolicySet();
        back.Steps[(int)PolicyField.GapDays] = 5;
        PolicyRules.Apply(null, back, null, true);
        bool start = true;
        for (int f = 0; f < (int)PolicyField.Count; f++)
        {
            start &= back.Steps[f] == PolicyCatalogue.Defs[f].Default;
        }
        Check(all && held && start, "policy templates: one sets every rule of the company, onto a region each is held to the one above and the company's own rules are left to it, and the starting policy puts every rule back");
    }

    private static void HierarchyTriggers()
    {
        int gap = PolicyCatalogue.Def(PolicyField.GapDays).Default, days = PolicyCatalogue.Value(PolicyField.GapDays, gap);
        Check(!EscalationRules.GapDue(0, 50, gap) && !EscalationRules.GapDue(50 - days + 1, 50, gap) && EscalationRules.GapDue(50 - days, 50, gap),
            "escalation triggers: a gap is escalated on its policy's day and not one before, and no gap never");
        int share = PolicyCatalogue.Def(PolicyField.BudgetShare).Default;
        Check(!EscalationRules.BudgetDue(799, 1000, share) && EscalationRules.BudgetDue(800, 1000, share) && !EscalationRules.BudgetDue(5000, 0, share) && EscalationRules.BudgetDue(1000, 1000, 4),
            "escalation triggers: a budget is escalated from its share used, at a budget of nothing never");
        int unmet = PolicyCatalogue.Def(PolicyField.UnmetDays).Default, wait = PolicyCatalogue.Value(PolicyField.UnmetDays, unmet);
        Check(!EscalationRules.UnmetDue(0, 30, unmet) && !EscalationRules.UnmetDue(30 - wait + 1, 30, unmet) && EscalationRules.UnmetDue(30 - wait, 30, unmet),
            "escalation triggers: a request or an offer is escalated when it has waited the policy's days, not before");
        int floor = PolicyCatalogue.Def(PolicyField.MoodFloor).Default, floorValue = PolicyCatalogue.Value(PolicyField.MoodFloor, floor);
        Check(EscalationRules.Low(floorValue - 1, floor) && !EscalationRules.Low(floorValue, floor) && !EscalationRules.Low(0, 0)
              && !EscalationRules.MoodDue(0, 9) && !EscalationRules.MoodDue(9, 9) && EscalationRules.MoodDue(9 - PolicyCatalogue.MoodDays + 1, 9),
            "escalation triggers: staff stand low under the floor only, a floor of none never, and are escalated after the days running");
        int wave = PolicyCatalogue.Def(PolicyField.WaveCount).Default, count = PolicyCatalogue.Value(PolicyField.WaveCount, wave);
        var watch = new PlaceWatch { Place = "p" };
        watch.Left.Add(10);
        watch.Left.Add(14);
        watch.Left.Add(16);
        Check(!EscalationRules.WaveDue(count - 1, wave) && EscalationRules.WaveDue(count, wave) && watch.LeftSince(16) == 3 && watch.LeftSince(17) == 2 && watch.LeftSince(30) == 0,
            "escalation triggers: a wave is the policy's leavers or more, counted over the last " + PolicyCatalogue.WaveWindow + " days");
        Check(EscalationRules.OnTarget(-500, 100, 2, true) && EscalationRules.OnTarget(100, 100, 0, false) && EscalationRules.OnTarget(75, 100, 2, false)
              && !EscalationRules.OnTarget(74, 100, 2, false) && EscalationRules.OnTarget(0, 0, 0, false) && !EscalationRules.OnTarget(-1, 0, 0, false),
            "escalation triggers: a figure is on target at or over it, inside the tolerance, with no target always, and a break-even target lets no loss through at no tolerance");
        Check(!EscalationRules.KpiDue(2, 2) && EscalationRules.KpiDue(3, 2) && EscalationRules.KpiDue(1, 0),
            "escalation triggers: a place is escalated after the policy's days off target in a row");
        Check(EscalationRules.Score(0, 100, true, 0, PolicyCatalogue.MoodNone, 2) == 100 && EscalationRules.Score(50, 100, false, 60, 60, 2) == 75
              && EscalationRules.Score(50, 100, false, 0, 60, 4) == 50 && EscalationRules.Score(-10, 0, false, 30, 60, 0) == 50 && EscalationRules.Score(500, 100, false, 99, 60, 2) == 100,
            "escalation triggers: a place's score weighs its profit's share of target against its standing's, 0 to 100, with no target counting whole");
    }

    private static void HierarchyRouting()
    {
        int R = EscalationRules.LevelRegion, X = EscalationRules.LevelExecutive, O = EscalationRules.LevelOwner, L1 = (int)Rung.StoreManager;
        Check(EscalationRules.Next(L1, true, true) == R && EscalationRules.Next(L1, false, true) == X && EscalationRules.Next(L1, false, false) == O
              && EscalationRules.Next(R, true, true) == X && EscalationRules.Next(R, true, false) == O && EscalationRules.Next(X, true, true) == O && EscalationRules.Next(O, true, true) == O,
            "escalation routing: from a manager to the regional manager, the executive, then you, skipping only who is not in post, and never past you");
        int nobody = PolicyCatalogue.ApproveNobody, region = PolicyCatalogue.ApproveRegion, people = PolicyCatalogue.ApprovePeople, owner = PolicyCatalogue.ApproveOwner;
        bool proposal = true, owners = true, kpi = true;
        for (int k = 0; k < (int)EscalationKind.Count; k++)
        {
            for (int a = 0; a < 4; a++)
            {
                for (int s = 0; s < 3; s++)
                {
                    owners &= EscalationRules.Tries((EscalationKind)k, O, a, s);
                    proposal &= k != (int)EscalationKind.Proposal || (!EscalationRules.Tries((EscalationKind)k, R, a, s) && !EscalationRules.Tries((EscalationKind)k, X, a, s));
                    kpi &= k != (int)EscalationKind.Kpi || (!EscalationRules.Tries((EscalationKind)k, R, a, s) && !EscalationRules.Tries((EscalationKind)k, X, a, s));
                }
            }
        }
        Check(owners && proposal && kpi, "escalation routing: you may settle anything, a proposal is only ever yours, and a place off its targets is reported to you, never settled below");
        Check(EscalationRules.Tries(EscalationKind.Exception, R, region, 0) && !EscalationRules.Tries(EscalationKind.Exception, R, people, 0) && !EscalationRules.Tries(EscalationKind.Exception, R, owner, 0)
              && EscalationRules.Tries(EscalationKind.Exception, X, people, 0) && EscalationRules.Tries(EscalationKind.Exception, X, region, 0) && !EscalationRules.Tries(EscalationKind.Exception, X, owner, 0)
              && !EscalationRules.Tries(EscalationKind.Exception, X, nobody, 0),
            "escalation routing: an exception is agreed only by whom the policy names, or a level above them, and one named for you is yours alone");
        Check(!EscalationRules.Tries(EscalationKind.Gap, R, 0, PolicyCatalogue.SourceOwn) && EscalationRules.Tries(EscalationKind.Gap, R, 0, PolicyCatalogue.SourceRegion)
              && EscalationRules.Tries(EscalationKind.Gap, X, 0, PolicyCatalogue.SourceMove) && !EscalationRules.Tries(EscalationKind.Budget, R, 0, 2) && EscalationRules.Tries(EscalationKind.Budget, X, 0, 0)
              && EscalationRules.Tries(EscalationKind.Mood, R, 0, 0) && !EscalationRules.Tries(EscalationKind.Demand, R, 0, 0) && EscalationRules.Tries(EscalationKind.Demand, X, 0, 0),
            "escalation routing: a regional manager fills a gap only past the own board, never spends past a budget, and leaves unanswered requests to the people chief");
        Check(EscalationRules.AreaOf(EscalationKind.Gap) == ExecSeat.Coo && EscalationRules.AreaOf(EscalationKind.Kpi) == ExecSeat.Coo
              && EscalationRules.AreaOf(EscalationKind.Budget) == ExecSeat.Cfo && EscalationRules.AreaOf(EscalationKind.Exception) == ExecSeat.Chro
              && EscalationRules.AreaOf(EscalationKind.Wave) == ExecSeat.Chro,
            "escalation routing: staffing and targets go to the operations chief, budgets to the finance chief, pay and people to the people chief");
    }

    private static void HierarchyAbove()
    {
        int kpi = 3;
        Check(!HierarchyRules.ReplaceDue(kpi * RegionCatalogue.ReplaceRuns - 1, kpi, 10) && HierarchyRules.ReplaceDue(kpi * RegionCatalogue.ReplaceRuns, kpi, RegionCatalogue.ReplaceScore - 1)
              && !HierarchyRules.ReplaceDue(99, kpi, RegionCatalogue.ReplaceScore) && HierarchyRules.ReplaceDue(RegionCatalogue.ReplaceRuns, 0, 0),
            "regional managers: a store's manager is replaced only after twice the days off target and under the score, never at it");
        var company = new PolicySet();
        company.Start();
        var busy = new WeekFigures { Gaps = 2, Budgets = 2, People = 3, Kpis = 0 };
        PolicyField f;
        int step;
        bool coo = HierarchyRules.Propose(ExecSeat.Coo, busy, company, out f, out step) && f == PolicyField.Sources && step == company.Of(PolicyField.Sources) + 1;
        bool chro = HierarchyRules.Propose(ExecSeat.Chro, busy, company, out f, out step) && f == PolicyField.PayBand && step == company.Of(PolicyField.PayBand) + 1;
        bool cfo = HierarchyRules.Propose(ExecSeat.Cfo, busy, company, out f, out step) && f == PolicyField.Budget && step == company.Of(PolicyField.Budget) + 1;
        var quiet = new WeekFigures { Gaps = 1, Budgets = 1, People = 1, Kpis = 1 };
        bool none = !HierarchyRules.Propose(ExecSeat.Coo, quiet, company, out f, out step) && !HierarchyRules.Propose(ExecSeat.Chro, quiet, company, out f, out step)
                    && !HierarchyRules.Propose(ExecSeat.Cfo, quiet, company, out f, out step) && !HierarchyRules.Propose(ExecSeat.None, busy, company, out f, out step);
        Check(coo && chro && cfo && none, "executives: two of an area's matters in a week bring a proposal one step looser on its first rule, fewer bring none, and no seat proposes nothing");
        var top = new PolicySet();
        top.Start();
        top.Steps[(int)PolicyField.Sources] = PolicyCatalogue.Def(PolicyField.Sources).Steps.Length - 1;
        top.Steps[(int)PolicyField.PayBand] = PolicyCatalogue.Def(PolicyField.PayBand).Steps.Length - 1;
        top.Steps[(int)PolicyField.Budget] = PolicyCatalogue.Def(PolicyField.Budget).Steps.Length - 1;
        top.Steps[(int)PolicyField.FinanceMost] = PolicyCatalogue.Def(PolicyField.FinanceMost).Steps.Length - 1;
        bool second = HierarchyRules.Propose(ExecSeat.Coo, busy, top, out f, out step) && f == PolicyField.RegionReplaces && step == 1
                      && HierarchyRules.Propose(ExecSeat.Chro, busy, top, out f, out step) && f == PolicyField.ExceptionMargin
                      && !HierarchyRules.Propose(ExecSeat.Cfo, busy, top, out f, out step);
        var kpis = new WeekFigures { Kpis = 2 };
        bool replace = HierarchyRules.Propose(ExecSeat.Coo, kpis, company, out f, out step) && f == PolicyField.RegionReplaces && step == 1;
        Check(second && replace, "executives: past a rule's last step the proposal is the area's second rule, past both none, and places off target bring the replacing of managers");
        string escalations = ModSource(@"Economy\Company\CompanyDirector.Escalations.cs"), executives = ModSource(@"Economy\Company\CompanyDirector.Executives.cs");
        string managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs"), app = ModSource(@"Economy\Company\CompanyApp.Managers.cs"), regions = ModSource(@"Economy\Company\CompanyDirector.Regions.cs");
        string links = ModSource(@"Wiring\Wiring.HierarchyLinks.cs"), company2 = ModSource(@"Wiring\CompanyLinks.cs");
        Check(MemberText(managers, "internal void ManagerRead()").Contains("mp.Area = AreaFor(p.Place, mp.District)") && MemberText(managers, "internal void ManagerRead()").Contains("mp.Region = RegionOf(mp.Area)")
              && MemberText(managers, "internal void ManagerRead()").Contains("HierarchyRules.CustomNumber(m.Place) > 0 && !HierarchyOn"),
            "regions: a store's regional manager is of the region it is in, and one of a region the player made keeps the post while the hierarchy is off");
        Check(MemberText(executives, "internal void ExecutivesRead(").Contains("if (!_prosKnown)") && MemberText(executives, "public void HierarchyAppointExecutive(").Contains("_mgr.Of(id) != null")
              && MemberText(executives, "private Escalation Propose(").Contains("EscalationRules.LevelOwner") && MemberText(executives, "partial void ProposalDecided(").Contains("_hier.Company.Steps[")
              && links.Contains("CompanyBridges.Professionals = Professionals") && company2.Contains("HierarchyLinks.Connect()"),
            "executives: sat from the professions, never a manager, a seat kept while the professions are off, and a proposal only ever the owner's to put in force");
        Check(MemberText(escalations, "partial void HierarchyDay(").Contains("RegionRound(day, Save.Hour)") && MemberText(regions, "private void RegionRound(").Contains("RegionRule(area, PolicyField.RegionAppoints) == 1")
              && MemberText(regions, "private void RegionRound(").Contains("RegionRule(area, PolicyField.RegionReplaces) == 1")
              && MemberText(regions, "internal int RegionAppoint(").Contains("GoingPublic.Mine(mp.Place.Reg)") && MemberText(regions, "internal int RegionReplace(").Contains("GoingPublic.Mine(mp.Place.Reg)"),
            "regional managers: they appoint and replace only where the region's policy says so, and only at the player's own stores");
        Check(MemberText(app, "partial void WriteManagers(").Contains("WriteOwnerOverview(box, hs, ref selected)") && MemberText(app, "partial void WriteManagers(").Contains("!hs.Deep"),
            "owner overview: the Managers tab opens on it until Deep Mode opens the directory");
    }

    private static void HierarchyPay()
    {
        Check(HierarchyRules.ExecutiveAllowance(5000) == 100 && HierarchyRules.ExecutiveAllowance(0) == 0 && HierarchyRules.ExecutiveAllowance(-300) == 0
              && HierarchyRules.ExecutiveAllowance(int.MaxValue) == 42949673 && HierarchyRules.ExecutiveAllowance(4999) == 100,
            "executives' pay: a share of a working day at their going rate, whole money, never less than nothing, and no overflow at the most");
        string executives = ModSource(@"Economy\Company\CompanyDirector.Executives.cs"), escalations = ModSource(@"Economy\Company\CompanyDirector.Escalations.cs");
        string managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs"), game = ModSource(@"Economy\Company\CompanyGame.cs");
        string pay = MemberText(executives, "private void ExecutivesPay(");
        Check(pay.Contains("Cost(amount, CompanyGame.ExecutiveKey, null, x.Name, CostKind.Wages, day - 1") && pay.Contains("!x.Seen || x.From >= day")
              && MemberText(escalations, "partial void HierarchyDay(").Contains("ExecutivesPay(day)") && game.Contains("ExecutiveKey = \"reallife_transaction_co_executive\"")
              && MemberText(managers, "partial void ManagerDay(").Contains("Cost(amount, CompanyGame.ManagerKey"),
            "executives' pay: each in a seat by yesterday is paid yesterday's allowance through the game's money as a business cost, as the managers and regional managers are");
        Check(MemberText(executives, "private void ExecutivesMonth(").Contains("HierarchyLog.Paid") && MemberText(executives, "private Escalation Propose(").Contains("HierarchyLog.Proposed")
              && ModSource(@"Economy\Company\CompanyDirector.Regions.cs").Contains("HierarchyLog.Appointed") && ModSource(@"Economy\Company\CompanyDirector.Regions.cs").Contains("HierarchyLog.Replaced")
              && MemberText(escalations, "private bool MoveOne(").Contains("HierarchyLog.Moved") && MemberText(escalations, "private bool TopUp(").Contains("HierarchyLog.TopUp")
              && MemberText(escalations, "private void Settled(").Contains("HierarchyLog.Settled") && MemberText(escalations, "private void PassUp(").Contains("HierarchyLog.Passed"),
            "hierarchy log: every decision above the managers is written down, what it changed, by whom, and each executive's month's pay");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool words = LanguagesRead("executives' pay", langs, codes);
        foreach (string key in new[] { "reallife_transaction_co_executive", "reallife_transaction_co_executive_label", "reallife_hier_paid_month", "reallife_hier_paid_last", "reallife_hier_exec_cost" })
        {
            words &= LanguagesLacking(langs, codes, key).Length == 0;
        }
        Check(words && Array.TrueForAll(langs, w => w.TryGetValue("reallife_guide_economy_hierarchy", out string g) && g.Contains("{hier_exec_premium}")),
            "executives' pay: its transaction, its rows and the Guide's line in every language");
        Check(AnyChangesRow(langs[0], "A company policy over your managers") && !AnyChangesRow(langs[0], "New: A company policy"),
            "hierarchy: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }

    private static void HierarchyAreas()
    {
        bool round = true;
        for (int n = 1; n <= HierarchyRules.CustomMost; n++)
        {
            round &= HierarchyRules.CustomNumber(HierarchyRules.CustomArea(n)) == n;
        }
        Check(round && HierarchyRules.CustomNumber("Midtown") == 0 && HierarchyRules.CustomNumber(null) == 0 && HierarchyRules.CustomNumber("~region:99") == 0
              && HierarchyRules.Letter(1) == "A" && HierarchyRules.Letter(HierarchyRules.CustomMost) == "H",
            "hierarchy regions: a region the player makes is told from a district, numbered 1 to " + HierarchyRules.CustomMost + " and named A to H");
        bool weekly = true;
        for (int day = 1; day <= 21; day++)
        {
            weekly &= HierarchyRules.DigestDue(PolicyCatalogue.ReportWeekly, day) == (ManagerRules.Weekday(day) == ManagerCatalogue.Sunday) && !HierarchyRules.DigestDue(PolicyCatalogue.ReportExceptions, day);
        }
        Check(weekly && !HierarchyRules.WeekOver(10, 16) && HierarchyRules.WeekOver(10, 17), "hierarchy reports: weekly only on a Sunday, only what needs you never, and a week is seven days");
    }

    private static HierarchyState HierarchySample()
    {
        var s = new HierarchyState { NextId = 40, NextTemplate = 3, WeekFrom = 21, Settled = 4, Deep = true, MonthFrom = 15 };
        s.Company.Steps[(int)PolicyField.PayBand] = 5;
        PolicySet r = s.Want(true, "Midtown");
        r.Steps[(int)PolicyField.SkillFloor] = 2;
        PolicySet st = s.Want(false, "Main%20Street~12");
        st.Steps[(int)PolicyField.Budget] = 6;
        st.Steps[(int)PolicyField.GapDays] = 0;
        var tpl = new PolicyTemplate { Number = 2, Day = 19 };
        tpl.Set.Start();
        tpl.Set.Steps[(int)PolicyField.Reports] = 1;
        s.Templates.Add(tpl);
        PlaceWatch w = s.Watch("Main%20Street~12", true);
        w.GapFrom = 18;
        w.LowFrom = 20;
        w.KpiRun = 2;
        w.KpiDay = 22;
        w.Staff = 7;
        w.TopUp = 500;
        w.TopUpFrom = 15;
        w.Left.Add(19);
        w.Left.Add(21);
        Escalation e = s.Add(EscalationKind.Exception, "Main%20Street~12", EscalationRules.LevelOwner, 22);
        e.A = 2100;
        e.B = 1800;
        e.C = 1;
        e.Subject = "emp_7";
        e.Detail = "Ana|Lee;^*";
        e.By = "Bo";
        Escalation g = s.Add(EscalationKind.Gap, "Main%20Street~12", EscalationRules.LevelRegion, 20);
        g.State = (int)EscalationState.Resolved;
        HierarchyEntry entry = s.Log(HierarchyLog.Moved, 2, "Cy", "Main%20Street~12", 21, 9);
        entry.A = 1;
        entry.B = 55;
        entry.Detail = "Di/Shop";
        s.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Cfo, Id = "pro:3", Name = "Ed", Role = TalentCatalogue.Accountant, Level = 3, Place = "HQ~1", From = 9, Decided = 2, Paid = 300, ProposedDay = 14 });
        s.PickArea("Main%20Street~12", HierarchyRules.CustomArea(2));
        return s;
    }

    private static void HierarchySave()
    {
        HierarchyState s = HierarchySample();
        string line = HierarchyStore.Write(s);
        int version;
        HierarchyState back = HierarchyStore.Read(line, out version);
        Check(back != null && version == HierarchyStore.Version && HierarchyStore.Write(back) == line, "hierarchy save: a full state reads back and writes the very same line");
        Check(back != null && back.Company.Of(PolicyField.PayBand) == 5 && back.Region("Midtown").Of(PolicyField.SkillFloor) == 2 && !back.Region("Midtown").Sets(PolicyField.PayBand)
              && back.Store("Main%20Street~12").Of(PolicyField.Budget) == 6 && back.Templates.Count == 1 && back.Templates[0].Set.Of(PolicyField.Reports) == 1 && back.Deep
              && back.Watch("Main%20Street~12", false).TopUpOf(15) == 500 && back.Watch("Main%20Street~12", false).Left.Count == 2 && back.Escalations.Count == 2
              && back.Escalations[0].Subject == "emp_7" && back.Escalations[0].Detail.IndexOfAny(new[] { '|', ';', '^', '*' }) < 0 && back.Executives[0].Area == (int)ExecSeat.Cfo
              && back.AreaPicked("Main%20Street~12") == HierarchyRules.CustomArea(2) && back.Entries[0].Detail == "Di/Shop" && back.NextId == s.NextId,
            "hierarchy save: every part comes back, a name cannot break the line, and the ids go on from where they were");
        Check(HierarchyStore.Read("v=1^c:1*2", out version) == null && HierarchyStore.Read("garbage", out version) == null && HierarchyStore.Read(string.Empty, out version) == null
              && HierarchyStore.Load(null, out version) == null && HierarchyStore.Load(new Dictionary<string, string>(), out version) == null,
            "hierarchy save: a line with no header, or none at all, is no state: the company starts at the starting policy");
        HierarchyState later = HierarchyStore.Read(line.Replace("^h:", "^z:new|part^h:") + "^q:more", out version);
        Check(later != null && HierarchyStore.Write(later) == line, "hierarchy save: a part a later version adds is skipped and the rest reads as before");
        HierarchyState bounded = HierarchyStore.Read("v=1^h:1|1|0|0|0^c:99*-5*7^s:Shop|3*3*3*3*3*3*2*3*1*1*1*1*3*3*0*0*2*2*1*2*2*1*2*1*0", out version);
        Check(bounded != null && bounded.Company.Of(PolicyField.SkillFloor) == PolicyCatalogue.Def(PolicyField.SkillFloor).Steps.Length - 1
              && bounded.Company.Of(PolicyField.HirePayCap) == PolicyCatalogue.Def(PolicyField.HirePayCap).Default && bounded.Company.Of(PolicyField.Reports) == PolicyCatalogue.Def(PolicyField.Reports).Default
              && !bounded.Store("Shop").Sets(PolicyField.Approver) && !bounded.Store("Shop").Sets(PolicyField.Reports) && bounded.Store("Shop").Of(PolicyField.Sources) == 2,
            "hierarchy save: every step is held inside its rule, a missing one is the start, and a store never keeps a rule only the company sets");
        var full = new HierarchyState();
        for (int i = 0; i < HierarchyState.EscalationsKept + 20; i++)
        {
            Escalation e = full.Add(EscalationKind.Gap, "p" + i, EscalationRules.LevelOwner, i);
            e.State = i < 10 ? (int)EscalationState.Open : (int)EscalationState.Resolved;
        }
        for (int i = 0; i < HierarchyState.EntriesKept + 30; i++)
        {
            full.Log(HierarchyLog.Report, 4, "x", string.Empty, i, 0);
        }
        bool openKept = true;
        for (int i = 0; i < 10; i++)
        {
            openKept &= full.OpenOf(EscalationKind.Gap, "p" + i) != null;
        }
        HierarchyState fullBack = HierarchyStore.Read(HierarchyStore.Write(full), out version);
        Check(full.Escalations.Count == HierarchyState.EscalationsKept && openKept && full.Entries.Count == HierarchyState.EntriesKept && fullBack != null
              && fullBack.Escalations.Count == HierarchyState.EscalationsKept && fullBack.Entries.Count == HierarchyState.EntriesKept,
            "hierarchy save: the lists keep their newest past their caps, and an open escalation is never the one let go");
        var picks = new HierarchyState();
        picks.PickArea("a", HierarchyRules.CustomArea(1));
        picks.PickArea("a", HierarchyRules.CustomArea(3));
        picks.PickArea("b", HierarchyRules.CustomArea(1));
        picks.PickArea("b", null);
        Check(picks.AreaPicks.Count == 1 && picks.AreaPicked("a") == HierarchyRules.CustomArea(3) && picks.AreaPicked("b") == null,
            "hierarchy regions: a store is in one region at a time, and put back into its district's with none");
    }

    // The director goes through the rules: every automatic decision reads the policy in force, and nothing runs per frame.
    private static void HierarchyWiring()
    {
        string decisions = ModSource(@"Economy\Company\CompanyDirector.ManagerDecisions.cs");
        string managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs");
        string hierarchy = ModSource(@"Economy\Company\CompanyDirector.Hierarchy.cs");
        string escalations = ModSource(@"Economy\Company\CompanyDirector.Escalations.cs");
        string game = ModSource(@"Economy\Company\HierarchyGame.cs");
        string app = ModSource(@"Economy\Company\CompanyApp.Hierarchy.cs");
        string tests = ModSource(@"Economy\Company\CompanyDirector.HierarchyTests.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string features = ModSource(@"Meta\FeatureList.Economy.cs");
        string settings = ModSource(@"Core\Settings.cs");
        string raises = MemberText(decisions, "private void DecideRaises("), offers = MemberText(decisions, "private void DecideOffers("), hiring = MemberText(decisions, "private void DecideHiring(");
        Check(raises.Contains("Rule(mp, PolicyField.PayBand)") && raises.Contains("PolicyRules.RaiseCeiling(") && raises.Contains("PolicyRules.RaiseDue(") && raises.Contains("ToApprover(mp, f, false")
              && !raises.Contains("m.WageBand)") && offers.Contains("PolicyRules.RaiseCeiling(") && offers.Contains("ToApprover(mp, f, true") && !offers.Contains("Ceiling(f.Going, m.WageBand)")
              && hiring.Contains("PolicyRules.Pick(") && !hiring.Contains("ManagerRules.Pick(") && hiring.Contains("HireFromRegion("),
            "hierarchy wiring: raises, offers and hires read the pay band, the largest raise, the days between, the cap and the floor in force, and send what is past them to the approver");
        Check(MemberText(managers, "private void Round(int day, int hour, ManagerTask task)").Contains("!PolicyAllows(mp, task)")
              && MemberText(managers, "internal bool Manages(").Contains("PolicyAllows(ManagedAt(place), task)")
              && MemberText(managers, "internal int BudgetOf(").Contains("TopUpOf(m)") && MemberText(managers, "private void Hour(int day, int hour)").Contains("HierarchyRules.DigestDue(")
              && MemberText(managers, "public void ManagerLimit(").Contains("StoreOwn(place, "),
            "hierarchy wiring: a switch of the policy stops its kind in every round and asks you again, the budget counts its top-up, the day's notice follows the reports' rule, and a manager's band is the store's rule");
        Check(MemberText(hierarchy, "internal int Rule(").Contains("if (!HierarchyOn)") && MemberText(hierarchy, "internal int Rule(").Contains("m.WageBand")
              && MemberText(hierarchy, "internal int Rule(").Contains("PolicyRules.Effective("),
            "hierarchy wiring: switched off, every manager decides by their own band and budget and the start for the rest, as before");
        Check(MemberText(hierarchy, "partial void HierarchyLoad(").Contains("KeepOwnLimits()") && MemberText(hierarchy, "private void KeepOwnLimits(").Contains("ManagerCatalogue.WageBandDefault"),
            "hierarchy wiring: a save from before the policy keeps each manager's own band and budget as their store's rule");
        Check(MemberText(game, "public static bool Move(").Contains("GoingPublic.Mine(from)") && MemberText(game, "public static bool Move(").Contains("GoingPublic.Mine(to)")
              && MemberText(game, "public static bool HireFor(").Contains("GoingPublic.Mine(to)") && MemberText(escalations, "private void WatchPlace(").Contains("GoingPublic.Mine(mp.Place.Reg)")
              && MemberText(escalations, "private void ScopeOf(").Contains("GoingPublic.Mine(other.Place.Reg)"),
            "hierarchy wiring: only the player's own places are looked at, hired for or moved between (Going Public)");
        bool frames = true;
        foreach (string code in new[] { hierarchy, escalations, game, app, tests })
        {
            frames &= !code.Contains("void Update(") && !code.Contains("LateUpdate") && !code.Contains("FindObjectsByType") && !code.Contains("UnityEngine.Random") && !code.Contains("new System.Random");
        }
        Check(frames && game.Length > 0 && app.Length > 0, "hierarchy wiring: nothing per frame, no whole-scene search and no unseeded roll anywhere in it");
        Check(MemberText(escalations, "partial void HierarchyDay(").Contains("WatchPlace(_managed[i], day)") && MemberText(escalations, "partial void HierarchyHour(").Contains("ManagerCatalogue.StaffHour")
              && MemberText(managers, "private void Hour(int day, int hour)").Contains("HierarchyHour(day, hour)"),
            "hierarchy wiring: the escalations are looked at on the day and the exceptions at the staff hour, nowhere else");
        string[] buttons = { "TestHierarchyStore", "TestHierarchyRegion", "TestHierarchyExecutives", "TestHierarchyOwner", "TestHierarchyGap", "TestHierarchyBudget", "TestHierarchyDemand", "TestHierarchyMood", "TestHierarchyWave",
            "TestHierarchyKpi", "TestHierarchyException" };
        bool wired = true;
        foreach (string b in buttons)
        {
            wired &= mod.Contains("Economy.CompanyDirector.Instance?." + b + "()") && MemberText(tests, "public void " + b + "()").Contains("HierarchyCheck(test, ");
        }
        Check(wired && features.Contains("\"reallife_hier_test_exception\"") && features.Contains("Economy.HierarchyStore.Key"),
            "hierarchy tests: a button for each level and each trigger, each writing its PASS or FAIL line, listed under the feature");
        Check(settings.Contains("public static bool EconomyHierarchy = true;") && mod.Contains("Settings.EconomyHierarchyId, \"reallife_hier_enabled\""),
            "hierarchy option: on by default, on the options page under the managers");
    }

    private static readonly string[] HierarchyKeys =
    {
        "reallife_hier_enabled", "reallife_opt_tip_economy_hierarchy", "reallife_meta_title_economy_hierarchy", "reallife_guide_economy_hierarchy_about", "reallife_guide_economy_hierarchy",
        "reallife_hier_dir_inbox", "reallife_hier_dir_policy", "reallife_pol_dir_own", "reallife_hier_inbox_none", "reallife_hier_inbox_what", "reallife_hier_note_owner",
        "reallife_hier_note_week", "reallife_hier_note_filled", "reallife_hier_note_gap_left", "reallife_hier_note_no_gap", "reallife_hier_note_raised", "reallife_hier_note_none_underpaid",
        "reallife_hier_test_pass", "reallife_hier_test_fail", "reallife_hier_test_nobody", "reallife_pol_note_applied", "reallife_pol_note_cannot", "reallife_pol_note_saved",
        "reallife_hier_say_gap", "reallife_hier_say_budget", "reallife_hier_say_demand", "reallife_hier_say_demand_strike", "reallife_hier_say_mood", "reallife_hier_say_wave",
        "reallife_hier_say_kpi", "reallife_hier_say_exception", "reallife_hier_say_exception_offer", "reallife_hier_say_proposal", "reallife_pol_what", "reallife_pol_held_what",
        "reallife_hier_dash_what", "reallife_hier_what_yours", "reallife_hier_what_others", "reallife_hier_what_closed", "reallife_hier_region_custom", "reallife_hier_level_chip",
        "reallife_pol_unit_day", "reallife_pol_unit_days", "reallife_pol_unit_skill", "reallife_pol_follow_one", "reallife_pol_held_line", "reallife_pol_company_only_line",
        "reallife_pol_scope_region", "reallife_pol_scope_store", "reallife_pol_tpl_own", "reallife_pol_tpl_onto",
        "reallife_hier_seat_what_coo", "reallife_hier_seat_what_chro", "reallife_hier_seat_what_cfo", "reallife_hier_seat_hire", "reallife_hier_seat_summary",
        "reallife_hier_seat_empty_line", "reallife_hier_seat_figures", "reallife_hier_owner_title", "reallife_hier_owner_what", "reallife_hier_deep", "reallife_hier_deep_what",
        "reallife_hier_region_dash_what", "reallife_hier_region_own", "reallife_hier_note_exec_appointed", "reallife_hier_note_exec_cannot", "reallife_hier_note_exec_gone",
        "reallife_hier_note_exec_stood_down", "reallife_hier_note_no_moves", "reallife_hier_note_region_appointed", "reallife_hier_note_region_filled",
        "reallife_hier_note_region_moved", "reallife_hier_note_region_nothing", "reallife_hier_note_region_raised"
    };

    private static void HierarchyWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("hierarchy", langs, codes))
        {
            return;
        }
        var keys = new List<string>(HierarchyKeys);
        foreach (PolicyDef d in PolicyCatalogue.Defs)
        {
            keys.Add(d.TextKey);
            keys.Add(d.TextKey + "_what");
            for (int s = 0; d.Unit == PolicyUnit.Choice && s < d.Steps.Length; s++)
            {
                keys.Add(d.TextKey + "_" + s);
            }
        }
        for (int r = 0; r <= (int)Rung.Owner; r++)
        {
            keys.Add(LadderRanks.TitleKey(r, ExecSeat.None));
        }
        foreach (ExecSeat seat in ExecutiveHiring.Seats)
        {
            keys.Add(LadderRanks.TitleKey((int)Rung.Executive, seat));
        }
        keys.AddRange(PolicyCatalogue.CategoryKeys);
        foreach (string t in PolicyCatalogue.TemplateKeys)
        {
            keys.Add(t);
            keys.Add(t + "_what");
        }
        for (int k = 0; k <= (int)HierarchyLog.Policy; k++)
        {
            keys.Add("reallife_hier_log_" + ((HierarchyLog)k).ToString().ToLowerInvariant());
        }
        foreach (string key in new[] { "gap", "budget", "demand", "mood", "wave", "kpi", "exception", "proposal" })
        {
            keys.Add("reallife_hier_kind_" + key);
        }
        var missing = new List<string>();
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "hierarchy: every text is in every language with the same {names} (" + keys.Count + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        Dictionary<string, string> en = langs[0];
        int notices = 0, long240 = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            if ((row.Key.StartsWith("reallife_hier_note_", StringComparison.Ordinal) || row.Key.StartsWith("reallife_pol_note_", StringComparison.Ordinal) || row.Key.StartsWith("reallife_hier_test_", StringComparison.Ordinal)))
            {
                notices++;
                long240 += row.Value.Length > 240 ? 1 : 0;
            }
        }
        Check(notices > 0 && long240 == 0, "hierarchy: every notice is at most 240 characters in English (" + notices + ")");
        bool guide = Array.TrueForAll(langs, w => w.TryGetValue("reallife_guide_economy_hierarchy", out string g) && GuideNames(w, g, "reallife_hier_dir_inbox", "reallife_hier_dir_policy",
            "reallife_hier_do_fill", "reallife_hier_do_dismiss", "reallife_hier_open_dashboard", "reallife_hier_quick_fill", "reallife_hier_quick_raise", "reallife_mgr_enabled")
            && g.Contains("{hier_gap_days}") && g.Contains("{hier_staff_hour}") && g.Contains("{hier_templates_most}"));
        Check(guide, "hierarchy: the Guide page names its buttons and pages as the app does, with its figures from the catalogue, in each language");
    }

    // Whether a Guide page names each of these texts in bold, as the app shows them.
    private static bool GuideNames(Dictionary<string, string> words, string guide, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (!words.TryGetValue(key, out string name) || !guide.Contains("<b>" + name + "</b>"))
            {
                return false;
            }
        }
        return true;
    }

    // The policy in force read for a large company's places, every rule, as a page or the day's round reads it.
    private static void HierarchyTiming()
    {
        var s = new HierarchyState();
        for (int i = 0; i < 16; i++)
        {
            s.Want(true, "R" + i).Steps[(int)PolicyField.PayBand] = i % 7;
        }
        for (int i = 0; i < PolicyCatalogue.StoresMost; i++)
        {
            s.Want(false, "S" + i).Steps[(int)PolicyField.GapDays] = i % 6;
        }
        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int round = 0; round < 10; round++)
        {
            for (int i = 0; i < PolicyCatalogue.StoresMost; i++)
            {
                PolicySet region = s.Region("R" + (i % 16)), store = s.Store("S" + i);
                for (int f = 0; f < (int)PolicyField.Count; f++)
                {
                    sum += PolicyRules.Effective(s.Company, region, store, (PolicyField)f);
                }
            }
        }
        double ms = watch.Elapsed.TotalMilliseconds / 10;
        Check(sum > 0 && ms < 2.0, "hierarchy: every rule in force at " + PolicyCatalogue.StoresMost + " stores reads in under 2 ms (" + ms.ToString("0.000") + " ms)");
    }
}
