using System.Collections.Generic;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// What insurance keeps about one car's policy. Whether the car is insured today is the Garage's to say (its paid period, CarRecord.InsuredUntil): this is the tier and
    /// the record that tier is priced and paid by. Kept while the player owns the car, insured or not, so that ending a policy and buying it again gives no new year.
    /// </summary>
    public sealed class CarPolicy
    {
        public string Id = string.Empty;   // the game's vehicle instance id
        public int Tier = InsuranceCatalogue.StandardTier;
        public int Since;                  // the day the cover in force began; 0 for one from before the feature (no waiting days) or none
        public int Pre;                    // thousandths of damage the car had when the cover began: never the insurer's
        public int NoClaimMonths;          // months renewed without a claim
        public int GraceFrom;              // the first morning a premium could not be taken; 0 while paid up
        public int Year = -1;              // the year of the calendar the two sums below are of
        public long PaidYear;              // paid out this year
        public int ClaimsYear;             // claims paid this year
        public int LastPremium;            // the premium last paid, for the difference when the tier is moved up
        public bool Live;                  // the Garage had it insured when last looked: a policy that has ended since begins anew
    }

    /// <summary>One home's policy. Kept after it ends while the home is the player's, with its year's sums and its claims.</summary>
    public sealed class HomePolicy
    {
        public string Place = string.Empty;   // the address as the mod writes it (street~number)
        public string Name = string.Empty;    // the address as the player reads it, kept for the history
        public int Tier = InsuranceCatalogue.StandardTier;
        public bool Active;
        public int Since;                  // the day the cover in force began
        public int NextDue;                // the day the next month's premium is taken
        public int GraceFrom;              // the first morning a premium could not be taken; 0 while paid up
        public int NoClaimMonths;
        public int Year = -1;
        public long PaidYear;
        public int ClaimsYear;
        public int Premium;                // the premium last paid
        public readonly List<int> ClaimDays = new List<int>(8);   // the days of the claims that may still count against the premium
    }

    /// <summary>A claim paid, for the app's history.</summary>
    public sealed class ClaimNote
    {
        public int Day;
        public int Line;                   // CoverLine
        public int Kind;                   // ClaimKind
        public string What = string.Empty; // the car or the home
        public long Loss, Excess, Payout;
    }

    /// <summary>A rival tycoon's health cover.</summary>
    public sealed class RivalCover
    {
        public int Plan;                   // 0 none, 1 basic, 2 standard, 3 full (Health's plans)
        public int NextDue;
        public int Missed;
        public int Year = -1;
        public long PaidYear;              // what the plan paid of their hospital bills this year
        public long PremiumsYear;          // what they paid for it this year
    }

    /// <summary>Everything insurance keeps in the save (<see cref="InsuranceStore"/>).</summary>
    public sealed class InsuranceState
    {
        public int LastDay;
        public readonly List<CarPolicy> Cars = new List<CarPolicy>(4);
        public readonly List<HomePolicy> Homes = new List<HomePolicy>(2);
        public readonly List<ClaimNote> Claims = new List<ClaimNote>(InsuranceCatalogue.ClaimsKept);
        public readonly List<int> CrashDays = new List<int>(InsuranceCatalogue.CrashesMost);   // the days of the crashes with damages to another car that still count
        public readonly RivalCover[] Rivals = { new RivalCover(), new RivalCover(), new RivalCover() };

        public CarPolicy Car(string id)
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].Id == id)
                {
                    return Cars[i];
                }
            }
            return null;
        }

        public HomePolicy Home(string place)
        {
            for (int i = 0; i < Homes.Count; i++)
            {
                if (Homes[i].Place == place)
                {
                    return Homes[i];
                }
            }
            return null;
        }
    }
}
