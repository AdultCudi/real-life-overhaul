using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: a player's own company whose price has fallen far under its year's high says why. The price of a listed company follows the books but also the city, its sector
    /// and a mood that fades over years (<see cref="Stock.Sent"/>), so a fall of a third can come with results that moved a tenth. The close measures it from the closes already kept
    /// (<see cref="OwnFallRules"/>) and, the day the price first crosses under, puts a line beside the news (never on the tape or in the save: every machine measures the same). Costs a
    /// comparison a close while no company of the player's is listed.
    /// </summary>
    public sealed partial class MarketEngine
    {
        // At a close, after the day's closes are kept: the first day a player's company stands under its high by the limit, once a quarter at most.
        private void OwnFalls(int day)
        {
            if (Replaying)
            {
                return;
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.Own == null || s.Own.Firm || s.Last <= 0 || s.BidCents > 0)
                {
                    continue;
                }
                OwnListing o = s.Own;
                int yesterday = OwnFallRules.CloseBefore(s.Daily, day);
                if (yesterday <= 0 || (o.FallDay > 0 && day - o.FallDay < OwnFallRules.GapDays(Year)))
                {
                    continue;
                }
                int window = OwnFallRules.WindowDays(Year);
                if (OwnFallRules.Drawdown(s.Daily, day, s.Last, window, out int _, out int _) < OwnFallRules.FallBp
                    || OwnFallRules.Drawdown(s.Daily, day - 1, yesterday, window, out int _, out int _) >= OwnFallRules.FallBp)
                {
                    continue;   // not under the limit, or it was already under yesterday: only the crossing is news
                }
                OwnFall f = OwnFallRules.Measure(State, s, day, s.Last, State.Env);
                if (!f.Fell)
                {
                    continue;
                }
                o.FallDay = day;
                o.Fall = f;
                SayDerived(new MarketNews { Day = day, Kind = DerivedNews.OwnFall, Stock = s.Id, A = (int)f.Cause, B = f.FallBp });
            }
        }
    }
}
