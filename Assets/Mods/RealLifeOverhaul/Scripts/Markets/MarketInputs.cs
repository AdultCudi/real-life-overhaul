using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What everything in the city does to the market: the tables, by sector. Game-free. A day's environment
    /// (<see cref="MarketEnv"/>) moves companies' margins and growth (their business) and the market's mood; an event
    /// during the day (a fire, a crime, a policy changed: <see cref="MarketEvent"/>) moves prices at once.
    ///
    /// Basis points (bp) are hundredths of a per cent. A margin effect is on a company's operating margin and lasts
    /// while its cause lasts (the market prices it the moment it is known); a "kick" is a one-off move of the price, the
    /// market's mood, which fades over time. Adding a source of news: one more Event kind, its row in
    /// <see cref="EventKicks"/>, and one line in Wiring/MarketLinks that hands it over.
    /// </summary>
    public static class MarketInputs
    {
        private const int Sectors = (int)MarketSector.Count;

        // ---------------------------------------------------------------- the game's trades the player competes in

        /// <summary>The parts of the game's business type ids a listed company can share a trade with (CompanyDef.Trade).</summary>
        public static readonly string[] Trades =
        {
            "supermarket", "fruitandvegetable", "liquorstore", "coffeeshop", "fastfood", "clothing", "electronics", "appliance", "furniture", "jewelry", "giftshop",
            "bookstore", "florist", "hairdresser", "gym", "nightclub", "cinema", "theater", "casino", "lawfirm", "webdevelopment", "graphicdesign", "marketingagency",
            "recruitment", "cardealership", "gasstation", "movingservice", "warehouse", "wholesale", "importexport", "factory", "officesupply", "travelagency",
            "eventplanning", "hospital", "clinic", "interiorinstallation", "truckgarage", "bank"
        };

        /// <summary>The trade of one of the game's business types, by its id; -1 for one no listed company is in.</summary>
        public static int TradeOf(string businessType)
        {
            if (string.IsNullOrEmpty(businessType))
            {
                return -1;
            }
            for (int i = 0; i < Trades.Length; i++)
            {
                if (businessType.IndexOf(Trades[i], StringComparison.Ordinal) >= 0)
                {
                    return i;
                }
            }
            return -1;
        }

        public const double PlayerShareTaken = 0.5;      // of the player's takings in a trade, what is taken from the listed companies in it
        public const double CompetitionMost = 0.25;      // of one company's turnover, at most

        // ---------------------------------------------------------------- supply: a kind of goods dearer or cheaper

        // Margin bp for each tenth the kind's price is above the usual (a glut: the other way), by kind and sector.
        private static readonly int[][] SupplyMargin = Table(
            new[] { G(MarketSector.Groceries, -25), G(MarketSector.Restaurants, -30), G(MarketSector.Goods, -10) },                              // fresh produce
            new[] { G(MarketSector.Groceries, -30), G(MarketSector.Restaurants, -40), G(MarketSector.Goods, 15), G(MarketSector.Leisure, -10) }, // groceries and drink
            new[] { G(MarketSector.Stores, -25), G(MarketSector.Specialists, -30), G(MarketSector.Goods, 10) },                                  // clothing
            new[] { G(MarketSector.Specialists, -30), G(MarketSector.Stores, -10), G(MarketSector.Hardware, 25) },                              // electronics
            new[] { G(MarketSector.Stores, -20), G(MarketSector.Specialists, -20), G(MarketSector.Industry, 10), G(MarketSector.Construction, -10) },   // home and office
            new[] { G(MarketSector.Stores, -15), G(MarketSector.Logistics, 5) });                                                                // general goods

        /// <summary>A sector's margin effect, in bp, of the day's supply.</summary>
        public static int SupplyBp(in MarketEnv env, MarketSector sector)
        {
            if (env.Supply == null)
            {
                return 0;
            }
            int bp = 0;
            for (int k = 0; k < SupplyMargin.Length && k < env.Supply.Length; k++)
            {
                bp += (env.Supply[k] - 1000) * SupplyMargin[k][(int)sector] / 100;
            }
            return bp;
        }

        // ---------------------------------------------------------------- the council's policies

        // Margin bp of each lever tightened or raised by one step (relaxed or cut: the other way), in the order of Civic's
        // levers: policing, traffic, business tax, trade levy, grants, rents, parking, transit, cleaning, night life, permits,
        // events, wages. Business tax is the tax rate itself (MarketCatalogue.TaxPerStep), not a margin.
        private static readonly int[][] LeverMargin = Table(
            new[] { G(MarketSector.Services, 30), G(MarketSector.Stores, 10), G(MarketSector.Specialists, 10), G(MarketSector.Insurance, 15) },
            new[] { G(MarketSector.Transport, -20), G(MarketSector.Logistics, -25), G(MarketSector.Energy, -10), G(MarketSector.Stores, -10) },
            new Cell[0],
            new[] { G(MarketSector.Stores, -25), G(MarketSector.Specialists, -25), G(MarketSector.Logistics, -10), G(MarketSector.Groceries, -15) },
            new[] { G(MarketSector.Services, 10), G(MarketSector.Construction, 10), G(MarketSector.Software, 5) },
            new[] { G(MarketSector.Property, -60), G(MarketSector.Stores, 10), G(MarketSector.Restaurants, 10) },
            new[] { G(MarketSector.Stores, -15), G(MarketSector.Specialists, -15), G(MarketSector.Restaurants, -10), G(MarketSector.Transport, 15) },
            new[] { G(MarketSector.Transport, 40), G(MarketSector.Stores, 5) },
            new[] { G(MarketSector.Services, 25) },
            new[] { G(MarketSector.Leisure, -50), G(MarketSector.Restaurants, -20), G(MarketSector.Goods, -10) },
            new[] { G(MarketSector.Construction, -40), G(MarketSector.Property, -15) },
            new[] { G(MarketSector.Leisure, 20), G(MarketSector.Media, 15), G(MarketSector.Restaurants, 10) },
            new[] { G(MarketSector.Restaurants, -40), G(MarketSector.Groceries, -25), G(MarketSector.Stores, -25), G(MarketSector.Leisure, -25), G(MarketSector.Services, -30),
                G(MarketSector.Healthcare, -15) });

        public const int BusinessTaxLever = 2;

        /// <summary>A sector's margin effect, in bp, of the council's policies as they stand.</summary>
        public static int LeversBp(in MarketEnv env, MarketSector sector)
        {
            int bp = 0;
            for (int lever = 0; lever < LeverMargin.Length; lever++)
            {
                bp += env.Lever(lever) * LeverMargin[lever][(int)sector];
            }
            return bp;
        }

        /// <summary>The tax on profit as the council's business tax stands.</summary>
        public static double TaxRate(in MarketEnv env)
        {
            return MarketCatalogue.TaxRate + env.Lever(BusinessTaxLever) * MarketCatalogue.TaxPerStep;
        }

        // ---------------------------------------------------------------- the day: weather, the street, crime

        // One-off moves of a sector's prices, in bp, at the open of the day after: rain, a storm; a heat wave, a cold snap, ice;
        // the street event (as the city's economy counts them: a market, the farmers' market, a parade, a protest, a fun run,
        // a film shoot, fireworks).
        private static readonly int[][] SkyKick = Table(
            new Cell[0],
            new[] { G(MarketSector.Stores, -8), G(MarketSector.Leisure, -10), G(MarketSector.Restaurants, -5), G(MarketSector.Transport, 5) },
            new[] { G(MarketSector.Insurance, -40), G(MarketSector.Transport, -30), G(MarketSector.Stores, -25), G(MarketSector.Leisure, -25), G(MarketSector.Power, 15),
                G(MarketSector.Construction, 15) });
        private static readonly int[][] SpellKick = Table(
            new Cell[0],
            new[] { G(MarketSector.Power, 25), G(MarketSector.Goods, 15), G(MarketSector.Leisure, 10), G(MarketSector.Groceries, 5), G(MarketSector.Healthcare, 5) },
            new[] { G(MarketSector.Power, 30), G(MarketSector.Water, 20), G(MarketSector.Energy, 20), G(MarketSector.Stores, -12), G(MarketSector.Transport, -10) },
            new[] { G(MarketSector.Transport, -30), G(MarketSector.Insurance, -25), G(MarketSector.Logistics, -20), G(MarketSector.Healthcare, 5) });
        private static readonly int[][] StreetKick = Table(
            new Cell[0],
            new[] { G(MarketSector.Groceries, 8), G(MarketSector.Restaurants, 10) },
            new[] { G(MarketSector.Groceries, 10), G(MarketSector.Restaurants, 6) },
            new[] { G(MarketSector.Restaurants, 12), G(MarketSector.Leisure, 15), G(MarketSector.Media, 5) },
            new[] { G(MarketSector.Stores, -15), G(MarketSector.Specialists, -10), G(MarketSector.Services, 5) },
            new[] { G(MarketSector.Restaurants, 8), G(MarketSector.Leisure, 10), G(MarketSector.Healthcare, 3) },
            new[] { G(MarketSector.Media, 20), G(MarketSector.Leisure, 5) },
            new[] { G(MarketSector.Restaurants, 12), G(MarketSector.Leisure, 15) });

        /// <summary>A sector's one-off move at the open, in bp, from the day's weather, street event and what was reported yesterday.</summary>
        public static int DayKickBp(in MarketEnv env, MarketSector sector)
        {
            int s = (int)sector;
            int bp = Row(SkyKick, env.Sky)[s] + Row(SpellKick, env.Spell)[s] + Row(StreetKick, env.Event)[s];
            bp += Capped(env.Crimes * CrimeKick[s], 20) + Capped(env.BreakIns * BreakInKick[s], 25) + Capped(env.Fires * FireKick[s], 30) + Capped(env.Accidents * AccidentKick[s], 15);
            return bp;
        }

        private static readonly int[] CrimeKick = Row(G(MarketSector.Services, 3), G(MarketSector.Stores, -2), G(MarketSector.Specialists, -2), G(MarketSector.Insurance, -1));
        private static readonly int[] BreakInKick = Row(G(MarketSector.Services, 5), G(MarketSector.Insurance, -5), G(MarketSector.Stores, -2));
        private static readonly int[] FireKick = Row(G(MarketSector.Insurance, -10), G(MarketSector.Construction, 5));
        private static readonly int[] AccidentKick = Row(G(MarketSector.Insurance, -4), G(MarketSector.Transport, -2), G(MarketSector.Healthcare, 1));

        private static int Capped(int bp, int most)
        {
            return bp > most ? most : bp < -most ? -most : bp;
        }

        // ---------------------------------------------------------------- events during the day

        /// <summary>
        /// What happened (MarketEvent.Kind). Saved on the tape as a number: never renumber. Trade and Volume are a player's
        /// trade in a Going Public session, where every player's trades move the one city's prices (MarketEngine.FillNow):
        /// the lasting part of its move (A the company, B the move in millionths of the log price) and its shares (B).
        /// They are 9 and 10 (8 is Flare's buzz, from social media).
        /// </summary>
        public const int EventFire = 1, EventCrime = 2, EventBreakIn = 3, EventAccident = 4, EventPolicy = 5, EventElection = 6, EventStorm = 7, EventTrade = 9, EventVolume = 10,
            EventCount = 11;

        /// <summary>A trade's lasting move of a price, in the millionths the tape keeps it in.</summary>
        public static int TradeMicros(double move)
        {
            double micros = Math.Round(move * 1000000.0);
            return micros > 1000000.0 ? 1000000 : micros < -1000000.0 ? -1000000 : (int)micros;
        }

        // The move of a sector's prices, in bp, the moment the market hears of it (a policy: a step tightened, by lever, below).
        private static readonly int[][] EventKicks = Table(
            new Cell[0],
            new[] { G(MarketSector.Insurance, -12), G(MarketSector.Construction, 4) },
            new[] { G(MarketSector.Services, 3), G(MarketSector.Stores, -2), G(MarketSector.Insurance, -1) },
            new[] { G(MarketSector.Services, 5), G(MarketSector.Insurance, -5), G(MarketSector.Stores, -2) },
            new[] { G(MarketSector.Insurance, -4), G(MarketSector.Transport, -2) },
            new Cell[0],
            new Cell[0],
            new[] { G(MarketSector.Insurance, -25), G(MarketSector.Transport, -15), G(MarketSector.Stores, -10), G(MarketSector.Power, 8) });

        public const int ElectionKickBp = 30;            // the whole market, when an election is counted (the doubt is over)
        public const int FireTradeKickBp = -60;          // a listed company in the trade of a business that burned

        /// <summary>
        /// A sector's move, in bp, when the market hears of an event. A policy (its lever and the step it moved: A, B) has
        /// none of its own: it is part of the business at once, and the market prices it as that (MarketEngine).
        /// </summary>
        public static int EventBp(in MarketEvent e, MarketSector sector)
        {
            if (e.Kind == EventPolicy)
            {
                return 0;
            }
            if (e.Kind == EventElection)
            {
                return ElectionKickBp;
            }
            return e.Kind > 0 && e.Kind < EventKicks.Length ? EventKicks[e.Kind][(int)sector] : 0;
        }

        // ---------------------------------------------------------------- the districts, inflation

        /// <summary>How much a sector's turnover follows what the city's households earn (the districts' income), in per cent.</summary>
        public static readonly int[] IncomeShare = Row(G(MarketSector.Stores, 60), G(MarketSector.Specialists, 80), G(MarketSector.Restaurants, 70), G(MarketSector.Leisure, 90),
            G(MarketSector.Goods, 30), G(MarketSector.Groceries, 20), G(MarketSector.Property, 40), G(MarketSector.Banks, 30), G(MarketSector.Media, 40), G(MarketSector.Transport, 30));

        // ---------------------------------------------------------------- building the tables

        private struct Cell
        {
            public MarketSector Sector;
            public int Bp;
        }

        private static Cell G(MarketSector sector, int bp)
        {
            return new Cell { Sector = sector, Bp = bp };
        }

        private static int[] Row(params Cell[] cells)
        {
            var row = new int[Sectors];
            for (int i = 0; cells != null && i < cells.Length; i++)
            {
                row[(int)cells[i].Sector] += cells[i].Bp;
            }
            return row;
        }

        private static int[][] Table(params Cell[][] rows)
        {
            var table = new int[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                table[r] = Row(rows[r]);
            }
            return table;
        }

        private static int[] Row(int[][] table, int index)
        {
            return index > 0 && index < table.Length ? table[index] : table[0];
        }
    }
}
