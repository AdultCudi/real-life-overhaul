using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

// The day's move (Scripts/Markets: MarketHistory.DayMoveBp and DayBase, MarketLife.DayBases): what the Companies tab, the
// watch list, a company's page and the ticket show as a company's move today, and the Market tab as the index's and each
// sector's. In a session it is the price against yesterday's close; after the close, overnight and over a weekend it is
// the session just ended, so a company that moved never shows 0.00% while its sector shows the move; the companies'
// moves add up to their sector's; a save read back (one written by 0.15.1, which moved every close at the close, too)
// shows the same; and two machines agree.
internal static partial class Program
{
    private static void MarketDayMoves()
    {
        MarketEngine m = NewMarket(41, 60);
        int day = TradingOnOrAfter(6);
        MarketState s = m.State;

        // In a session: the move is from yesterday's close, for every company, the index and each sector.
        RunTo(m, day, MarketCatalogue.PhaseOpen + 40);
        int listed = 0, moved = 0, wrong = 0;
        for (int i = 0; i < s.Stocks.Count; i++)
        {
            Stock k = s.Stocks[i];
            if (!k.Listed || k.Daily.Count < 2)
            {
                continue;
            }
            listed++;
            int was = k.Daily[k.Daily.Count - 1].C;
            wrong += k.PrevClose != was || MarketHistory.DayMoveBp(k) != MarketHistory.MoveBp(k.Last, was) ? 1 : 0;
            moved += k.Last != was && MarketHistory.DayMoveBp(k) != 0 ? 1 : 0;
        }
        Check(listed > 100 && wrong == 0, "day's move: in a session every company's is from yesterday's close (" + wrong + " of " + listed + " not)");
        Check(moved * 10 >= listed * 9, "day's move: in a session nearly every company has moved (" + moved + " of " + listed + ")");
        Check(s.IndexPrevClose == s.IndexDaily[s.IndexDaily.Count - 1].C && MarketHistory.IndexDayMoveBp(s) != 0, "day's move: and the index's, from its close yesterday");
        Check(SectorsFrom(s, true) == 0, "day's move: and every sector's");

        // After the close: the session just ended, not 0 for all (until 0.15.1 every company showed 0.00% from here to the open).
        RunTo(m, day, MarketCatalogue.PhaseClose);
        DayMovesShut(s, "after the close");
        int[] closed = Moves(s);
        int indexClosed = MarketHistory.IndexDayMoveBp(s);

        // The night and, after the Friday, a weekend: the same figures until the next open.
        int friday = day;
        while (MarketCatalogue.Weekday(friday) != 4)
        {
            friday++;
        }
        RunTo(m, friday, MarketCatalogue.PhaseClose);
        DayMovesShut(s, "after a Friday's close");
        int[] fridayMoves = Moves(s);
        RunTo(m, friday + 2, MarketCatalogue.PhaseClose);
        Check(Same(Moves(s), fridayMoves) && !MarketCatalogue.TradingDay(friday + 2), "day's move: over the weekend every company still shows the Friday's move");
        int monday = TradingOnOrAfter(friday + 1);
        RunTo(m, monday, MarketCatalogue.PhaseResults);
        DayMovesShut(s, "on the Monday before the open");

        // At the open: from the last close (the Friday's) again.
        RunTo(m, monday, MarketCatalogue.PhaseOpen);
        int fromFriday = 0, others = 0;
        for (int i = 0; i < s.Stocks.Count; i++)
        {
            Stock k = s.Stocks[i];
            if (k.Listed && k.Daily.Count >= 1 && k.ListedDay < monday)
            {
                others++;
                fromFriday += k.PrevClose == k.Daily[k.Daily.Count - 1].C && k.Daily[k.Daily.Count - 1].Day == friday ? 1 : 0;
            }
        }
        Check(others > 100 && fromFriday == others, "day's move: at the Monday's open every company's is from the Friday's close (" + fromFriday + " of " + others + ")");
        Check(s.IndexPrevClose == s.IndexDaily[s.IndexDaily.Count - 1].C && SectorsFrom(s, true) == 0, "day's move: and the index's and every sector's");

        // A save read back, at the close and in a session, shows the same; one written by 0.15.1 after a close (every
        // close moved to the last price) shows the session's move at once.
        MarketEngine b = NewMarket(41, 60);
        RunTo(b, day, MarketCatalogue.PhaseClose);
        MarketState read = MarketStore.Read(MarketStore.Write(b.State), out _);
        Check(Same(Moves(read), closed) && MarketHistory.IndexDayMoveBp(read) == indexClosed, "day's move: a save read back after the close shows the same moves");
        MarketState old = MarketStore.Read(MarketStore.Write(b.State), out _);
        for (int i = 0; i < old.Stocks.Count; i++)
        {
            old.Stocks[i].PrevClose = old.Stocks[i].Last;
        }
        old.IndexPrevClose = old.IndexLast;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            old.SectorPrevClose[sec] = old.SectorLast[sec];
        }
        int zero = 0;
        int[] before = Moves(old);
        for (int i = 0; i < before.Length; i++)
        {
            zero += before[i] == 0 ? 1 : 0;
        }
        MarketState mended = MarketStore.Read(MarketStore.Write(old), out _);
        Check(zero == before.Length && Same(Moves(mended), closed) && MarketHistory.IndexDayMoveBp(mended) == indexClosed && SectorsFrom(mended, false) == 0,
            "day's move: a save of 0.15.1's after the close (every move 0) reads with the session's moves");
        RunTo(b, TradingOnOrAfter(day + 1), MarketCatalogue.PhaseOpen + 20);
        MarketState inSession = MarketStore.Read(MarketStore.Write(b.State), out _);
        Check(inSession.InSession && Same(Moves(inSession), Moves(b.State)), "day's move: a save read back in a session shows the same moves");

