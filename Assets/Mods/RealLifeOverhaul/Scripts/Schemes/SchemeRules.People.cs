using System;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>What came of an offer of money to somebody.</summary>
    public struct OfferResult
    {
        public Temper Temper;
        public bool Taken;         // the money was taken and the favour is done
        public bool Cold;          // they had said no before: nothing was offered again
        public long Price;         // what was paid, whole money; 0 when it was not taken
        public bool Cash;          // paid out of the cash in hand
    }

    // The people: who takes money is a trait of the person, fixed by the save's seed, so asking twice changes nothing.
    public static partial class SchemeRules
    {
        /// <summary>A contact's id, by their place in the social life's cast.</summary>
        public static string Contact(int person) => "p" + person.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A councillor's id, by their name: the same councillor whoever asks, the player or a rival.</summary>
        public static string Councillor(string name) => "c" + (name ?? string.Empty).Replace('|', ' ').Replace('\n', ' ');

        /// <summary>The investigator of an inquiry: each inquiry has its own.</summary>
        public static string Investigator(SchemeKind kind, int opened) => "i" + ((int)kind * 100 + opened).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The judge of a court case, by its number.</summary>
        public static string Judge(int caseNumber) => "j" + caseNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// What somebody does when money is offered: by their office, the save's seed and who they are. Never drawn at the
        /// moment of asking, so it is the same on every load and for everybody who asks.
        /// </summary>
        public static Temper TemperOf(int seed, Office office, string who)
        {
            int at = SchemeCatalogue.Clamp((int)office, 0, SchemeCatalogue.TakesPermille.Length - 1);
            int roll = Roll(seed, DrawTemper, at, Text(who));
            if (roll < SchemeCatalogue.TakesPermille[at])
            {
                return Temper.Takes;
            }
            return roll < SchemeCatalogue.TakesPermille[at] + SchemeCatalogue.ReportsPermille[at] ? Temper.Reports : Temper.Remembers;
        }

        /// <summary>True for somebody who said no before: they are not asked again.</summary>
        public static bool IsCold(SchemeState s, string who)
        {
            return s.Cold.Contains(who ?? string.Empty);
        }

        /// <summary>What a favour costs today: its price at today's level; the investigator asks a share of what the file holds, if that is more.</summary>
        public static long PriceOf(SchemeState s, Favour favour, SchemeInquiry inquiry, int day, int level)
        {
            long price = SchemeCatalogue.AtPrices(SchemeCatalogue.FavourPrice[(int)favour], level);
            if (favour == Favour.Investigator && inquiry != null)
            {
                price = Math.Max(price, SchemeCatalogue.Share(Brought(s, inquiry.Kind, day), SchemeCatalogue.InvestigatorShare));
            }
            return Round(price);
        }

        /// <summary>A sum rounded to tens, as prices are named.</summary>
        public static long Round(long amount)
        {
            return amount < 100 ? Math.Max(0L, amount) : (amount + 5) / 10 * 10;
        }

        /// <summary>
        /// Money is offered to somebody for a favour. Taken: they are kept as somebody who knows, the act goes on the
        /// graft trail (heavier for a payment through the bank account) and the price is owed by the caller. Refused: they
        /// remember, and some report it. Whoever said no before is not asked again and nothing happens.
        /// </summary>
        public static OfferResult Offer(SchemeState s, Favour favour, Office office, string who, long price, bool cash, int day)
        {
            var result = new OfferResult { Temper = TemperOf(s.Seed, office, who), Cash = cash };
            if (IsCold(s, who))
            {
                result.Cold = true;
                result.Temper = result.Temper == Temper.Takes ? Temper.Remembers : result.Temper;
                return result;
            }
            if (result.Temper != Temper.Takes)
            {
                Chill(s, who);
                if (result.Temper == Temper.Reports)
                {
                    AddHeat(s, SchemeKind.Graft, SchemeCatalogue.HeatReported);
                }
                return result;
            }
            result.Taken = true;
            result.Price = Math.Max(0L, price);
            if (cash)
            {
                s.Dirty = Math.Max(0L, s.Dirty - result.Price);
            }
            SchemeAct act = Record(s, SchemeKind.Graft, day, 0, SchemeCatalogue.FavourSize[(int)favour], 1, !cash, string.Empty, (int)favour);
            act.Paid = result.Price;
            while (s.Paid.Count >= SchemeCatalogue.PaidMost)
            {
                s.Paid.RemoveAt(0);
            }
            s.Paid.Add(new SchemePaid { Who = who ?? string.Empty, Day = day, Amount = result.Price, What = favour });
            return result;
        }

        /// <summary>Somebody said no: they remember, and are not asked again.</summary>
        public static void Chill(SchemeState s, string who)
        {
            if (string.IsNullOrEmpty(who) || s.Cold.Contains(who))
            {
                return;
            }
            while (s.Cold.Count >= SchemeCatalogue.ColdMost)
            {
                s.Cold.RemoveAt(0);
            }
            s.Cold.Add(who);
        }

        /// <summary>Whether somebody paid talks on a day: a small chance a day, a larger one while an inquiry into graft is open. Fixed by the seed, the person and the day.</summary>
        public static bool Talks(int seed, string who, int day, bool pressed)
        {
            return Roll(seed, DrawTalk, Text(who), day) < (pressed ? SchemeCatalogue.TalkPressed : SchemeCatalogue.TalkPermille);
        }

        /// <summary>A contact's next favour is bought: kept until it is used.</summary>
        public static void Buy(SchemeState s, int person)
        {
            if (person < 0 || s.Bought.Contains(person))
            {
                return;
            }
            while (s.Bought.Count >= SchemeCatalogue.BoughtMost)
            {
                s.Bought.RemoveAt(0);
            }
            s.Bought.Add(person);
        }

        /// <summary>
        /// Somebody was hosted (a party, an evening laid on for them): for one who takes gifts it counts as a gift taken,
        /// with no money and no trail; for anybody else it is an evening out and nothing more. True if their next favour is now bought.
        /// </summary>
        public static bool Hosted(SchemeState s, int person)
        {
            string who = Contact(person);
            if (person < 0 || IsCold(s, who) || TemperOf(s.Seed, Office.Contact, who) != Temper.Takes)
            {
                return false;
            }
            Buy(s, person);
            return true;
        }

        /// <summary>The councillor a vote is asked of: one seat of the council, by the seed and the push it is about.</summary>
        public static int Seat(int seed, int push, int seats)
        {
            return seats <= 0 ? -1 : Roll(seed, DrawSeat, push, seats) % seats;
        }
    }
}
