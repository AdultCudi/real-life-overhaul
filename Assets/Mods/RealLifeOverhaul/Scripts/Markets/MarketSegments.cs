using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The size segment of a listed company by its market value: large, mid or small, against the catalogue's own bands, reviewed once a quarter with a margin so a
    /// company near a band's edge does not flicker between two. The catalogue rows give each company's starting segment; the review keeps it where the company's
    /// value puts it. Every rule of the market that reads a company's size (<see cref="Stock.Size"/>: its premium, the crises, bids and bots that look at it, the
    /// barometer of the large) follows the segment from the moment it changes. Game-free; checked offline (EconomyCheck, --segments). COST: a few comparisons a company a quarter.
    /// </summary>
    public static class MarketSegments
    {
        /// <summary>
        /// The least market value of a large company, in millions at the start's prices (the catalogue's own measure: every company's turnover is written in them). The bands that keep
        /// most of the catalogue's companies in the segment their rows give them (EconomyCheck, --market-why searches for them: 85 in a hundred stay, the rows' own sizes overlap).
        /// </summary>
        public const int LargeFromM = 7250;

        /// <summary>The least market value of a mid company, in millions at the start's prices: the pair of bands that moves fewest of the catalogue's rows (see above).</summary>
        public const int MidFromM = 1450;

        /// <summary>
        /// How far under a band a company must fall to leave the segment above it, in basis points of the band: a company joins a segment at its band, and leaves it only a
        /// fifth under, so a price that crosses the edge and back each week keeps its segment.
        /// </summary>
        public const int HoldBp = 2000;

        /// <summary>The bands at today's prices: the start's grown with the price level the market's turnovers are (a tenth of a point a unit, a thousand at the start).</summary>
        public static double Band(int millions, int priceLevel)
        {
            return millions * 1000000.0 * Math.Max(100, priceLevel) / 1000.0;
        }

        /// <summary>The segment a market value falls in with no history: the first quarter of a company the catalogue has no row for.</summary>
        public static MarketSize Of(double value, int priceLevel)
        {
            return value >= Band(LargeFromM, priceLevel) ? MarketSize.Large : value >= Band(MidFromM, priceLevel) ? MarketSize.Mid : MarketSize.Small;
        }

        /// <summary>
        /// The segment after a review: a company moves up when its value reaches the band above and down only when it is <see cref="HoldBp"/> under the band of the segment it is in
        /// (it may then skip a segment). With a value in between it stays.
        /// </summary>
        public static MarketSize Review(MarketSize now, double value, int priceLevel)
        {
            double large = Band(LargeFromM, priceLevel), mid = Band(MidFromM, priceLevel);
            double largeStay = large * (10000 - HoldBp) / 10000.0, midStay = mid * (10000 - HoldBp) / 10000.0;
            if (value >= large)
            {
                return MarketSize.Large;
            }
            if (now == MarketSize.Large)
            {
                return value >= largeStay ? MarketSize.Large : value >= midStay ? MarketSize.Mid : MarketSize.Small;
            }
            if (value >= mid)
            {
                return MarketSize.Mid;
            }
            return now == MarketSize.Mid && value >= midStay ? MarketSize.Mid : MarketSize.Small;
        }

        /// <summary>True if a company is due its review at a close: it has not been reviewed in this quarter of the game (a new quarter, or one never reviewed).</summary>
        public static bool Due(int reviewedQuarter, int quarter)
        {
            return reviewedQuarter != quarter;
        }

        /// <summary>The share of a band a company's value stands at, in basis points: the page's "how near the next segment".</summary>
        public static int NextBandBp(MarketSize now, double value, int priceLevel)
        {
            if (now == MarketSize.Large)
            {
                return 10000;
            }
            double band = Band(now == MarketSize.Mid ? LargeFromM : MidFromM, priceLevel);
            return (int)Math.Max(0, Math.Min(10000, value / band * 10000.0));
        }
    }
}