        // Two machines: the same moves, step by step.
        MarketEngine c1 = NewMarket(77, 60), c2 = NewMarket(77, 60);
        RunTo(c1, day, MarketCatalogue.PhaseClose);
        RunTo(c2, day, MarketCatalogue.PhaseClose);
        Check(Same(Moves(c1.State), Moves(c2.State)) && SectorsFrom(c2.State, false) == 0, "day's move: the same on two machines");

        // The rule itself.
        Check(MarketHistory.MoveBp(10100, 10000) == 100 && MarketHistory.MoveBp(9900, 10000) == -100 && MarketHistory.MoveBp(500, 0) == 0, "day's move: basis points against the close, 0 with no close");
        var oneDay = new List<Candle> { new Candle(3, 100, 110, 90, 105, 1) };
        Check(MarketHistory.DayBase(oneDay, true, 77) == 105 && MarketHistory.DayBase(oneDay, false, 77) == 77 && MarketHistory.DayBase(new List<int>(), true, 5) == 5,
            "day's move: a company listed at the last session keeps the price it was listed at after that session's close");
    }

    // Between sessions every company's move is the last session's, from the close before it; nearly all have moved; the
    // index's and each sector's agree; and the companies' moves at the close, weighed by what each was worth, add up to their sector's.
    private static void DayMovesShut(MarketState s, string when)
    {
        int listed = 0, moved = 0, wrong = 0;
        var worth = new double[(int)MarketSector.Count];
        var gained = new double[(int)MarketSector.Count];
        for (int i = 0; i < s.Stocks.Count; i++)
        {
            Stock k = s.Stocks[i];
            if (!k.Listed || k.Daily.Count < 2)
            {
                continue;
            }
            listed++;
            int last = k.Daily[k.Daily.Count - 1].C, was = k.Daily[k.Daily.Count - 2].C;
            wrong += k.PrevClose != was || (k.Last == last && MarketHistory.DayMoveBp(k) != MarketHistory.MoveBp(last, was)) ? 1 : 0;
            moved += MarketHistory.DayMoveBp(k) != 0 ? 1 : 0;
            worth[(int)k.Sector] += k.Shares * (double)was;
            gained[(int)k.Sector] += k.Shares * (double)(last - was);
        }
        Check(listed > 100 && wrong == 0, "day's move: " + when + " every company's is the last session's (" + wrong + " of " + listed + " not)");
        Check(moved * 10 >= listed * 9, "day's move: " + when + " nearly every company shows a move, not 0.00% (" + moved + " of " + listed + ")");
        int n = s.IndexDaily.Count;
        Check(n >= 2 && s.IndexPrevClose == s.IndexDaily[n - 2].C && MarketHistory.IndexDayMoveBp(s) == MarketHistory.MoveBp(s.IndexDaily[n - 1].C, s.IndexDaily[n - 2].C)
              && MarketHistory.IndexDayMoveBp(s) != 0, "day's move: " + when + " the index shows the last session's move");
        Check(SectorsFrom(s, false) == 0, "day's move: " + when + " so does every sector");
        int apart = 0, sectors = 0;
        string worst = string.Empty;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            if (worth[sec] <= 0.0 || s.SectorPrevClose[sec] <= 0)
            {
                continue;
            }
            sectors++;
            int companies = (int)Math.Round(gained[sec] / worth[sec] * 10000.0);
            int sector = MarketHistory.SectorDayMoveBp(s, sec);
            if (Math.Abs(companies - sector) > 3 || (Math.Abs(sector) >= 10 && Math.Sign(companies) != Math.Sign(sector)))
            {
                apart++;
                worst = MarketCatalogue.SectorKeys[sec] + ": the companies " + companies + " bp, the sector " + sector + " bp";
            }
        }
        Check(sectors > 10 && apart == 0, "day's move: " + when + " the companies' moves add up to their sector's (" + apart + " of " + sectors + " apart" + (worst.Length > 0 ? "; " + worst : string.Empty) + ")");
    }

    // How many sectors' moves are not from the close the rule says (in a session the last, between sessions the one before).
    private static int SectorsFrom(MarketState s, bool session)
    {
        int wrong = 0;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            List<int> closes = s.SectorDaily[sec];
            int at = closes.Count - (session ? 1 : 2);
            if (at < 0 || closes[at] <= 0 || s.SectorLast[sec] <= 0)
            {
                continue;
            }
            wrong += s.SectorPrevClose[sec] != closes[at] || MarketHistory.SectorDayMoveBp(s, sec) != MarketHistory.MoveBp(s.SectorLast[sec], closes[at]) ? 1 : 0;
        }
        return wrong;
    }

    private static int[] Moves(MarketState s)
    {
        var moves = new int[s.Stocks.Count];
        for (int i = 0; i < s.Stocks.Count; i++)
        {
            moves[i] = s.Stocks[i].Listed ? MarketHistory.DayMoveBp(s.Stocks[i]) : 0;
        }
        return moves;
    }
}
