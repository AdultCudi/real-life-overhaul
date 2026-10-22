using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>Who holds a line of a cap table: the saved number of <see cref="Holder.Kind"/>.</summary>
    public enum HolderKind
    {
        Founders = 0, Pool = 1, Vc = 2, Angel = 3, Fund = 4, Player = 5
    }

    /// <summary>A line of a cap table: who, how many shares, what they put in, the preference that comes back first on a sale. Game-free.</summary>
    public sealed class Holder
    {
        public int Kind;          // HolderKind
        public int Who;           // a VC firm's id (VentureCatalogue.Firms), else 0
        public long Shares;
        public long Invested;     // cents
        public long Preference;   // cents that come back first on a sale
        public int Round;         // the stage they came in at
        public bool ProRata;      // may keep their share in later rounds
        public bool Board;        // holds a board seat
        public long Paid;         // what a sale paid this line, cents (the day of the sale; not saved)

        public HolderKind K => (HolderKind)Kind;
        public bool Players => Kind == (int)HolderKind.Angel || Kind == (int)HolderKind.Fund || Kind == (int)HolderKind.Player;
    }

    /// <summary>A term sheet: what an investor offers, or what founders ask of the player. Game-free.</summary>
    public sealed class TermSheet
    {
        public int Vc = -1;       // the firm offering it; -1: the founders' ask of the player
        public long Amount, Pre;  // cents
        public bool Board, ProRata;
        public int Lapse;         // the last day it can be taken
        public int Score;         // how the meeting went, 0 to 1000 (for the player's own raise)

        public long Post => Pre + Amount;
        public int EquityMille => Post > 0 ? (int)(Amount * 1000 / Post) : 0;
    }

    /// <summary>One of the city's startups. What it claims and what it is can differ until it is looked into. Game-free.</summary>
    public sealed class Startup
    {
        public int Id;
        public string Name = string.Empty, Founder = string.Empty;
        public int Sector, Stage, State;
        public int Founded;
        public int Quality;              // the founders' quality, 0 to 1000 (found out by diligence and a meeting)
        public long Mrr;                 // recurring revenue a month, cents
        public int ClaimBp;              // what its deck overstates the revenue by, basis points
        public long Cash;                // cents
        public long Burn;                // cents a day
        public long Post;                // the value after its last round, cents
        public int LastRound, FailedRaises;
        public long Carry;               // millionths of a cent of growth carried day to day
        public readonly List<Holder> Holders = new List<Holder>(6);
        // a round it is raising now
        public TermSheet Ask;            // what the founders ask of the player; null if they have not come to the player
        public bool Diligence;           // a report bought on it
        public int Revealed;             // what a meeting found out: 1 the revenue, 2 the founders, 4 the market
        // the player's pro rata follow-on of the last round (the end of the save's line; a line without them offers none)
        public int RoundDay = -1;        // the day the last round closed; -1 none known
        public long BeforeMine;          // the player's shares just before that round
        public long BeforeTotal;         // all the shares just before that round; 0 none known
        public bool Followed;            // the player has taken their follow-on of that round

        public StartupState S => (StartupState)State;
        public RoundStage R => (RoundStage)Stage;
        public bool Alive => State == (int)StartupState.Running || State == (int)StartupState.Raising;

        public long Total
        {
            get
            {
                long t = 0;
                for (int i = 0; i < Holders.Count; i++)
                {
                    t += Holders[i].Shares;
                }
                return t;
            }
        }
    }

    /// <summary>A firm of the player's that has taken outside money: its cap table, its board, its targets, a raise under way and a bridge. Game-free.</summary>
    public sealed class Cap
    {
        public string Place = string.Empty;
        public readonly List<Holder> Holders = new List<Holder>(6);
        public int Stage;                // the last round raised (RoundStage); -1 before the first
        public long Post;
        public int BoardSeats = 1, VcSeats;
        public long TargetMrr;           // what the board expects by TargetDay, cents a month
        public int TargetDay, Misses, Pressure;   // Pressure: 0 none, 1 warned, 2 the board has replaced the chief executive
        public string Ceo = string.Empty;         // the professional who is chief executive (a file's id); empty: the player
        public int RaiseUntil;           // a raise under way runs to this day; 0 none
        public int Meetings;
        public readonly List<TermSheet> Sheets = new List<TermSheet>(3);
        public long Bridge;              // a bridge loan owed, cents (converts at the next round)
        public int BridgeDay;
        public int OptionsGranted;       // thousandths of the firm granted to staff
        public long LastMrr;             // the recurring revenue a month when the target was set
        public long Unassigned;          // staff option shares nobody has been given yet, handed out when the firm has people
        public int OptionsChecked;       // 1 once the cap's options have been looked over for shares nobody holds

        public long Total
        {
            get
            {
                long t = 0;
                for (int i = 0; i < Holders.Count; i++)
                {
                    t += Holders[i].Shares;
                }
                return t;
            }
        }
    }

    /// <summary>The player's venture fund: their own money put aside for startups, and what it has made. Game-free.</summary>
    public sealed class Fund
    {
        public bool Open;
        public long Committed;   // cents the player has put in
        public long Cash;        // cents not yet invested
        public long Invested, Returned;
    }

    /// <summary>Everything the venture part keeps in a save. Game-free.</summary>
    public sealed class VentureFile
    {
        public int Seed, FirstDay, LastDay = -1, NextId = 1;
        public readonly List<Startup> Startups = new List<Startup>(VentureCatalogue.StartupsMost);
        public readonly List<Cap> Caps = new List<Cap>(4);
        public readonly Fund Fund = new Fund();
        public readonly int[] ClosedUntil = new int[VentureCatalogue.FirmCount];   // a firm that caught the player stretching the truth will not meet them until this day

        public Startup StartupOf(int id)
        {
            for (int i = 0; i < Startups.Count; i++)
            {
                if (Startups[i].Id == id)
                {
                    return Startups[i];
                }
            }
            return null;
        }

        public Cap CapOf(string place)
        {
            for (int i = 0; i < Caps.Count; i++)
            {
                if (Caps[i].Place == place)
                {
                    return Caps[i];
                }
            }
            return null;
        }

        public int Next() => NextId++;
    }

    /// <summary>What a day of the city's startups came to, for the director to tell.</summary>
    public struct VentureNote
    {
        public int Kind;          // 1 founded, 2 raised, 3 down round, 4 failed, 5 acquired, 6 pitches the player, 7 a follow-on offer for the player, 8 could list, 9 acqui-hired
        public int Startup;
        public long Amount;       // cents: a round's size, a sale's price, what came back to the player
    }
}
