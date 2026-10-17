using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy director: the day's rent, wages, takings and shops and services adjusted to the city's prices.</summary>
    public sealed partial class CityEconomyDirector
    {
        // The day that has ended: the game's own figures of that day, each adjusted by one transaction.
        private void Adjust(int day)
        {
            DayBooks books = CityGame.DayFigures(day);
            int costs = CityModel.Costs(_state), spend = CityModel.Takings(_state, _level);
            Settle(CityAdjust.Of(books.Rent.Total, costs), ref _state.CarryRent, CityGame.RentKey, day, costs, "rent", books.Rent);
            Settle(CityAdjust.Of(books.Wages.Total, costs), ref _state.CarryWages, CityGame.WagesKey, day, costs, "wages", books.Wages);
            Settle(-CityAdjust.Of(books.Takings.Total, spend), ref _state.CarryTakings, CityGame.TakingsKey, day, spend, "takings", books.Takings);
            Settle(CityAdjust.Of(books.Living.Total, costs), ref _state.CarryLiving, CityGame.LivingKey, day, costs, "the game's shops and services", books.Living);
            GameBooks.Refresh(day);   // the game made the day's statements before these lines were booked
        }

        /// <summary>What the game's own takings come to at today's prices, in thousandths (prices and what customers spend); 1000 while this is not running.</summary>
        public static int TakingsFactor
        {
            get
            {
                CityEconomyDirector d = Instance;
                int factor = d != null && d._running ? CityModel.Takings(d._state, d._level) : 1000;
                return Math.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet ? 1000 : factor;   // the quiet band, as CityAdjust.Of
            }
        }

        /// <summary>Tonight's rent with the city's adjustment of it and what was carried, as the next morning takes it; the game's own while this is not running.</summary>
        public static float RentWithCity(float rent)
        {
            CityEconomyDirector d = Instance;
            return d != null && d._running ? CityAdjust.With(rent, CityModel.Costs(d._state), d._state.CarryRent) : rent;
        }

        // A charge (positive: the player pays) is taken as far as the cash goes and the rest carried; a refund is paid.
        private void Settle(int charge, ref int carried, string key, int day, int factor, string what, DayLine line)
        {
            float figure = line.Total;
            int before = carried;
            int taken = CityAdjust.Take(charge, CityGame.Cash, ref carried);
            int booked = taken != 0 ? -BookShared(-taken, key, day, CityText.Change(factor), line) : 0;
            if (booked != taken)
            {
                carried = (int)Math.Min((long)carried + taken - booked, int.MaxValue / 2);   // the game refused a part: it is still owed
                taken = booked;
            }
            if (Verbose && (charge != 0 || before != 0))
            {
                Logger?.Info("Economy check (city, adjustment for day " + day + "): " + what + " " + figure.ToString("0") + " by the game, factor " + Factor(factor) + ", so "
                             + (charge >= 0 ? charge + " more to pay" : -charge + " back") + "; carried from before " + before + "; "
                             + (taken >= 0 ? taken + " taken" : -taken + " paid") + ", " + carried + " carried to tomorrow: "
                             + ((long)before + charge == (long)taken + carried ? "PASS" : "FAIL"));
            }
        }

        // Booked where the game's own figures were (a business, a home, a statement's category), shared out by them;
        // with nothing to share by, one line of the player's own. Returns what was booked.
        private static int BookShared(int amount, string key, int day, string percent, DayLine line)
        {
            float weight = 0f;
            for (int i = 0; i < line.Figures.Count; i++)
            {
                weight += line.Figures[i];
            }
            if (line.Figures.Count == 0 || Math.Abs(weight) < 0.5f)
            {
                return CityGame.Book(amount, key, day, percent) ? amount : 0;
            }
            int[] parts = CityAdjust.Split(amount, line.Figures.ToArray());
            return CityAdjust.BookParts(parts, (i, part) => CityGame.Book(part, key, day, percent, line.AddressAt(i), line.CategoryAt(i), line.DeductedAt(i)));
        }
    }
}
