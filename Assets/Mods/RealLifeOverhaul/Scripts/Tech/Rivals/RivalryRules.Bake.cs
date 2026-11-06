using System;

namespace RealLifeOverhaul.Tech
{
    public static partial class RivalryRules
    {
        // ---------------------------------------------------------------- bake offs

        /// <summary>
        /// Whether a big customer in a niche holds a bake off today: only where one of the player's firms sells, none while one is open, at most once in half the usual gap, and
        /// then a seeded roll that comes up about once in <see cref="RivalryCatalogue.BakeEvery"/> days.
        /// </summary>
        public static bool BakeWanted(RivalryWorld w, int niche, int day)
        {
            if (niche < 0 || niche >= NicheCatalogue.Count || !YouIn(w, niche).HasValue || w.Rec.OpenIn(niche) != null || w.Rec.Bakes.Count >= RivalryCatalogue.BakesMost)
            {
                return false;
            }
            return day - w.Rec.BakeLast[niche] >= RivalryCatalogue.BakeEvery / 2 && TechRandom.Mille(w.Seed, niche, day, 97) * RivalryCatalogue.BakeEvery < 1000;
        }

        /// <summary>Which of <paramref name="count"/> businesses of the niche's kinds in the game's list is the customer: a seeded pick, the same on every machine.</summary>
        public static int PickCustomer(RivalryWorld w, int niche, int day, int count) => count <= 1 ? 0 : TechRandom.Range(w.Seed, niche, day, 93, 0, count - 1);

        /// <summary>A bake off opened for a customer of the niche: the sites it signs for, and the contract's value (its sites for so many months at the niche's going price).</summary>
        public static BakeOff Open(RivalryWorld w, int niche, string customer, int day)
        {
            NicheDef d = NicheCatalogue.Def(niche);
            int id = w.Rec.NextBake++;
            int least = d.Buyers == (int)SectorBuyers.Enterprises ? RivalryCatalogue.BakeSeatsEnterprises : RivalryCatalogue.BakeSeatsBusinesses;
            int market = NicheRules.MarketOf(d, false);
            int going = NicheRules.Going(d, w.Sector.Markets[market].Going);
            int seg = d.Buyers == (int)SectorBuyers.Enterprises ? (int)Segment.Enterprise : (int)Segment.Business;
            int seats = TechRandom.Range(w.Seed, id, niche, 5, least, least * 2);
            var b = new BakeOff
            {
                Id = id, Niche = niche, Customer = customer ?? string.Empty, Opened = day, Closes = day + RivalryCatalogue.BakeDays, Seats = seats,
                Value = (long)seats * FirmsCatalogue.Price[seg] * Math.Max(1, going) / 1000 * RivalryCatalogue.BakeMonths
            };
            w.Rec.Bakes.Add(b);
            w.Rec.BakeLast[niche] = day;
            w.Opened.Add(b);
            while (w.Rec.Bakes.Count > RivalryCatalogue.BakesMost)
            {
                w.Rec.Bakes.RemoveAt(0);
            }
            return b;
        }

        /// <summary>The player's firm that can enter a bake off in its niche, or null: a firm of the player's selling into the niche that has not entered it yet.</summary>
        public static RivalryYou? Entrant(RivalryWorld w, BakeOff b, string place)
        {
            if (b == null || b.State != BakeState.Open || b.EntryOf(place) != null)
            {
                return null;
            }
            for (int i = 0; i < w.You.Count; i++)
            {
                if (w.You[i].Place == place && (w.You[i].Served & (1 << b.Niche)) != 0)
                {
                    return w.You[i];
                }
            }
            return null;
        }

        /// <summary>A firm of the player's takes part, by one of three tactics (0 a demo, 1 a sharper price, 2 the relationships). False when it cannot.</summary>
        public static bool Enter(RivalryWorld w, BakeOff b, string place, int tactic)
        {
            if (!Entrant(w, b, place).HasValue || b.Entries.Count >= 4)
            {
                return false;
            }
            b.Entries.Add(new BakeEntry { Place = place, Tactic = Clamp(tactic, 0, 2) });
            return true;
        }

        /// <summary>
        /// What a tactic adds to the player's score, thousandths: a demo with Product sense, the relationships with Relationships, a sharper price a flat amount (and the contract
        /// brings less, <see cref="RivalryCatalogue.BakePriceCut"/>).
        /// </summary>
        public static int TacticBonus(int tactic, int sense, int relationships)
        {
            switch (tactic)
            {
                case 0: return RivalryCatalogue.BakeDemo * Clamp(sense, 0, 100) / 100;
                case 1: return RivalryCatalogue.BakePrice;
                default: return RivalryCatalogue.BakeRelations * Clamp(relationships, 0, 100) / 100;
            }
        }

