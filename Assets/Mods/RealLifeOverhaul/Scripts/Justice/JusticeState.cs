using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    // Game-free, like JusticeLaw.cs: the director tells it the day.

    public enum CaseStage
    {
        Bail = 0,       // opened; the bail decision has not been put to the player yet
        Pending = 1,    // a court date is set
        Warrant = 2,    // the date was missed: a bench warrant is out
        Closed = 3
    }

    public enum CaseOutcome
    {
        None = 0,
        Dismissed = 1,  // dropped for want of evidence (the wrongful arrest)
        Acquitted = 2,
        Convicted = 3,  // after a trial
        Guilty = 4,     // pleaded guilty
        Deal = 5,       // pleaded to a deal
        Quashed = 6     // convicted, then overturned on appeal
    }

    public enum LawyerKind
    {
        None = 0,
        PublicDefender = 1,
        Firm = 2,
        Self = 3
    }

    public sealed class JusticeCharge
    {
        public string Id;
        public float Evidence;      // 0 to 1
        public bool Dropped;        // withdrawn or dismissed before the verdict
        public bool Convicted;
        public bool Reduced;        // came down from a graver charge in a deal
    }

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
        public int BailPaid;        // refundable, returned on appearing; 0 for a bond or none
        public bool Bond;           // a bondsman stood bail: nothing to refund
        public bool Remand;         // held until the hearing
        public int Seed;            // everything decided by chance in this case comes from this: reloading changes nothing
        public bool DealReady;      // the lawyer has a deal on the table
        public bool MotionTried;    // the one motion to dismiss has been made
        public bool MetInPerson;    // prepared face to face at the lawyer's office
        public bool Reviewed;       // the prosecutor's review of the evidence has happened
        public int Reminded;        // bits: 1 the evening before, 2 the morning, 4 the last hour
        public int WarrantDay;      // the day the warrant went out
        public int ClosedDay;
        public int FineImposed, PrisonImposed, JailImposed, CommunityImposed, ProbationImposed, SuspensionImposed;
        public bool Appealed;
        public float LawyerQuality; // as it was at the hearing: the appeal is argued by the same counsel
        public int Postponed;       // times the date was moved because the player could not have come

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

    public sealed class JusticeDebt
    {
        public const int Fine = 0, Fee = 1, Civil = 2;
        public const int Final = 999999;    // as a case number: a fine that stands in for unworked hours, never turned back into hours

        public int Kind;
        public int Left;
        public int PerDay;
        public int Missed;      // days running on which it could not be taken
        public int CaseNumber;
    }

    public sealed class JusticeRecord
    {
        public int Day;
        public string ChargeId;
        public int CaseNumber;
        public bool Sealed;
    }

    public sealed class JusticeSuit
    {
        public int Number;
        public int Claim;
        public int DueDay;
        public bool Injury;
        public bool Contested;
        public int Seed;
    }

    /// <summary>
    /// Everything the justice system remembers in a savegame: cases, the lawyer, what is owed,
    /// probation, the record, a prison term in progress. One line of text in the save's modData
    /// (a plain string dictionary the base game carries along without reading), so a save made
    /// with it loads without the mod, and one from before it simply has no history. Anything in
    /// that line that does not read as expected is left at nothing: a damaged line costs the
    /// history, never the load.
    /// </summary>
    public static class JusticeState
    {
        public const string Key = "reallife:justice";
        private const string Version = "1";
        private const int ClosedKept = 6;      // closed cases remembered (for appeals and the file)

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
        public static bool ServeWarrant;        // the police have the player and a warrant is out: taken in when it is safe
        public static int Compensated;          // what has been paid to the player for wrongful arrests, for the file

        public static LawyerKind Lawyer;
        public static string LawyerName = string.Empty;
        public static string LawyerStreet = string.Empty;
        public static int LawyerNumber;
        public static int LawyerStars;

        // A prison term in progress. Total 0: none.
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

        // ---------------------------------------------------------------- questions

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

        /// <summary>The most recent closed case that ended in a conviction and can still be appealed on this day, or null.</summary>
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

        // ---------------------------------------------------------------- changes

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

        /// <summary>Whatever is still owed on one case's fine is struck off (an appeal won). Returns how much that was.</summary>
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

        public static int RemoveRecord(int caseNumber)
        {
            int removed = 0;
            for (int i = Record.Count - 1; i >= 0; i--)
            {
                if (Record[i].CaseNumber == caseNumber)
                {
                    Record.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>Seals every entry. Returns how many were open.</summary>
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

        /// <summary>Old closed cases are dropped so the line in the save stays short.</summary>
        public static void Trim()
        {
            int closed = 0;
            for (int i = Cases.Count - 1; i >= 0; i--)
            {
                if (Cases[i].Stage == CaseStage.Closed && ++closed > ClosedKept)
                {
                    Cases.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------- the save

        public static void Load(Dictionary<string, string> data)
        {
            Clear();
            Loaded = true;
            if (data != null && data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    Parse(text);
                }
                catch (Exception)
                {
                    Clear();   // a damaged line: no history, and the load goes on
                }
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (Loaded && data != null)
            {
                data[Key] = Write();
            }
        }

        public static string Write()
        {
            Trim();
            var text = new StringBuilder(256);
            text.Append(Version);
            text.Append("|H:");
            Join(text, NextNumber, Day, ProbationUntil, SuspendedUntil, CommunityHours, CommunityDue, LastConvictionDay, LastSuitDay,
                JailHoursPending, HearNow, ServeWarrant ? 1 : 0, Compensated);
            text.Append("|L:");
            Join(text, (int)Lawyer, LawyerNumber, LawyerStars);
            text.Append(',').Append(Escape(LawyerName)).Append(',').Append(Escape(LawyerStreet));
            text.Append("|P:");
            Join(text, PrisonTotal, PrisonOriginal, PrisonServed, PrisonBehaviour, PrisonEarned, PrisonStudied, PrisonExercised, PrisonRemitted,
                PrisonParoleHeard ? 1 : 0, PrisonCase);
            text.Append(',').Append(Escape(PrisonCar)).Append(',').Append(Escape(PrisonSeen));
            text.Append("|C:");
            for (int i = 0; i < Cases.Count; i++)
            {
                JusticeCase c = Cases[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                int flags = (c.Cameras ? 1 : 0) | (c.InTheAct ? 2 : 0) | (c.Fled ? 4 : 0) | (c.Complied ? 8 : 0) | (c.Bond ? 16 : 0) | (c.Remand ? 32 : 0)
                            | (c.DealReady ? 64 : 0) | (c.MotionTried ? 128 : 0) | (c.MetInPerson ? 256 : 0) | (c.Reviewed ? 512 : 0) | (c.Appealed ? 1024 : 0);
                Join(text, c.Number, c.OpenedDay, c.CourtDay, (int)c.Stage, (int)c.Outcome, c.Witnesses, flags, c.PaidAtScene, c.HeldHours, c.BailPaid,
                    c.Seed, c.Reminded, c.WarrantDay, c.ClosedDay, c.FineImposed, c.PrisonImposed, c.JailImposed, c.CommunityImposed, c.ProbationImposed,
                    c.SuspensionImposed, Mille(c.LawyerQuality), c.Postponed);
                text.Append(',');
                for (int k = 0; k < c.Charges.Count; k++)
                {
                    JusticeCharge charge = c.Charges[k];
                    if (k > 0)
                    {
                        text.Append('/');
                    }
                    text.Append(Escape(charge.Id)).Append('~').Append(Mille(charge.Evidence).ToString(CultureInfo.InvariantCulture)).Append('~')
                        .Append(((charge.Dropped ? 1 : 0) | (charge.Convicted ? 2 : 0) | (charge.Reduced ? 4 : 0)).ToString(CultureInfo.InvariantCulture));
                }
            }
            text.Append("|D:");
            for (int i = 0; i < Debts.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Debts[i].Kind, Debts[i].Left, Debts[i].PerDay, Debts[i].Missed, Debts[i].CaseNumber);
            }
            text.Append("|R:");
            for (int i = 0; i < Record.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Record[i].Day, Record[i].CaseNumber, Record[i].Sealed ? 1 : 0);
                text.Append(',').Append(Escape(Record[i].ChargeId));
            }
            text.Append("|S:");
            for (int i = 0; i < Suits.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Suits[i].Number, Suits[i].Claim, Suits[i].DueDay, Suits[i].Injury ? 1 : 0, Suits[i].Contested ? 1 : 0, Suits[i].Seed);
            }
            return text.ToString();
        }

        public static void Parse(string text)
        {
            string[] sections = text.Split('|');
            if (sections.Length < 2 || sections[0] != Version)
            {
                return;
            }
            for (int s = 1; s < sections.Length; s++)
            {
                string section = sections[s];
                if (section.Length < 2 || section[1] != ':')
                {
                    continue;
                }
                string body = section.Substring(2);
                switch (section[0])
                {
                    case 'H':
                    {
                        var f = new Fields(body);
                        NextNumber = Math.Max(1, f.Int());
                        Day = f.Int();
                        ProbationUntil = f.Int();
                        SuspendedUntil = f.Int();
                        CommunityHours = Math.Min(JusticeLaw.CommunityCapHours, Math.Max(0, f.Int()));
                        CommunityDue = f.Int();
                        LastConvictionDay = f.Int();
                        LastSuitDay = f.Int();
                        JailHoursPending = Math.Min(JusticeLaw.CustodyCapHours, Math.Max(0, f.Int()));
                        HearNow = f.Int();
                        ServeWarrant = f.Int() != 0;
                        Compensated = f.Int();
                        break;
                    }
                    case 'L':
                    {
                        var f = new Fields(body);
                        int kind = f.Int();
                        Lawyer = kind >= 0 && kind <= 3 ? (LawyerKind)kind : LawyerKind.None;
                        LawyerNumber = f.Int();
                        LawyerStars = f.Int();
                        LawyerName = f.Text();
                        LawyerStreet = f.Text();
                        break;
                    }
                    case 'P':
                    {
                        var f = new Fields(body);
                        PrisonTotal = Math.Min(JusticeLaw.PrisonCapMax, Math.Max(0, f.Int()));
                        PrisonOriginal = f.Int();
                        PrisonServed = Math.Max(0, f.Int());
                        PrisonBehaviour = f.Signed();
                        PrisonEarned = f.Int();
                        PrisonStudied = f.Int();
                        PrisonExercised = f.Int();
                        PrisonRemitted = f.Int();
                        PrisonParoleHeard = f.Int() != 0;
                        PrisonCase = f.Int();
                        PrisonCar = f.Text();
                        PrisonSeen = f.Text();
                        break;
                    }
                    case 'C':
                        foreach (string one in body.Split(';'))
                        {
                            JusticeCase c = ParseCase(one);
                            if (c != null)
                            {
                                Cases.Add(c);
                            }
                        }
                        break;
                    case 'D':
                        foreach (string one in body.Split(';'))
                        {
                            if (one.Length == 0)
                            {
                                continue;
                            }
                            var f = new Fields(one);
                            var debt = new JusticeDebt { Kind = f.Int(), Left = f.Int(), PerDay = Math.Max(10, f.Int()), Missed = f.Int(), CaseNumber = f.Int() };
                            if (debt.Left > 0 && debt.Kind >= 0 && debt.Kind <= 2)
                            {
                                Debts.Add(debt);
                            }
                        }
                        break;
                    case 'R':
                        foreach (string one in body.Split(';'))
                        {
                            if (one.Length == 0)
                            {
                                continue;
                            }
                            var f = new Fields(one);
                            var entry = new JusticeRecord { Day = f.Int(), CaseNumber = f.Int(), Sealed = f.Int() != 0, ChargeId = f.Text() };
                            if (JusticeLaw.Find(entry.ChargeId) != null)   // a charge this build does not have is dropped
                            {
                                Record.Add(entry);
                            }
                        }
                        break;
                    case 'S':
                        foreach (string one in body.Split(';'))
                        {
                            if (one.Length == 0)
                            {
                                continue;
                            }
                            var f = new Fields(one);
                            var suit = new JusticeSuit { Number = f.Int(), Claim = f.Int(), DueDay = f.Int(), Injury = f.Int() != 0, Contested = f.Int() != 0, Seed = f.Int() };
                            if (suit.Claim > 0)
                            {
                                Suits.Add(suit);
                            }
                        }
                        break;
                }
            }
            // What must hold whatever the line said.
            if (HearNow != 0 && (Find(HearNow) == null || !Find(HearNow).Open))
            {
                HearNow = 0;
            }
            if (PrisonTotal > 0 && PrisonServed > PrisonTotal)
            {
                PrisonServed = PrisonTotal;
            }
        }

        private static JusticeCase ParseCase(string text)
        {
            if (text.Length == 0)
            {
                return null;
            }
            var f = new Fields(text);
            var c = new JusticeCase { Number = f.Int(), OpenedDay = f.Int(), CourtDay = f.Int() };
            int stage = f.Int();
            int outcome = f.Int();
            c.Stage = stage >= 0 && stage <= 3 ? (CaseStage)stage : CaseStage.Closed;
            c.Outcome = outcome >= 0 && outcome <= 6 ? (CaseOutcome)outcome : CaseOutcome.None;
            c.Witnesses = f.Int();
            int flags = f.Int();
            c.Cameras = (flags & 1) != 0;
            c.InTheAct = (flags & 2) != 0;
            c.Fled = (flags & 4) != 0;
            c.Complied = (flags & 8) != 0;
            c.Bond = (flags & 16) != 0;
            c.Remand = (flags & 32) != 0;
            c.DealReady = (flags & 64) != 0;
            c.MotionTried = (flags & 128) != 0;
            c.MetInPerson = (flags & 256) != 0;
            c.Reviewed = (flags & 512) != 0;
            c.Appealed = (flags & 1024) != 0;
            c.PaidAtScene = f.Int();
            c.HeldHours = f.Int();
            c.BailPaid = f.Int();
            c.Seed = f.Int();
            c.Reminded = f.Int();
            c.WarrantDay = f.Int();
            c.ClosedDay = f.Int();
            c.FineImposed = f.Int();
            c.PrisonImposed = f.Int();
            c.JailImposed = f.Int();
            c.CommunityImposed = f.Int();
            c.ProbationImposed = f.Int();
            c.SuspensionImposed = f.Int();
            c.LawyerQuality = f.Int() / 1000f;
            c.Postponed = f.Int();
            string charges = f.Raw();
            if (charges != null)
            {
                foreach (string one in charges.Split('/'))
                {
                    string[] parts = one.Split('~');
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    string id = Unescape(parts[0]);
                    if (JusticeLaw.Find(id) == null
                        || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int evidence)
                        || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bits))
                    {
                        continue;
                    }
                    c.Charges.Add(new JusticeCharge
                    {
                        Id = id, Evidence = JusticeLaw.Clamp(evidence / 1000f, 0f, 1f), Dropped = (bits & 1) != 0, Convicted = (bits & 2) != 0, Reduced = (bits & 4) != 0
                    });
                }
            }
            if (c.Number <= 0)
            {
                return null;
            }
            if (c.Open && c.Standing == 0)
            {
                c.Stage = CaseStage.Closed;   // an open case with no charge this build knows: nothing to answer
                c.Outcome = CaseOutcome.Dismissed;
            }
            return c;
        }

        // Reads comma-separated fields in order; past the end every number is 0 and every text empty, so a line
        // written by an older build (fewer fields) still reads.
        private struct Fields
        {
            private readonly string[] _parts;
            private int _next;

            public Fields(string text)
            {
                _parts = text.Split(',');
                _next = 0;
            }

            public int Signed()
            {
                return _next < _parts.Length && int.TryParse(_parts[_next++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
            }

            public int Int()
            {
                return Math.Max(0, Signed());
            }

            public string Text()
            {
                return _next < _parts.Length ? Unescape(_parts[_next++]) : string.Empty;
            }

            public string Raw()
            {
                return _next < _parts.Length ? _parts[_next++] : null;
            }
        }

        private static int Mille(float value)
        {
            return (int)Math.Round(JusticeLaw.Clamp(value, 0f, 1f) * 1000f);
        }

        private static void Join(StringBuilder text, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        // Names come from the game's data and other mods: anything that would break the line is written as %XX.
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            StringBuilder escaped = null;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool plain = ch < 128 && (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '.' || ch == ':' || ch == '&' || ch == '\'');
                if (!plain && escaped == null)
                {
                    escaped = new StringBuilder(value.Length + 8).Append(value, 0, i);
                }
                if (escaped != null)
                {
                    if (plain)
                    {
                        escaped.Append(ch);
                    }
                    else
                    {
                        escaped.Append('%').Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                    }
                }
            }
            return escaped != null ? escaped.ToString() : value;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
            {
                return value ?? string.Empty;
            }
            var plain = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '%' && i + 4 < value.Length
                    && int.TryParse(value.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                {
                    plain.Append((char)code);
                    i += 4;
                }
                else
                {
                    plain.Append(value[i]);
                }
            }
            return plain.ToString();
        }
    }
}
