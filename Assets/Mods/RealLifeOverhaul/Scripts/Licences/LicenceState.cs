using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    // Game-free: nothing in this file touches the game or the engine, so the rules can be read (and checked) on their own.

    /// <summary>What a driving licence covers. Taxi is an endorsement on a car licence; the motorcycle licence stands on its own.</summary>
    public enum LicenceClass
    {
        Car = 0,          // cars, SUVs, pickups
        Commercial = 1,   // vans and trucks
        Taxi = 2,         // carrying passengers for hire
        Motorcycle = 3    // motorbikes (bicycles need no licence)
    }

    public enum LicenceStanding
    {
        None,        // never held
        Valid,
        Expired,     // run out, still renewable for a fee
        Lapsed,      // run out too long ago: the tests again
        Suspended    // twelve points
    }

    /// <summary>What goes on a driving record, with the points it carries.</summary>
    public enum PointCode
    {
        RedLight = 0,
        Speeding = 1,
        Reckless = 2,        // on the pavement
        WrongSide = 3,
        FleeingStop = 4,
        HitAndRun = 5,
        InjuredPerson = 6,
        KilledPerson = 7,
        EvadedCheck = 8,
        NoLicence = 9,
        DrunkDriving = 10
    }

    /// <summary>Every figure of the driving licence rules: fees, validity, points, penalties.</summary>
    public static class LicenceLaw
    {
        public static int TheoryFee => Prices.At(60);
        public const int TheoryQuestions = 10;
        public const int TheoryPass = 8;
        public const int TheoryValidDays = 30;       // a pass counts towards a practical test for this long
        public static int LessonFee => Prices.At(90);
        public static int CarTestFee => Prices.At(220);
        public static int CommercialTestFee => Prices.At(380);
        public static int MotorcycleFee => Prices.At(180);        // the ride assessment, recorded at the desk (the school has no motorbike to lend)
        public static int RetestFee => Prices.At(120);            // after a failed practical test (either class)
        public static int TaxiFee => Prices.At(180);
        public const int TaxiHeldDays = 7;           // a car licence held this long...
        public const int TaxiMostPoints = 5;         // ...with no more points than this
        public static int RenewalFee => Prices.At(90);
        public const int ValidDays = 120;            // an in-game year or so
        public const int RenewFromDays = 14;         // renewable from this many days before it runs out
        public const int LapsedAfterDays = 30;       // expired longer than this: theory and practical again
        public const int RetestWaitDays = 1;         // after a failed test, not before the next day

        public const int PointsExpireDays = 21;      // three in-game weeks
        public const int PointsLimit = 12;
        public const int SuspensionDays = 7;

        public static int NoLicenceFine => Prices.At(800);
        public static int WrongClassFine => Prices.At(500);
        public static int ExpiredFine => Prices.At(250);          // expired but renewable: a fine and a warning, the car stays
        public static int SuspendedFine => Prices.At(1500);
        public static int EvadedCheckFine => Prices.At(400);      // drove on from a roadside check: added at the next stop
        public static int ImpoundFee => Law.LawBook.ImpoundFee;   // the city pound's own (Law)
        public const int ArrestFromCount = 3;        // the third time caught within the window is an arrest
        public const int RepeatWindowDays = 30;
        public const int ArrestHours = 6;

        public const int MinorsAllowed = 6;          // a practical test passes with no serious fault and at most this many minor ones

        public static int Points(PointCode code)
        {
            switch (code)
            {
                case PointCode.RedLight: return 3;
                case PointCode.Speeding: return 3;
                case PointCode.Reckless: return 4;
                case PointCode.WrongSide: return 4;
                case PointCode.FleeingStop: return 6;
                case PointCode.HitAndRun: return 8;
                case PointCode.InjuredPerson: return 4;
                case PointCode.KilledPerson: return 9;
                case PointCode.EvadedCheck: return 3;
                case PointCode.NoLicence: return 6;
                case PointCode.DrunkDriving: return 6;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// The player's driving licences and record for this save: one line in the save's modData
    /// ("reallife:licences"), written and read here and nowhere else. A save without the line is a
    /// save from before licences; a save with it loads without the mod, which simply ignores it.
    /// </summary>
    public static class LicenceState
    {
        public const string Key = "reallife:licences";

        public struct Card
        {
            public int Issued;     // day; 0 = never held
            public int Expires;
            public bool Held => Issued > 0;
        }

        public struct Entry
        {
            public int Day;
            public int Points;
            public PointCode Code;
        }

        private static readonly Card[] Cards = new Card[4];
        private static readonly List<Entry> Record = new List<Entry>(16);

        public static bool Loaded { get; private set; }

        /// <summary>The day the licence rules first ran on this save; 0 until then.</summary>
        public static int FirstDay;

        /// <summary>
        /// Whether it has been looked at once if the save already owns a motorbike (which then gets the motorcycle licence, as a car
        /// owner got the car licence). False for a save from before the class; the answer is kept, so it is asked only once.
        /// </summary>
        public static bool MotorcycleDecided;
        public static int SuspendedUntil;       // day on which a suspension ends; 0 = not suspended
        public static int Caught;                // times caught driving without entitlement...
        public static int LastCaughtDay;         // ...the last of them on this day
        public static int EvadedChecks;          // roadside checks driven on from, not yet answered for
        public static int LastCheckDay;          // the last roadside check
        public static readonly int[] TheoryPassedDay = new int[2];      // by class (car, commercial)
        public static readonly int[] RetestDay = new int[2];            // the first day a failed practical may be sat again
        public static readonly bool[] RetestDue = new bool[2];          // the last practical was failed: the retest fee applies
        public static int TheoryRetryDay;
        public static readonly int[] Lessons = new int[2];
        public static int TestsPassed;
        public static int TestsFailed;

        public static IReadOnlyList<Entry> Entries => Record;

        public static void Reset()
        {
            Loaded = false;
            for (int i = 0; i < Cards.Length; i++)
            {
                Cards[i] = default;
            }
            Record.Clear();
            FirstDay = 0;
            MotorcycleDecided = false;
            SuspendedUntil = 0;
            Caught = 0;
            LastCaughtDay = 0;
            EvadedChecks = 0;
            LastCheckDay = 0;
            TheoryRetryDay = 0;
            TestsPassed = 0;
            TestsFailed = 0;
            for (int i = 0; i < 2; i++)
            {
                TheoryPassedDay[i] = 0;
                RetestDay[i] = 0;
                RetestDue[i] = false;
                Lessons[i] = 0;
            }
        }

        public static Card Of(LicenceClass kind) => Cards[(int)kind];

        /// <summary>Where a licence stands on a day. The taxi endorsement stands or falls with the car licence under it.</summary>
        public static LicenceStanding Standing(LicenceClass kind, int day)
        {
            Card card = Cards[(int)kind];
            if (!card.Held)
            {
                return LicenceStanding.None;
            }
            if (kind == LicenceClass.Taxi)
            {
                LicenceStanding car = Standing(LicenceClass.Car, day);
                if (car != LicenceStanding.Valid)
                {
                    return car == LicenceStanding.None ? LicenceStanding.Lapsed : car;
                }
            }
            if (SuspendedUntil > day)
            {
                return LicenceStanding.Suspended;
            }
            if (day <= card.Expires)
            {
                return LicenceStanding.Valid;
            }
            return day - card.Expires > LicenceLaw.LapsedAfterDays ? LicenceStanding.Lapsed : LicenceStanding.Expired;
        }

        public static bool Valid(LicenceClass kind, int day) => Standing(kind, day) == LicenceStanding.Valid;

        public static void Grant(LicenceClass kind, int day)
        {
            Cards[(int)kind] = new Card { Issued = day > 0 ? day : 1, Expires = (day > 0 ? day : 1) + LicenceLaw.ValidDays };
        }

        public static void Revoke(LicenceClass kind)
        {
            Cards[(int)kind] = default;
            if (kind == LicenceClass.Car)
            {
                Cards[(int)LicenceClass.Taxi] = default;
            }
        }

        /// <summary>A renewal runs from the old expiry if that has not passed, else from today.</summary>
        public static void Renew(LicenceClass kind, int day)
        {
            Card card = Cards[(int)kind];
            if (card.Held)
            {
                card.Expires = Math.Max(card.Expires, day) + LicenceLaw.ValidDays;
                Cards[(int)kind] = card;
            }
        }

        public static bool Renewable(LicenceClass kind, int day)
        {
            Card card = Cards[(int)kind];
            return card.Held && day >= card.Expires - LicenceLaw.RenewFromDays && day - card.Expires <= LicenceLaw.LapsedAfterDays;
        }

        /// <summary>The points that still count on a day.</summary>
        public static int PointsOn(int day)
        {
            int total = 0;
            for (int i = 0; i < Record.Count; i++)
            {
                if (day - Record[i].Day < LicenceLaw.PointsExpireDays)
                {
                    total += Record[i].Points;
                }
            }
            return total;
        }

        /// <summary>Adds an entry. True if this took the record to the limit and a suspension started.</summary>
        public static bool AddPoints(PointCode code, int day)
        {
            int points = LicenceLaw.Points(code);
            if (points <= 0)
            {
                return false;
            }
            if (Record.Count >= 40)
            {
                Record.RemoveAt(0);
            }
            Record.Add(new Entry { Day = day, Points = points, Code = code });
            if (PointsOn(day) >= LicenceLaw.PointsLimit && SuspendedUntil <= day)
            {
                SuspendedUntil = day + LicenceLaw.SuspensionDays;
                return true;
            }
            return false;
        }

        /// <summary>A new day: entries that no longer count go, and a suspension that has run out ends with a clean slate. True if a suspension ended.</summary>
        public static bool NewDay(int day)
        {
            for (int i = Record.Count - 1; i >= 0; i--)
            {
                if (day - Record[i].Day >= LicenceLaw.PointsExpireDays)
                {
                    Record.RemoveAt(i);
                }
            }
            if (SuspendedUntil > 0 && day >= SuspendedUntil)
            {
                SuspendedUntil = 0;
                Record.Clear();   // served: the record starts again
                return true;
            }
            return false;
        }

        /// <summary>Counts being caught driving without entitlement. Returns how many times within the window, this one included.</summary>
        public static int CountCaught(int day)
        {
            Caught = day - LastCaughtDay > LicenceLaw.RepeatWindowDays ? 1 : Caught + 1;
            LastCaughtDay = day;
            return Caught;
        }

        // ---------------------------------------------------------------- the save

        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            Loaded = true;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string name = part.Substring(0, eq);
                string[] v = part.Substring(eq + 1).Split(',');
                switch (name)
                {
                    case "car": Cards[0] = new Card { Issued = Int(v, 0), Expires = Int(v, 1) }; break;
                    case "com": Cards[1] = new Card { Issued = Int(v, 0), Expires = Int(v, 1) }; break;
                    case "taxi": Cards[2] = new Card { Issued = Int(v, 0), Expires = Int(v, 1) }; break;
                    case "moto": Cards[3] = new Card { Issued = Int(v, 0), Expires = Int(v, 1) }; MotorcycleDecided = Int(v, 2) != 0; break;
                    case "first": FirstDay = Int(v, 0); break;
                    case "susp": SuspendedUntil = Int(v, 0); break;
                    case "caught": Caught = Int(v, 0); LastCaughtDay = Int(v, 1); break;
                    case "check": EvadedChecks = Int(v, 0); LastCheckDay = Int(v, 1); break;
                    case "theory": TheoryPassedDay[0] = Int(v, 0); TheoryPassedDay[1] = Int(v, 1); TheoryRetryDay = Int(v, 2); break;
                    case "retest": RetestDay[0] = Int(v, 0); RetestDay[1] = Int(v, 1); RetestDue[0] = Int(v, 2) != 0; RetestDue[1] = Int(v, 3) != 0; break;
                    case "lessons": Lessons[0] = Int(v, 0); Lessons[1] = Int(v, 1); break;
                    case "tests": TestsPassed = Int(v, 0); TestsFailed = Int(v, 1); break;
                    case "pts":
                        for (int i = 0; i < v.Length; i++)
                        {
                            string[] e = v[i].Split(':');
                            int code = Int(e, 2);
                            if (e.Length == 3 && Int(e, 1) > 0 && code >= 0 && code <= (int)PointCode.DrunkDriving)
                            {
                                Record.Add(new Entry { Day = Int(e, 0), Points = Int(e, 1), Code = (PointCode)code });
                            }
                        }
                        break;
                }
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (data == null || !Loaded)
            {
                return;
            }
            var text = new StringBuilder(160);
            text.Append("v=1");
            Pair(text, "car", Cards[0].Issued, Cards[0].Expires);
            Pair(text, "com", Cards[1].Issued, Cards[1].Expires);
            Pair(text, "taxi", Cards[2].Issued, Cards[2].Expires);
            Pair(text, "moto", Cards[3].Issued, Cards[3].Expires, MotorcycleDecided ? 1 : 0);
            Pair(text, "first", FirstDay);
            Pair(text, "susp", SuspendedUntil);
            Pair(text, "caught", Caught, LastCaughtDay);
            Pair(text, "check", EvadedChecks, LastCheckDay);
            Pair(text, "theory", TheoryPassedDay[0], TheoryPassedDay[1], TheoryRetryDay);
            Pair(text, "retest", RetestDay[0], RetestDay[1], RetestDue[0] ? 1 : 0, RetestDue[1] ? 1 : 0);
            Pair(text, "lessons", Lessons[0], Lessons[1]);
            Pair(text, "tests", TestsPassed, TestsFailed);
            text.Append(";pts=");
            for (int i = 0; i < Record.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(Record[i].Day.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(Record[i].Points.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(((int)Record[i].Code).ToString(CultureInfo.InvariantCulture));
            }
            data[Key] = text.ToString();
        }

        internal static void Pair(StringBuilder text, string name, params int[] values)
        {
            text.Append(';').Append(name).Append('=');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        internal static int Int(string[] values, int index)
        {
            return index < values.Length && int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
