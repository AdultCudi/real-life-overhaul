using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A ring of the city's builders on one tender, and the player's part in it. Game-free.</summary>
    public sealed class RingFile
    {
        public const int NoAnswer = 0, Refused = 1, Joined = 2, Reported = 3;

        public int Tender;
        public int Leader;            // the builder who is to win
        public int Members;           // a bit for each builder in it, the leader's too
        public bool Asked;            // the player's company was asked to cover
        public int Answer;            // NoAnswer, Refused, Joined, Reported
        public int Awarded;           // the day its tender was awarded; 0 before
        public bool LeaderWon;        // the leader won it
        public long Cut;              // what the player was paid for covering
        public int Found;             // the day the inquiry found it out; 0 for not yet
        public int Case;              // the bid rigging case it brought against the player, if any
    }

    /// <summary>A bill owed to the company or by it, and where it stands. Game-free.</summary>
    public sealed class BillFile
    {
        public int Id;
        public BillKind Kind;
        public int Project;           // the project it is of; 0 for none
        public int Who;               // a client's, foreman's or merchant's number in the catalogue; the flats for buyers
        public int Crew;              // the crew owed wages; 0 for none
        public long Amount;
        public int Raised;            // the day it was raised
        public int Due;               // the day it fell due
        public int PaysOn;            // owed to the company: the day the debtor pays, or BillRules.Never
        public int Chased;            // letters sent
        public int ChasedDay;
        public int Case;              // the court's number for a claim over it
        public int Closed;            // the day it was settled; 0 while open
        public int Outcome;           // how it ended (Paid, Won, Lost, Settled, Sued)
        public bool City;             // the debtor is the city

        public const int Open = 0, Paid = 1, Won = 2, Lost = 3, Settled = 4, Sued = 5;

        public bool Owed => Kind == BillKind.Client || Kind == BillKind.Buyers;   // owed to the company
        public bool Live => Closed == 0;
    }

    /// <summary>A building finished in the city in a year, a candidate for the building of the year. Game-free.</summary>
    public sealed class AwardEntry
    {
        public const int OfPlayer = 0, OfDevelopers = 1, OfCity = 2;

        public int Kind;              // the player's project, a developers' building, a public building a rival put up
        public int Project;           // the player's project; 0 for none
        public string Lot = string.Empty;
        public string Street = string.Empty;
        public int Number;
        public BuildUse Use;
        public int Builder;           // -1 the player's company, else a rival's number
        public int Day;               // finished on
        public int Score;             // the critic's, 5 to 98
        public int Votes;
        public bool Short;            // shortlisted

        public void CopyFrom(AwardEntry other)
        {
            Kind = other.Kind;
            Project = other.Project;
            Lot = other.Lot;
            Street = other.Street;
            Number = other.Number;
            Use = other.Use;
            Builder = other.Builder;
            Day = other.Day;
            Score = other.Score;
            Votes = other.Votes;
            Short = other.Short;
        }
    }

    /// <summary>The building of the year: the year's finished buildings, its vote, and the last award made. Game-free.</summary>
    public sealed class AwardFile
    {
        public int Year = -1;                        // the year the entries are of; -1 for none yet
        public bool Voting;                          // the shortlist is out
        public readonly List<AwardEntry> Entries = new List<AwardEntry>(AwardRules.EntriesMost);
        public int LastYear = -1;                    // the year last awarded
        public readonly AwardEntry Last = new AwardEntry();   // ...and its winner
        public int LastVotes;                        // the votes cast in it
    }

    public sealed partial class BuildState
    {
        public readonly int[] ClassAdded = new int[BuildCatalogue.ClassCount];   // uses the council allowed on every lot of a class, bits of BuildUse
        public readonly List<RingFile> Rings = new List<RingFile>(4);
        public readonly List<BillFile> Bills = new List<BillFile>(4);
        public readonly AwardFile Award = new AwardFile();
        public int NextBill = 1;

        public const int RingsKept = 24, BillsKept = 24;

        public RingFile Ring(int tender)
        {
            for (int i = 0; tender > 0 && i < Rings.Count; i++)
            {
                if (Rings[i].Tender == tender)
                {
                    return Rings[i];
                }
            }
            return null;
        }

        public BillFile Bill(int id)
        {
            for (int i = 0; id > 0 && i < Bills.Count; i++)
            {
                if (Bills[i].Id == id)
                {
                    return Bills[i];
                }
            }
            return null;
        }

        /// <summary>The open bill of a kind for a project (or a crew), or null.</summary>
        public BillFile OpenBill(BillKind kind, int project, int crew)
        {
            for (int i = 0; i < Bills.Count; i++)
            {
                BillFile b = Bills[i];
                if (b.Live && b.Kind == kind && b.Project == project && b.Crew == crew)
                {
                    return b;
                }
            }
            return null;
        }

        /// <summary>The uses a lot may be built for: its own zoning and its class's, as the council has it.</summary>
        public int ZonesOf(LotFile lot, int lotClass)
        {
            int c = BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1);
            return lot == null ? 0 : LandRules.ZonesWith(lot.Zones, ClassAdded[c]);
        }

        /// <summary>A kept list kept short: settled bills and rings long done go first.</summary>
        public void Tidy()
        {
            for (int i = 0; i < Bills.Count && Bills.Count > BillsKept; i++)
            {
                if (!Bills[i].Live)
                {
                    Bills.RemoveAt(i);
                    i--;
                }
            }
            while (Rings.Count > RingsKept)
            {
                Rings.RemoveAt(0);
            }
            if (Bills.Count > BillsKept * 2)
            {
                Bills.RemoveRange(0, Bills.Count - BillsKept * 2);
            }
        }
    }
}
