using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class RivalryRules
    {
        /// <summary>The days VentureRules.Day waits after a startup starts raising before the round closes.</summary>
        public const int RaiseDays = 10;

        // ---------------------------------------------------------------- a day of the rivals' reactions

        /// <summary>
        /// A day of reactions, after the markets' day and the city's startups': the buyers named for the day's sales, rounds brought forward closing, the player's raises answered,
        /// a lead copied, prices moved in each niche, hiring noted, listed companies coming into a big niche or buying a startup there, bake offs decided. Seeded, on the day only.
        /// <paramref name="todays"/> are the venture notes of the day. COST: a pass over the rival products and the player's firms.
        /// </summary>
        public static void Day(RivalryWorld w, int day, List<VentureNote> todays)
        {
            w.Told.Clear();
            w.Decided.Clear();
            w.Opened.Clear();
            w.Sales.Clear();
            if (w.Rec.Since < 0)
            {
                w.Rec.Since = day;
            }
            Backers(w, day, todays);
            Chests(w, day);
            Raises(w, day);
            Copies(w, day);
            Shares(w, day);
            Hiring(w, day);
            Incumbents(w, day);
            Bakes(w, day);
            Looked(w, day);
            Prune(w);
        }

        private static RivalryYou? YouIn(RivalryWorld w, int niche)
        {
            for (int i = 0; niche >= 0 && i < w.You.Count; i++)
            {
                if ((w.You[i].Served & (1 << niche)) != 0)
                {
                    return w.You[i];
                }
            }
            return null;
        }

        // A reaction made: told always, kept for the firm's page when it is against one of the player's firms.
        private static void Say(RivalryWorld w, int kind, int day, RivalProduct r, string place, int niche, long amount, string other = "")
        {
            var x = new RivalryReaction
            {
                Day = day, Kind = kind, Rival = r.Id, Name = r.Name ?? string.Empty, Place = place ?? string.Empty, Niche = niche, Amount = amount, Other = other ?? string.Empty
            };
            w.Told.Add(x);
            if (x.Place.Length > 0)
            {
                w.Rec.Say(x);
            }
        }

        private static string PlaceIn(RivalryWorld w, int niche)
        {
            RivalryYou? y = YouIn(w, niche);
            return y.HasValue ? y.Value.Place : string.Empty;
        }

        // ---------------------------------------------------------------- selling out to a listed company

        /// <summary>
        /// The listed company that buys a startup selling in a market: one that sells there, seeded; with none, any that sells software. -1 when no company does.
        /// </summary>
        public static int Buyer(RivalryWorld w, int market, int day, int startup)
        {
            int near = 0, any = 0;
            for (int c = 0; c < SectorCatalogue.CompanyTeam.Length; c++)
            {
                any += SectorCatalogue.CompanyTeam[c] > 0 ? 1 : 0;
                near += SectorCatalogue.CompanyTeam[c] > 0 && (SectorCatalogue.CompanyMarkets[c] & (1 << market)) != 0 ? 1 : 0;
            }
            if (any == 0)
            {
                return -1;
            }
            bool close = near > 0;
            int pick = TechRandom.Range(w.Seed, startup, day, 99, 0, (close ? near : any) - 1);
            for (int c = 0; c < SectorCatalogue.CompanyTeam.Length; c++)
            {
                bool fits = SectorCatalogue.CompanyTeam[c] > 0 && (!close || (SectorCatalogue.CompanyMarkets[c] & (1 << market)) != 0);
                if (fits && pick-- == 0)
                {
                    return c;
                }
            }
            return -1;
        }

        // The day's sales of startups that have a product are made by a named buyer, and the product goes on with its money.
        private static void Backers(RivalryWorld w, int day, List<VentureNote> todays)
        {
            for (int i = 0; todays != null && i < todays.Count; i++)
            {
                if (todays[i].Kind != VentureRules.NoteAcquired)
                {
                    continue;
                }
                RivalProduct r = w.Sector.StartupProduct(todays[i].Startup);
                if (r != null)
                {
                    Back(w, r, day, Buyer(w, r.Market, day, r.Startup), false);
                }
            }
        }

        private static void Back(RivalryWorld w, RivalProduct r, int day, int company, bool deliberate)
        {
            if (company < 0)
            {
                return;
            }
            RivalryRec rec = w.Rec.RecFor(r.Id);
            if (rec.Backer >= 0)
            {
                return;
            }
            int niche = NicheOf(w, r, rec);
            rec.Backer = company;
            rec.BackerDay = day;
            rec.Niche = niche;
            if (r.Tier > 0)
            {
                r.Tier = 0;
                r.LastCut = day;
            }
            Say(w, deliberate ? ReactKind.Bought : ReactKind.Backed, day, r, PlaceIn(w, niche), niche, 0, HiringCatalogue.RivalFirms[company]);
        }

        /// <summary>Whether a rival product is kept when its startup is gone: a listed company bought it, and it goes on as that company's money lets it.</summary>
        public static bool Kept(RivalryFile rec, RivalProduct r)
        {
            RivalryRec x = rec != null ? rec.RecOf(r.Id) : null;
            return x != null && x.Backer >= 0;
        }

        // ---------------------------------------------------------------- a war chest

        /// <summary>Whether a startup could bring its next round forward: it runs, has not raised lately, is not at the top and meets the bar the round rules set.</summary>
        public static bool CanBringForward(Startup s, int day)
        {
            if (s == null || !s.Alive || s.State != (int)StartupState.Running || s.Stage >= (int)RoundStage.Growth || day - s.LastRound < RivalryCatalogue.ChestGap)
            {
                return false;
            }
            int next = s.Stage + 1;
            return s.Mrr >= VentureCatalogue.RoundBar[TalentCatalogue.Clamp(next, 0, (int)RoundStage.Count - 1)] || (next <= (int)RoundStage.Seed && s.Quality >= 600);
        }

        /// <summary>
        /// A startup starts raising its next round now and the round rules close it after <see cref="RivalryCatalogue.ChestBring"/> days instead of ten: the lead, the value and the
        /// terms are the round rules' own (<see cref="VentureRules.Day"/> closes it), so it is priced and papered like any city startup's.
        /// </summary>
        public static void BringForward(Startup s, int day)
        {
            s.State = (int)StartupState.Raising;
            s.LastRound = day - (RaiseDays - RivalryCatalogue.ChestBring);
        }

        // The player raised: rivals in the niche that can bring a round forward may, and a round that has closed starts its war chest.
        private static void Raises(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.You.Count; i++)
            {
                RivalryYou y = w.You[i];
                RivalrySeen seen = w.Rec.SeenOf(y.Place);
                if (seen == null)
                {
                    continue;   // the first look only notes where the firm stands: nothing before it counts
                }
                long grew = y.Invested - seen.Invested;
                if (grew <= 0 || grew < VentureRules.RoundSize(Math.Max(0, y.Stage)) * RivalryCatalogue.ChestMinShare / 1000)
                {
                    continue;
                }
                for (int k = 0; k < w.Sector.Rivals.Count; k++)
                {
                    RivalProduct r = w.Sector.Rivals[k];
                    if (!r.Startups || r.Niche < 0 || (y.Served & (1 << r.Niche)) == 0)
                    {
                        continue;
                    }
                    Startup s = w.Venture.StartupOf(r.Startup);
                    RivalryRec rec = w.Rec.RecFor(r.Id);
                    if (rec.ChestStage >= 0 || !CanBringForward(s, day) || TechRandom.Mille(w.Seed, s.Id, day, 91) >= RivalryCatalogue.ChestChance)
                    {
                        continue;
                    }
                    BringForward(s, day);
                    rec.ChestStage = s.Stage;
                    rec.ChestDay = day;
                    Say(w, ReactKind.Chest, day, r, y.Place, r.Niche, VentureRules.RoundSize(s.Stage + 1));
                }
            }
        }

        // A round brought forward that has closed puts its money to work on reach and price; one that found no lead is forgotten.
        private static void Chests(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.Rec.Recs.Count; i++)
            {
                RivalryRec rec = w.Rec.Recs[i];
                if (rec.ChestStage < 0)
                {
                    continue;
                }
                RivalProduct r = ProductOf(w.Sector, rec.Id);
                Startup s = r != null && r.Startups ? w.Venture.StartupOf(r.Startup) : null;
                if (s == null || !s.Alive)
                {
                    rec.ChestStage = -1;
                }
                else if (s.Stage > rec.ChestStage)
                {
                    rec.ChestMoney = VentureRules.RoundSize(s.Stage);
                    rec.ChestUntil = day + RivalryCatalogue.ChestDays;
                    rec.ChestStage = -1;
                    if (r.Tier > 0)
                    {
                        r.Tier = 0;
                        r.LastCut = day;
                    }
                    int niche = NicheOf(w, r, rec);
                    Say(w, ReactKind.Funded, day, r, PlaceIn(w, niche), niche, rec.ChestMoney);
                }
                else if (s.State == (int)StartupState.Running && day - rec.ChestDay > RaiseDays + 5)
                {
                    rec.ChestStage = -1;
                }
            }
        }

        // ---------------------------------------------------------------- copying a lead

        /// <summary>
        /// The days a rival takes to build the features between its own and a target with the share of its team that copies, and for a lab
        /// to train them too at its own pace (<see cref="InfraRules.TrainAlone"/>, less what it has trained already), as every model's
        /// features are: never under a week, never over three months.
        /// </summary>
        public static int CopyDelay(SectorFile f, RivalProduct r, int target)
        {
            long cost = 0, train = 0;
            bool lab = r.Market >= (int)SectorMarket.ConsumerAi;
            for (int k = Math.Max(0, r.Features); k < target && k < Math.Max(0, r.Features) + 200; k++)
            {
                cost += FirmsRules.FeatureCost(k);
                train = lab ? Math.Min(long.MaxValue / 4, train + InfraRules.TrainNeed(k)) : 0;
            }
            long perDay = Math.Max(1L, (long)SectorRules.RivalPoints(f, r) * RivalryCatalogue.CopyFocus / 1000);
            long days = (cost + perDay - 1) / perDay;
            if (lab)
            {
                long pace = Math.Max(1L, InfraRules.TrainAlone(SectorRules.LabOwn(r), out _));
                days = Math.Max(days, (Math.Max(0L, train - Math.Max(0L, r.Compute)) + pace - 1) / pace);
            }
            return (int)Math.Max(RivalryCatalogue.CopyLeast, Math.Min(RivalryCatalogue.CopyMost, days));
        }

        private static void Copies(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.You.Count; i++)
            {
                RivalryYou y = w.You[i];
                RivalrySeen seen = w.Rec.SeenOf(y.Place);
                if (seen == null || y.Features <= seen.Features)
                {
                    continue;   // a lead is a feature shipped since the last look
                }
                for (int k = 0; k < w.Sector.Rivals.Count; k++)
                {
                    RivalProduct r = w.Sector.Rivals[k];
                    if (r.Niche < 0 || (y.Served & (1 << r.Niche)) == 0 || r.Market != y.Market || r.Features >= y.Features)
                    {
                        continue;
                    }
                    RivalryRec rec = w.Rec.RecFor(r.Id);
                    if (rec.CopyTarget > 0 || day - rec.CopyDone < RivalryCatalogue.CopyGap)
                    {
                        continue;
                    }
                    int delay = CopyDelay(w.Sector, r, y.Features);
                    rec.CopyTarget = y.Features;
                    rec.CopyAt = day + delay;
                    rec.CopyPlace = y.Place;
                    Say(w, ReactKind.Copying, day, r, y.Place, r.Niche, delay);
                }
            }
            for (int i = 0; i < w.Rec.Recs.Count; i++)
            {
                RivalryRec rec = w.Rec.Recs[i];
                if (rec.CopyTarget <= 0 || day < rec.CopyAt)
                {
                    continue;
                }
                RivalProduct r = ProductOf(w.Sector, rec.Id);
                if (r != null)
                {
                    int gap = Math.Max(0, rec.CopyTarget - r.Features);
                    for (int k = Math.Max(0, r.Features); r.Market >= (int)SectorMarket.ConsumerAi && k < rec.CopyTarget && r.Compute > 0; k++)
                    {
                        r.Compute = Math.Max(0L, r.Compute - InfraRules.TrainNeed(k));   // a lab's copied features spend their training as built ones do (SectorRules.RivalDay)
                    }
                    if (gap > 0)
                    {
                        r.Features = rec.CopyTarget;
                        r.LastShip = day;
                        r.Progress = 0;
                    }
                    Say(w, ReactKind.Copied, day, r, rec.CopyPlace, r.Niche, gap);
                }
                rec.CopyTarget = 0;
                rec.CopyDone = day;
            }
        }

        // ---------------------------------------------------------------- prices in a niche

        // A rival that sells into a niche cuts its price a step when it has lost a good part of the share it held there lately, and raises it back once it holds it again.
        private static void Shares(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.Sector.Rivals.Count; i++)
            {
                RivalProduct r = w.Sector.Rivals[i];
                if (r.Niche < 0)
                {
                    continue;
                }
                RivalryRec rec = w.Rec.RecFor(r.Id);
                int share = ShareOf(r);
                if (share > rec.PeakShare || day - rec.PeakDay > SectorCatalogue.RivalPeakDays)
                {
                    rec.PeakShare = share;
                    rec.PeakDay = day;
                }
                bool war = rec.ChestUntil > day || (rec.Backer >= 0 && day - rec.BackerDay < SectorCatalogue.RivalRaiseGap);
                if (r.Tier > 0 && rec.PeakShare > 0 && share < rec.PeakShare * (1000 - SectorCatalogue.RivalCutLoss) / 1000 && day - r.LastCut >= SectorCatalogue.RivalCutGap)
                {
                    r.Tier--;
                    r.LastCut = day;
                    Say(w, ReactKind.Cut, day, r, PlaceIn(w, r.Niche), r.Niche, share);
                }
                else if (r.Tier < 1 && !war && rec.PeakShare > 0 && share >= rec.PeakShare * 950 / 1000 && day - r.LastCut >= SectorCatalogue.RivalRaiseGap)
                {
                    r.Tier++;
                    r.LastCut = day;
                    Say(w, ReactKind.Raised, day, r, PlaceIn(w, r.Niche), r.Niche, share);
                }
            }
        }

        // ---------------------------------------------------------------- hiring

        private static void Hiring(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.You.Count; i++)
            {
                RivalProduct r = Poacher(w, w.You[i].Served, out int pull);
                if (r == null)
                {
                    continue;
                }
                RivalryRec rec = w.Rec.RecFor(r.Id);
                if (day - rec.PoachNote >= RivalryCatalogue.PoachNoteGap || rec.PoachNote == 0)
                {
                    rec.PoachNote = day;
                    Say(w, ReactKind.Poach, day, r, w.You[i].Place, r.Niche, pull);
                }
            }
        }

        // ---------------------------------------------------------------- listed companies

        /// <summary>Whether a niche has got big: it holds a good deal more buyers than the average niche of its kind of buyer.</summary>
        public static bool Big(long buyers, long average) => average > 0 && buyers * 1000 >= average * RivalryCatalogue.BigTimes;

        private static void Incumbents(RivalryWorld w, int day)
        {
            long[] sum = new long[(int)SectorBuyers.Count];
            int[] count = new int[(int)SectorBuyers.Count];
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if (w.Soft[n] > 0)
                {
                    sum[NicheCatalogue.Def(n).Buyers] += w.Soft[n];
                    count[NicheCatalogue.Def(n).Buyers]++;
                }
            }
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                NicheDef d = NicheCatalogue.Def(n);
                if (w.Soft[n] <= 0 || !Big(w.Soft[n], sum[d.Buyers] / Math.Max(1, count[d.Buyers])))
                {
                    continue;
                }
                RivalProduct proven = null;
                int entrants = 0;
                for (int i = 0; i < w.Sector.Rivals.Count; i++)
                {
                    RivalProduct r = w.Sector.Rivals[i];
                    RivalryRec rec = r.Niche == n ? w.Rec.RecOf(r.Id) : null;
                    entrants += rec != null && rec.Entrant ? 1 : 0;
                    proven = proven ?? (r.Niche == n && r.Startups && r.Customers > 0 ? r : null);
                }
                if (proven == null && !YouIn(w, n).HasValue)
                {
                    continue;
                }
                int market = NicheRules.MarketOf(d, false);
                if (entrants < RivalryCatalogue.EntrantsMost && TechRandom.Mille(w.Seed, n, day, 95) < RivalryCatalogue.EnterChance)
                {
                    Enter(w, n, market, day);
                }
                for (int i = 0; i < w.Sector.Rivals.Count; i++)
                {
                    RivalProduct r = w.Sector.Rivals[i];
                    Startup s = r.Niche == n && r.Startups ? w.Venture.StartupOf(r.Startup) : null;
                    RivalryRec rec = s != null ? w.Rec.RecOf(r.Id) : null;
                    if (s == null || !s.Alive || s.Stage < (int)RoundStage.Seed || (rec != null && rec.Backer >= 0) || TechRandom.Mille(w.Seed, s.Id, day, 96) >= RivalryCatalogue.BuyChance)
                    {
                        continue;
                    }
                    long price = Math.Max(s.Mrr * 12 * TechRandom.Range(w.Seed, s.Id, day, 44, VentureCatalogue.AcquireMultipleLeast, VentureCatalogue.AcquireMultipleMost), s.Post / 2);
                    VentureRules.Sell(w.Venture, s, price, VentureRules.NoteAcquired, w.Sales);
                    Back(w, r, day, Buyer(w, r.Market, day, s.Id), true);
                    break;
                }
            }
        }

        private static void Enter(RivalryWorld w, int niche, int market, int day)
        {
            if (w.Sector.Rivals.Count >= SectorCatalogue.RivalProductsMost)
            {
                return;
            }
            int count = 0;
            for (int c = 0; c < SectorCatalogue.CompanyTeam.Length; c++)
            {
                count += SectorCatalogue.CompanyTeam[c] > 0 && w.Sector.RivalOf(c, market) == null ? 1 : 0;
            }
            if (count == 0)
            {
                return;
            }
            int pick = TechRandom.Range(w.Seed, niche, day, 94, 0, count - 1), company = -1;
            for (int c = 0; c < SectorCatalogue.CompanyTeam.Length && company < 0; c++)
            {
                if (SectorCatalogue.CompanyTeam[c] > 0 && w.Sector.RivalOf(c, market) == null && pick-- == 0)
                {
                    company = c;
                }
            }
            SectorMarketDay md = w.Sector.Markets[market];
            var r = new RivalProduct
            {
                Id = w.Sector.NextRival++, Company = company, Market = market, Name = HiringCatalogue.RivalFirms[company],
                Features = Math.Max(1, Math.Max(md.RivalFeatures, md.Average)), LastShip = day, LastCut = day - SectorCatalogue.RivalCutGap, PeakDay = day,
                Peak = 1   // a company's first day sets the customers it has by its share of the market (SectorRules.Settle); one that comes into a niche starts with none
            };
            w.Sector.Rivals.Add(r);
            RivalryRec rec = w.Rec.RecFor(r.Id);
            rec.Entrant = true;
            rec.Niche = niche;
            Say(w, ReactKind.Entered, day, r, PlaceIn(w, niche), niche, 0, r.Name);
        }

        // ---------------------------------------------------------------- housekeeping

        // Where the player's firms stand, for tomorrow's raise and lead to be new.
        private static void Looked(RivalryWorld w, int day)
        {
            for (int i = 0; i < w.You.Count; i++)
            {
                RivalryYou y = w.You[i];
                RivalrySeen seen = w.Rec.SeenOf(y.Place);
                if (seen == null)
                {
                    seen = new RivalrySeen { Place = y.Place };
                    w.Rec.Seen.Add(seen);
                }
                seen.Invested = y.Invested;
                seen.Features = y.Features;
                seen.Day = day;
            }
        }

        private static void Prune(RivalryWorld w)
        {
            for (int i = w.Rec.Recs.Count - 1; i >= 0; i--)
            {
                if (ProductOf(w.Sector, w.Rec.Recs[i].Id) == null)
                {
                    w.Rec.Recs.RemoveAt(i);
                }
            }
        }
    }
}