        /// <summary>
        /// The score of a product of the player's in a bake off: its appeal in the niche (features, price, soundness and the firm's name), raised by Selling, by how well it fits
        /// the niche and by the tactic it went in with. A rival's is its appeal by the same rule, with only the day's roll added to it (<see cref="RivalScore"/>).
        /// </summary>
        public static long YouScore(int appeal, int selling, int fit, int tactic, int sense, int relationships)
        {
            int bonus = RivalryCatalogue.BakeSelling * Clamp(selling, 0, 100) / 100 + (Clamp(fit, 0, 1000) - 500) * RivalryCatalogue.BakeFit / 500 + TacticBonus(tactic, sense, relationships);
            return Math.Max(0L, (long)appeal) * Math.Max(0, 1000 + bonus) / 1000;
        }

        public static long RivalScore(RivalryWorld w, BakeOff b, RivalProduct r)
        {
            int roll = TechRandom.Range(w.Seed, b.Id, r.Id, 98, -RivalryCatalogue.BakeJitter, RivalryCatalogue.BakeJitter);
            return Math.Max(0L, (long)r.Appeal) * (1000 + roll) / 1000;
        }

        // Decides every bake off that has closed. The best fitted product wins; a tie goes to the rival. A rival that wins signs the customer's sites.
        private static void Bakes(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.Rec.Bakes.Count; i++)
            {
                BakeOff b = w.Rec.Bakes[i];
                if (b.State == BakeState.Open && day >= b.Closes)
                {
                    Decide(w, b, day);
                }
            }
        }

        /// <summary>
        /// Decides one bake off: the products bound to the niche and the best listed company's in its market against the player's firms that entered, each by its score. The money is not
        /// moved here: <see cref="BakeOff.Booked"/> says what the winner's firm takes, and the director books it at the firm's address.
        /// </summary>
        public static void Decide(RivalryWorld w, BakeOff b, int day)
        {
            NicheDef d = NicheCatalogue.Def(b.Niche);
            long best = -1;
            RivalProduct rival = null;
            RivalProduct company = null;
            for (int i = 0; i < w.Sector.Rivals.Count; i++)
            {
                RivalProduct r = w.Sector.Rivals[i];
                bool bound = r.Niche == b.Niche;
                bool incumbent = r.Niche < 0 && !r.Startups && r.Customers > 0 && NicheRules.Serves(d, r.Market);
                if (incumbent && (company == null || r.Appeal > company.Appeal))
                {
                    company = r;
                }
                long score = bound ? RivalScore(w, b, r) : -1;
                if (bound && score > best)
                {
                    best = score;
                    rival = r;
                }
            }
            if (company != null)
            {
                long score = RivalScore(w, b, company);
                if (score > best)
                {
                    best = score;
                    rival = company;
                }
            }
            int winner = -1;
            long yours = -1;
            for (int e = 0; e < b.Entries.Count; e++)
            {
                RivalryYou? y = YouAt(w, b, b.Entries[e].Place);
                if (!y.HasValue)
                {
                    continue;
                }
                int roll = TechRandom.Range(w.Seed, b.Id, TechRandom.Of(y.Value.Place), 98, -RivalryCatalogue.BakeJitter, RivalryCatalogue.BakeJitter);
                long score = YouScore(y.Value.Appeal, w.Selling, y.Value.Fit, b.Entries[e].Tactic, w.Sense, w.Relationships) * (1000 + roll) / 1000;
                if (score > yours)
                {
                    yours = score;
                    winner = e;
                }
            }
            if (winner >= 0 && yours > best)
            {
                BakeEntry won = b.Entries[winner];
                RivalryYou y = YouAt(w, b, won.Place).Value;
                b.State = BakeState.Won;
                b.Winner = y.Name;
                b.Place = y.Place;
                b.Booked = b.Value * (won.Tactic == 1 ? 1000 - RivalryCatalogue.BakePriceCut : 1000) / 1000;
            }
            else
            {
                b.State = b.Entries.Count > 0 ? BakeState.Lost : BakeState.Missed;
                b.Winner = rival != null ? rival.Name : string.Empty;
                if (rival != null)
                {
                    rival.Customers += b.Seats;
                }
            }
            w.Decided.Add(b);
        }

        // A firm of the player's in a niche, whether or not it has entered (for a decision on the day, when it must still be selling there).
        private static RivalryYou? YouAt(RivalryWorld w, BakeOff b, string place)
        {
            for (int i = 0; i < w.You.Count; i++)
            {
                if (w.You[i].Place == place && (w.You[i].Served & (1 << b.Niche)) != 0)
                {
                    return w.You[i];
                }
            }
            return null;
        }
    }
}
