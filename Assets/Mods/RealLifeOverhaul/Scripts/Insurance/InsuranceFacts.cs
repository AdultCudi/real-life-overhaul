using System.Collections.Generic;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>A car and its policy as a page shows them.</summary>
    public sealed class CarCoverFact
    {
        public string Id = string.Empty, Name = string.Empty;
        public bool Insured;                 // the Garage has it insured today
        public int Tier = InsuranceCatalogue.StandardTier;
        public int DaysLeft;                 // of the paid month
        public readonly int[] Premium = new int[InsuranceCatalogue.Tiers];   // a month by tier, at the driver's record today
        public readonly int[] Change = new int[InsuranceCatalogue.Tiers];    // what moving to a tier costs now
        public int WaitUntil;                // the last day of the first days, 0 once they are over
        public int GraceUntil;               // the last morning a missed premium is asked for again, 0 while paid up
        public int NoClaim;                  // the no-claims discount, thousandths
        public long PaidYear;
        public int ClaimsYear;
        public bool Sound;                   // no damage to speak of and not broken down: its tier may be moved up
    }

    /// <summary>The player's health plan as a page shows it: Health's own figures, handed over by the wiring.</summary>
    public sealed class HealthCoverFact
    {
        public bool On;                      // Health runs
        public int Plan;                     // 0 none, 1 basic, 2 standard, 3 full
        public int Employer;                 // the employer's plan, 0 for none
        public readonly string[] Names = { string.Empty, string.Empty, string.Empty };
        public readonly int[] Premium = new int[InsuranceCatalogue.Tiers];
        public readonly int[] HospitalShare = new int[InsuranceCatalogue.Tiers];   // thousandths
        public readonly int[] DoctorShare = new int[InsuranceCatalogue.Tiers];
        public readonly int[] Excess = new int[InsuranceCatalogue.Tiers];
        public readonly long[] YearCap = new long[InsuranceCatalogue.Tiers];
        public int NextDue, Missed, WaitUntil, LapseAfter = 2;
        public long PaidYear, LastBill, LastClaim;
        public int LastBillDay;
    }

    /// <summary>A home and its policy as a page shows them.</summary>
    public sealed class HomeCoverFact
    {
        public string Place = string.Empty, Name = string.Empty;
        public bool Active;
        public int Tier = InsuranceCatalogue.StandardTier;
        public readonly int[] Premium = new int[InsuranceCatalogue.Tiers];
        public readonly int[] Change = new int[InsuranceCatalogue.Tiers];
        public int NextDue, WaitUntil, GraceUntil, NoClaim, ClaimsYear, Sqm, District = 1000, Loading;
        public long PaidYear;
    }

    /// <summary>One kind of cover at one business as a page shows it: the Company's own figures, handed over by the wiring.</summary>
    public sealed class BizCoverFact
    {
        public string Place = string.Empty, Name = string.Empty, KindName = string.Empty;
        public int Kind;
        public bool Active;
        public int Insurer = -1;
        public int Premium;                  // a day, of the policy in force
        public int Until, Claims;
        public long Most;                    // the most one claim is counted at
        public readonly int[] Quote = new int[InsuranceCatalogue.Tiers];        // a day, by insurer
        public readonly bool[] Writes = new bool[InsuranceCatalogue.Tiers];     // whether the insurer would write it
        public readonly int[] ExcessShare = new int[InsuranceCatalogue.Tiers];  // thousandths of a loss the holder bears
        public readonly int[] ExcessLeast = new int[InsuranceCatalogue.Tiers];
        public readonly int[] PayDays = new int[InsuranceCatalogue.Tiers];
    }

    /// <summary>A claim as the history shows it.</summary>
    public struct ClaimFact
    {
        public int Day, Line, Kind;
        public string What;
        public long Loss, Excess, Payout;
        public int Id;                       // a business claim's number at the Company, -1 for the others
        public int Stage;                    // 0 paid, 1 still to be put in (by StageDay), 2 put in (paid on StageDay), 3 never put in
        public int StageDay;
        public string KindName;              // a business claim's kind of cover in words; null for the others
    }

    /// <summary>
    /// What the Insurance pages read, filled by the director from the state, the game and the other features in one call (InsuranceDirector.Facts). Game-free, so the
    /// samples and the checks build the same pages from made-up facts.
    /// </summary>
    public sealed class InsuranceFacts
    {
        public bool Ready;
        public int Day, Level = 1000, MonthDays = 5, YearDays = 60;
        public long Cash;
        public int Grade = InsuranceCatalogue.CreditUnknown, Points, Crashes;
        public bool CarsOn;                  // car ownership runs
        public bool BusinessOn;              // the Company's cover runs
        public readonly List<CarCoverFact> Cars = new List<CarCoverFact>(4);
        public readonly HealthCoverFact Health = new HealthCoverFact();
        public readonly List<HomeCoverFact> Homes = new List<HomeCoverFact>(2);
        public readonly List<BizCoverFact> Business = new List<BizCoverFact>(12);
        public readonly string[] Insurers = { string.Empty, string.Empty, string.Empty };
        public readonly List<ClaimFact> Claims = new List<ClaimFact>(InsuranceCatalogue.ClaimsKept);
        public long PremiumsMonth;           // every cover in force, a month
        public long PaidYear;                // paid by the insurers this year
        public int InForce;                  // covers in force
    }
}
