using System.Collections.Generic;

namespace RealLifeOverhaul.Schools
{
    /// <summary>What kind of school one of the city's is: the campus, a trust's private school, or the council's free school of a district.</summary>
    public enum SchoolKind
    {
        Campus = 0,
        Trust = 1,
        Council = 2
    }

    /// <summary>An inspection's verdict, as the council's inspectors give it.</summary>
    public enum SchoolGrade
    {
        None = 0,
        Outstanding = 1,
        Good = 2,
        Improve = 3,      // requires improvement
        Inadequate = 4
    }

    /// <summary>Who runs a school: the council, the player, or one of the four education trusts (0 to 3).</summary>
    public static class SchoolOwner
    {
        public const int Council = -2;
        public const int Player = -1;

        public static bool IsTrust(int owner) => owner >= 0 && owner < SchoolRules.Trusts;
    }

    /// <summary>
    /// One school as the save keeps it. Money is whole money at the start's prices (Core/Prices turns it into today's when it
    /// is shown or charged); reputation in tenths of a point, so a day's drift is never lost to rounding.
    /// </summary>
    public sealed class SchoolFile
    {
        public int Id;                 // its place in SchoolRules.Catalogue
        public int Owner;              // SchoolOwner: the council, the player, a trust
        public int Places;             // places open now: its classes times the class size
        public int Pupils;             // pupils this term
        public int Waiting;            // families waiting for a place this term
        public int Fee;                // a pupil's day, whole money at the start's prices; the council's schools charge none
        public int Teachers;
        public int Skill;              // the teachers' skill, 0 to 100
        public int PayBp;              // their pay against the city's going rate, basis points (10000 the going rate)
        public int Head;               // the headteacher's seed (their name is the game's own from it); 0 none
        public int HeadSkill;          // 0 to 100
        public int Scholar;            // per cent of its places given as scholarships
        public int Reputation;         // tenths of a point, 0 to 1000
        public int Results = -1;       // the last results day's: per cent of good passes, -1 before the first
        public int Grade;              // SchoolGrade of its last inspection
        public int Inspected;          // the day of it
        public long Cash;              // a trust's or the council's account at the school; the player's money is the game's own
        public long Week;              // the player's school: its money of the week so far, booked to the game at the week's end
        public int LossWeeks;          // weeks in a row it lost money: a trust sells a school that keeps losing
    }

    /// <summary>A piece of the City Ledger the market has to print: its kind, its day and what fills it.</summary>
    public struct SchoolNews
    {
        public int Day;
        public int Kind;     // SchoolRules.News*
        public int School;   // the school it is about
        public int Other;    // a second school, a trust or a rank
        public long Value;   // a price, a score, a count
    }

    /// <summary>The whole market as one save keeps it: every school, the player's children at the campus, the passes' days.</summary>
    public sealed class SchoolState
    {
        public readonly SchoolFile[] Files = new SchoolFile[SchoolRules.Count];
        public int Opened;             // the first day the campus stood in this save; 0 before
        public int LastDay;            // the day of the last day's pass
        public int Term = -1;          // the term of the last term's start
        public int Year;               // the year of the last results day
        public int OfferPrice;         // a trust's offer for the player's school, thousands of the start's money; 0 none
        public int OfferBy = -1;       // the trust that made it
        public int OfferEnd;           // the day it lapses
        public int OwnedSince;         // the day the player took the school; 0 when not theirs
        public readonly List<string> Children = new List<string>(4);   // the player's children at the campus, by name
        public readonly List<SchoolNews> News = new List<SchoolNews>(16);

        public SchoolState()
        {
            SchoolRules.Fresh(this);
        }

        /// <summary>The campus's own file.</summary>
        public SchoolFile Campus => Files[SchoolRules.CampusId];
    }
}
