using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The heavy goods licence's rules: which vehicles need it, what the school asks before each step, its figures, the heavy
    /// loads it opens on the courier round, and its part of the licence line in the save. Game-free: EconomyCheck runs it.
    /// No cost a frame: everything here is asked on an event (a vehicle got into, the desk's list, a stop, a round taken).
    /// </summary>
    public static class HeavyRules
    {
        private const string Prefix = "ba:vehicletype_";

        /// <summary>The game's own heaviest vehicle, and the one the school lends for the heavy test.</summary>
        public const string FreightTruck = "freighttruckt1";

        /// <summary>
        /// The game's own speed tier of its heaviest trucks (the Freight Truck's 50; the Vord Courier box truck is 55). Another
        /// mod's truck at or under it is taken to be as heavy, since the game's data has no weight to go on.
        /// </summary>
        public const int HeaviestTierKmh = 50;

        public static int TheoryFee => Prices.At(90);
        public static int LessonFee => Prices.At(150);
        public static int TestFee => Prices.At(560);
        public static int RetestFee => Prices.At(200);          // after a failed driven test
        public const int TheoryQuestions = 10;
        public const int TheoryPass = 8;
        public const int TheoryValidDays = 30;                 // as the car's paper: a pass counts this long towards the driven test
        public const int WaitDays = 2;                         // days from the heavy paper's pass, and from a failed drive, to the driven test
        public const int PaperRetryDays = 1;                   // a failed paper may be sat again from the next day, as the car's

        /// <summary>Whether a vehicle type needs the heavy class rather than the commercial one. <paramref name="truck"/>: the mod's kind for it is a truck.</summary>
        public static bool IsHeavy(string typeName, bool truck, int gameTopKmh)
        {
            string name = typeName ?? string.Empty;
            if (name.StartsWith(Prefix, System.StringComparison.Ordinal))
            {
                name = name.Substring(Prefix.Length);
            }
            if (name == FreightTruck)
            {
                return true;
            }
            if (name == "deliverytruck" || !truck)
            {
                return false;   // the box truck stays on the commercial licence
            }
            return gameTopKmh > 0 && gameTopKmh <= HeaviestTierKmh;
        }

        // Heavy loads on the courier round: freight on pallets, in a heavy truck, for a holder of the licence.
        public const float LoadPay = 1.8f;                    // a drop and the distance pay this many times a parcel's
        public const float LoadTime = 1.35f;                  // the round's clock is this much longer: a truck is slower to park and to unload
        public static int TruckHireFee => Prices.At(260);     // a day, as the van's 125: a truck with a heavy load's fuel

        /// <summary>Whether a round is a heavy load: driven in a heavy truck by somebody the licence rules let drive one.</summary>
        public static bool IsLoad(bool heavyTruck, bool licensed) => heavyTruck && licensed;

        /// <summary>What a heavy load pays for something a parcel round pays <paramref name="parcelPay"/> for.</summary>
        public static int LoadPayFor(int parcelPay) => parcelPay <= 0 ? 0 : (int)System.Math.Round(parcelPay * (double)LoadPay, System.MidpointRounding.AwayFromZero);

        /// <summary>The fee due today for a truck when <paramref name="paidToday"/> has already been paid for the day's hire (a van's, or nothing).</summary>
        public static int TruckFeeDue(int paidToday, int truckFee) => System.Math.Max(0, truckFee - System.Math.Max(0, paidToday));

        /// <summary>The steps of the heavy licence at the school.</summary>
        public enum Step { Theory, Lesson, Test }

        /// <summary>
        /// Why a step cannot be had, as a locale key, or null if it can. The desk's own conditions (hours, suspension, the fee)
        /// are asked before this. <paramref name="heavyHeld"/>: valid or still renewable; <paramref name="heavyValid"/>: valid.
        /// </summary>
        public static string Refusal(Step step, bool commercialValid, bool heavyHeld, bool heavyValid, in Record record, int day)
        {
            bool theoryInDate = record.TheoryDay > 0 && day - record.TheoryDay <= TheoryValidDays;
            switch (step)
            {
                case Step.Theory:
                    return heavyHeld ? "reallife_school_no_have"
                        : !commercialValid ? "reallife_school_no_need_commercial"
                        : theoryInDate ? "reallife_school_no_theory_passed"
                        : day < record.PaperRetryDay ? "reallife_school_no_wait"
                        : null;
                case Step.Lesson:
                    return heavyValid ? null
                        : heavyHeld ? "reallife_school_no_have"
                        : !commercialValid ? "reallife_school_no_need_commercial"
                        : !theoryInDate ? "reallife_school_no_need_heavy_theory"
                        : null;
                default:
                    return heavyHeld ? "reallife_school_no_have"
                        : !commercialValid ? "reallife_school_no_need_commercial"
                        : !theoryInDate ? "reallife_school_no_need_heavy_theory"
                        : record.Lessons < 1 ? "reallife_school_no_need_lesson"
                        : day < record.TheoryDay + WaitDays ? "reallife_school_no_heavy_wait"
                        : day < record.RetestDay ? "reallife_school_no_heavy_retest"
                        : null;
            }
        }

        /// <summary>The fee of a step, the retest's after a failed drive.</summary>
        public static int Fee(Step step, in Record record)
        {
            return step == Step.Theory ? TheoryFee : step == Step.Lesson ? LessonFee : record.RetestDue ? RetestFee : TestFee;
        }

        /// <summary>The heavy class's progress at the school, saved as "hgv=" in the licence line.</summary>
        public struct Record
        {
            public int TheoryDay;        // the heavy paper passed on this day; 0 = not
            public int PaperRetryDay;    // a failed paper may be sat again from this day
            public int Lessons;
            public int RetestDay;        // a failed drive may be sat again from this day
            public bool RetestDue;       // the last drive was failed: the retest fee applies
            public bool Decided;         // the save has been looked at once for a heavy truck of the player's own

            public void Passed()
            {
                RetestDue = false;
                RetestDay = 0;
            }

            public void Failed(int day)
            {
                RetestDue = true;
                RetestDay = day + WaitDays;
            }

            public void PaperMarked(bool passed, int day)
            {
                if (passed)
                {
                    TheoryDay = day;
                    PaperRetryDay = 0;
                }
                else
                {
                    PaperRetryDay = day + PaperRetryDays;
                }
            }
        }

        /// <summary>The record's part of the licence line, after its own name: "hgv=theory,retry,lessons,retest,due,decided".</summary>
        public static void Write(StringBuilder text, in Record record)
        {
            text.Append(";hgv=")
                .Append(record.TheoryDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.PaperRetryDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.Lessons.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.RetestDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.RetestDue ? '1' : '0').Append(',')
                .Append(record.Decided ? '1' : '0');
        }

        /// <summary>Reads what follows "hgv=". Missing or broken values read as 0, so a shorter line from a later trim still loads.</summary>
        public static Record Read(string[] values)
        {
            return new Record
            {
                TheoryDay = Int(values, 0),
                PaperRetryDay = Int(values, 1),
                Lessons = Int(values, 2),
                RetestDay = Int(values, 3),
                RetestDue = Int(values, 4) != 0,
                Decided = Int(values, 5) != 0
            };
        }

        private static int Int(string[] values, int index)
        {
            return values != null && index < values.Length && int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        /// <summary>The heavy paper's questions, in four subjects: size and height, weight and loads, braking, turning and mirrors.</summary>
        public static readonly string[] QuestionKeys =
        {
            "reallife_lic_qh_01", "reallife_lic_qh_02", "reallife_lic_qh_03", "reallife_lic_qh_04", "reallife_lic_qh_05", "reallife_lic_qh_06",
            "reallife_lic_qh_07", "reallife_lic_qh_08", "reallife_lic_qh_09", "reallife_lic_qh_10", "reallife_lic_qh_11", "reallife_lic_qh_12",
            "reallife_lic_qh_13", "reallife_lic_qh_14", "reallife_lic_qh_15", "reallife_lic_qh_16", "reallife_lic_qh_17", "reallife_lic_qh_18",
            "reallife_lic_qh_19", "reallife_lic_qh_20", "reallife_lic_qh_21", "reallife_lic_qh_22", "reallife_lic_qh_23", "reallife_lic_qh_24"
        };

        /// <summary>Where each subject starts in <see cref="QuestionKeys"/>.</summary>
        public static readonly int[] QuestionGroups = { 0, 6, 12, 18 };
    }
}
