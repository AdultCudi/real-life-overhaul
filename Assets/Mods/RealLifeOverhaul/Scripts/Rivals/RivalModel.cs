using System.Collections.Generic;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>One tycoon as the save keeps them. Their name and holding are the catalogue's (<see cref="RivalCatalogue.Tycoons"/>, by <see cref="Index"/>).</summary>
    public sealed class Tycoon
    {
        public int Index;
        public long Worth;                     // dollars, today's prices
        public long WeekAgo;                   // their worth at the last weekly mark (the rich list's change)
        public int ActiveDay = -1;             // the day they came forward; -1 while they have not
        public int DefeatedDay = -1;           // the day the player took their holding over; -1 while they stand
        public int QuietUntil;                 // no move of theirs before this day (a counter)
        public int SlowUntil;                  // their fortune grows at half the pace before this day (a counter)
        public int BarredUntil;                // no lobbying or buying up buildings before this day (a counter)
        public int LastMoveDay = -1;
        public int StakeDay = -1;              // the day their stake in the player's company came
        public int BidDay = -1;                // the day their bid for it came
        public bool Listed;                    // their holding has come to the stock market
        public readonly int[] CounterDay = { -1000, -1000, -1000, -1000, -1000 };   // the day each counter was last used against them

        public bool Active => ActiveDay >= 0 && DefeatedDay < 0;
        public TycoonDef Def => RivalCatalogue.Tycoons[RivalCatalogue.Clamp(Index, 0, RivalCatalogue.TycoonCount - 1)];
    }

    /// <summary>One move of a rival's: what it is, against what, and where it stands.</summary>
    public sealed class RivalMove
    {
        public int Id;
        public int Tycoon;
        public RivalMoveKind Kind;
        public RivalMoveState State;
        public int Announced;                  // the day it was announced
        public int Land;                       // the day it comes
        public int End;                        // the first day without its effect (for one that runs)
        public string Place = string.Empty;    // the player's business it is aimed at, as business pressure writes a place
        public string Name = string.Empty;     // that business's name, as it was then
        public string District = string.Empty; // the game's own key of its district
        public string Type = string.Empty;     // the game's business type there (an outlet: of the same kind)
        public long Amount;                    // its size: per mille (property, poach, lobby), per cent (an outlet), dollars (a suit)
        public long Second;                    // a second size: the rent rise per mille (property); the claim's case number once filed (a suit)
        public bool Rented;                    // property: the player's business there pays rent
        public bool Test;                      // made by a test button
        public string Who = string.Empty;      // a poaching: the employee approached, as the company named them

        public bool Live => State == RivalMoveState.Announced || State == RivalMoveState.Running;
    }

    /// <summary>One counter of the player's: what it was, against whom, on which day, and what it cost.</summary>
    public sealed class RivalCounterNote
    {
        public int Id;
        public int Tycoon;
        public RivalCounter Kind;
        public int Day;
        public long Cost;                      // dollars paid (a takeover: what the shares bought cost)
        public int Move;                       // the move it stopped, or 0
    }

    /// <summary>
    /// The rivals of a save: the tycoons, the moves, the player's counters and the marks the rich list counts from.
    /// Game-free: what the game is today comes in a <see cref="RivalWorld"/>.
    /// </summary>
    public sealed class RivalFile
    {
        public int Seed;
        public int StartDay;
        public int SettledDay = -1;            // the last day played; -1 for a new file
        public int NextMoveDay;                // no new move announced before this day
        public int NextId = 1;
        public int Tier;                       // the player's tier when the last day was played
        public long PlayerWorth;               // the player's worth when the last day was played
        public long PlayerWeekAgo;
        public int WeekDay = -1;               // the day of the last weekly mark
        public int TopDay = -1;                // the first day the player topped the rich list; -1 while they never have
        public long BreakUp;                   // a takeover's break-up money still to come to the player
        public int BreakUpDay;                 // ...on this day
        public int BreakUpTycoon = -1;
        public readonly Tycoon[] Tycoons = new Tycoon[RivalCatalogue.TycoonCount];
        public readonly List<RivalMove> Moves = new List<RivalMove>(RivalCatalogue.MovesKept + 2);
        public readonly List<RivalCounterNote> Counters = new List<RivalCounterNote>(RivalCatalogue.CountersKept + 2);

        public RivalFile()
        {
            for (int i = 0; i < Tycoons.Length; i++)
            {
                Tycoons[i] = new Tycoon { Index = i };
            }
        }

        /// <summary>The move with this id, or null.</summary>
        public RivalMove Move(int id)
        {
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Moves[i].Id == id)
                {
                    return Moves[i];
                }
            }
            return null;
        }

        /// <summary>The move announced and still to come, or null: there is never more than one.</summary>
        public RivalMove Pending()
        {
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Moves[i].State == RivalMoveState.Announced)
                {
                    return Moves[i];
                }
            }
            return null;
        }
    }

    /// <summary>One of the player's businesses as the rivals see it, read from the game each morning.</summary>
    public struct RivalShop
    {
        public string Place;                   // as business pressure writes a place
        public string Name;
        public string District;                // the game's own key
        public string Type;                    // the game's business type
        public long Week;                      // what it took in the last seven days
        public bool Rented;                    // the player rents the building
        public int Rent;                       // the rent a day, if rented
    }

    /// <summary>What the game is today, as the rules need it: read by the director (RivalGame) each morning, never per frame.</summary>
    public sealed class RivalWorld
    {
        public long Worth;                     // the player's net worth, as the Finance app shows it
        public int Level = 1000;               // the city's price level, tenths of a point
        public int Cycle = 1;                  // the city's cycle: 0 boom, 1 steady, 2 slowdown, 3 recession
        public bool Listed;                    // the player's company is on the stock market, settled
        public bool Market;                    // the stock market runs here (alone: not following another player's)
        public bool Staff;                     // the company's staff part runs (a poaching offer can be made)
        public bool Issues;                    // business pressure runs (a smear, an inspection)
        public bool Court;                     // the justice system takes civil claims
        public bool Paper;                     // the City Ledger prints
        public readonly List<RivalShop> Shops = new List<RivalShop>(16);

        public void Clear()
        {
            Worth = 0;
            Level = 1000;
            Cycle = 1;
            Listed = Market = Staff = Issues = Court = Paper = false;
            Shops.Clear();
        }
    }

    /// <summary>One row of the city's rich list.</summary>
    public struct RichRow
    {
        public int Who;                        // a tycoon's index; <see cref="Player"/>; or a fortune's (<see cref="FortuneBase"/> and up)
        public long Worth;
        public long WeekAgo;                   // 0 where it is not kept
        public string Name;

        public const int Player = -1;
        public const int FortuneBase = 100;
    }
}
