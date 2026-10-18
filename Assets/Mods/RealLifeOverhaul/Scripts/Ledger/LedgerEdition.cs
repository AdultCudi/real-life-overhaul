using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>Where a piece of the paper goes. Saved as a number: a new section goes at the end.</summary>
    public enum LedgerSection { City = 0, Economy, Business, Markets, Weather, People, AroundTown, Count }

    /// <summary>One piece of an edition: a locale key and the names that fill it, written out in the language of the moment. Game-free.</summary>
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

    /// <summary>The paper of one morning: its pieces, the market's figures at the close before, and the economy's. Game-free.</summary>
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
}
