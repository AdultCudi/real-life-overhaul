using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>How a spell with an employer ended, as the next one will read it.</summary>
    public enum CareerReference
    {
        None = 0,        // left before there was anything to say
        Good = 1,        // worked the notice, in good standing
        MarkedDown = 2   // let go, or walked out
    }

    /// <summary>What a performance review came to.</summary>
    public enum ReviewOutcome
    {
        Promoted,
        Raise,
        Steady,
        Warning,
        Dismissed
    }

    /// <summary>
    /// What a savegame remembers of the player's career: the employer, rank and rota, the marks
    /// against them, what has happened since the last review, and the employment history. One
    /// line of text in the save's modData (which the base game carries along without reading),
    /// so a save made with a career loads without the mod and one from before it starts with none.
    /// A shift in progress is not saved. Game-free: whoever calls it says what day it is.
    /// </summary>
    public static class CareerState
    {
        private const string Key = "reallife:career";
        private const string Version = "1";

        public struct Spell
        {
            public int Employer;      // index in CareerCatalogue.All
            public int From;
            public int To;
            public int Rank;          // the rank reached
            public int Shifts;
            public CareerReference Reference;
        }

        public static bool Loaded { get; private set; }

        public static int EmployerIndex { get; private set; } = -1;
        public static int Rank { get; private set; }
        public static int RotaDays { get; private set; }
        public static int RotaHour { get; private set; }
        public static int HiredDay { get; private set; }
        public static int Marks { get; private set; }
        public static int Raises { get; private set; }
        public static int ShiftsSinceReview { get; private set; }
        public static bool ReviewDue { get; private set; }
        public static int NoticeDay { get; private set; }         // the day notice was given; 0 when none
        public static int NoticeShiftsLeft { get; private set; }
        public static int HandledDay { get; private set; }        // the last rota day already dealt with: worked, missed or excused
        public static int RemindedDay { get; set; }
        public static int SpellShifts { get; private set; }       // rota shifts worked for this employer

        // Since the last review: what the boss reads out.
        public static int OnTime { get; private set; }
        public static int Late { get; private set; }
        public static int NoShow { get; private set; }
        public static int Complaints { get; private set; }
        public static int Crashes { get; private set; }
        public static int Stops { get; private set; }
        public static int Tips { get; private set; }
        public static int Shifts { get; private set; }

        public static int TotalShifts { get; private set; }
        public static int TotalEarned { get; private set; }
        public static int CartPaidDay { get; set; }
        public static int GigDay { get; set; }
        /// <summary>Hours worked at the game's own employer job (the cashier), counted read-only: for the CV and as retail experience.</summary>
        public static int CashierHours { get; set; }

        private static readonly int[] Banned = new int[CareerCatalogue.All.Length];
        private static readonly List<Spell> Past = new List<Spell>(CareerCatalogue.HistoryMost);

        public static bool Employed => EmployerIndex >= 0;
        public static Employer Employer => EmployerIndex >= 0 ? CareerCatalogue.All[EmployerIndex] : null;
        public static CareerRank CurrentRank => EmployerIndex >= 0 ? CareerCatalogue.All[EmployerIndex].Ranks[Rank] : null;
        public static bool OnNotice => NoticeDay > 0;
        public static IReadOnlyList<Spell> History => Past;

        /// <summary>The basic hourly wage right now: the rank's, plus the raises earned at it.</summary>
        public static float Hourly => Employed ? CurrentRank.Hourly + (CurrentRank.Hourly > 0f ? Raises * CareerCatalogue.RaiseStep : 0f) : 0f;

        /// <summary>References that count for the next employer: good ones less marked-down ones, never below zero.</summary>
        public static int ReferenceScore(bool gigLetter)
        {
            int score = gigLetter ? 1 : 0;
            for (int i = 0; i < Past.Count; i++)
            {
                score += Past[i].Reference == CareerReference.Good ? 1 : Past[i].Reference == CareerReference.MarkedDown ? -1 : 0;
            }
            return score < 0 ? 0 : score;
        }

        public static bool BannedAt(int employer, int day)
        {
            return employer >= 0 && employer < Banned.Length && Banned[employer] > day;
        }

        public static int BannedUntil(int employer)
        {
            return employer >= 0 && employer < Banned.Length ? Banned[employer] : 0;
        }

        /// <summary>True if today is a rota day that has not been worked, missed or excused yet.</summary>
        public static bool ShiftOpen(int day)
        {
            return Employed && CareerCatalogue.RotaDay(RotaDays, day) && HandledDay < day && day > HiredDay;
        }

        /// <summary>The next day with a shift still to come, from a day on; -1 if none.</summary>
        public static int NextShiftDay(int day)
        {
            if (!Employed)
            {
                return -1;
            }
            int from = day <= HandledDay ? HandledDay + 1 : day;
            if (from <= HiredDay)
            {
                from = HiredDay + 1;
            }
            return CareerCatalogue.NextRotaDay(RotaDays, from);
        }

        public static void Reset()
        {
            Loaded = false;
            Clear();
        }

        private static void Clear()
        {
            ClearSpell();
            TotalShifts = TotalEarned = CartPaidDay = GigDay = CashierHours = 0;
            for (int i = 0; i < Banned.Length; i++)
            {
                Banned[i] = 0;
            }
            Past.Clear();
        }

        private static void ClearSpell()
        {
            EmployerIndex = -1;
            Rank = RotaDays = RotaHour = HiredDay = Marks = Raises = ShiftsSinceReview = NoticeDay = NoticeShiftsLeft = 0;
            HandledDay = RemindedDay = SpellShifts = 0;
            ReviewDue = false;
            ClearReview();
        }

        private static void ClearReview()
        {
            OnTime = Late = NoShow = Complaints = Crashes = Stops = Tips = Shifts = 0;
        }

        // ---------------------------------------------------------------- what happens

        /// <summary>Taken on. <paramref name="credit"/> is experience brought along, in shifts towards the first review.</summary>
        public static void SignOn(int employer, CareerRota rota, int day, int credit)
        {
            if (Employed)
            {
                Leave(day, CareerReference.None);
            }
            ClearSpell();
            EmployerIndex = employer;
            RotaDays = rota.Days;
            RotaHour = rota.Hour;
            HiredDay = day;
            HandledDay = day;   // the first shift is the next rota day, never the day of signing
            ShiftsSinceReview = credit < 0 ? 0 : credit;
        }

        /// <summary>A rota shift was clocked on to. A late one is a mark. Returns true if that mark is the last straw.</summary>
        public static bool ClockOn(int day, bool late)
        {
            HandledDay = day;
            if (!late)
            {
                OnTime++;
                return false;
            }
            Late++;
            Marks++;
            return Marks >= CareerCatalogue.MarksToDismiss;
        }

        /// <summary>The shift clocked on to is over.</summary>
        public static void ShiftDone(bool failed, int tips, int earned)
        {
            Shifts++;
            SpellShifts++;
            TotalShifts++;
            ShiftsSinceReview++;
            Tips += tips > 0 ? tips : 0;
            TotalEarned += earned > 0 ? earned : 0;
            if (failed)
            {
                Complaints++;
            }
            if (NoticeDay > 0 && NoticeShiftsLeft > 0)
            {
                NoticeShiftsLeft--;
            }
            if (ShiftsSinceReview >= CareerCatalogue.ShiftsPerReview)
            {
                ReviewDue = true;
            }
        }

        public static void Crash()
        {
            Crashes++;
        }

        /// <summary>A complaint that is not a failed shift: the boss saw the player posting on social media on shift (Wiring/FlareLinks).</summary>
        public static void Complaint()
        {
            Complaints++;
        }

        public static void PoliceStop()
        {
            Stops++;
        }

        /// <summary>A rota day went by with nobody turning up. Returns true if that is the last straw.</summary>
        public static bool Missed(int day)
        {
            HandledDay = day;
            NoShow++;
            Marks++;
            return Marks >= CareerCatalogue.MarksToDismiss;
        }

        /// <summary>A rota day excused (a sick note): no mark, no pay.</summary>
        /// <summary>Careers were switched off for a while: rota days up to a day are let go by, with nothing held against anybody.</summary>
        public static void SkipTo(int day)
        {
            if (Employed && HandledDay < day)
            {
                HandledDay = day;
            }
        }

        public static void Excused(int day)
        {
            HandledDay = day;
        }

        public static float Score()
        {
            return CareerCatalogue.ReviewScore(OnTime, Late, NoShow, Complaints, Crashes, Stops, Tips, Shifts);
        }

        /// <summary>
        /// The review is held: the score decides, the counters start again. <paramref name="mayPromote"/> is
        /// false when the next rank cannot be given (there is none, or the player is working their notice).
        /// </summary>
        public static ReviewOutcome Review(bool mayPromote, out float score)
        {
            score = Score();
            ReviewOutcome outcome;
            Employer employer = Employer;
            if (score < CareerCatalogue.WarnScore)
            {
                Marks++;
                outcome = Marks >= CareerCatalogue.MarksToDismiss ? ReviewOutcome.Dismissed : ReviewOutcome.Warning;
            }
            else if (score >= CareerCatalogue.PromoteScore && mayPromote && employer != null && Rank < employer.Ranks.Length - 1)
            {
                Rank++;
                Raises = 0;
                if (Marks > 0)
                {
                    Marks--;
                }
                outcome = ReviewOutcome.Promoted;
            }
            else if (score >= CareerCatalogue.RaiseScore && Raises < CareerCatalogue.MaxRaises && CurrentRank != null && CurrentRank.Hourly > 0f)
            {
                Raises++;
                if (Marks > 0)
                {
                    Marks--;
                }
                outcome = ReviewOutcome.Raise;
            }
            else
            {
                outcome = ReviewOutcome.Steady;
            }
            ShiftsSinceReview = 0;
            ReviewDue = false;
            ClearReview();
            return outcome;
        }

        /// <summary>Test button: up a rank at once. False at the top.</summary>
        public static bool Promote()
        {
            Employer employer = Employer;
            if (employer == null || Rank >= employer.Ranks.Length - 1)
            {
                return false;
            }
            Rank++;
            Raises = 0;
            return true;
        }

        /// <summary>Test button: the review is due now.</summary>
        public static void ForceReview()
        {
            if (Employed)
            {
                ReviewDue = true;
            }
        }

        public static void GiveNotice(int day)
        {
            if (Employed && NoticeDay == 0)
            {
                NoticeDay = day;
                NoticeShiftsLeft = CareerCatalogue.NoticeShifts;
            }
        }

        /// <summary>True once the notice has been worked: the shifts done, or the days gone by.</summary>
        public static bool NoticeServed(int day)
        {
            return NoticeDay > 0 && (NoticeShiftsLeft <= 0 || day - NoticeDay >= CareerCatalogue.NoticeDays);
        }

        /// <summary>The reference a spell that ends by working the notice earns.</summary>
        public static CareerReference EarnedReference()
        {
            return SpellShifts >= CareerCatalogue.ShiftsPerReview && Marks < 2 ? CareerReference.Good : CareerReference.None;
        }

        /// <summary>The spell ends and goes into the history. A marked-down reference also closes that employer's door for a while.</summary>
        public static void Leave(int day, CareerReference reference)
        {
            if (!Employed)
            {
                return;
            }
            if (Past.Count >= CareerCatalogue.HistoryMost)
            {
                Past.RemoveAt(0);
            }
            Past.Add(new Spell { Employer = EmployerIndex, From = HiredDay, To = day, Rank = Rank, Shifts = SpellShifts, Reference = reference });
            if (reference == CareerReference.MarkedDown)
            {
                Banned[EmployerIndex] = day + CareerCatalogue.BanDays;
            }
            ClearSpell();
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
                catch (System.Exception)
                {
                    Clear();   // a damaged line costs the career, never the load
                }
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            var text = new StringBuilder(160);
            text.Append(Version).Append('|');
            text.Append(EmployerIndex >= 0 ? CareerCatalogue.All[EmployerIndex].Id : "-").Append('|');
            Join(text, Rank, RotaDays, RotaHour, HiredDay, Marks, Raises, ShiftsSinceReview, ReviewDue ? 1 : 0, NoticeDay, NoticeShiftsLeft,
                HandledDay, RemindedDay, SpellShifts);
            text.Append('|');
            Join(text, OnTime, Late, NoShow, Complaints, Crashes, Stops, Tips, Shifts);
            text.Append('|');
            Join(text, TotalShifts, TotalEarned, CartPaidDay, GigDay, CashierHours);
            text.Append('|');
            for (int i = 0; i < Banned.Length; i++)
            {
                if (Banned[i] > 0)
                {
                    text.Append(CareerCatalogue.All[i].Id).Append('=').Append(Banned[i].ToString(CultureInfo.InvariantCulture)).Append(';');
                }
            }
            text.Append('|');
            for (int i = 0; i < Past.Count; i++)
            {
                Spell spell = Past[i];
                text.Append(CareerCatalogue.All[spell.Employer].Id).Append('=');
                Join(text, spell.From, spell.To, spell.Rank, spell.Shifts, (int)spell.Reference);
                text.Append(';');
            }
            data[Key] = text.ToString();
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

        private static void Parse(string text)
        {
            string[] parts = text.Split('|');
            if (parts.Length < 7 || parts[0] != Version)
            {
                return;
            }
            int[] spell = Numbers(parts[2], 13);
            int[] review = Numbers(parts[3], 8);
            int[] totals = Numbers(parts[4], 5);
            if (spell == null || review == null || totals == null)
            {
                return;
            }
            TotalShifts = totals[0];
            TotalEarned = totals[1];
            CartPaidDay = totals[2];
            GigDay = totals[3];
            CashierHours = totals[4];

            foreach (string entry in parts[5].Split(';'))
            {
                int equals = entry.IndexOf('=');
                int index = equals > 0 ? CareerCatalogue.IndexOf(entry.Substring(0, equals)) : -1;
                if (index >= 0 && int.TryParse(entry.Substring(equals + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int until) && until > 0)
                {
                    Banned[index] = until;
                }
            }
            foreach (string entry in parts[6].Split(';'))
            {
                int equals = entry.IndexOf('=');
                int index = equals > 0 ? CareerCatalogue.IndexOf(entry.Substring(0, equals)) : -1;   // an employer this build does not have is dropped
                int[] values = index >= 0 ? Numbers(entry.Substring(equals + 1), 5) : null;
                if (values == null || Past.Count >= CareerCatalogue.HistoryMost)
                {
                    continue;
                }
                Past.Add(new Spell
                {
                    Employer = index, From = values[0], To = values[1],
                    Rank = Clamp(values[2], 0, CareerCatalogue.All[index].Ranks.Length - 1), Shifts = values[3],
                    Reference = values[4] == 1 ? CareerReference.Good : values[4] == 2 ? CareerReference.MarkedDown : CareerReference.None
                });
            }

            int employer = CareerCatalogue.IndexOf(parts[1]);
            if (employer < 0 || spell[1] == 0)
            {
                return;   // not employed (or by somebody this build does not have): the history above still stands
            }
            EmployerIndex = employer;
            Rank = Clamp(spell[0], 0, CareerCatalogue.All[employer].Ranks.Length - 1);
            RotaDays = spell[1] & 127;
            RotaHour = Clamp(spell[2], 0, 23);
            HiredDay = spell[3];
            Marks = Clamp(spell[4], 0, CareerCatalogue.MarksToDismiss);   // at the limit: the dismissal that was pending is made on the next hour
            Raises = Clamp(spell[5], 0, CareerCatalogue.MaxRaises);
            ShiftsSinceReview = spell[6];
            ReviewDue = spell[7] != 0;
            NoticeDay = spell[8];
            NoticeShiftsLeft = spell[9];
            HandledDay = spell[10];
            RemindedDay = spell[11];
            SpellShifts = spell[12];
            OnTime = review[0];
            Late = review[1];
            NoShow = review[2];
            Complaints = review[3];
            Crashes = review[4];
            Stops = review[5];
            Tips = review[6];
            Shifts = review[7];
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }

        private static int[] Numbers(string text, int count)
        {
            string[] fields = text.Split(',');
            if (fields.Length < count)
            {
                return null;
            }
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (!int.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]) || values[i] < 0)
                {
                    return null;
                }
            }
            return values;
        }
    }
}
