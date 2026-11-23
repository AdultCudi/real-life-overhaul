using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: why the market moved, and the size segments. The causes of the day are written down as the engine applies them (<see cref="_why"/>) and the close names
    /// the cause of the sector that moved most and of the large companies that moved most, from that record alone (<see cref="MarketWhy"/>): nothing is drawn here and nothing is
    /// invented. The segments are reviewed at a close once a quarter and a change is decided only where this close is played first (as a split is), then put on the tape.
    /// </summary>
    public sealed partial class MarketEngine
    {
        private readonly WhyDay _why = new WhyDay();
        private MarketEnv _prevEnv;       // yesterday's city, for what its change was worth
        private bool _hasPrev;

        // At the open: yesterday's city kept, the day's record empty.
        private void WhyOpen(int day, in MarketEnv today)
        {
            _hasPrev = State.Env.Supply != null && State.Env.TradeOf != null;
            _prevEnv = State.Env;
            _why.Clear(day);
        }

        // An event heard of: what it did to each sector, by what it was. A policy and an election are noted where the engine prices them.
        private void WhyHeard(in MarketEvent e)
        {
            WhyCause cause = e.Kind == MarketInputs.EventFire ? WhyCause.Fire : e.Kind == MarketInputs.EventCrime ? WhyCause.Crime : e.Kind == MarketInputs.EventBreakIn ? WhyCause.BreakIn
                : e.Kind == MarketInputs.EventAccident ? WhyCause.Accident : e.Kind == MarketInputs.EventStorm ? WhyCause.Weather : e.Kind == MarketInputs.EventBuzz ? WhyCause.Buzz : WhyCause.None;
            if (cause == WhyCause.None)
            {
                return;
            }
            int trade = e.Kind == MarketInputs.EventBuzz ? SectorOfTrade(e.A) : -1;
            for (int sec = 0; sec < Sectors; sec++)
            {
                int bp = MarketInputs.EventBp(e, (MarketSector)sec) + (trade >= 0 ? MarketInputs.BuzzBp(e, (MarketSector)sec, (MarketSector)trade) : 0);
                if (bp != 0)
                {
                    _why.Event(sec, cause, bp);
                }
            }
        }

        // The close's why: the cause of the sector that moved most (its piece of news comes first), then of the large companies that moved most up and down.
        private void WhyNews(int day, int sector, int sectorBp, Stock up, int upBp, Stock down, int downBp)
        {
            WhyPick sectorPick = default;
            bool given = false;
            if (sector >= 0 && Math.Abs(sectorBp) >= MarketWhy.SectorLeastBp)
            {
                given = MarketWhy.ForSector(State, _why, State.Env, _prevEnv, _hasPrev, sector, sectorBp, day, out sectorPick);
                if (given)
                {
                    Say(MarketWhy.News(day, 0, sector, sectorBp, sectorPick));
                }
            }
            WhyOfStock(day, up, upBp, given, sectorPick, sector, sectorBp);
            WhyOfStock(day, down, downBp, given, sectorPick, sector, sectorBp);
        }

        private void WhyOfStock(int day, Stock k, int bp, bool given, in WhyPick sectorPick, int sector, int sectorBp)
        {
            if (k == null || k.Size != MarketSize.Large || Math.Abs(bp) < MarketWhy.CompanyLeastBp)
            {
                return;
            }
            bool same = given && (int)k.Sector == sector;
            if (MarketWhy.ForStock(State, k, bp, day, same, sectorPick, sectorBp, out WhyPick pick))
            {
                Say(MarketWhy.News(day, k.Id, (int)k.Sector, bp, pick));
            }
        }

        // ------------------------------------------------------------------------------------------------ the size segments

        /// <summary>
        /// After the companies' own lives at a close: a company not yet reviewed in this quarter of the game is held to the bands by its market value, and one whose segment
        /// changes is put on the tape. Decided only where this close is played for the first time on a market of its own; everywhere else the tape says it.
        /// </summary>
        private void ExtrasSegments(int day)
        {
            if (!ExtrasOn || State.Following || Replaying)
            {
                return;
            }
            int quarter = MarketCatalogue.QuarterOf(day, Year);
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.BidCents > 0 || !MarketSegments.Due(s.SizeQuarter, quarter) || (s.Own != null && s.Own.Firm))
                {
                    continue;
                }
                MarketSize next = MarketSegments.Review(s.Size, s.MarketValue, State.Env.PriceLevel);
                if (next != s.Size)
                {
                    Event(MarketInputs.EventSegment, s.Id, (int)next);
                }
                else
                {
                    s.SizeQuarter = quarter;   // kept where it was: nothing for the tape, nothing to replay
                }
            }
        }

        // A segment change from the tape, applied at the next moment. The price stays exactly where it closed: what the new segment's premium and risks are worth goes into the
        // outlook, as the close's own settling does with a day's news (Settle), so a company is re-rated in what is expected of it and never in its price.
        private void SegmentEvent(in MarketEvent e)
        {
            Stock s = State.Stock(e.A);
            if (s == null || !s.Listed || e.B < 0 || e.B > (int)MarketSize.Small || (s.Own != null && s.Own.Firm))
            {
                return;
            }
            var next = (MarketSize)e.B;
            s.SizeQuarter = MarketCatalogue.QuarterOf(State.Day, Year);
            if (next == s.Size)
            {
                return;
            }
            MarketSize was = s.Size;
            s.Size = next;
            s.SizeDay = State.Day;
            if (s.BidCents <= 0)
            {
                Settle(s, State.Day);
            }
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Segment, Stock = s.Id, A = (int)next, B = (int)was, C = (int)Math.Min(int.MaxValue / 2, s.MarketValue / 1000000.0) });
        }
    }
}
