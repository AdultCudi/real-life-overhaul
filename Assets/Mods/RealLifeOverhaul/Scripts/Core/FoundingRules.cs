using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the player's businesses as it stands now: its building's place key, its name, and the start day the game holds for it
    /// (BuildingRegistration.creationDay; 0 or less when it holds none).
    /// </summary>
    public struct FoundingSeen
    {
        public string Place;
        public string Name;
        public int Created;
    }

    /// <summary>What was kept of a business: the day it started, where it stood and under which name when last seen, and the day that was.</summary>
    public sealed class FoundingRecord
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public int Day;      // the day the business started (never 0 or less)
        public int Seen;     // the last day it was seen standing at Place under Name
        public bool Guess;   // the day is an estimate (the business was moved before the day was kept): the game's own, if it ever gives an earlier one, is taken instead
    }

    /// <summary>The start days kept for the player's running businesses (Core/FoundingStore writes them in the save).</summary>
    public sealed class FoundingFile
    {
        public readonly List<FoundingRecord> Records = new List<FoundingRecord>(16);

        public FoundingRecord At(string place, string name)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                if (string.Equals(Records[i].Place, place, StringComparison.Ordinal) && string.Equals(Records[i].Name, name, StringComparison.Ordinal))
                {
                    return Records[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The day a business of the player's started, kept across a move. The game writes a business's start day (creationDay) when it
    /// is started or taken over, and the moving company then moves the business to another building without it: the
    /// building it left is shut down (BusinessHelper.ShutdownBusiness sets its day to -1) and the one it came to keeps its own,
    /// -1 when it was rented empty. So a business moved looks new, or has no day at all, from then on. Here each business of the
    /// player's is kept by its building and name, seen again and again while its day is still the game's; when it turns up under
    /// the same name in another building and the record of the building it left is unclaimed, the record follows it
    /// (a record is never given to two businesses). A business seen for the first time with no day of the game's (moved before
    /// this was kept) takes the day <see cref="Sample"/> is handed an estimate for, or none. Game-free, checked by EconomyCheck.
    /// COST: a pass over the player's businesses against at most <see cref="RecordsMost"/> records, when asked (at most once a game hour).
    /// </summary>
    public static class FoundingRules
    {
        public const int RecordsMost = 64;   // records kept: a player runs far fewer businesses; the longest unseen go first
        public const int Grace = 5;          // game days a record whose business is not seen is kept for (a business is not seen while its building is still loading, say)

        /// <summary>
        /// Brings the records up to date with the businesses standing now, on <paramref name="day"/>. <paramref name="estimate"/> gives
        /// a start day for a business with none of the game's and no record (place, name; 0 for none); asked at most once for each.
        /// Returns how many records were added, followed or dropped.
        /// </summary>
        public static int Sample(FoundingFile file, IList<FoundingSeen> now, int day, Func<string, string, int> estimate)
        {
            if (file == null || now == null)
            {
                return 0;
            }
            List<FoundingRecord> was = file.Records;
            int changes = 0;
            var used = new bool[was.Count];
            var taken = new bool[now.Count];
            var next = new List<FoundingRecord>(was.Count + now.Count);
            // 1. a business where it was kept, under its name
            for (int s = 0; s < now.Count; s++)
            {
                FoundingSeen b = now[s];
                int r = IndexOf(was, used, b.Place, b.Name);
                if (r < 0)
                {
                    continue;
                }
                used[r] = true;
                taken[s] = true;
                FoundingRecord f = was[r];
                if (f.Guess && b.Created > 0 && b.Created < f.Day)
                {
                    f.Day = b.Created;   // the game knows an earlier day than the estimate
                    f.Guess = false;
                    changes++;
                }
                f.Seen = day;
                next.Add(f);
            }
            // 2. a business under a name kept for a building it is no longer in: it was moved, and the record goes with it
            for (int s = 0; s < now.Count; s++)
            {
                if (taken[s])
                {
                    continue;
                }
                FoundingSeen b = now[s];
                int r = IndexOfName(was, used, b.Name);
                if (r < 0)
                {
                    continue;
                }
                used[r] = true;
                taken[s] = true;
                FoundingRecord f = was[r];
                f.Place = b.Place;
                f.Seen = day;
                next.Add(f);
                changes++;
            }
            // 3. a business never seen before: the game's day, else an estimate, else it stays unknown
            for (int s = 0; s < now.Count; s++)
            {
                if (taken[s])
                {
                    continue;
                }
                FoundingSeen b = now[s];
                bool known = b.Created > 0;
                int first = known ? b.Created : estimate != null ? estimate(b.Place, b.Name) : 0;
                if (first > 0 && first <= day)
                {
                    next.Add(new FoundingRecord { Place = b.Place, Name = b.Name, Day = first, Seen = day, Guess = !known });
                    changes++;
                }
            }
            // 4. records of businesses not seen stay a while
            for (int r = 0; r < was.Count; r++)
            {
                if (used[r])
                {
                    continue;
                }
                if (day - was[r].Seen <= Grace)
                {
                    next.Add(was[r]);
                }
                else
                {
                    changes++;
                }
            }
            if (next.Count > RecordsMost)
            {
                next.Sort(BySeenThenDay);
                changes += next.Count - RecordsMost;
                next.RemoveRange(RecordsMost, next.Count - RecordsMost);
            }
            was.Clear();
            was.AddRange(next);
            return changes;
        }

        /// <summary>
        /// The day a business started, as best known: the record of its building and name, or the game's own day when it has none
        /// (a building a business was moved to keeps the day of the business it replaced, so the game's day is not taken over a record
        /// that is not an estimate); 0 when nothing is known. A day of 0 or less is never a day.
        /// </summary>
        public static int DayOf(FoundingFile file, string place, string name, int created)
        {
            FoundingRecord f = file != null ? file.At(place, name) : null;
            if (f != null)
            {
                return f.Guess && created > 0 && created < f.Day ? created : f.Day;
            }
            return created > 0 ? created : 0;
        }

        private static int IndexOf(List<FoundingRecord> was, bool[] used, string place, string name)
        {
            for (int r = 0; r < was.Count; r++)
            {
                if (!used[r] && string.Equals(was[r].Place, place, StringComparison.Ordinal) && string.Equals(was[r].Name, name, StringComparison.Ordinal))
                {
                    return r;
                }
            }
            return -1;
        }

        private static int IndexOfName(List<FoundingRecord> was, bool[] used, string name)
        {
            for (int r = 0; r < was.Count; r++)
            {
                if (!used[r] && string.Equals(was[r].Name, name, StringComparison.Ordinal))
                {
                    return r;
                }
            }
            return -1;
        }

        // Newest seen first, then the older start day first: the longest unseen are the ones cut.
        private static int BySeenThenDay(FoundingRecord a, FoundingRecord b)
        {
            int c = b.Seen.CompareTo(a.Seen);
            return c != 0 ? c : a.Day.CompareTo(b.Day);
        }
    }
}
