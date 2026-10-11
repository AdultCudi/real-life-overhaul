using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// Somebody who would move in, or has: the mod's one person model (<see cref="SocialPerson"/>: a name, a look, a job,
    /// three figures for how things stand) with what living together adds to it.
    /// </summary>
    public sealed class Flatmate
    {
        public SocialPerson Person;
        public RoomTrait Traits;
        public RoomTrait Known;      // the traits the player has seen so far
        public int SharePct;         // of the rent, in per cent
        public int Answers;          // day * 24 + hour at which their answer to the notice comes
        public bool Told;            // the player has been told of their answer
        public int Friend = -1;      // their place in the social cast, when they are one of its people
    }

    /// <summary>What waits to be talked about, a bit each: the conversation at home takes the first that is set.</summary>
    [Flags]
    public enum Topic
    {
        None = 0, Leaving = 1, PartnerIn = 2, Rent = 4, Noise = 8, Raid = 16, Borrow = 32, Cooked = 64
    }

    /// <summary>
    /// Everything a save keeps of the feature (see <see cref="RoommateStore"/>). Game-free. With nobody living in and
    /// no notice up it is empty but for the seed.
    /// </summary>
    public sealed class RoommateState
    {
        public int Seed;
        public int LastDay;          // the day the daily round was last done for
        public int LastStamp;        // day * 24 + hour the hourly round was last done for

        // ---- the notice
        public bool AdUp;
        public int AdStamp;          // day * 24 + hour it went up
        public int Ads;              // how many notices this save has had: each brings different people
        public readonly List<Flatmate> Candidates = new List<Flatmate>(RoommateRules.CandidatesMost);

        // ---- the roommate
        public Flatmate Mate;       // null: nobody lives in
        public string HomeStreet;    // the home they live in (the game's own address)
        public int HomeNumber;
        public bool Lodger;          // the player owns that home: the roommate pays rent to the player
        public bool OnSofa;
        public int MovedIn;          // the day
        public int RentDay;          // the next one
        public int LastRentDay;      // the last one that was settled (or the day they moved in)
        public int Weekly;           // what a week came to when it was last worked out
        public int Owed;             // rent outstanding
        public int OwedDue;          // the day it is promised for
        public int OwedSlips;
        public int Loan;             // what they owe the player otherwise
        public int LoanDue;
        public int BorrowAsk;        // what they asked to borrow, while the question waits
        public Topic Topics;
        public string RaidItem;      // the game's name of what they last took
        public int RaidWeek;         // the week (days in / 7) the count below belongs to
        public int Raids;
        public bool RaidStopped;
        public bool QuietAsked;
        public int Covered;          // times the player let a week's rent go
        public int Meals;            // meals they left
        public int LastTalkDay;      // the day's conversation was had
        public int LastMealDay;      // a meal was shared
        public int LastCompanyDay;   // company at dinner was counted
        public int LastGreetStamp;
        public bool MadeFriend;      // they are one of the social cast now
        public bool Introduced;      // they have introduced somebody
        public bool PartnerSeen;     // the player's partner lived in when this was last looked at
        public EndWhy Notice;        // None: no notice given
        public int LeaveDay;

        public bool Living => Mate != null;
        public bool OnNotice => Mate != null && Notice != EndWhy.None;

        /// <summary>Nobody lives in any more: everything about them is forgotten, the notice and the seed are kept.</summary>
        public void ClearMate()
        {
            Mate = null;
            HomeStreet = null;
            HomeNumber = 0;
            Lodger = false;
            OnSofa = false;
            MovedIn = 0;
            RentDay = 0;
            LastRentDay = 0;
            Weekly = 0;
            Owed = 0;
            OwedDue = 0;
            OwedSlips = 0;
            Loan = 0;
            LoanDue = 0;
            BorrowAsk = 0;
            Topics = Topic.None;
            RaidItem = null;
            RaidWeek = 0;
            Raids = 0;
            RaidStopped = false;
            QuietAsked = false;
            Covered = 0;
            Meals = 0;
            LastTalkDay = 0;
            LastMealDay = 0;
            LastCompanyDay = 0;
            LastGreetStamp = 0;
            MadeFriend = false;
            Introduced = false;
            Notice = EndWhy.None;
            LeaveDay = 0;
        }

        public void ClearAd()
        {
            AdUp = false;
            AdStamp = 0;
            Candidates.Clear();
        }

        /// <summary>Somebody moves in on a day: the arrangement starts, the notice comes down.</summary>
        public void MoveIn(Flatmate who, int day, string street, int number, bool lodger, bool sofa, int weekly)
        {
            ClearMate();
            Mate = who;
            HomeStreet = street;
            HomeNumber = number;
            Lodger = lodger;
            OnSofa = sofa;
            MovedIn = day;
            LastRentDay = day;
            RentDay = day + RoommateRules.RentEveryDays;
            Weekly = weekly;
            MadeFriend = who.Friend >= 0;
            ClearAd();
        }

        /// <summary>Notice is given, by either: they leave in <see cref="RoommateRules.NoticeDays"/> days.</summary>
        public void GiveNotice(EndWhy why, int day)
        {
            if (Mate == null || why == EndWhy.None)
            {
                return;
            }
            Notice = why;
            LeaveDay = day + RoommateRules.NoticeDays;
        }

        public void TakeBackNotice()
        {
            Notice = EndWhy.None;
            LeaveDay = 0;
            Topics &= ~Topic.Leaving;
        }

        /// <summary>
        /// The feature was switched off for so many days and is on again: every date moves on by as much, so nothing
        /// is owed for the days nobody counted and nobody leaves or answers in a gap that was never played.
        /// </summary>
        public void Shift(int days)
        {
            if (days <= 0)
            {
                return;
            }
            if (AdUp)
            {
                AdStamp += days * 24;
                for (int i = 0; i < Candidates.Count; i++)
                {
                    Candidates[i].Answers += days * 24;
                }
            }
            if (Mate == null)
            {
                return;
            }
            MovedIn += days;
            RentDay += days;
            LastRentDay += days;
            OwedDue += Owed > 0 ? days : 0;
            LoanDue += Loan > 0 ? days : 0;
            LeaveDay += Notice != EndWhy.None ? days : 0;
        }

        /// <summary>The first topic that waits, in the order they matter; None if nothing does.</summary>
        public Topic NextTopic()
        {
            for (int bit = 1; bit <= (int)Topic.Cooked; bit <<= 1)
            {
                if ((Topics & (Topic)bit) != 0)
                {
                    return (Topic)bit;
                }
            }
            return Topic.None;
        }
    }
}
