using System;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a business's value is worked out from, read from the game and the rest of the mod by the director.</summary>
    public struct SaleInputs
    {
        public float AvgProfit7;         // mean daily profit over the last seven days of statements (what investors value it by)
        public int DaysOnRecord;         // days with statements, of the last fourteen
        public int Sector;               // Ventures' sector of its business type
        public int Income;               // its district's income level, 60 to 140
        public int Phase;                // the economy: -1 a slowdown, 0 steady, 1 a boom
        public int Fixtures;             // its stock and furniture at the game's own resale price
        public int Deposit;              // the deposit the landlord holds
        public int Investors;            // what buying its investors out costs today
        public int Loans;                // what is owed on business loans secured on it
    }

    /// <summary>A business's value, line by line as the page shows it.</summary>
    public struct SaleBreakdown
    {
        public int Base;                 // so many days of its recent profit: the investors' valuation
        public int TypePct, DistrictPct, ClimatePct, HistoryPct;
        public int Goodwill;             // the base by those four
        public int Fixtures;
        public int Value;                // goodwill and fixtures
        public int Investors, Loans;     // paid out of the proceeds first
        public int Net;                  // what a sale at its value would leave
        public int Deposit;              // comes back from the landlord beside the price
    }

    /// <summary>The money of a sale on the day it completes.</summary>
    public struct SaleMoney
    {
        public int Now;                  // the buyer pays this on the day
        public int Part, PartsLeft;      // ...and this much each week after, so many times
        public int Investors, Loans;     // paid out of it at once
        public int Short;                // what the seller's cash would still not cover; above nothing the sale cannot complete
        public int Left;                 // the day's payment after investors and loans (below nothing: taken from the seller's cash)
        public int Gain;                 // the price above the worth of the stock and furniture: the capital gain
    }

    /// <summary>How a buyer answered a counter.</summary>
    public enum CounterResult { Accepted, HalfWay, Refused, Walked }

    /// <summary>
    /// Selling a business, game-free and deterministic: every drawn figure comes from the save's seed. The asking price
    /// sets how often offers come and how many; an offer follows it part of the way, never above it or that buyer's ceiling.
    /// </summary>
    public static class SaleRules
    {
        private const int PurposeKind = 21, PurposeJitter = 22, PurposeCeiling = 23, PurposeDays = 24, PurposeWalk = 25, PurposeNone = 26, PurposeFace = 27, PurposeParts = 28;

        public static int ClimatePct(int phase)
        {
            return phase > 0 ? ServicesCatalogue.ClimateBoomPct : phase < 0 ? ServicesCatalogue.ClimateSlowPct : 100;
        }

        public static int DistrictPct(int income)
        {
            return ServicesCatalogue.Clamp(100 + (income - 100) * ServicesCatalogue.DistrictSwingPct / 100, ServicesCatalogue.DistrictLeastPct, ServicesCatalogue.DistrictMostPct);
        }

        public static SaleBreakdown Value(in SaleInputs i)
        {
            var b = new SaleBreakdown
            {
                Base = VenturesCatalogue.Share(i.AvgProfit7 * VenturesCatalogue.OfferValueDays, 10000),
                TypePct = ServicesCatalogue.TypePct[ServicesCatalogue.Clamp(i.Sector, 0, ServicesCatalogue.TypePct.Length - 1)],
                DistrictPct = DistrictPct(i.Income),
                ClimatePct = ClimatePct(i.Phase),
                HistoryPct = ServicesCatalogue.Clamp(i.DaysOnRecord, 0, ServicesCatalogue.HistoryDays) * 100 / ServicesCatalogue.HistoryDays,
                Fixtures = Math.Max(0, i.Fixtures),
                Investors = Math.Max(0, i.Investors),
                Loans = Math.Max(0, i.Loans),
                Deposit = Math.Max(0, i.Deposit)
            };
            long goodwill = b.Base;
            goodwill = goodwill * b.TypePct / 100 * b.DistrictPct / 100 * b.ClimatePct / 100 * b.HistoryPct / 100;
            b.Goodwill = ServicesCatalogue.Round10(goodwill);
            b.Value = (int)Math.Min(int.MaxValue / 4, (long)b.Goodwill + b.Fixtures);
            b.Net = b.Value - b.Investors - b.Loans;
            return b;
        }

        public static int Asking(int value, int step)
        {
            return ServicesCatalogue.Round10(ServicesCatalogue.Pct(Math.Max(0, value), ServicesCatalogue.AskingPct[ServicesCatalogue.Clamp(step, 0, ServicesCatalogue.AskingPct.Length - 1)]));
        }

        public static int Quick(int value) => ServicesCatalogue.Round10(ServicesCatalogue.Pct(Math.Max(0, value), ServicesCatalogue.QuickPct));

        /// <summary>Puts a business on the market at a step of the asking price, or changes its step. The first offer can come the next day.</summary>
        public static SaleListing List(ServicesState s, string place, string name, int value, int step, int day)
        {
            SaleListing l = s.Listing(place);
            if (l == null)
            {
                if (s.Listings.Count >= ServicesCatalogue.ListingsMost)
                {
                    return null;
                }
                l = new SaleListing { Place = place, ListedDay = day, NextOfferDay = day + 1 };
                s.Listings.Add(l);
            }
            l.Name = name ?? string.Empty;
            l.Step = ServicesCatalogue.Clamp(step, 0, ServicesCatalogue.AskingPct.Length - 1);
            l.Value = Math.Max(0, value);
            l.Asking = Asking(value, l.Step);
            return l;
        }

        public static bool OfferDue(SaleListing l, int day)
        {
            return l != null && l.Value > 0 && day >= l.NextOfferDay && l.OffersMade < ServicesCatalogue.AskingOffersMost[l.Step];
        }

        public static bool Expired(SaleListing l, int day) => day - l.ListedDay > ServicesCatalogue.ListingDays;

        /// <summary>The listing's next offer; null if this turn brings none, and the turn is used up either way.</summary>
        public static SaleOffer Offer(ServicesState s, SaleListing l, int day)
        {
            int n = l.OffersMade, place = ServicesCatalogue.Number(l.Place);
            int asked = ServicesCatalogue.AskingPct[l.Step];
            l.OffersMade++;
            l.NextOfferDay = day + ServicesCatalogue.AskingGapDays[l.Step];
            if (l.Value <= 0 || s.SaleOffers.Count >= ServicesCatalogue.OffersKept)
            {
                return null;
            }
            if (asked >= ServicesCatalogue.AskingPct[ServicesCatalogue.AskingPct.Length - 1] && ServicesCatalogue.Roll(s.Seed, place, n, PurposeNone, 100) < 50)
            {
                return null;
            }
            int kind = (ServicesCatalogue.Roll(s.Seed, place, l.ListedDay, PurposeKind, (int)BuyerKind.Count) + n) % (int)BuyerKind.Count;
            int pull = (asked - 100) * ServicesCatalogue.AskingPullPct / 100;
            int jitter = ServicesCatalogue.Roll(s.Seed, place, n, PurposeJitter, 2 * ServicesCatalogue.JitterPct + 1) - ServicesCatalogue.JitterPct;
            int price = Math.Min(l.Asking, ServicesCatalogue.Round10(ServicesCatalogue.Pct(l.Value, ServicesCatalogue.BuyerPct[kind] + pull + jitter)));
            if (price <= 0)
            {
                return null;
            }
            int own = ServicesCatalogue.Roll(s.Seed, place, n, PurposeCeiling, ServicesCatalogue.CeilingSpreadPct + 1);
            int parts = ServicesCatalogue.BuyerInstalments[kind];
            if (kind == (int)BuyerKind.Chain && ServicesCatalogue.Roll(s.Seed, place, n, PurposeParts, 2) == 0)
            {
                parts = 0;   // a chain pays at once half the time
            }
            var offer = new SaleOffer
            {
                Id = s.NextId++, Place = l.Place, Buyer = kind, Price = price,
                Ceiling = Math.Max(price, ServicesCatalogue.Round10(ServicesCatalogue.Pct(l.Value, ServicesCatalogue.BuyerCeilingPct[kind] + pull + own))),
                KeepsStaff = ServicesCatalogue.BuyerKeepsStaff[kind], Parts = parts, ClauseDays = ServicesCatalogue.BuyerClauseDays[kind],
                Deadline = day + ServicesCatalogue.OfferDaysLeast + ServicesCatalogue.Roll(s.Seed, place, n, PurposeDays, ServicesCatalogue.OfferDaysMost - ServicesCatalogue.OfferDaysLeast + 1),
                Face = ServicesCatalogue.Roll(s.Seed, place, n, PurposeFace, 3)
            };
            s.SaleOffers.Add(offer);
            return offer;
        }

        /// <summary>Takes away offers whose deadline has passed. Returns how many.</summary>
        public static int ExpireOffers(ServicesState s, int day)
        {
            int gone = 0;
            for (int i = s.SaleOffers.Count - 1; i >= 0; i--)
            {
                if (day > s.SaleOffers[i].Deadline)
                {
                    s.SaleOffers.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        // What a counter asks: a tenth more, the asking price at most, never under the offer.
        private static int CounterWant(SaleOffer o, int asking)
        {
            return Math.Min(Math.Max(asking, o.Price), ServicesCatalogue.Round10(ServicesCatalogue.Pct(o.Price, 100 + ServicesCatalogue.CounterStepPct)));
        }

        /// <summary>Whether an offer may be countered: once, and only while a counter would ask for more than it gives.</summary>
        public static bool CanCounter(SaleOffer o, int asking)
        {
            return o != null && !o.Countered && CounterWant(o, asking) > o.Price;
        }

        /// <summary>The one counter an offer allows: a tenth more, the asking price at most; met, met half way, refused, or the buyer walks away.</summary>
        public static CounterResult Counter(ServicesState s, SaleOffer o, int asking)
        {
            if (!CanCounter(o, asking))
            {
                return CounterResult.Refused;   // nothing to ask for: the offer stands as it was
            }
            o.Countered = true;
            int want = CounterWant(o, asking);
            int half = ServicesCatalogue.Round10(((long)o.Price + want) / 2);
            if (want > o.Price && want <= o.Ceiling)
            {
                o.Price = want;
                return CounterResult.Accepted;
            }
            if (half > o.Price && half <= o.Ceiling)
            {
                o.Price = half;
                return CounterResult.HalfWay;
            }
            if (ServicesCatalogue.Roll(s.Seed, o.Id, o.Price, PurposeWalk, 100) < ServicesCatalogue.CounterWalkPct)
            {
                s.SaleOffers.Remove(o);
                return CounterResult.Walked;
            }
            return CounterResult.Refused;
        }

        /// <summary>The money on the day: paid now and later, owed to investors and lenders at once, and whether the cash covers the rest.</summary>
        public static SaleMoney Settle(int price, int parts, in SaleBreakdown b, int cash)
        {
            var m = new SaleMoney { Investors = b.Investors, Loans = b.Loans };
            price = Math.Max(0, price);
            if (parts <= 1)
            {
                m.Now = price;
            }
            else
            {
                m.Part = price / parts;
                m.PartsLeft = parts - 1;
                m.Now = price - m.Part * m.PartsLeft;   // the first part carries what does not divide
            }
            long owed = (long)m.Investors + m.Loans;
            m.Left = (int)Math.Max(int.MinValue / 2, m.Now - owed);
            m.Short = (int)Math.Max(0, owed - m.Now - Math.Max(0, cash));
            m.Gain = Math.Max(0, price - b.Fixtures);
            return m;
        }

        /// <summary>How many parts a price is paid in: at once while as many prices as are kept are still being paid.</summary>
        public static int Parts(ServicesState s, int parts)
        {
            return s.Dues.Count >= ServicesCatalogue.DuesMost ? Math.Min(parts, 1) : parts;   // Due would drop the oldest, whose parts would never come
        }

        /// <summary>The parts of a price still to come; null if it was paid at once.</summary>
        public static SaleDue Due(ServicesState s, string name, int buyer, in SaleMoney m, int day)
        {
            if (m.PartsLeft <= 0 || m.Part <= 0)
            {
                return null;
            }
            var due = new SaleDue { Name = name ?? string.Empty, Buyer = buyer, Part = m.Part, Left = m.PartsLeft, NextDay = day + ServicesCatalogue.InstalmentDays };
            if (s.Dues.Count >= ServicesCatalogue.DuesMost)
            {
                s.Dues.RemoveAt(0);
            }
            s.Dues.Add(due);
            return due;
        }

        /// <summary>A part that falls due today: its sum, or nothing. A part missed while the feature was off is paid on the first day after.</summary>
        public static int TakeDue(SaleDue d, int day)
        {
            if (d == null || d.Left <= 0 || day < d.NextDay)
            {
                return 0;
            }
            d.Left--;
            d.NextDay = Math.Max(d.NextDay + ServicesCatalogue.InstalmentDays, day + 1);
            return d.Part;
        }

        /// <summary>Notes a sale for the tax return; the oldest note goes when there are too many.</summary>
        public static SaleRecord Record(ServicesState s, string name, int price, int gain, int day)
        {
            var record = new SaleRecord { Day = day, Name = name ?? string.Empty, Price = Math.Max(0, price), Gain = Math.Max(0, gain) };
            if (s.Records.Count >= ServicesCatalogue.RecordsKept)
            {
                s.Records.RemoveAt(0);
            }
            s.Records.Add(record);
            return record;
        }

        /// <summary>The capital gains of sales completed between two days, both counted.</summary>
        public static int Gains(ServicesState s, int fromDay, int toDay)
        {
            long sum = 0;
            for (int i = 0; s != null && i < s.Records.Count; i++)
            {
                sum += s.Records[i].Day >= fromDay && s.Records[i].Day <= toDay ? s.Records[i].Gain : 0;
            }
            return (int)Math.Min(int.MaxValue / 2, sum);
        }

        /// <summary>
        /// A promise to stay out of a trade in a district for some days; null for none. A business of the kind taken on later on
        /// the day of the sale breaks it too, unless one was already taken on that day (<paramref name="takenToday"/>), which the
        /// day alone cannot tell from a later one.
        /// </summary>
        public static SaleClause Clause(ServicesState s, string type, string district, string name, int price, int days, int day, bool takenToday)
        {
            if (days <= 0 || string.IsNullOrEmpty(type))
            {
                return null;
            }
            var clause = new SaleClause { Type = type, District = district ?? string.Empty, Name = name ?? string.Empty, FromDay = takenToday ? day : day - 1, Until = day + days, Price = Math.Max(0, price) };
            if (s.Clauses.Count >= ServicesCatalogue.ClausesMost)
            {
                s.Clauses.RemoveAt(0);
            }
            s.Clauses.Add(clause);
            return clause;
        }

        /// <summary>True if a business of the player's breaks a clause: the same kind, the same district, taken on after the sale and before the clause ran out.</summary>
        public static bool Breaks(SaleClause c, string type, string district, int createdDay)
        {
            return c != null && createdDay > c.FromDay && createdDay <= c.Until && string.Equals(c.Type, type, StringComparison.Ordinal)
                   && string.Equals(c.District, district ?? string.Empty, StringComparison.Ordinal);
        }

        public static int ClausePenalty(int price) => ServicesCatalogue.Pct(Math.Max(0, price), ServicesCatalogue.ClausePenaltyPct);

        /// <summary>Takes away clauses that have run out. Returns how many.</summary>
        public static int ExpireClauses(ServicesState s, int day)
        {
            int gone = 0;
            for (int i = s.Clauses.Count - 1; i >= 0; i--)
            {
                if (day > s.Clauses[i].Until)
                {
                    s.Clauses.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }
    }
}
