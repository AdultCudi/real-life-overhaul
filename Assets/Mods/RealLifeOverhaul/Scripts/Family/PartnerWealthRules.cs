namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// How the partner takes the player's money, game-free: the player's prosperity (the daily income held all week and the
    /// cash) against five steps, a reaction once at each new step by how they lean (proud, envious or neither), and while
    /// the player is well off a daily drift of the home's harmony: down for the envious as the gap grows, half as much once
    /// married, a touch up for the supportive. Nothing here is read per frame.
    /// </summary>
    public static class PartnerWealthRules
    {
        public const int Neither = 0, Supportive = 1, Envious = 2;

        /// <summary>The steps of the player's prosperity at the start's prices: the cash in hand...</summary>
        public static readonly int[] Steps = { 2500, 10000, 50000, 100000, 500000 };

        /// <summary>...and the daily income held all week with it (IncomeRules).</summary>
        public static readonly int[] StepIncome = { 200, 800, 2500, 5000, 20000 };

        public const int DriftFrom = 3;                 // the step (2,500 a day and 50,000) from which the gap is felt day to day
        public const float EnviousDrift = -0.15f;       // harmony a day for each step past the one before DriftFrom
        public const float SupportiveDrift = 0.1f;      // harmony a day, from DriftFrom on
        public const float MarriedShare = 0.5f;         // what the envious drift is once married: what is the player's is theirs too

        /// <summary>How many steps are reached, 0 to 5: the income held all week and the cash, at today's prices (level 1000 at the start's).</summary>
        public static int StepOf(float cash, int held, int level)
        {
            int step = 0;
            for (int i = 0; i < Steps.Length; i++)
            {
                if (IncomeRules.Reached(held, cash, (int)(StepIncome[i] * (level / 1000f)), (int)(Steps[i] * (level / 1000f))))
                {
                    step = i + 1;
                }
            }
            return step;
        }

        /// <summary>How somebody without Social's traits leans, from their seed: about one in five envious, one in five supportive.</summary>
        public static int LeaningOf(int seed)
        {
            int roll = ((seed >> 8) & 0x7fffffff) % 5;
            return roll == 0 ? Envious : roll == 1 ? Supportive : Neither;
        }

        /// <summary>The home's harmony when a new step is reached.</summary>
        public static float StepHarmony(int leaning) => leaning == Supportive ? 4f : leaning == Envious ? -5f : 1f;

        /// <summary>The partner's affection when a new step is reached.</summary>
        public static float StepAffection(int leaning) => leaning == Supportive ? 2f : leaning == Envious ? -2f : 0f;

        /// <summary>The home's harmony a day at a step of the cash: nothing below <see cref="DriftFrom"/>.</summary>
        public static float Drift(int leaning, int step, bool married)
        {
            if (step < DriftFrom)
            {
                return 0f;
            }
            if (leaning == Envious)
            {
                return EnviousDrift * (step - DriftFrom + 1) * (married ? MarriedShare : 1f);
            }
            return leaning == Supportive ? SupportiveDrift : 0f;
        }

        /// <summary>The step to react to today: the cash's step when it is past the highest reacted to, else -1 (one reaction for skipped steps).</summary>
        public static int NewStep(int step, int highest) => step > highest ? step : -1;

        /// <summary>The leaning from Social's traits: supportive first, as the kinder of the two shows first.</summary>
        public static int LeaningOfTraits(bool supportive, bool envious) => supportive ? Supportive : envious ? Envious : Neither;
    }
}
