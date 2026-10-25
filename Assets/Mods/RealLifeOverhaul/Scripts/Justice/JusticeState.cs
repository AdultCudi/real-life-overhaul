using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>Where a case stands.</summary>
    public enum CaseStage
    {
        Bail = 0,       // the bail decision has not been put to the player yet
        Pending = 1,    // a court date is set
        Warrant = 2,    // the date was missed
        Closed = 3
    }

    /// <summary>How a case ended.</summary>
    public enum CaseOutcome
    {
        None = 0,
        Dismissed = 1,  // dropped for want of evidence: a wrongful arrest
        Acquitted = 2,
        Convicted = 3,  // after a trial
        Guilty = 4,     // pleaded guilty
        Deal = 5,       // pleaded to a deal
        Quashed = 6     // overturned on appeal
    }

    /// <summary>Who represents the player.</summary>
    public enum LawyerKind
    {
        None = 0,
        PublicDefender = 1,
        Firm = 2,
        Self = 3
    }

    /// <summary>One charge in a case.</summary>
    public sealed class JusticeCharge
    {
        public string Id;
        public float Evidence;      // 0 to 1
        public bool Dropped;        // withdrawn or dismissed before the verdict
        public bool Convicted;
        public bool Reduced;        // came down from a graver charge in a deal
    }

    /// <summary>A criminal case against the player, from the arrest or summons to the appeal.</summary>
    public sealed class JusticeCase
    {
        public int Number;
        public int OpenedDay;
        public int CourtDay;
        public CaseStage Stage;
        public CaseOutcome Outcome;
        public readonly List<JusticeCharge> Charges = new List<JusticeCharge>(4);
        public int Witnesses;
        public bool Cameras, InTheAct, Fled, Complied;
        public int PaidAtScene;     // fined at the roadside for the same thing: credited against the court's fine
        public int HeldHours;       // held by the police before release: counted in any compensation
        public int BailPaid;        // refundable on appearing; 0 for a bond or none
        public bool Bond;           // a bondsman stood bail: nothing to refund
        public bool Remand;
        public int Seed;            // everything decided by chance in this case comes from this: reloading changes nothing
        public bool DealReady;      // the lawyer has a deal on the table
        public bool MotionTried;    // the one motion to dismiss has been made
        public bool MetInPerson;    // prepared face to face at the lawyer's office
        public bool Reviewed;       // the prosecutor has reviewed the evidence
        public int Reminded;        // bits: 1 the evening before, 2 the morning, 4 the last hour
        public int WarrantDay;
        public int ClosedDay;
        public int FineImposed, PrisonImposed, JailImposed, CommunityImposed, ProbationImposed, SuspensionImposed;
        public bool Appealed;
        public float LawyerQuality; // as at the hearing: the appeal is argued by the same counsel
        public int Postponed;       // times the date was moved because the player could not have come
        public int Damages;         // a getaway's damages the case took from the later bill: paid with a conviction, dropped otherwise

        public bool Open => Stage != CaseStage.Closed;

        public bool WasConviction => Outcome == CaseOutcome.Convicted || Outcome == CaseOutcome.Guilty || Outcome == CaseOutcome.Deal;

        /// <summary>The gravest class among the charges still standing.</summary>
        public ChargeClass Weight
        {
            get
            {
                ChargeClass worst = ChargeClass.Misdemeanour;
                for (int i = 0; i < Charges.Count; i++)
                {
                    ChargeDef def = Charges[i].Dropped ? null : JusticeLaw.Find(Charges[i].Id);
                    if (def != null && def.Class > worst)
                    {
                        worst = def.Class;
                    }
                }
                return worst;
            }
        }

        /// <summary>The charge still standing that carries most, or null when none stands.</summary>
        public JusticeCharge Main
        {
            get
            {
                JusticeCharge main = null;
                int most = -1;
                for (int i = 0; i < Charges.Count; i++)
                {
                    ChargeDef def = Charges[i].Dropped ? null : JusticeLaw.Find(Charges[i].Id);
                    int weight = def == null ? -1 : (int)def.Class * 100000 + def.Fine;
                    if (weight > most)
                    {
                        most = weight;
                        main = Charges[i];
                    }
                }
                return main;
            }
        }

        /// <summary>Charges not dropped.</summary>
        public int Standing
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Charges.Count; i++)
                {
                    if (!Charges[i].Dropped)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool Has(string chargeId)
        {
            for (int i = 0; i < Charges.Count; i++)
            {
                if (Charges[i].Id == chargeId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Money owed to the courts or a lawyer, taken in daily instalments.</summary>
    public sealed class JusticeDebt
    {
        public const int Fine = 0, Fee = 1, Civil = 2;
        public const int Final = 999999;    // as a case number: a fine standing in for unworked hours, never turned back into hours

        public int Kind;
        public int Left;
        public int PerDay;
        public int Missed;      // days running on which it could not be taken
        public int CaseNumber;
    }

    /// <summary>A conviction on the player's record.</summary>
    public sealed class JusticeRecord
    {
        public int Day;
        public string ChargeId;
        public int CaseNumber;
        public bool Sealed;
    }

    /// <summary>A civil claim against the player.</summary>
    public sealed class JusticeSuit
    {
        public int Number;
        public int Claim;
        public int DueDay;
        public bool Injury;
        public bool Contested;
        public int Seed;
    }

    /// <summary>Everything the justice system remembers in a save. Game-free: the director tells it the day.</summary>
    public static partial class JusticeState
    {
        public const string Key = "reallife:justice";
        private const int ClosedKept = 6;      // closed cases remembered, for appeals and the file

        public static bool Loaded { get; private set; }

        public static readonly List<JusticeCase> Cases = new List<JusticeCase>(8);
        public static readonly List<JusticeDebt> Debts = new List<JusticeDebt>(6);
        public static readonly List<JusticeRecord> Record = new List<JusticeRecord>(12);
        public static readonly List<JusticeSuit> Suits = new List<JusticeSuit>(2);

        public static int NextNumber = 1;
        public static int Day;                  // the last day the daily business was done
        public static int ProbationUntil;       // the last day of probation; 0 when none
        public static int SuspendedUntil;       // driving licence (the default Licences bridge keeps it here)
        public static int CommunityHours;       // still owed
        public static int CommunityDue;         // the day by which
        public static int LastConvictionDay;
        public static int LastSuitDay;
        public static int JailHoursPending;     // a custody sentence not yet begun
        public static int HearNow;              // the case to be heard the moment the player is brought up; 0 for none
        public static bool ServeWarrant;        // a warrant is out and the police have the player: taken in when it is safe
        public static int Compensated;          // paid to the player for wrongful arrests, for the file

        public static LawyerKind Lawyer;
        public static string LawyerName = string.Empty;
        public static string LawyerStreet = string.Empty;
        public static int LawyerNumber;
        public static int LawyerStars;

        // A prison term in progress; none while PrisonTotal is 0.
        public static int PrisonTotal;          // days, after any remission so far
        public static int PrisonOriginal;
        public static int PrisonServed;         // mornings seen
        public static int PrisonBehaviour;
        public static int PrisonEarned;
        public static int PrisonStudied;
        public static int PrisonExercised;
        public static int PrisonRemitted;
        public static bool PrisonParoleHeard;
        public static int PrisonCase;
        public static string PrisonCar = string.Empty;      // the id of the car left behind
        public static string PrisonSeen = string.Empty;     // ids of the events this term has had, comma separated

        public static bool InPrison => PrisonTotal > 0;

        public static void Reset()
        {
            Loaded = false;
            Clear();
        }

        /// <summary>Everything forgotten: the "clear everything" test button, and the start of every load.</summary>
        public static void Clear()
        {
            Cases.Clear();
            Debts.Clear();
            Record.Clear();
            Suits.Clear();
            Claims.Clear();
            NextNumber = 1;
            Day = ProbationUntil = SuspendedUntil = CommunityHours = CommunityDue = LastConvictionDay = LastSuitDay = 0;
            JailHoursPending = HearNow = Compensated = 0;
            ServeWarrant = false;
            ClearLawyer();
            ClearPrison();
        }

        public static void ClearLawyer()
        {
            Lawyer = LawyerKind.None;
            LawyerName = LawyerStreet = string.Empty;
            LawyerNumber = LawyerStars = 0;
        }

        public static void ClearPrison()
        {
            PrisonTotal = PrisonOriginal = PrisonServed = PrisonBehaviour = PrisonEarned = PrisonStudied = PrisonExercised = PrisonRemitted = PrisonCase = 0;
            PrisonParoleHeard = false;
            PrisonCar = PrisonSeen = string.Empty;
        }

        public static JusticeCase Find(int number)
        {
            for (int i = 0; i < Cases.Count; i++)
            {
                if (Cases[i].Number == number)
                {
                    return Cases[i];
                }
            }
            return null;
        }

        /// <summary>The open case that comes up first (a warrant before anything), or null.</summary>
        public static JusticeCase NextOpen
        {
            get
            {
                JusticeCase next = null;
                for (int i = 0; i < Cases.Count; i++)
                {
                    JusticeCase c = Cases[i];
                    if (!c.Open)
                    {
                        continue;
                    }
                    if (next == null || Order(c) < Order(next))
                    {
                        next = c;
                    }
                }
                return next;
            }
        }

        private static int Order(JusticeCase c)
        {
            return c.Stage == CaseStage.Warrant ? -100000 + c.Number : c.Stage == CaseStage.Bail ? -50000 + c.Number : c.CourtDay * 100 + c.Number % 100;
        }

        public static int OpenCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Cases.Count; i++)
                {
                    if (Cases[i].Open)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public static bool HasWarrant
        {
            get
            {
                for (int i = 0; i < Cases.Count; i++)
                {
                    if (Cases[i].Stage == CaseStage.Warrant)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Convictions on the record that have not been sealed.</summary>
        public static int Priors
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Record.Count; i++)
                {
                    if (!Record[i].Sealed)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public static int Owed
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Debts.Count; i++)
                {
                    total += Debts[i].Left;
                }
                return total;
            }
        }

        public static bool OnProbation(int day)
        {
            return ProbationUntil > 0 && day <= ProbationUntil;
        }

        public static bool Suspended(int day)
        {
            return SuspendedUntil > 0 && day <= SuspendedUntil;
        }

        /// <summary>The latest conviction that can still be appealed on this day, or null.</summary>
        public static JusticeCase Appealable(int day)
        {
            for (int i = Cases.Count - 1; i >= 0; i--)
            {
                JusticeCase c = Cases[i];
                if (c.Stage == CaseStage.Closed && c.WasConviction && !c.Appealed && day - c.ClosedDay <= JusticeLaw.AppealWindowDays)
                {
                    return c;
                }
            }
            return null;
        }

        public static JusticeCase Open(int day)
        {
            var c = new JusticeCase { Number = NextNumber++, OpenedDay = day, Stage = CaseStage.Bail };
            c.Seed = JusticeLaw.Hash("case" + c.Number.ToString(CultureInfo.InvariantCulture)) ^ (day * 7919);
            Cases.Add(c);
            return c;
        }

        public static void AddDebt(int kind, int amount, int planDays, int caseNumber)
        {
            if (amount <= 0)
            {
                return;
            }
            planDays = Math.Max(1, planDays);
            Debts.Add(new JusticeDebt { Kind = kind, Left = amount, PerDay = Math.Max(10, (amount + planDays - 1) / planDays), CaseNumber = caseNumber });
        }

        /// <summary>Strikes off what is still owed of one kind on one case. Returns how much that was.</summary>
        public static int CancelDebts(int caseNumber, int kind)
        {
            int cancelled = 0;
            for (int i = Debts.Count - 1; i >= 0; i--)
            {
                if (Debts[i].CaseNumber == caseNumber && Debts[i].Kind == kind)
                {
                    cancelled += Debts[i].Left;
                    Debts.RemoveAt(i);
                }
            }
            return cancelled;
        }

        public static void AddRecord(string chargeId, int day, int caseNumber)
        {
            Record.Add(new JusticeRecord { ChargeId = chargeId, Day = day, CaseNumber = caseNumber });
            while (Record.Count > 24)
            {
                Record.RemoveAt(0);
            }
        }

        public static void RemoveRecord(int caseNumber)
        {
            for (int i = Record.Count - 1; i >= 0; i--)
            {
                if (Record[i].CaseNumber == caseNumber)
                {
                    Record.RemoveAt(i);
                }
            }
        }

        /// <summary>Seals every entry. Returns how many were not sealed before.</summary>
        public static int Seal()
        {
            int count = 0;
            for (int i = 0; i < Record.Count; i++)
            {
                if (!Record[i].Sealed)
                {
                    Record[i].Sealed = true;
                    count++;
                }
            }
            return count;
        }

        /// <summary>Drops the oldest closed cases (and claims of the player's long decided) so the save's line stays short.</summary>
        public static void Trim()
        {
            TrimClaims();
            int closed = 0;
            for (int i = Cases.Count - 1; i >= 0; i--)
            {
                if (Cases[i].Stage == CaseStage.Closed && ++closed > ClosedKept)
                {
                    Cases.RemoveAt(i);
                }
            }
        }
    }
}
