namespace RealLifeOverhaul.Ui
{
    /// <summary>The glyphs the Company app puts on things that have no portrait. Game-free; read by the live source and the samples alike. Cost: an array read.</summary>
    public static class CompanyGlyphs
    {
        // One glyph for each rule of the policy centre, by the rule's number (Economy PolicyField): hiring, pay, demands, performance, escalation each read at a glance.
        private static readonly UiIcon[] Rules =
        {
            UiIcon.Cap,             // 0 SkillFloor: the least skill hired
            UiIcon.Coin,            // 1 HirePayCap: the most a hire is paid
            UiIcon.MapTrifold,      // 2 Sources: where a gap is filled from
            UiIcon.Percent,         // 3 PayBand: the most anybody is paid
            UiIcon.Lightning,       // 4 RaiseMost: the largest raise
            UiIcon.Calendar,        // 5 RaiseEvery: days between two raises
            UiIcon.Key,             // 6 Approver: who may agree an exception
            UiIcon.Scales,          // 7 ExceptionMargin: how far past the band
            UiIcon.Wallet,          // 8 AutoRaises: requests the managers answer
            UiIcon.Handshake,       // 9 AutoOffers: rivals' offers they answer
            UiIcon.Clock,           // 10 AutoOvertime: overtime they answer
            UiIcon.ShieldCheck,     // 11 AutoClaims: insurance claims they put in
            UiIcon.PiggyBank,       // 12 Budget: a manager's month
            UiIcon.Bank,            // 13 FinanceMost: what the finance chief adds
            UiIcon.Receipt,         // 14 ProfitTarget: a place's profit a day
            UiIcon.SmileyHappy,     // 15 MoodTarget: the staff's standing it is held to
            UiIcon.Level,           // 16 Tolerance: how far short is on target
            UiIcon.Timer,           // 17 KpiDays: days off target before it is escalated
            UiIcon.ChartPie,        // 18 ProfitWeight: the share of the score that is profit
            UiIcon.People,          // 19 GapDays: days a gap may last
            UiIcon.Tag,             // 20 BudgetShare: the share of a budget that escalates
            UiIcon.Envelope,        // 21 UnmetDays: days a request may wait
            UiIcon.SmileySad,       // 22 MoodFloor: the standing under which a place escalates
            UiIcon.SignOut,         // 23 WaveCount: staff leaving that make a wave
            UiIcon.Newspaper,       // 24 Reports: how often managers report
            UiIcon.IdBadge,         // 25 RegionAppoints: a regional manager appoints
            UiIcon.Reset            // 26 RegionReplaces: a regional manager replaces
        };

        /// <summary>The glyph of a policy rule, by its number; the sliders for a number that has none.</summary>
        public static UiIcon Policy(int field) => field >= 0 && field < Rules.Length ? Rules[field] : UiIcon.Sliders;
    }
}
