using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD's small sums, game-free (checked offline, EconomyCheck): today's money so far, a group's count at its band's
    /// right, a goal's progress at its line's right and the rota shift a stay in hospital went over.
    /// </summary>
    public static class HudNoteRules
    {
        /// <summary>Today's money so far: the money now less the game's balance at the last midnight; false while it has none.</summary>
        public static bool Today(double money, IList<float> midnights, out double net)
        {
            net = 0d;
            if (midnights == null || midnights.Count == 0)
            {
                return false;
            }
            net = money - midnights[midnights.Count - 1];
            return true;
        }

        /// <summary>Which way today's figure points: +1 a gain, -1 a loss, 0 for none (under half a cent either way).</summary>
        public static int Trend(double net) => net >= 0.005d ? 1 : net <= -0.005d ? -1 : 0;

        /// <summary>
        /// A group's count at its band's right: how many lines are open ("3") while none is ticked, "1 of 3 done" once one is
        /// (<paramref name="doneWords"/> with {done} and {count}).
        /// </summary>
        public static string GroupNote(int done, int count, string doneWords)
        {
            if (count <= 0)
            {
                return null;
            }
            if (done <= 0 || string.IsNullOrEmpty(doneWords))
            {
                return (count - System.Math.Max(0, done)).ToString(CultureInfo.InvariantCulture);
            }
            return doneWords.Replace("{done}", done.ToString(CultureInfo.InvariantCulture)).Replace("{count}", count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A band's count of all its goals: "12 of 96 done" (<paramref name="doneWords"/> with {done} and {count}); null for none.</summary>
        public static string DoneNote(int done, int count, string doneWords)
        {
            if (count <= 0 || string.IsNullOrEmpty(doneWords))
            {
                return null;
            }
            return doneWords.Replace("{done}", System.Math.Max(0, done).ToString(CultureInfo.InvariantCulture)).Replace("{count}", count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// A goal's progress at its line's right, from the game's own figures: a money figure alone ("$31,400"), as the goal's
        /// title names its target; any other "1 of 2" (<paramref name="ofWords"/> with {current} and {amount}); null for none.
        /// </summary>
        public static string Progress(string current, string amount, string currency, string ofWords)
        {
            if (string.IsNullOrEmpty(current))
            {
                return null;
            }
            if (!string.IsNullOrEmpty(currency) && current.Contains(currency))
            {
                return current;
            }
            if (string.IsNullOrEmpty(amount) || string.IsNullOrEmpty(ofWords))
            {
                return current;
            }
            return ofWords.Replace("{current}", current).Replace("{amount}", amount);
        }

        /// <summary>
        /// The day of the rota shift a stay went over, 0 for none: a rota day's shift (<paramref name="rotaHour"/> for
        /// <paramref name="shiftHours"/>) that starts before the discharge and ends after the collapse. Day 1 is a Monday.
        /// </summary>
        public static int MissedShift(int rotaDays, int rotaHour, int shiftHours, int fromDay, int fromMinute, int toDay, int toMinute)
        {
            if (rotaDays == 0 || shiftHours <= 0 || toDay < fromDay)
            {
                return 0;
            }
            long from = fromDay * 1440L + fromMinute, to = toDay * 1440L + toMinute;
            for (int day = fromDay; day <= toDay; day++)
            {
                if (!RealLifeOverhaul.CareerCatalogue.RotaDay(rotaDays, day))
                {
                    continue;
                }
                long start = day * 1440L + rotaHour * 60L, end = start + shiftHours * 60L;
                if (start < to && end > from)
                {
                    return day;
                }
            }
            return 0;
        }
    }
}
