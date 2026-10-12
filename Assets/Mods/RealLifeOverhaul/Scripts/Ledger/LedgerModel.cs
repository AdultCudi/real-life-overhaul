using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>Where a piece of the paper goes. Saved as a number: a new section goes at the end.</summary>
    public enum LedgerSection { City = 0, Economy, Business, Markets, Weather, People, AroundTown, Count }

    /// <summary>What the morning's round came to for the subscription (<see cref="LedgerRules.Morning"/>).</summary>
    public enum LedgerMorning
    {
        None,       // no subscription, or this morning's round is done already
        Kept,       // paid for: today's paper is delivered
        Billed,     // a new week was paid for this morning, and today's paper is delivered
        Lapsed,     // the new week could not be paid: the subscription has stopped, nothing is delivered
        Ended       // a cancelled subscription has run to the last day paid for
    }

    /// <summary>
    /// One piece of an edition: a story or a brief. Its words are a locale key and the names that fill it, kept as the
    /// paper's other pieces are (written out in the language of the moment when the page is opened), with the key of the
    /// paper's own headline for it where it has one for that kind of piece. Game-free.
    /// </summary>
    public sealed class LedgerPiece
    {
        public static readonly string[] NoData = new string[0];

        public LedgerSection Section;
        public int Weight;               // how much it matters: the front page leads with the heaviest
        public string Key = string.Empty;
        public string Head;              // the paper's headline for it, a locale key filled from the same names; null for none
        public string[] Data = NoData;   // name, value, name, value

        public LedgerPiece()
        {
        }

        public LedgerPiece(LedgerSection section, int weight, string key, string head, string[] data)
        {
            Section = section;
            Weight = weight;
            Key = key ?? string.Empty;
            Head = head;
            Data = data ?? NoData;
        }

        /// <summary>The value of one of its names, or null.</summary>
        public string Value(string name)
        {
            for (int i = 0; i + 1 < Data.Length; i += 2)
            {
                if (Data[i] == name)
                {
                    return Data[i + 1];
                }
            }
            return null;
        }
    }

    /// <summary>One company in the share prices: as it closed at the session the edition reports on.</summary>
    public sealed class LedgerQuote
    {
        public const int Bid = 1, ExDividend = 2, New = 4, Split = 8;

        public string Name = string.Empty;
        public string Ticker = string.Empty;
        public int Sector;               // its place in the edition's sectors
        public int Close;                // cents
        public int ChangeBp;             // on the session before
        public long Volume;              // shares traded in the session
        public int Flags;                // Bid, ExDividend, New, Split: the marks after its name
    }

    /// <summary>One sector of the market in an edition: its name's key, its index and its move on the session.</summary>
    public sealed class LedgerSector
    {
        public string Key = string.Empty;
        public int Level;                // hundredths
        public int ChangeBp;
    }

    /// <summary>
    /// The paper of one morning, written once when the city's paper is out (06:00) and read as often as the player likes
    /// until the next: the pieces, the market's figures at the close before (the index and its last weeks, the sectors,
    /// every company's price) and the economy's (the bank rate, inflation, the state of the cycle). Game-free.
    /// </summary>
    public sealed class LedgerEdition
    {
        public int Day;
        public bool HasMarket;
        public int MarketDay;            // the session the market's figures are of
        public int IndexLevel;           // hundredths
        public int IndexChangeBp, IndexWeekBp;
        public int RateBp = -1;          // the bank rate; -1 not known
        public int InflationBp = int.MinValue;
        public string CycleKey;          // the name of the state of the city's economy; null not known
        public readonly List<LedgerPiece> Pieces = new List<LedgerPiece>(32);
        public readonly List<LedgerSector> Sectors = new List<LedgerSector>(24);
        public readonly List<LedgerQuote> Quotes = new List<LedgerQuote>(160);
        public readonly List<int> IndexCloses = new List<int>(LedgerCatalogue.ChartDays);   // oldest first, hundredths
    }

    /// <summary>Where a kiosk stands: its middle on the pavement, the way its counter faces (degrees), the building behind it.</summary>
    public struct KioskSpot
    {
        public float X, Y, Z, Heading;
        public string Building;          // the address of the building it stands in front of (it lights up on the map)
    }

    /// <summary>
    /// What the City Ledger keeps of a save (<see cref="LedgerStore"/>): the subscription, the copy the player has, where
    /// the kiosks near their home stand, how the council's policies stood at the last edition and the latest edition.
    /// Game-free.
    /// </summary>
    public sealed class LedgerState
    {
        // the subscription
        public bool Subscribed;
        public bool Cancelled;           // it ends after the last day paid for
        public int PaidUntil;            // the last morning paid for
        public int Since;                // the day it was taken out
        public int Weeks;                // weeks paid, ever
        public int LastMorning;          // the last morning the round has been done for (a delivery, a payment)

        // the copy the player has
        public int CopyDay;              // the edition's day; 0 none
        public bool Delivered;           // left inside the door of the home
        public bool Bought;              // bought at a kiosk
        public int Copies;               // copies bought, ever

        // the kiosks
        public string KiosksFor;         // the address of the home they were found near; null none
        public readonly List<KioskSpot> Kiosks = new List<KioskSpot>(LedgerCatalogue.KiosksWanted);

        // the paper
        public string Council;           // the council's policies as they stood at the last edition (the wiring's words); null none yet
        public LedgerEdition Edition;
    }
}
