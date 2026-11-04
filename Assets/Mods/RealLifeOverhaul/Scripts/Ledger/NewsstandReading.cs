using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>What the reading page stands on: no title has an edition to read, the title picked is not one you hold, or an edition is open.</summary>
    public enum NewsstandReadState : byte { Nothing = 0, NotHeld, Edition }

    /// <summary>
    /// The Newsstand's reading page as the old page kept it (NewsstandApp.Reading): the title and the day chosen, the days a title
    /// can be read on and the titles that have something to read. A title is held while an edition of it came and is still kept
    /// (a subscription that ended leaves what came); a title that is not held has nothing to open, only the way to take it out.
    /// Game-free. COST: a few list reads when the page is filled.
    /// </summary>
    public sealed class NewsstandReading
    {
        public int Title = -1;                    // the title picked; -1 for none yet
        public int Day;                           // the day of its edition being read
        public NewsstandReadState State;
        public readonly List<int> Days = new List<int>(NewsstandCatalogue.KeepDays);   // the picked title's days, newest first
        private readonly bool[] _held = new bool[NewsstandCatalogue.Titles];
        private readonly List<int> _scratch = new List<int>(NewsstandCatalogue.KeepDays);

        /// <summary>The days a title can be read on into <paramref name="into"/>, newest first: those that came, within the days kept, whose edition is still kept.</summary>
        public static void ReadableDays(NewsstandState s, int title, int today, Func<int, bool> kept, List<int> into)
        {
            NewsstandRules.Days(s, title, today, into);
            for (int i = into.Count - 1; i >= 0; i--)
            {
                if (kept == null || !kept(into[i]))
                {
                    into.RemoveAt(i);
                }
            }
        }

        /// <summary>The page brought up to date for a day: the held titles found, a title picked if none is, the day kept to one that can be read.</summary>
        public void Settle(NewsstandState s, int today, Func<int, bool> kept)
        {
            int newest = 0, first = -1;
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                ReadableDays(s, t, today, kept, _scratch);
                _held[t] = _scratch.Count > 0;
                if (_held[t] && _scratch[0] > newest)
                {
                    newest = _scratch[0];
                    first = t;
                }
            }
            if (Title < 0 || Title >= NewsstandCatalogue.Titles)
            {
                Title = first;   // the title with the newest edition, else none
                Day = 0;
            }
            Days.Clear();
            if (Title < 0)
            {
                State = NewsstandReadState.Nothing;
                return;
            }
            if (!_held[Title])
            {
                State = NewsstandReadState.NotHeld;
                Day = 0;
                return;
            }
            ReadableDays(s, Title, today, kept, Days);
            if (!Days.Contains(Day))
            {
                Day = Days[0];
            }
            State = NewsstandReadState.Edition;
        }

        /// <summary>Picks a title to read (the one open is picked again without a change). False for a place that is no title.</summary>
        public bool PickTitle(int title)
        {
            if (title < 0 || title >= NewsstandCatalogue.Titles)
            {
                return false;
            }
            if (title != Title)
            {
                Day = 0;
            }
            Title = title;
            return true;
        }

        /// <summary>Picks a day of the open title's editions. False for a day that cannot be read, or when no edition is open.</summary>
        public bool PickDay(int day)
        {
            if (State != NewsstandReadState.Edition || !Days.Contains(day))
            {
                return false;
            }
            Day = day;
            return true;
        }
    }
}
