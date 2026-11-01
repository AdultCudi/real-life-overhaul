using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class SectorRules
    {
        public const int PoachDays = 30;             // days after a rival's lead or win that it hires: it comes for the player's people
        public const int PoachPull = 1500;           // thousandths: how much harder a hiring rival pulls (TalentRules.Approached)

        // ---------------------------------------------------------------- the rivals set up

        /// <summary>
        /// Each listed company's product in every market it sells in, made once with the features given for its market (an
        /// older save's products' own rivals, so nothing jumps; else the market's usual rival), its name and its company's
        /// reputation. Returns how many were made.
        /// </summary>
        public static int Companies(SectorFile f, int day, int[] features)
        {
            int made = 0;
            for (int c = 0; c < SectorCatalogue.CompanyMarkets.Length; c++)
            {
                if (f.CompanyRep[c] <= 0)
                {
                    f.CompanyRep[c] = SectorCatalogue.CompanyReputation[c];
                }
                for (int m = 0; m < (int)SectorMarket.Count; m++)
                {
                    if ((SectorCatalogue.CompanyMarkets[c] & (1 << m)) == 0 || f.RivalOf(c, m) != null || f.Rivals.Count >= SectorCatalogue.RivalProductsMost)
                    {
                        continue;
                    }
                    f.Rivals.Add(new RivalProduct
                    {
                        Id = f.NextRival++, Company = c, Market = m, Name = c < HiringCatalogue.RivalFirms.Length ? HiringCatalogue.RivalFirms[c] : string.Empty,
                        Features = Math.Max(1, features != null && m < features.Length ? features[m] : FirmsCatalogue.RivalFeaturesAtStart[BuyersOf(m)]),
                        LastShip = day, LastCut = day - SectorCatalogue.RivalCutGap, PeakDay = day
                    });
                    made++;
                }
            }
            return made;
        }

        /// <summary>
        /// The startups that run and sell software of these kinds each have a product in their market (made with a few features
        /// for its rounds, its team growing with them), and a startup's product goes when the startup does. The host's or a lone
        /// player's (a guest applies the host's). Notes the products that came, by where they stand once the closed ones are gone.
        /// </summary>
        public static void Startups(SectorFile f, IList<Startup> startups, int day, List<RivalNote> notes)
        {
            notes?.Clear();
            for (int i = f.Rivals.Count - 1; i >= 0; i--)
            {
                RivalProduct r = f.Rivals[i];
                if (r.Startups && !Running(startups, r.Startup))
                {
                    f.Rivals.RemoveAt(i);
                }
            }
            for (int i = 0; startups != null && i < startups.Count; i++)
            {
                Startup s = startups[i];
                int m = s != null && s.Alive && s.Sector >= 0 && s.Sector < SectorCatalogue.StartupMarket.Length ? SectorCatalogue.StartupMarket[s.Sector] : -1;
                if (m < 0)
                {
                    continue;
                }
                RivalProduct r = f.StartupProduct(s.Id);
                if (r == null)
                {
                    if (f.Rivals.Count >= SectorCatalogue.RivalProductsMost)
                    {
                        continue;
                    }
                    r = new RivalProduct
                    {
                        Id = f.NextRival++, Startup = s.Id, Market = m, Name = s.Name ?? string.Empty, Features = 2 + Math.Max(0, s.Stage), LastShip = day,
                        LastCut = day - SectorCatalogue.RivalCutGap, PeakDay = day
                    };
                    f.Rivals.Add(r);
                    notes?.Add(new RivalNote { Rival = f.Rivals.Count - 1, Kind = NoteNew });
                }
                r.Stage = Math.Max(0, s.Stage);
            }
        }

        private static bool Running(IList<Startup> startups, int id)
        {
            for (int i = 0; startups != null && i < startups.Count; i++)
            {
                if (startups[i] != null && startups[i].Id == id)
                {
                    return startups[i].Alive;
                }
            }
            return false;
        }

        /// <summary>
        /// On a company product's first day its customers are those its share of the market gives it: the companies are
        /// long established. Only products from <paramref name="fromId"/> on, so one that has lost every customer stays
        /// where it is.
        /// </summary>
        public static void Settle(SectorFile f, int fromId = 0)
        {
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                if (!r.Startups && r.Id >= fromId && r.Customers == 0 && r.Peak == 0)
                {
                    r.Customers = Target(f.Markets[Clamp(r.Market, 0, f.Markets.Length - 1)], r.Attraction);
                    r.Peak = r.Customers;
                }
            }
        }

        // ---------------------------------------------------------------- a rival product's pull

        /// <summary>A rival's work points a day for one product: its company's team over the products it sells, or a startup's team by its rounds.</summary>
        public static int RivalPoints(SectorFile f, RivalProduct r)
        {
            if (r.Startups)
            {
                return SectorCatalogue.StartupTeamSeed + SectorCatalogue.StartupTeamPerStage * Math.Max(0, r.Stage);
            }
            int c = Clamp(r.Company, 0, SectorCatalogue.CompanyTeam.Length - 1), n = 0;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                n += f.Rivals[i].Company == c ? 1 : 0;
            }
            return SectorCatalogue.CompanyTeam[c] / Math.Max(1, n);
        }

        /// <summary>A rival's marketing a day for one product, cents.</summary>
        public static long RivalMarketing(RivalProduct r) =>
            r.Startups ? SectorCatalogue.StartupMarketing : SectorCatalogue.CompanyMarketing[Clamp(r.Company, 0, SectorCatalogue.CompanyMarketing.Length - 1)];

        /// <summary>A rival's name: its company's (moving with its products), or a startup's.</summary>
        public static int RivalReputation(SectorFile f, RivalProduct r) =>
            r.Startups ? SectorCatalogue.StartupReputation : f.CompanyRep[Clamp(r.Company, 0, f.CompanyRep.Length - 1)];

        /// <summary>A rival product's appeal by the player's own rule: its features against the market's going ones, how sound it is, its price against the going price, its name.</summary>
        public static int RivalAppeal(SectorFile f, RivalProduct r, SectorMarketDay m)
        {
            long a = FeatureAppeal(r.Features, m.Average);
            a = a * Soundness(r.Bugs, RivalRoom(r)) / 1000;
            a = a * PriceAppeal(r.Tier, m.Going) / 1000;
            a = a * (500 + RivalReputation(f, r)) / 1000;
            return (int)Math.Min(int.MaxValue / 4, a);
        }

        /// <summary>The bugs a rival's product holds before it falls far: a service's room (FirmsRules.Allowance's rule).</summary>
        public static int RivalRoom(RivalProduct r) => Math.Max(1, r.Features) * FirmsCatalogue.BugsPerFeature * 2 + 10;

        // ---------------------------------------------------------------- a day of the rivals

        /// <summary>
        /// A day of the rivals (NPC parity), after every product's pull is in: each builds with its team by the player's rule
        /// (a feature costs what FeatureCost says, bugs come with the work and some are fixed), its customers move towards its
        /// share of the market at the same pace as the player's and churn by the same rule, it cuts its price a step when it has
        /// lost a good part of its customers lately and raises it back once it holds them again, and its company's name follows
        /// the soundness of what it sells. Seeded from the sector's seed (a Going Public guest applies the host's instead).
        /// Notes a feature that took the lead from the player's best in a market, and price moves. COST: a pass over the rival products.
        /// </summary>
        public static void RivalDay(SectorFile f, int day, List<RivalNote> notes)
        {
            notes?.Clear();
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                SectorMarketDay m = f.Markets[Clamp(r.Market, 0, f.Markets.Length - 1)];
                int points = RivalPoints(f, r);
                long build = (long)points * SectorCatalogue.RivalFeatureShare / 1000;
                r.Progress += build;
                r.Bugs = (int)Math.Min(int.MaxValue / 2, r.Bugs + build * SectorCatalogue.RivalFaults / 1000);
                r.Bugs = Math.Max(0, r.Bugs - points * SectorCatalogue.RivalFixShare / 1000 / FirmsCatalogue.PointsPerFix);
                int before = r.Features;
                bool lab = r.Market >= (int)SectorMarket.ConsumerAi;
                if (lab)
                {
                    r.Compute += r.Startups ? SectorCatalogue.StartupLabCompute : SectorCatalogue.RivalLabCompute;
                }
                while (r.Features < SectorCatalogue.FeaturesMost && r.Progress >= FirmsRules.FeatureCost(r.Features) && (!lab || r.Compute >= InfraRules.TrainNeed(r.Features)))
                {
                    if (lab)
                    {
                        r.Compute -= InfraRules.TrainNeed(r.Features);   // a model's feature is a generation's training too, as the player's
                    }
                    r.Progress -= FirmsRules.FeatureCost(r.Features);
                    r.Features++;
                }
                if (r.Features > before)
                {
                    r.LastShip = day;
                    if (m.MineFeatures > 0 && before <= m.MineFeatures && r.Features > m.MineFeatures)
                    {
                        notes?.Add(new RivalNote { Rival = i, Kind = NoteShipped });
                    }
                }
                RivalCustomers(f, r, m, day);
                if (r.Customers > 0 && TechRandom.Hash(f.Seed, r.Id, day, 11) % 1000000u < (long)FirmsCatalogue.OutageChance * (1000 - SectorCatalogue.RivalReliability))
                {
                    r.Customers -= r.Customers * FirmsCatalogue.OutageChurn / 1000;   // the player's own outage rule
                    if (!r.Startups)
                    {
                        f.CompanyRep[r.Company] = Math.Max(0, f.CompanyRep[r.Company] - 30);
                    }
                    notes?.Add(new RivalNote { Rival = i, Kind = NoteOutage });
                }
                if (r.Customers > r.Peak || day - r.PeakDay > SectorCatalogue.RivalPeakDays)
                {
                    r.Peak = r.Customers;
                    r.PeakDay = day;
                }
                if (r.Tier > 0 && r.Customers < r.Peak * (1000 - SectorCatalogue.RivalCutLoss) / 1000 && day - r.LastCut >= SectorCatalogue.RivalCutGap)
                {
                    r.Tier--;
                    r.LastCut = day;
                    notes?.Add(new RivalNote { Rival = i, Kind = NoteCut });
                }
                else if (r.Tier < 1 && r.Customers >= r.Peak * 950 / 1000 && day - r.LastCut >= SectorCatalogue.RivalRaiseGap)
                {
                    r.Tier++;
                    r.LastCut = day;
                    notes?.Add(new RivalNote { Rival = i, Kind = NoteRaised });
                }
            }
            for (int c = 0; c < f.CompanyRep.Length; c++)
            {
                long sound = 0;
                int n = 0;
                for (int i = 0; i < f.Rivals.Count; i++)
                {
                    if (f.Rivals[i].Company == c)
                    {
                        sound += Soundness(f.Rivals[i].Bugs, RivalRoom(f.Rivals[i]));
                        n++;
                    }
                }
                if (n > 0)
                {
                    f.CompanyRep[c] = Toward(f.CompanyRep[c], SectorCatalogue.ReputationBase + (int)(SectorCatalogue.ReputationSpan * (sound / n) / 1000));
                }
            }
        }

        // A rival's customers: towards its share at the market's pace (back towards it when past it), less churn by its soundness.
        private static void RivalCustomers(SectorFile f, RivalProduct r, SectorMarketDay m, int day)
        {
            int who = Clamp(m.Who, 0, (int)SectorBuyers.Count - 1);
            long target = Target(m, r.Attraction);
            long micro = (target - r.Customers) * SectorCatalogue.Pace[who] + r.Carry;
            long churn = FirmsCatalogue.Churn[who] * (2000L - Soundness(r.Bugs, RivalRoom(r))) / 1000;
            micro -= r.Customers * churn * 1000000 / 1000 / FirmsCatalogue.MonthDays;
            long whole = micro / 1000000;
            r.Carry = micro % 1000000;
            r.Customers = Math.Max(0L, r.Customers + whole);
        }

        // ---------------------------------------------------------------- tenders won by the same rule

        /// <summary>
        /// A council tender decided on its last day by one rule for every bidder: each bid weighed by its bidder's name (the
        /// player's firm with its office and a pitch held at the desk, each rival company with its own name in the sector's
        /// file), the lowest weighed bid wins. True if the player's firm won (its bid becomes the value); else the winning
        /// company in <paramref name="winner"/> (-1 for none) and its win remembered.
        /// </summary>
        public static bool Decide(FirmsFile ff, SectorFile f, Contract t, Firm bidder, int day, int office, int pitch, out int winner)
        {
            winner = -1;
            if (t.Bid <= 0 || bidder == null)
            {
                t.Stage = (int)WorkStage.Lost;
                winner = BestRival(ff, f, t, out _);
                Won(f, winner, day);
                return false;
            }
            long mine = t.Bid * (1500 - Clamp(bidder.Reputation, 0, 1000) - Clamp(office, 0, 50) - Clamp(pitch, -200, 200)) / 1000;
            int best = BestRival(ff, f, t, out long theirs);
            if (best >= 0 && theirs < mine)
            {
                t.Stage = (int)WorkStage.Lost;
                winner = best;
                Won(f, winner, day);
                return false;
            }
            t.Value = t.Bid;
            return true;
        }

        // The rival bidder whose weighed bid is lowest, and that weighed bid; -1 with none.
        private static int BestRival(FirmsFile ff, SectorFile f, Contract t, out long weighed)
        {
            weighed = long.MaxValue;
            int best = -1;
            for (int r = 0; r < t.Rivals; r++)
            {
                int c = CompetitorRules.TenderRival(ff.Seed, t, r);
                int rep = c >= 0 && c < f.CompanyRep.Length && f.CompanyRep[c] > 0 ? f.CompanyRep[c] : SectorCatalogue.RivalServiceReputation;
                long bid = FirmsRules.RivalBid(ff.Seed, t, r, t.Value) * (1500 - rep) / 1000;
                if (bid < weighed)
                {
                    weighed = bid;
                    best = c;
                }
            }
            return best;
        }

        private static void Won(SectorFile f, int company, int day)
        {
            if (company >= 0 && company < f.CompanyWon.Length)
            {
                f.CompanyWon[company] = day;
                f.CompanyRep[company] = Clamp(f.CompanyRep[company] + 10, 0, 1000);
            }
        }

        // ---------------------------------------------------------------- the rivals come for the player's people

        /// <summary>
        /// The rival that comes for the people of a firm of a kind, and how hard (thousandths for TalentRules.Approached): a
        /// company whose product lately took a lead in a market the kind sells in, else one that lately won a tender for work
        /// the kind takes, else none (1000, the usual pull). The newest first.
        /// </summary>
        public static int Poacher(SectorFile f, int kind, int day, out int pull)
        {
            pull = 1000;
            if (f == null || kind < 0 || kind >= (int)FirmKind.Count)
            {
                return -1;
            }
            int best = -1, bestDay = int.MinValue;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                if (!r.Startups && day - r.LastShip < PoachDays && r.LastShip > bestDay && Sells(kind, r.Market))
                {
                    best = r.Company;
                    bestDay = r.LastShip;
                }
            }
            for (int c = 0; best < 0 && c < f.CompanyWon.Length; c++)
            {
                if (f.CompanyWon[c] > 0 && day - f.CompanyWon[c] < PoachDays && f.CompanyWon[c] > bestDay && CompetitorRules.CompanyCompetes(kind, c))
                {
                    best = c;
                    bestDay = f.CompanyWon[c];
                }
            }
            if (best >= 0)
            {
                pull = PoachPull;
            }
            return best;
        }

        /// <summary>True when a firm of a kind can make a product for a market.</summary>
        public static bool Sells(int kind, int market)
        {
            for (int k = 0; k < (int)ProductKind.Count; k++)
            {
                if (k == (int)ProductKind.Game || (FirmsCatalogue.MakesProducts[Clamp(kind, 0, (int)FirmKind.Count - 1)] & (1 << k)) == 0)
                {
                    continue;
                }
                for (int s = 0; s < (int)Segment.Count; s++)
                {
                    if (MarketOf((ProductKind)k, s) == market)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
