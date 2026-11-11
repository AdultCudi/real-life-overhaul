using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The professionals the hour's seating looks at, asked through this so that the game side (TechDirector.Seating) and the offline check run the
    /// same rounds. They are numbered from the oldest hire to the newest and looked at from the newest back. Game-free.
    /// </summary>
    public interface ISeatBoard
    {
        /// <summary>How many professionals there are.</summary>
        int Count { get; }

        /// <summary>
        /// True if the professional may still need a seat: the game's employee, not found with a shift at a desk, and not one the pass leaves to the
        /// player. Cheap, since it is asked of everyone every hour. <paramref name="everyone"/> (the test) asks it of those found seated too.
        /// </summary>
        bool Open(int i, bool everyone);

        /// <summary>The place key of the business they were assigned to when last read.</summary>
        string Place(int i);

        /// <summary>The game skill they are hired under.</summary>
        string Skill(int i);

        /// <summary>
        /// Looks closely: true, with the place key of the business they are assigned to now, if they have no shift at a desk there. False for one
        /// who has a shift (not asked about again) or whom the pass leaves to the player now.
        /// </summary>
        bool Idle(int i, out string place);

        /// <summary>The business is read for the hires to be seated in it.</summary>
        void Begin(string place);

        /// <summary>
        /// Seats the professional in the business read: <see cref="SeatPlan.Seated"/>, or why not (<see cref="SeatPlan.Closed"/>,
        /// <see cref="SeatPlan.NoDesk"/>, <see cref="SeatPlan.Full"/>).
        /// </summary>
        int Seat(int i);

        /// <summary>The business is done with: the game is told of what was written and the player of what they should know.</summary>
        void End(string place);
    }

    /// <summary>A professional to be seated: their number on the board, the business and the skill the memory of the day is kept by.</summary>
    public struct SeatPick
    {
        public int Index;
        public string Place, Skill;
    }

    /// <summary>What a run of rounds came to: those found without a seat, the looks it took, those seated, and those not by why.</summary>
    public struct SeatTally
    {
        public int Seen, Looked, Seated, Closed, NoDesk, Full;

        /// <summary>Those found without a seat who were not seated.</summary>
        public int Unseated => Closed + NoDesk + Full;

        /// <summary>One more without a seat, not seated, for the reason a plan came to.</summary>
        public void Add(int result)
        {
            Closed += result == SeatPlan.Closed ? 1 : 0;
            NoDesk += result == SeatPlan.NoDesk ? 1 : 0;
            Full += result == SeatPlan.Full ? 1 : 0;
        }
    }

    /// <summary>
    /// What the seating remembers so that a business with no room is asked once and not once for each hire waiting there: the answer a business
    /// gave for a skill today (closed, no desk they may use, every desk taken) and the notices already told. A business that answered is left
    /// alone for the rest of the day and tried again on the next, one hire first. Game-free; a few dictionary looks, never per frame.
    /// </summary>
    public sealed class SeatLedger
    {
        private readonly Dictionary<(string Place, string Skill), int> _answers = new Dictionary<(string Place, string Skill), int>(32);   // day * 4 + the result
        private readonly HashSet<(string Place, int Reason)> _told = new HashSet<(string Place, int Reason)>();
        internal readonly List<SeatPick> Picks = new List<SeatPick>(SeatPlan.TestMost);

        /// <summary>
        /// The answer the business gave on the day for the skill (<see cref="SeatPlan.Closed"/>, <see cref="SeatPlan.NoDesk"/> or
        /// <see cref="SeatPlan.Full"/>), or <see cref="SeatPlan.Seated"/> if it has not answered no today.
        /// </summary>
        public int Answered(string place, string skill, int day)
        {
            return place != null && skill != null && _answers.TryGetValue((place, skill), out int kept) && kept >> 2 == day ? kept & 3 : SeatPlan.Seated;
        }

        /// <summary>The business answered no for the skill on the day.</summary>
        public void Answer(string place, string skill, int day, int result)
        {
            if (place != null && skill != null && result != SeatPlan.Seated)
            {
                _answers[(place, skill)] = day * 4 + (result & 3);
            }
        }

        /// <summary>The business seated somebody: it has room again, so a shortage there later is told afresh.</summary>
        public void Seated(string place)
        {
            _told.Remove((place, SeatPlan.Closed));
            _told.Remove((place, SeatPlan.NoDesk));
            _told.Remove((place, SeatPlan.Full));
        }

        /// <summary>True the first time a business's reason is asked until it seats somebody: the player is told then and not again.</summary>
        public bool Tell(string place, int reason)
        {
            return place != null && _told.Add((place, reason));
        }

        /// <summary>Everything forgotten.</summary>
        public void Clear()
        {
            _answers.Clear();
            _told.Clear();
            Picks.Clear();
        }
    }

    /// <summary>
    /// The hour's seating, in rounds. Each round takes, from the newest hire back, the professionals who have no shift at a desk and whose business
    /// has not already answered no for their skill today, as many as there are seats left in the batch, and seats them a business at a time. A hire
    /// seated counts against the batch; one for whom there was no room does not, and nor do the rest of that business's hires for the day (the
    /// business is asked once), so a business with hundreds waiting and no desk cannot hold up the next, today or on any day. Another round follows
    /// while the batch is not full and anybody is left to look at, at most <c>look</c> looked at closely. Game-free; the cost is a look for each
    /// professional who is open and a plan for each hire seated or each business asked.
    /// </summary>
    public static class SeatRound
    {
        /// <summary>
        /// Runs the rounds for a day: up to <paramref name="quota"/> seated, at most <paramref name="look"/> looked at closely. With
        /// <paramref name="everyone"/> every one found without a seat is counted, those of a business that answered no included.
        /// </summary>
        public static SeatTally Run(ISeatBoard board, SeatLedger ledger, int day, int quota, int look, bool everyone)
        {
            var tally = new SeatTally();
            List<SeatPick> picks = ledger.Picks;
            int next = board.Count - 1;
            while (tally.Seated < quota && next >= 0 && tally.Looked < look)
            {
                picks.Clear();
                int room = quota - tally.Seated;
                while (next >= 0 && picks.Count < room && tally.Looked < look)
                {
                    int i = next--;
                    if (!board.Open(i, everyone))
                    {
                        continue;
                    }
                    string skill = board.Skill(i);
                    int shut = ledger.Answered(board.Place(i), skill, day);
                    if (shut != SeatPlan.Seated && !everyone)
                    {
                        continue;
                    }
                    tally.Looked++;
                    if (!board.Idle(i, out string place))
                    {
                        continue;
                    }
                    tally.Seen++;
                    shut = ledger.Answered(place, skill, day);
                    if (shut != SeatPlan.Seated)
                    {
                        tally.Add(shut);   // the test counts it; the hour leaves it for tomorrow
                        continue;
                    }
                    picks.Add(new SeatPick { Index = i, Place = place, Skill = skill });
                }
                if (picks.Count == 0)
                {
                    break;
                }
                for (int a = 0; a < picks.Count; a++)
                {
                    if (picks[a].Index < 0)
                    {
                        continue;
                    }
                    string place = picks[a].Place;
                    board.Begin(place);
                    try
                    {
                        for (int b = a; b < picks.Count; b++)
                        {
                            SeatPick pick = picks[b];
                            if (pick.Index < 0 || pick.Place != place)
                            {
                                continue;
                            }
                            picks[b] = new SeatPick { Index = -1 };
                            int shut = ledger.Answered(place, pick.Skill, day);
                            if (shut != SeatPlan.Seated)
                            {
                                tally.Add(shut);   // an earlier hire of this round found no room for the skill: not asked again
                                continue;
                            }
                            int result = board.Seat(pick.Index);
                            if (result == SeatPlan.Seated)
                            {
                                tally.Seated++;
                                ledger.Seated(place);
                            }
                            else
                            {
                                ledger.Answer(place, pick.Skill, day, result);
                                tally.Add(result);
                            }
                        }
                    }
                    finally
                    {
                        board.End(place);
                    }
                }
            }
            return tally;
        }
    }
}
