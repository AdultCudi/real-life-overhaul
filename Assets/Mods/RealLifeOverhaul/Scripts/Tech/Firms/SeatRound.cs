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
        /// True if the professional may still need a seat: the game's employee, assigned to a business now, and neither found with a shift at a desk
        /// there nor left to the player there (<see cref="SeatMemory"/>). Cheap, since it is asked of everyone every hour. <paramref name="everyone"/>
        /// (the test) asks it of those found with a shift too.
        /// </summary>
        bool Open(int i, bool everyone);

        /// <summary>The place key of the business they are assigned to now.</summary>
        string Place(int i);

        /// <summary>The game skill they are hired under.</summary>
        string Skill(int i);

        /// <summary>The hours a week their demands leave them, which the week for them is planned for. Cheap: kept for each professional.</summary>
        int Need(int i);

        /// <summary>
        /// Looks closely: true, with the place key of the business they are assigned to now, if they have no shift at a desk there. False for one
        /// who has a shift (not asked about again while they stay there) or whom the pass leaves to the player there.
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

    /// <summary>
    /// What the hour's seating remembers of one professional so as not to look at them again while nothing about them has changed: the business where they were
    /// found with a shift at a desk or given one, and the business where they were found to be none of the pass's business (a lawyer at a software studio, anyone
    /// at a law firm). The business is the one the game has them assigned to, passed as an object that compares by value (the game's address does), so this
    /// stays game-free. A memory holds for the business it was made at and no other: a professional the game moves to another business, or takes off every
    /// business (its moving, its training and the closing of a business all strip every shift of theirs and assign them elsewhere or nowhere), is looked at afresh
    /// there. One left to the player is looked at again when the business has become one the pass seats at (its type changed under the same address). While
    /// they stay where they are, a shift the player takes away is not given back. A field or two of memory for each professional; a comparison to ask.
    /// </summary>
    public sealed class SeatMemory
    {
        private object _seated, _left;

        /// <summary>True if they were left to the player at the business: the pass then asks whether it is still none of its business (<see cref="Reopen"/>).</summary>
        public bool LeftAt(object at)
        {
            return at != null && object.Equals(at, _left);
        }

        /// <summary>They are to be looked at afresh: that they were left to the player is let go.</summary>
        public void Reopen()
        {
            _left = null;
        }

        /// <summary>
        /// True if the pass has nothing to ask of a professional assigned to <paramref name="at"/>: they were found with a shift there (not for <paramref name="everyone"/>,
        /// the test, which asks about them again) or left to the player there. Null is no business: nothing to ask, and what was known of them is let go, so that
        /// wherever they are assigned next they are looked at.
        /// </summary>
        public bool Settled(object at, bool everyone)
        {
            if (at == null)
            {
                _seated = null;
                _left = null;
                return true;
            }
            return object.Equals(at, _left) || (!everyone && object.Equals(at, _seated));
        }

        /// <summary>They were found with a shift at a desk at the business, or given one there.</summary>
        public void Seated(object at)
        {
            _seated = at;
        }

        /// <summary>They were found to be none of the pass's business at the business.</summary>
        public void Left(object at)
        {
            _left = at;
        }
    }

    /// <summary>A professional to be seated: their number on the board, the business, and the skill and hours a week the memory of the day is kept by.</summary>
    public struct SeatPick
    {
        public int Index, Need;
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
    /// What the seating remembers so that a business with no room is asked once and not once for each hire waiting there: the answer a business gave today
    /// for a kind of hire (their skill and the hours a week they want: what the desks they may use and the hours free at them depend on, so what holds for one
    /// holds for the rest of the kind, and no more) and the notices already told. A business that answered is left alone for that kind for the rest of the day and
    /// tried again on the next, one hire first. A hire who wants fewer hours, or has another skill, is a kind of their own and is asked. Game-free; a few
    /// dictionary looks, never per frame.
    /// </summary>
    public sealed class SeatLedger
    {
        private readonly Dictionary<(string Place, string Skill, int Need), int> _answers = new Dictionary<(string Place, string Skill, int Need), int>(32);   // day * 4 + the result
        private readonly HashSet<(string Place, int Reason)> _told = new HashSet<(string Place, int Reason)>();
        internal readonly List<SeatPick> Picks = new List<SeatPick>(SeatPlan.TestMost);

        /// <summary>
        /// The answer the business gave on the day for the kind of hire (<see cref="SeatPlan.Closed"/>, <see cref="SeatPlan.NoDesk"/> or
        /// <see cref="SeatPlan.Full"/>), or <see cref="SeatPlan.Seated"/> if it has not answered no today.
        /// </summary>
        public int Answered(string place, string skill, int need, int day)
        {
            return place != null && skill != null && _answers.TryGetValue((place, skill, need), out int kept) && kept >> 2 == day ? kept & 3 : SeatPlan.Seated;
        }

        /// <summary>The business answered no for the kind of hire on the day.</summary>
        public void Answer(string place, string skill, int need, int day, int result)
        {
            if (place != null && skill != null && result != SeatPlan.Seated)
            {
                _answers[(place, skill, need)] = day * 4 + (result & 3);
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
    /// has not already answered no for their kind today, as many as there are seats left in the batch, and seats them a business at a time. A hire
    /// seated counts against the batch; one for whom there was no room does not, and nor do the rest of that business's hires of their kind for the day
    /// (the business is asked once for the kind), so a business with hundreds waiting and no desk cannot hold up the next, today or on any day. Another round
    /// follows while the batch is not full and anybody is left to look at, at most <c>look</c> looked at closely. Game-free; the cost is a look for each
    /// professional who is open and a plan for each hire seated or each kind of hire a business is asked about.
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
                    int need = board.Need(i);
                    int shut = ledger.Answered(board.Place(i), skill, need, day);
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
                    shut = ledger.Answered(place, skill, need, day);
                    if (shut != SeatPlan.Seated)
                    {
                        tally.Add(shut);   // the test counts it; the hour leaves it for tomorrow
                        continue;
                    }
                    picks.Add(new SeatPick { Index = i, Place = place, Skill = skill, Need = need });
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
                            int shut = ledger.Answered(place, pick.Skill, pick.Need, day);
                            if (shut != SeatPlan.Seated)
                            {
                                tally.Add(shut);   // an earlier hire of this round, of this kind, found no room: not asked again
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
                                ledger.Answer(place, pick.Skill, board.Need(pick.Index), day, result);   // the hours the plan was made for, which the seating has just read again
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
