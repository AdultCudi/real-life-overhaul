using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One lease on rented premises, by its place. The game has no end date for a rent: the lease is the mod's, a term and a renewal day over the
    /// game's own rent, which is never changed. What a renewal adds to the rent is a rise, paid on top of the game's rent a day as a business cost
    /// (as an agreed raise is paid on top of a wage). Saved under <see cref="LeaseStore.Key"/>.
    /// </summary>
    public sealed class LeaseFile
    {
        public string Place = string.Empty;
        public int Start, Term;         // the first day of the term and its length in days: it renews on <see cref="End"/>
        public int Rent;                // the game's rent a day when this was last read, whole money: the figure the rise is a share of
        public int Rise;                // per mille over Rent that renewals have agreed, in all
        public int Offer;               // per mille over Rent, in all, that the landlord asks at the coming renewal; 0 is a real offer too (the same rent): whether one is made is Asked
        public int Asked;               // the day the offer was made; 0 until then. The one fact that an offer is made (LeaseRules.Made): an offer of no rise is still an offer
        public int Renewals;            // terms renewed under this lease
        public bool Hold;               // the offer was declined: month to month at the rent paid now, a premium over it, asked again at the month's end
        public int Last;                // per mille the last renewal raised the rent by (what the last answer came to), for the page
        public bool Haggled;            // the landlord was asked to take less at this renewal: once
        public int Answer;              // what was answered to the offer, taking effect on the day the lease ends: 0 nothing yet, 1 accepted, 2 declined (LeaseRules.Accepted, Declined)
        public int Told;                // the day the owner was asked about this offer (a manager passed it up, or nobody manages the place); 0 until then: asked once, not every hour

        // Read from the game; never saved.
        public string Name = string.Empty;
        public string District = string.Empty;

        /// <summary>The day the term ends and the lease renews.</summary>
        public int End => Start + Term;

        /// <summary>Per mille over the game's rent the rent runs at today: the agreed rise, and the hold-over's premium while the offer is declined.</summary>
        public int Running => Rise + (Hold ? LeaseRules.HoldPremium : 0);
    }

    /// <summary>What the leases keep for a save: the leases themselves, a ring of the city's renewals by month, and the day the last pass ran.</summary>
    public sealed class LeaseState
    {
        public readonly List<LeaseFile> Leases = new List<LeaseFile>(8);
        public readonly List<int> Trend = new List<int>(LeaseRules.TrendKept);   // the city's mean rise at a renewal, per mille, a month each, the newest last
        public int TrendDay;            // the first day of the month the newest figure is of
        public int RivalSum, RivalCount;   // the rises (per mille, summed) and the number of renewals the rivals' own leases made this month, to go in the month's trend

        /// <summary>A rival's lease renewed with this rise, per mille over its rent: kept with the month's, to be counted in the city's month (<see cref="LeaseRules.CityMonth"/>).</summary>
        public void RivalRenewed(int risePermille)
        {
            RivalSum += Math.Max(0, risePermille);
            RivalCount++;
        }

        public LeaseFile Of(string place)
        {
            for (int i = 0; i < Leases.Count; i++)
            {
                if (string.Equals(Leases[i].Place, place, StringComparison.Ordinal))
                {
                    return Leases[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Leases, game-free (EconomyCheck compiles them): a term seeded for each place, the landlord's offer 14 days before it ends (what the city's
    /// own landlords have been asking in the district, plus a seeded spread, plus what a rival buying up the district adds, moved toward the market rent
    /// where the property feature knows one), what each answer comes to. The city's own leases renew by the very same rule from a seed and a district
    /// alone, so they need no state, and they are a trend only: they set the figure the landlords ask by and move no rent. The leases that are real are
    /// the player's and the rivals' businesses' (Rivals/RivalFirms), whose renewals add to that trend.
    /// </summary>
    public static class LeaseRules
    {
        public const int NoticeDays = 14;           // days before a lease ends that the landlord makes the offer
        public const int RiseMost = 250;            // per mille: the most one renewal can raise the rent by
        public const int SpreadLow = -20, SpreadHigh = 40;   // per mille: a landlord's own draw around the city's trend
        public const int HoldPremium = 100;         // per mille over the game's rent while month to month
        public const int HaggleChance = 400;        // per mille that a landlord halves their rise when asked once
        public const int TrendKept = 12;            // months of the city's renewals kept
        public const int CityLeases = 8;            // the city's own leases in a district that are followed: a trend only, stateless from the seed, with no rent of their own
        public const int PressureShare = 500;       // per mille of a rival's buying up in the district that lands on a renewal's rise
        public const int MarketShare = 300;         // per mille of the way a landlord moves their ask from the rise they would ask to the market rent, where one is known
        public static readonly int[] TermMonths = { 6, 12, 12, 24, 36 };
        private const int PurposeTerm = 301, PurposeRise = 302, PurposeHaggle = 303, PurposeCity = 304;

        /// <summary>A lease's term in days for a place: a seeded choice of 6, 12, 12, 24 or 36 months, never under a month's days.</summary>
        public static int Term(int seed, string place, int renewal, int monthDays)
        {
            int months = TermMonths[ManagerRules.Roll(seed, renewal, place, PurposeTerm, string.Empty) % TermMonths.Length];
            return Math.Max(1, months) * Math.Max(1, monthDays);
        }

        /// <summary>
        /// The rise a landlord asks at a renewal, per mille over the rent now paid: the city's trend (what landlords in the district have been asking),
        /// their own seeded draw around it, and a share of what a rival buying up the district adds. Never under nothing, never over <see cref="RiseMost"/>.
        /// The one rule for every lease: the player's, a rival's and the city's.
        /// </summary>
        public static int Rise(int seed, string key, int renewal, int trend, int pressure)
        {
            int roll = ManagerRules.Roll(seed, renewal, key, PurposeRise, string.Empty);
            int spread = SpreadLow + roll * (SpreadHigh - SpreadLow + 1) / 1000;
            return CompanyCatalogue.Clamp(trend + spread + Math.Max(0, pressure) * PressureShare / 1000, 0, RiseMost);
        }

        /// <summary>
        /// The offer for a lease coming to its end: the rent's rise in all, over the game's rent, per mille. <paramref name="market"/> is the market rent a day
        /// for these premises from the one function that knows it (CompanyBridges.MarketRent, which the property feature feeds): the landlord moves their ask
        /// <see cref="MarketShare"/> of the way from the rise they would ask to the market, either way; 0 where no market is known, and the ask is the rise alone.
        /// </summary>
        public static int Offer(LeaseFile l, int seed, int trend, int pressure, int market)
        {
            int rise = l.Rise + Rise(seed, l.Place, l.Renewals + 1, trend, pressure);
            if (market > 0 && l.Rent > 0)
            {
                int toMarket = (int)Math.Min(4L * RiseMost, (long)(market - l.Rent) * 1000 / l.Rent);   // the market over the game's rent, per mille
                rise += (toMarket - rise) * MarketShare / 1000;
            }
            return CompanyCatalogue.Clamp(rise, 0, 4 * RiseMost);
        }

        /// <summary>
        /// Whether the landlord makes the offer today: none made yet, and the lease ends within the notice. Also when the lease has ended already (days were
        /// skipped past the whole notice): the offer is made then, and <see cref="Settle"/> applies it at once at the landlord's own figure.
        /// </summary>
        public static bool OfferDue(LeaseFile l, int day)
        {
            return !Made(l) && day >= Math.Max(l.Start, l.End - NoticeDays);
        }

        /// <summary>
        /// Whether the landlord has made the offer for the coming renewal. It is its own fact, not "the offer is over nothing": a landlord may ask the same rent (an
        /// offer of 0 over it), and that is an offer that renews, is within any manager's limit and is told as such.
        /// </summary>
        public static bool Made(LeaseFile l)
        {
            return l.Asked > 0;
        }

        /// <summary>
        /// Marks the offer as made on a day (never day 0, which would read as none): the one write of <see cref="LeaseFile.Asked"/> that every lease, the player's and a rival's,
        /// goes through.
        /// </summary>
        public static void Make(LeaseFile l, int offer, int day)
        {
            l.Offer = offer;
            l.Asked = Math.Max(1, day);
            l.Answer = 0;
        }

        public const int Accepted = 1, Declined = 2;

        /// <summary>Whether an offer waits for an answer: made, not answered, and the lease not yet at its end.</summary>
        public static bool Waiting(LeaseFile l, int day)
        {
            return Made(l) && l.Answer == 0 && day < l.End;
        }

        /// <summary>The notice that tells the owner of an offer: with a rise, its share; an offer of no rise says the rent stays the same.</summary>
        public static string OfferNote(LeaseFile l)
        {
            int over = OfferOver(l);
            return over > 0 ? "reallife_co_note_lease_offer" : over == 0 ? "reallife_co_note_lease_offer_same" : "reallife_co_note_lease_offer_less";
        }

        /// <summary>The notice that tells a lease came to its end and was settled: held over, renewed with a rise, or renewed at the same rent.</summary>
        public static string RenewedNote(LeaseFile l)
        {
            return l.Hold ? "reallife_co_note_lease_hold" : l.Last > 0 ? "reallife_co_note_lease_renewed" : l.Last == 0 ? "reallife_co_note_lease_renewed_same"
                : "reallife_co_note_lease_renewed_less";
        }

        /// <summary>
        /// What the rent offered at a renewal comes to over a year against the rent paid now, the one the question card says: a rise in <paramref name="more"/>,
        /// a cut in <paramref name="less"/>. The rent paid
        /// now is the running one (the agreed rise and any hold-over premium in it), so a landlord who comes down in a falling market shows a cut and not a nought.
        /// </summary>
        public static void YearChange(int paidNow, int offered, int daysPerYear, out int more, out int less)
        {
            more = Math.Max(0, offered - paidNow) * daysPerYear;
            less = Math.Max(0, paidNow - offered) * daysPerYear;
        }

        /// <summary>The rent a day at a rise over the game's rent, whole money.</summary>
        public static int RentAt(int rent, int risePermille)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, rent) * (1000 + Math.Max(0, risePermille)) / 1000);
        }

        /// <summary>What a lease costs on top of the game's rent in a day, whole money: the rent's rise as it runs.</summary>
        public static int TopUp(LeaseFile l)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, l.Rent) * Math.Max(0, l.Running) / 1000);
        }

        /// <summary>The rise an offer asks over the rent paid now, per mille of that rent (what the page says: "up 8%").</summary>
        public static int OfferOver(LeaseFile l)
        {
            int now = 1000 + l.Running;
            return Made(l) ? (int)(((long)(1000 + l.Offer) - now) * 1000 / Math.Max(1, now)) : 0;
        }

        /// <summary>Whether an offer is inside a manager's limit (per mille over the rent paid now): their own to accept; past it, it is the owner's.</summary>
        public static bool Within(LeaseFile l, int limitPermille)
        {
            return Made(l) && OfferOver(l) <= limitPermille;
        }

        /// <summary>
        /// An answer to the offer, taking effect on the day the lease ends (the rent runs as it does until then); false when no offer waits. Accepting
        /// agrees the rise for a new term; declining keeps the tenant month to month at the rent paid now and a premium over it.
        /// </summary>
        public static bool Respond(LeaseFile l, int day, bool accept)
        {
            if (!Waiting(l, day))
            {
                return false;
            }
            l.Answer = accept ? Accepted : Declined;
            return true;
        }

        /// <summary>
        /// The day a lease has come to its end with an offer made: the answer is applied. Accepted, or left unanswered (the landlord renews at their
        /// own figure), the rise is agreed and a new term begins from the day the old one ended; declined, the tenant stays month to month and is
        /// asked again at the month's end. True when something was settled.
        /// </summary>
        public static bool Settle(LeaseFile l, int day, int seed, int monthDays)
        {
            if (!Made(l) || day < l.End)
            {
                return false;
            }
            int from = l.End;
            if (l.Answer == Declined)
            {
                l.Hold = true;
                l.Last = 0;
                l.Term = Math.Max(1, monthDays);
            }
            else
            {
                l.Last = OfferOver(l);
                l.Rise = l.Offer;
                l.Renewals++;
                l.Hold = false;
                l.Term = Term(seed, l.Place, l.Renewals, monthDays);
            }
            l.Start = from + l.Term <= day ? day : from;   // the old clock goes on, unless days were skipped past the whole new term
            l.Offer = l.Asked = l.Answer = l.Told = 0;
            l.Haggled = false;
            return true;
        }

        /// <summary>
        /// A landlord asked once to take less: with <see cref="HaggleChance"/> they halve their rise (to what is over the rent now), else the offer stands.
        /// True when it came down. Seeded by the place, the renewal and the day, so asking again the same day gives the same answer.
        /// </summary>
        public static bool Haggle(LeaseFile l, int seed, int day)
        {
            if (!CanHaggle(l, day))
            {
                return false;
            }
            l.Haggled = true;   // asked: whatever the answer, not again at this renewal
            if (ManagerRules.Roll(seed, day, l.Place, PurposeHaggle, l.Renewals.ToString(CultureInfo.InvariantCulture)) >= HaggleChance)
            {
                return false;
            }
            l.Offer = l.Rise + (l.Offer - l.Rise) / 2;
            return true;
        }

        /// <summary>Whether the landlord may be asked to take less: an offer waits, it rises the rent, and nobody has asked at this renewal.</summary>
        public static bool CanHaggle(LeaseFile l, int day)
        {
            return Waiting(l, day) && !l.Haggled && l.Offer > l.Rise;
        }

        /// <summary>
        /// The number a lease's question has in the app when no escalation stands behind it (the hierarchy is off): negative, so that it is never an escalation's, and
        /// the same for the same place wherever it is asked (a hash of the place, 1 to 2^30).
        /// </summary>
        public static int QuestionId(string place)
        {
            uint h = 2166136261u;
            for (int i = 0; place != null && i < place.Length; i++)
            {
                h = (h ^ place[i]) * 16777619u;
            }
            return -(int)(1 + (h & 0x3FFFFFFFu));
        }

        /// <summary>What the notice of an offer carries as its pick, so that a click on it opens the question: "ls:" and the place.</summary>
        public static string NoticePick(string place)
        {
            return "ls:" + place;
        }

        /// <summary>The question a notice's pick opens (<see cref="NoticePick"/>): the lease's number, negative; 0 for a pick that is no lease's.</summary>
        public static int QuestionOfPick(string pick)
        {
            return pick != null && pick.StartsWith("ls:", StringComparison.Ordinal) && pick.Length > 3 ? QuestionId(pick.Substring(3)) : 0;
        }

        /// <summary>A lease made for premises seen first: a seeded term, starting today.</summary>
        public static LeaseFile Begin(int seed, string place, int rent, int day, int monthDays)
        {
            var l = new LeaseFile { Place = place, Start = day, Rent = Math.Max(0, rent) };
            l.Term = Term(seed, place, 0, monthDays);
            return l;
        }

        // ---------------------------------------------------------------- the city's own leases: stateless, by the same rule

        /// <summary>
        /// One of the city's businesses' leases in a district, as a function of the seed alone: its term in days, its start offset in its first term, so
        /// that renewals fall on different days, the way the player's would.
        /// </summary>
        public static void CityLease(int seed, string district, int n, int monthDays, out int term, out int phase)
        {
            string key = district + "#" + n.ToString(CultureInfo.InvariantCulture);
            term = Term(seed, key, 0, monthDays);
            phase = ManagerRules.Roll(seed, 0, key, PurposeCity, string.Empty) * term / 1000;
        }

        /// <summary>
        /// What the city's landlords did in a district on a day: how many of its leases came to their end and the sum of the rises they asked
        /// (by <see cref="Rise"/> with the trend given), per mille. The city's leases renew every term and take the landlord's offer.
        /// </summary>
        public static int CityDay(int seed, string district, int day, int monthDays, int trend, int pressure, out int renewed)
        {
            renewed = 0;
            int sum = 0;
            for (int n = 0; n < CityLeases; n++)
            {
                int term, phase;
                CityLease(seed, district, n, monthDays, out term, out phase);
                if (day <= phase || (day - phase) % term != 0)
                {
                    continue;
                }
                int renewal = (day - phase) / term;
                sum += Rise(seed, district + "#" + n.ToString(CultureInfo.InvariantCulture), renewal, trend, pressure);
                renewed++;
            }
            return sum;
        }

        /// <summary>
        /// The city's month, once a month: the renewals its landlords made in the districts given (the city's own leases, from the seed alone) and those the rivals'
        /// leases made (kept in the state through the month), by the same rule, set the trend. Returns the number of renewals counted; -1 on a day the month has not
        /// ended (the first call only starts the first month: nothing to look back on).
        /// </summary>
        public static int CityMonth(LeaseState s, int seed, IList<string> districts, int day, int monthDays, Func<string, int, int> pressure)
        {
            if (s.TrendDay == 0)
            {
                s.TrendDay = day;
                return -1;
            }
            if (day - s.TrendDay < monthDays)
            {
                return -1;
            }
            int sum = s.RivalSum, renewed = s.RivalCount, trend = TrendOf(s.Trend);
            s.RivalSum = s.RivalCount = 0;
            for (int d = s.TrendDay; d < s.TrendDay + monthDays; d++)
            {
                for (int k = 0; k < districts.Count; k++)
                {
                    int count;
                    sum += CityDay(seed, districts[k], d, monthDays, trend, pressure != null ? pressure(districts[k], d) : 0, out count);
                    renewed += count;
                }
            }
            if (districts.Count > 0 || renewed > 0)
            {
                KeepTrend(s.Trend, sum, renewed);
            }
            s.TrendDay += monthDays;
            return renewed;
        }

        /// <summary>The month's mean rise at a city renewal in a district, per mille, added to the ring; a month with no renewal keeps the last figure.</summary>
        public static void KeepTrend(List<int> ring, int sum, int renewed)
        {
            int last = ring.Count > 0 ? ring[ring.Count - 1] : 0;
            ring.Add(renewed > 0 ? sum / renewed : last);
            while (ring.Count > TrendKept)
            {
                ring.RemoveAt(0);
            }
        }

        /// <summary>The city's trend: the mean of the months kept, per mille; 0 before any month has been kept (the landlord's own draw alone).</summary>
        public static int TrendOf(List<int> ring)
        {
            if (ring == null || ring.Count == 0)
            {
                return 0;
            }
            long sum = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                sum += ring[i];
            }
            return (int)(sum / ring.Count);
        }
    }

    /// <summary>The leases in the save, game-free: "v=1^h:trendDay^t:the ring of the city's months^l:leases". A newer version adds fields at the end of a record.</summary>
    public static class LeaseStore
    {
        public const string Key = "reallife:co_leases";
        public const int Version = 1;
        public const int LeasesMost = 64;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(LeaseState s)
        {
            var t = new StringBuilder(64 + s.Leases.Count * 64);
            t.Append("v=").Append(Version).Append("^h:").Append(s.TrendDay.ToString(Inv)).Append('|').Append(s.RivalSum.ToString(Inv)).Append('|').Append(s.RivalCount.ToString(Inv));
            t.Append("^t:");
            for (int i = 0; i < s.Trend.Count && i < LeaseRules.TrendKept; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(s.Trend[i].ToString(Inv));
            }
            t.Append("^l:");
            for (int i = 0; i < s.Leases.Count && i < LeasesMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty);
                Record(t, s.Leases[i]);
            }
            return t.ToString();
        }

        /// <summary>One lease as a record of fields split by '|' (no ';' or '^' in it): the player's and a rival's are kept the same way. A newer figure goes at the end.</summary>
        public static void Record(StringBuilder t, LeaseFile l)
        {
            t.Append(l.Start.ToString(Inv)).Append('|').Append(l.Term.ToString(Inv)).Append('|').Append(l.Rent.ToString(Inv)).Append('|').Append(l.Rise.ToString(Inv)).Append('|')
                .Append(l.Offer.ToString(Inv)).Append('|').Append(l.Asked.ToString(Inv)).Append('|').Append(l.Renewals.ToString(Inv)).Append('|').Append(l.Hold ? '1' : '0').Append('|')
                .Append(l.Last.ToString(Inv)).Append('|').Append(CompanyStore.Clean(l.Place)).Append('|').Append(l.Answer.ToString(Inv)).Append('|').Append(l.Haggled ? '1' : '0')
                .Append('|').Append(l.Told.ToString(Inv));
        }

        /// <summary>One lease from a record's fields (<see cref="Record"/>); null for a record with no place or too few fields.</summary>
        public static LeaseFile Parse(string[] c)
        {
            if (c == null || c.Length < 10 || c[9].Length == 0)
            {
                return null;
            }
            return new LeaseFile
            {
                Start = Math.Max(0, I(c, 0)), Term = Math.Max(1, I(c, 1, 1)), Rent = Math.Max(0, I(c, 2)),
                Rise = CompanyCatalogue.Clamp(I(c, 3), 0, 4 * LeaseRules.RiseMost), Offer = CompanyCatalogue.Clamp(I(c, 4), 0, 4 * LeaseRules.RiseMost),
                Asked = Math.Max(I(c, 4) > 0 ? 1 : 0, I(c, 5)), Renewals = Math.Max(0, I(c, 6)), Hold = I(c, 7) != 0, Last = CompanyCatalogue.Clamp(I(c, 8), -1000, 4 * LeaseRules.RiseMost),
                Place = c[9], Answer = CompanyCatalogue.Clamp(I(c, 10), 0, LeaseRules.Declined), Haggled = I(c, 11) != 0, Told = Math.Max(0, I(c, 12))
            };
        }

        /// <summary>Reads a line; null for one that is not this feature's.</summary>
        public static LeaseState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                string[] parts = text.Split('^');
                if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    return null;
                }
                var s = new LeaseState();
                bool header = false;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    switch (part[0])
                    {
                        case 'h':
                            string[] head = body.Split('|');
                            s.TrendDay = Math.Max(0, I(head, 0));
                            s.RivalSum = Math.Max(0, I(head, 1));
                            s.RivalCount = Math.Max(0, I(head, 2));
                            header = true;
                            break;
                        case 't':
                            foreach (string one in body.Length == 0 ? new string[0] : body.Split('|'))
                            {
                                int v;
                                if (int.TryParse(one, NumberStyles.Integer, Inv, out v) && s.Trend.Count < LeaseRules.TrendKept)
                                {
                                    s.Trend.Add(CompanyCatalogue.Clamp(v, 0, LeaseRules.RiseMost));
                                }
                            }
                            break;
                        case 'l':
                            foreach (string record in body.Length == 0 ? new string[0] : body.Split(';'))
                            {
                                LeaseFile one = Parse(record.Split('|'));
                                if (one != null && s.Leases.Count < LeasesMost && s.Of(one.Place) == null)
                                {
                                    s.Leases.Add(one);
                                }
                            }
                            break;
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }
    }
}
