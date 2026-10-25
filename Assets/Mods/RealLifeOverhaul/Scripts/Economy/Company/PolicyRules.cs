using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The five headings of the policy centre, in its order. Not saved.</summary>
    public enum PolicyCategory
    {
        Hiring = 0, Pay = 1, Demands = 2, Performance = 3, Escalation = 4, Count = 5
    }

    /// <summary>Every rule of the company's policy. Saved as a number each: never renumber, add at the end.</summary>
    public enum PolicyField
    {
        SkillFloor = 0,         // the least skill a manager hires
        HirePayCap = 1,         // the most a hire is paid, thousandths of the going rate
        Sources = 2,            // where a gap is filled from: the place's own job board, the region's boards, a move from a store with spare staff
        PayBand = 3,            // the most anybody is paid after a raise or an offer matched, thousandths of the going rate
        RaiseMost = 4,          // the largest raise a manager agrees, thousandths of the pay
        RaiseEvery = 5,         // days at least between two raises a manager agrees for one person
        Approver = 6,           // who may agree a raise or an offer past the band: nobody, the regional manager, the people chief, the owner
        ExceptionMargin = 7,    // how far past the band they may go, thousandths of the going rate
        AutoRaises = 8,         // requests for a raise answered by the managers
        AutoOffers = 9,         // rivals' offers answered by the managers
        AutoOvertime = 10,      // overtime the staff offer answered by the managers
        AutoClaims = 11,        // insurance claims put in by the managers
        Budget = 12,            // a manager's month for overtime and hires, a step of ManagerCatalogue.Budgets
        FinanceMost = 13,       // the most the finance chief adds to a month's budget that is running out
        ProfitTarget = 14,      // a place's profit a day it is held to
        MoodTarget = 15,        // the staff's standing it is held to
        Tolerance = 16,         // how far short of a target is still on target, thousandths
        KpiDays = 17,           // days off target in a row before it is escalated
        ProfitWeight = 18,      // per cent of a place's score that is its profit, the rest its staff's standing
        GapDays = 19,           // days a gap in the staff may last before it is escalated
        BudgetShare = 20,       // thousandths of a budget used from which it is escalated
        UnmetDays = 21,         // days a request or an offer may wait unanswered before it is escalated
        MoodFloor = 22,         // the staff's standing under which a place is escalated
        WaveCount = 23,         // staff leaving one place within a week that make a wave
        Reports = 24,           // how often the managers report: only what needs the owner, a week, a day
        RegionAppoints = 25,    // a regional manager appoints a manager at a store of theirs with none
        RegionReplaces = 26,    // a regional manager replaces a store's manager whose place stays off its targets
        Count = 27
    }

    /// <summary>
    /// Which way a rule's steps go. A lower scope (a region, a store) may set a rule only on the strict side of the scope
    /// above it: for <see cref="LooseUp"/> a higher step is looser, for <see cref="LooseDown"/> a lower one. A
    /// <see cref="CompanyOnly"/> rule is the company's alone.
    /// </summary>
    public enum PolicyWay
    {
        LooseUp = 0, LooseDown = 1, CompanyOnly = 2
    }

    /// <summary>How a rule's step reads: what its number is.</summary>
    public enum PolicyUnit
    {
        Skill = 0,      // points of the game's skill
        Share = 1,      // thousandths, shown as a share
        Days = 2,
        Money = 3,      // whole money at a price index of 100
        Choice = 4,     // one of the rule's own words
        OnOff = 5,      // 0 off, 1 on
        Count = 6,      // a number of people
        Percent = 7,
        BudgetStep = 8  // a step of ManagerCatalogue.Budgets
    }

    /// <summary>One rule: its heading, its steps, the step a company starts at and which way its steps go.</summary>
    public sealed class PolicyDef
    {
        public readonly PolicyField Field;
        public readonly PolicyCategory Category;
        public readonly string Key;             // reallife_pol_field_{Key}; its words reallife_pol_field_{Key}_{n} for a Choice
        public readonly int[] Steps;
        public readonly int Default;            // the step a company starts at: what the managers did before there was a policy
        public readonly PolicyWay Way;
        public readonly PolicyUnit Unit;

        public PolicyDef(PolicyField field, PolicyCategory category, string key, int[] steps, int def, PolicyWay way, PolicyUnit unit)
        {
            Field = field;
            Category = category;
            Key = key;
            Steps = steps;
            Default = def;
            Way = way;
            Unit = unit;
        }

        public string TextKey => PolicyCatalogue.Mod + "_pol_field_" + Key;
    }

    /// <summary>
    /// The company's rules as data: every rule, its steps and its starting step. The starting steps are what the managers
    /// did before there were policies (a band of ManagerCatalogue.WageBandDefault, no floor, no cap, every demand answered,
    /// a report a day), so a save that never opens the policy centre is decided exactly as before. Game-free.
    /// </summary>
    public static class PolicyCatalogue
    {
        public const string Mod = "reallife";       // the start of every key of the mod's, put before a rule's own name

        // Who may agree an exception (PolicyField.Approver): its steps are these.
        public const int ApproveNobody = 0, ApproveRegion = 1, ApprovePeople = 2, ApproveOwner = 3;
        // Where a gap is filled from (PolicyField.Sources).
        public const int SourceOwn = 0, SourceRegion = 1, SourceMove = 2;
        // How often the managers report (PolicyField.Reports).
        public const int ReportExceptions = 0, ReportWeekly = 1, ReportDaily = 2;

        public const int MoodNone = 0;              // a mood target or floor of nothing: none
        public const int ProfitNone = -1;           // a profit target of this: none
        public const int RaiseAny = 100000;         // a largest raise of this: no limit

        public static readonly PolicyDef[] Defs =
        {
            new PolicyDef(PolicyField.SkillFloor, PolicyCategory.Hiring, "skill_floor", new[] { 0, 10, 20, 30, 40, 50, 60, 70 }, 0, PolicyWay.LooseDown, PolicyUnit.Skill),
            new PolicyDef(PolicyField.HirePayCap, PolicyCategory.Hiring, "hire_cap", new[] { 900, 950, 1000, 1050, 1100, 1200, 1300 }, 6, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.Sources, PolicyCategory.Hiring, "sources", new[] { SourceOwn, SourceRegion, SourceMove }, SourceOwn, PolicyWay.LooseUp, PolicyUnit.Choice),
            new PolicyDef(PolicyField.PayBand, PolicyCategory.Pay, "pay_band", ManagerCatalogue.WageBands, ManagerCatalogue.WageBandDefault, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.RaiseMost, PolicyCategory.Pay, "raise_most", new[] { 20, 50, 100, 150, 200, 300, RaiseAny }, 6, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.RaiseEvery, PolicyCategory.Pay, "raise_every", new[] { 0, 7, 14, 30, 60, 90 }, 0, PolicyWay.LooseDown, PolicyUnit.Days),
            new PolicyDef(PolicyField.Approver, PolicyCategory.Pay, "approver", new[] { ApproveNobody, ApproveRegion, ApprovePeople, ApproveOwner }, ApproveNobody,
                PolicyWay.CompanyOnly, PolicyUnit.Choice),
            new PolicyDef(PolicyField.ExceptionMargin, PolicyCategory.Pay, "margin", new[] { 50, 100, 150, 200 }, 1, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.AutoRaises, PolicyCategory.Demands, "auto_raises", new[] { 0, 1 }, 1, PolicyWay.LooseUp, PolicyUnit.OnOff),
            new PolicyDef(PolicyField.AutoOffers, PolicyCategory.Demands, "auto_offers", new[] { 0, 1 }, 1, PolicyWay.LooseUp, PolicyUnit.OnOff),
            new PolicyDef(PolicyField.AutoOvertime, PolicyCategory.Demands, "auto_overtime", new[] { 0, 1 }, 1, PolicyWay.LooseUp, PolicyUnit.OnOff),
            new PolicyDef(PolicyField.AutoClaims, PolicyCategory.Demands, "auto_claims", new[] { 0, 1 }, 1, PolicyWay.LooseUp, PolicyUnit.OnOff),
            new PolicyDef(PolicyField.Budget, PolicyCategory.Demands, "budget", new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, ManagerCatalogue.BudgetDefault, PolicyWay.LooseUp, PolicyUnit.BudgetStep),
            new PolicyDef(PolicyField.FinanceMost, PolicyCategory.Demands, "finance_most", new[] { 0, 250, 500, 1000, 2500, 5000 }, 3, PolicyWay.LooseUp, PolicyUnit.Money),
            new PolicyDef(PolicyField.ProfitTarget, PolicyCategory.Performance, "profit_target", new[] { ProfitNone, 0, 100, 250, 500, 1000, 2500 }, 0, PolicyWay.LooseDown, PolicyUnit.Money),
            new PolicyDef(PolicyField.MoodTarget, PolicyCategory.Performance, "mood_target", new[] { MoodNone, 40, 50, 60, 70 }, 0, PolicyWay.LooseDown, PolicyUnit.Count),
            new PolicyDef(PolicyField.Tolerance, PolicyCategory.Performance, "tolerance", new[] { 0, 100, 250, 500 }, 2, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.KpiDays, PolicyCategory.Performance, "kpi_days", new[] { 1, 2, 3, 5, 7 }, 2, PolicyWay.LooseUp, PolicyUnit.Days),
            new PolicyDef(PolicyField.ProfitWeight, PolicyCategory.Performance, "profit_weight", new[] { 0, 25, 50, 75, 100 }, 2, PolicyWay.CompanyOnly, PolicyUnit.Percent),
            new PolicyDef(PolicyField.GapDays, PolicyCategory.Escalation, "gap_days", new[] { 1, 2, 3, 5, 7, 14 }, 2, PolicyWay.LooseUp, PolicyUnit.Days),
            new PolicyDef(PolicyField.BudgetShare, PolicyCategory.Escalation, "budget_share", new[] { 600, 700, 800, 900, 1000 }, 2, PolicyWay.LooseUp, PolicyUnit.Share),
            new PolicyDef(PolicyField.UnmetDays, PolicyCategory.Escalation, "unmet_days", new[] { 1, 2, 3 }, 1, PolicyWay.LooseUp, PolicyUnit.Days),
            new PolicyDef(PolicyField.MoodFloor, PolicyCategory.Escalation, "mood_floor", new[] { MoodNone, 20, 30, 40, 50 }, 2, PolicyWay.LooseDown, PolicyUnit.Count),
            new PolicyDef(PolicyField.WaveCount, PolicyCategory.Escalation, "wave_count", new[] { 2, 3, 4, 5 }, 1, PolicyWay.LooseUp, PolicyUnit.Count),
            new PolicyDef(PolicyField.Reports, PolicyCategory.Escalation, "reports", new[] { ReportExceptions, ReportWeekly, ReportDaily }, ReportDaily, PolicyWay.CompanyOnly, PolicyUnit.Choice),
            new PolicyDef(PolicyField.RegionAppoints, PolicyCategory.Hiring, "region_appoints", new[] { 0, 1 }, 0, PolicyWay.LooseUp, PolicyUnit.OnOff),
            new PolicyDef(PolicyField.RegionReplaces, PolicyCategory.Hiring, "region_replaces", new[] { 0, 1 }, 0, PolicyWay.LooseUp, PolicyUnit.OnOff)
        };

        /// <summary>reallife_pol_cat_{name}, by PolicyCategory.</summary>
        public static readonly string[] CategoryKeys = { "reallife_pol_cat_hiring", "reallife_pol_cat_pay", "reallife_pol_cat_demands", "reallife_pol_cat_performance", "reallife_pol_cat_escalation" };

        public const int TemplatesMost = 6;             // templates of the player's own a save keeps
        public const int RegionsMost = 32;              // regions with a policy of their own
        public const int StoresMost = 64;               // stores with a policy of their own (ManagerCatalogue.ManagersMost)
        public const int MoodDays = 2;                  // days under the floor in a row before a place is escalated
        public const int WaveWindow = 7;                // days a wave of leavers is counted over

        public static PolicyDef Def(PolicyField field)
        {
            int f = (int)field;
            return Defs[f >= 0 && f < Defs.Length ? f : 0];
        }

        /// <summary>The number a step of a rule stands for.</summary>
        public static int Value(PolicyField field, int step)
        {
            PolicyDef d = Def(field);
            return d.Steps[CompanyCatalogue.Clamp(step, 0, d.Steps.Length - 1)];
        }

        /// <summary>
        /// The built-in templates, by name key, each a step for every rule in PolicyField order (-1: the company's start).
        /// Careful holds pay and spending tight and asks the owner early; Lean answers less and reports a week; Generous pays
        /// to keep people and lets the regional managers and the people chief decide more.
        /// </summary>
        public static readonly string[] TemplateKeys = { "reallife_pol_tpl_start", "reallife_pol_tpl_careful", "reallife_pol_tpl_lean", "reallife_pol_tpl_generous" };

        public static readonly int[][] Templates =
        {
            null,
            new[] { 3, 3, 0, 2, 2, 3, 3, 0, 1, 1, 0, 1, 2, 1, 1, 2, 1, 1, 2, 1, 1, 0, 3, 0, 2, 0, 0 },
            new[] { 2, 2, 1, 2, 1, 4, 3, 0, 1, 0, 0, 1, 1, 0, 1, 0, 2, 2, 3, 3, 3, 1, 2, 1, 1, 1, 1 },
            new[] { 1, 6, 2, 5, 4, 1, 1, 2, 1, 1, 1, 1, 5, 4, 0, 3, 2, 2, 2, 2, 2, 1, 2, 1, 2, 1, 0 }
        };
    }

    /// <summary>
    /// One scope's rules: a step for every rule, or -1 where the scope sets none and the one above decides. The company's
    /// set has every step; a region's or a store's only those it sets. Game-free.
    /// </summary>
    public sealed class PolicySet
    {
        public string Scope = string.Empty;         // empty for the company, a region's key or a store's place
        public readonly int[] Steps = new int[(int)PolicyField.Count];

        public PolicySet()
        {
            Clear();
        }

        /// <summary>Every rule unset: the scope above decides them all.</summary>
        public void Clear()
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                Steps[i] = -1;
            }
        }

        /// <summary>Every rule at the company's starting step.</summary>
        public void Start()
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                Steps[i] = PolicyCatalogue.Defs[i].Default;
            }
        }

        public bool Sets(PolicyField field) => Steps[(int)field] >= 0;

        public int Of(PolicyField field) => Steps[(int)field];

        /// <summary>How many rules the scope sets itself.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Steps.Length; i++)
                {
                    n += Steps[i] >= 0 ? 1 : 0;
                }
                return n;
            }
        }

        public void CopyFrom(PolicySet other)
        {
            Array.Copy(other.Steps, Steps, Steps.Length);
        }
    }

    /// <summary>One template: a name and every rule's step, kept to put on any scope.</summary>
    public sealed class PolicyTemplate
    {
        public int Number;                          // 1 and up, its name "Template n"
        public int Day;                             // when it was saved
        public readonly PolicySet Set = new PolicySet();
    }

    /// <summary>
    /// The policy's rules, game-free (EconomyCheck compiles them): the rule in force at a place, from the company's, its
    /// region's and its own; a lower scope's step held to the strict side of the one above; what the managers may decide
    /// under it; and a template put on a scope. Every automatic decision of a manager, a regional manager or an executive
    /// asks <see cref="Effective"/>; nothing else decides their limits.
    /// </summary>
    public static class PolicyRules
    {
        /// <summary>
        /// The step in force: the company's, then the region's if it sets one, then the store's if it sets one, each held to
        /// the strict side of the step above it. A rule only the company sets is the company's. Either set may be null.
        /// </summary>
        public static int Effective(PolicySet company, PolicySet region, PolicySet store, PolicyField field)
        {
            PolicyDef d = PolicyCatalogue.Def(field);
            int step = company != null && company.Sets(field) ? company.Of(field) : d.Default;
            step = CompanyCatalogue.Clamp(step, 0, d.Steps.Length - 1);
            if (d.Way == PolicyWay.CompanyOnly)
            {
                return step;
            }
            if (region != null && region.Sets(field))
            {
                step = Held(d.Way, step, region.Of(field), d.Steps.Length);
            }
            if (store != null && store.Sets(field))
            {
                step = Held(d.Way, step, store.Of(field), d.Steps.Length);
            }
            return step;
        }

        /// <summary>A lower scope's step held to the strict side of the step above it.</summary>
        public static int Held(PolicyWay way, int above, int below, int steps)
        {
            below = CompanyCatalogue.Clamp(below, 0, steps - 1);
            if (way == PolicyWay.LooseUp)
            {
                return Math.Min(above, below);
            }
            return way == PolicyWay.LooseDown ? Math.Max(above, below) : above;
        }

        /// <summary>Whether a lower scope's step would be looser than the step above it, and so is held to it.</summary>
        public static bool Past(PolicyWay way, int above, int below)
        {
            return way == PolicyWay.LooseUp ? below > above : way == PolicyWay.LooseDown && below < above;
        }

        /// <summary>The steps a scope may pick for a rule: every one for the company; for a lower scope those not past the one above.</summary>
        public static bool Allowed(PolicyWay way, bool company, int above, int step)
        {
            if (company)
            {
                return true;
            }
            return way != PolicyWay.CompanyOnly && !Past(way, above, step);
        }

        /// <summary>The policy switch a kind of manager's decision answers to; -1 for a kind no switch of the policy covers.</summary>
        public static int SwitchOf(ManagerTask task)
        {
            switch (task)
            {
                case ManagerTask.Raises: return (int)PolicyField.AutoRaises;
                case ManagerTask.Offers: return (int)PolicyField.AutoOffers;
                case ManagerTask.Overtime: return (int)PolicyField.AutoOvertime;
                case ManagerTask.Claims: return (int)PolicyField.AutoClaims;
                default: return -1;
            }
        }

        /// <summary>
        /// The most an hour a raise may bring somebody to under the policy: the band's ceiling, and no more than the largest
        /// raise on the pay now. Hundredths an hour.
        /// </summary>
        public static int RaiseCeiling(int paid, int going, int bandStep, int raiseMostStep)
        {
            int band = ManagerRules.Ceiling(going, bandStep);
            int most = PolicyCatalogue.Value(PolicyField.RaiseMost, raiseMostStep);
            if (most >= PolicyCatalogue.RaiseAny)
            {
                return band;
            }
            long capped = Math.Max(0, paid) + (long)Math.Max(0, paid) * most / 1000;
            return (int)Math.Min(band, Math.Min(int.MaxValue / 4, capped));
        }

        /// <summary>Whether a manager may agree a raise for somebody today: none since the policy's days between two raises.</summary>
        public static bool RaiseDue(int lastRaise, int day, int everyStep)
        {
            int every = PolicyCatalogue.Value(PolicyField.RaiseEvery, everyStep);
            return every <= 0 || lastRaise <= 0 || day - lastRaise >= every;
        }

        /// <summary>The most a hire is paid an hour: the pay band's ceiling, and the hiring cap under it. Hundredths.</summary>
        public static int HireCeiling(int going, int bandStep, int capStep)
        {
            int cap = CompanyCatalogue.Share(Math.Max(0, going), PolicyCatalogue.Value(PolicyField.HirePayCap, capStep));
            return Math.Min(ManagerRules.Ceiling(going, bandStep), cap);
        }

        /// <summary>
        /// The candidate hired under the policy, by their place in the lists; -1 for none. As ManagerRules.Pick, with only
        /// those at or over the skill floor and inside the hiring cap: the same pick as before at the starting steps.
        /// </summary>
        public static int Pick(int[] wages, int[] goings, int[] skills, int count, int bandStep, int capStep, int floorStep, int left, bool mistake)
        {
            int floor = PolicyCatalogue.Value(PolicyField.SkillFloor, floorStep);
            int best = -1, second = -1;
            for (int i = 0; i < count; i++)
            {
                if (wages[i] <= 0 || skills[i] < floor || wages[i] > HireCeiling(goings[i], bandStep, capStep) || ManagerRules.HireCost(wages[i]) > left)
                {
                    continue;
                }
                if (best < 0 || Better(i, best, wages, skills))
                {
                    second = best;
                    best = i;
                }
                else if (second < 0 || Better(i, second, wages, skills))
                {
                    second = i;
                }
            }
            return mistake && second >= 0 ? second : best;
        }

        private static bool Better(int a, int b, int[] wages, int[] skills)
        {
            return skills[a] != skills[b] ? skills[a] > skills[b] : wages[a] < wages[b];
        }

        /// <summary>
        /// The most an approver may agree past the band, hundredths an hour: nothing with nobody named to approve, else the
        /// band's ceiling and the margin over it.
        /// </summary>
        public static int ExceptionCeiling(int going, int bandStep, int approverStep, int marginStep)
        {
            if (PolicyCatalogue.Value(PolicyField.Approver, approverStep) == PolicyCatalogue.ApproveNobody)
            {
                return 0;
            }
            return ManagerRules.Ceiling(going, bandStep) + CompanyCatalogue.Share(Math.Max(0, going), PolicyCatalogue.Value(PolicyField.ExceptionMargin, marginStep));
        }

        /// <summary>A template's steps put on a scope: every rule for the company; for a lower scope each held to the one above, the rules only the company sets left unset.</summary>
        public static void Apply(int[] steps, PolicySet onto, PolicySet above, bool company)
        {
            for (int f = 0; f < (int)PolicyField.Count; f++)
            {
                PolicyDef d = PolicyCatalogue.Defs[f];
                int step = steps != null && f < steps.Length && steps[f] >= 0 ? CompanyCatalogue.Clamp(steps[f], 0, d.Steps.Length - 1) : d.Default;
                if (company)
                {
                    onto.Steps[f] = step;
                    continue;
                }
                onto.Steps[f] = d.Way == PolicyWay.CompanyOnly ? -1 : above != null && above.Sets((PolicyField)f) ? Held(d.Way, above.Of((PolicyField)f), step, d.Steps.Length) : step;
            }
        }
    }
}
