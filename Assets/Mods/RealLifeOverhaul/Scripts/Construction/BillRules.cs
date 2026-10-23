using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a bill is: owed to the company (a client, flat buyers) or by it (a crew's wages, a merchant's account). Saved as a number.</summary>
    public enum BillKind
    {
        Client = 0,     // a tender's client: the final payment at the handover
        Buyers = 1,     // buyers of flats sold before they were built: their balance at the handover
        Wages = 2,      // a crew's wages the company could not pay
        Merchant = 3    // materials a merchant delivered on account
    }

    /// <summary>
    /// Unpaid bills, game-free: a client or flat buyers who pay late or not at all, the company chasing the debt and then
    /// taking it to court (a civil claim of Justice's); and the other way, a crew left unpaid or a merchant's account not
    /// settled in time, who take the company to court. Every draw is seeded by the save's seed and the bill, so a debtor
    /// pays on the same day however often asked.
    /// </summary>
    public static class BillRules
    {
        public const int Never = -1;                       // a debtor's day to pay: they will not, unless chased or sued
        public const int ClientLatePermille = 260;         // a client pays late, of the handovers...
        public const int ClientNeverPermille = 90;         // ...or not at all (the city always pays, at worst late)
        public const int LateLeast = 3, LateMost = 20;     // days late
        public const int BuyerLatePermille = 170;          // a buyer of a flat sold off plan pays late or not at all
        public const int BuyerNeverPermille = 350;         // ...of those, not at all
        public const int ChaseMost = 2;                    // letters the company sends before it must go to court
        public const int ChaseRest = 3;                    // days between two
        public const int ChaseWorksPermille = 350;         // a debtor who would not pay pays after a letter
        public const int ChasedLeast = 2, ChasedMost = 5;  // ...within these days
        public const int SueAfterDays = 7;                 // days overdue before the company may sue without a letter
        public const int StrongPermille = 780;             // a claim for a debt on the company's own books: how strong
        public const int ChasedStrongPermille = 60;        // ...stronger for each letter sent
        public const int CityStrongPermille = 900;         // the city's own debt
        public const int WagesSueDays = 5;                 // days a crew's wages stand unpaid before it sues
        public const int MerchantTerms = 14;               // days a merchant gives on account
        public const int MerchantSueDays = 7;              // days overdue before a merchant sues
        public const int AccountLeastReputation = 30;      // a merchant gives an account to a company of this standing
        public const int RepPaidLate = 2;                  // the company's standing lost when it is taken to court for a debt of its own

        /// <summary>
        /// The day a client pays a bill due on a day: on it, some days late, or <see cref="Never"/>; the city (a public
        /// building or its works) pays late at worst.
        /// </summary>
        public static int ClientPays(int seed, int project, int due, bool city)
        {
            BuildRandom random = BuildRandom.Of(seed, project, 401);
            int roll = random.Range(0, 1000);
            int late = random.Range(LateLeast, LateMost + 1);
            if (roll < ClientNeverPermille && !city)
            {
                return Never;
            }
            return roll < ClientNeverPermille + ClientLatePermille ? due + late : due;
        }

        /// <summary>How many of a block's off plan buyers pay their balance late, and how many of those not at all.</summary>
        public static int BuyersLate(int seed, int project, int buyers, out int never)
        {
            never = 0;
            int late = 0;
            for (int b = 0; b < Math.Max(0, buyers); b++)
            {
                BuildRandom random = BuildRandom.Of(seed, project * 97 + b, 409);
                if (random.Chance(BuyerLatePermille))
                {
                    late++;
                    never += random.Chance(BuyerNeverPermille) ? 1 : 0;
                }
            }
            return late;
        }

        /// <summary>The day buyers who are late pay: some days on, or <see cref="Never"/> when none of them will.</summary>
        public static int BuyersPay(int seed, int project, int due, int late, int never)
        {
            if (late <= 0)
            {
                return due;
            }
            if (never >= late)
            {
                return Never;
            }
            BuildRandom random = BuildRandom.Of(seed, project, 419);
            return due + random.Range(LateLeast, LateMost + 1);
        }

        /// <summary>The day a debtor pays after the company's letter: sooner for one already paying late; for one who would not, sometimes.</summary>
        public static int AfterChase(int seed, int bill, int chase, int day, int paysOn)
        {
            BuildRandom random = BuildRandom.Of(seed, bill * 13 + chase, 421);
            int soon = day + random.Range(ChasedLeast, ChasedMost + 1);
            if (paysOn == Never)
            {
                return random.Chance(ChaseWorksPermille) ? soon : Never;
            }
            return Math.Min(paysOn, soon);
        }

        /// <summary>True when the company may chase a bill now: owed to it, overdue, letters left and rested.</summary>
        public static bool MayChase(int chased, int chasedDay, int due, int day)
        {
            return day >= due && chased < ChaseMost && (chased == 0 || day - chasedDay >= ChaseRest);
        }

        /// <summary>True when the company may go to court for a bill: chased at least once, or overdue a week.</summary>
        public static bool MaySue(int chased, int due, int day)
        {
            return day >= due && (chased > 0 || day - due >= SueAfterDays);
        }

        /// <summary>How strong the company's claim is, in thousandths: a debt on its books, more for each letter, the city's the strongest.</summary>
        public static int Strength(int chased, bool city)
        {
            return city ? CityStrongPermille : Math.Min(950, StrongPermille + Math.Max(0, chased) * ChasedStrongPermille);
        }

        /// <summary>True when the company's creditor goes to court: a crew after its days unpaid, a merchant after its account's days overdue.</summary>
        public static bool CreditorSues(BillKind kind, int due, int day)
        {
            return kind == BillKind.Wages ? day - due >= WagesSueDays : kind == BillKind.Merchant && day - due >= MerchantSueDays;
        }

        /// <summary>True when a merchant puts an order on account for a company short of cash: of some standing, and no account of theirs overdue.</summary>
        public static bool Account(int reputation, bool overdue)
        {
            return reputation >= AccountLeastReputation && !overdue;
        }
    }
}
