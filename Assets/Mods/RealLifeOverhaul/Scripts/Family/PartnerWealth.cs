namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// How the partner takes the player's money (PartnerWealthRules): a text and a change of heart at each new step of the
    /// player's prosperity, by how they lean (Social's Supportive and Envious traits, or the default partner's seed),
    /// and the home's harmony drifting with the gap while the player is well off. With the option off nothing happens.
    /// Cost: nothing per frame; a few comparisons once a game hour in the day, and once a day.
    /// </summary>
    internal static class PartnerWealth
    {
        private static readonly string[] Texts = { "reallife_family_wealth_plain", "reallife_family_wealth_proud", "reallife_family_wealth_envious" };

        /// <summary>How the partner leans: Social's traits when it keeps them, else the default partner's seed.</summary>
        public static int Leaning => Partner.Kept(out PersonInfo p) ? p.Leaning : PartnerWealthRules.LeaningOf(FamilyState.S.PSeed);

        private static int Step => PartnerWealthRules.StepOf(SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f, FamilyState.IncomeHeld, Prices.Level);

        /// <summary>Once a game hour in the day: a reaction at a new step of the cash, when the partner's texts allow (PartnerVoice).</summary>
        public static void Hourly(int day)
        {
            FamilySave s = FamilyState.S;
            int fresh = PartnerWealthRules.NewStep(Step, s.PWealthStep);
            if (!Settings.FamilyWealthMood || !FamilyState.Together || fresh <= 0 || !PartnerVoice.Free)
            {
                return;
            }
            s.PWealthStep = fresh;
            React(Leaning);
        }

        /// <summary>Once a day: the home's drift with the gap.</summary>
        public static void Daily(int day)
        {
            if (!Settings.FamilyWealthMood || !FamilyState.Together)
            {
                return;
            }
            int step = Step, leaning = Leaning;
            float drift = PartnerWealthRules.Drift(leaning, step, FamilyState.Bond == Bond.Married);
            if (drift != 0f)
            {
                FamilyState.Harmony(drift);
            }
        }

        /// <summary>A new step reached: their text, and what it does to the home and to how fond they are.</summary>
        public static void React(int leaning)
        {
            FamilyState.Harmony(PartnerWealthRules.StepHarmony(leaning));
            float affection = PartnerWealthRules.StepAffection(leaning);
            if (affection != 0f)
            {
                Partner.Tell(affection > 0f ? FamilyEvent.SharedTime : FamilyEvent.BrokenPromise, affection);
            }
            PartnerVoice.Say(Texts[leaning], Args.Us);
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " heard how well the player is doing (" + (leaning == PartnerWealthRules.Supportive ? "proud"
                : leaning == PartnerWealthRules.Envious ? "envious" : "pleased") + ").");
        }

        /// <summary>Somebody new, or nobody: they react to the player's money from the first step reached with them.</summary>
        public static void Reset(FamilySave s)
        {
            s.PWealthStep = 0;
        }
    }
}
