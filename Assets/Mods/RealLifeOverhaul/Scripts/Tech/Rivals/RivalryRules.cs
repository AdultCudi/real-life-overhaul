using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of competition in niches (the plan's section 7): the city's startups sell into the niche their founding gave them, from the same pool of buyers as the player's
    /// products; rivals react on the day, seeded; big customers hold bake offs. Whole numbers and seeded rolls only, so every machine gets the same. Game-free: EconomyCheck's
    /// StartupRivalsChecks hold it.
    /// </summary>
    public static partial class RivalryRules
    {
        private static int Clamp(int v, int least, int most) => v < least ? least : v > most ? most : v;

        // ---------------------------------------------------------------- one pool of buyers

        /// <summary>
        /// How much of the shared niche pool counts on a day, thousandths: none on the day a save is first run with competition (so nothing jumps), all of it after
        /// <see cref="RivalryCatalogue.RampDays"/> days. The market's share of the niche is what an older save's products were sold into.
        /// </summary>
        public static int Weight(int day, int since) => since < 0 ? 0 : Clamp((day - since) * 1000 / RivalryCatalogue.RampDays, 0, 1000);

        /// <summary>A figure moving from what it was to what it is, by the weight.</summary>
        public static long Blend(long old, long now, int weight) => old + (now - old) * Clamp(weight, 0, 1000) / 1000;

        /// <summary>The product a rival id belongs to, or null.</summary>
        public static RivalProduct ProductOf(SectorFile f, int id)
        {
            for (int i = 0; f != null && i < f.Rivals.Count; i++)
            {
                if (f.Rivals[i].Id == id)
                {
                    return f.Rivals[i];
                }
            }
            return null;
        }

        /// <summary>The niche a rival product sells into: a startup's from its seed (or the one it had when a listed company bought it), a company's that came in, else -1.</summary>
        public static int NicheOf(RivalryWorld w, RivalProduct r, RivalryRec rec)
        {
            if (rec != null && rec.Niche >= 0)
            {
                return rec.Niche;
            }
            if (r.Startups && r.Startup > 0 && w.Venture != null)
            {
                Startup s = w.Venture.StartupOf(r.Startup);
                return s != null ? NicheRules.OfStartup(w.Venture.Seed, s.Id, s.Sector) : -1;
            }
            return -1;
        }

        /// <summary>
        /// A day's rivals set up before their pull is worked out: which niche each sells into, the frame it is weighed in (the niche's buyers and the price it bears, fading in from
        /// the market's), and what a war chest or a listed company behind it adds to its reach and team. Call after the markets are begun and the player's products counted.
        /// </summary>
        public static void Prepare(RivalryWorld w, int day)
        {
            w.Pool.Clear();
            int weight = Weight(day, w.Rec.Since);
            List<RivalProduct> rivals = w.Sector.Rivals;
            for (int i = 0; i < rivals.Count; i++)
            {
                RivalProduct r = rivals[i];
                RivalryRec rec = w.Rec.RecOf(r.Id);
                r.Niche = -1;
                r.PushMarketing = 0;
                r.PushPoints = 0;
                if (rec != null)
                {
                    Push(rec, day, r);
                }
                int n = NicheOf(w, r, rec);
                SectorMarketDay md = w.Sector.Markets[Clamp(r.Market, 0, w.Sector.Markets.Length - 1)];
                long nb = n >= 0 && NicheRules.Serves(NicheCatalogue.Def(n), r.Market) ? Math.Min(w.NicheBuyers(n, r.Market), md.Buyers) : 0L;
                if (nb <= 0)
                {
                    r.Frame = null;
                    r.View = null;
                    continue;
                }
                r.Niche = n;
                SectorMarketDay frame = r.Frame ?? (r.Frame = new SectorMarketDay());
                NicheRules.View(frame, md, Blend(md.Buyers, nb, weight), (int)Blend(md.Going, NicheRules.Going(NicheCatalogue.Def(n), md.Going), weight));
            }
        }

        // The reach and team a war chest or a listed company's money adds to a rival today.
        private static void Push(RivalryRec rec, int day, RivalProduct r)
        {
            if (rec.ChestUntil > day)
            {
                r.PushMarketing += ChestReach(rec.ChestMoney);
            }
            if (rec.Backer >= 0 && rec.Backer < SectorCatalogue.CompanyMarketing.Length)
            {
                r.PushMarketing += SectorCatalogue.CompanyMarketing[rec.Backer] * RivalryCatalogue.BackMarketing / 1000;
                r.PushPoints += SectorCatalogue.CompanyTeam[rec.Backer] * RivalryCatalogue.BackPoints / 1000;
            }
        }

        /// <summary>
        /// What a round's money spends on reach a day while the chest lasts: its share over its days, never more than the reach of the average listed company that sells
        /// software (the catalogue's own marketing figures).
        /// </summary>
        public static long ChestReach(long money)
        {
            long sum = 0;
            int n = 0;
            for (int c = 0; c < SectorCatalogue.CompanyMarketing.Length; c++)
            {
                if (SectorCatalogue.CompanyMarketing[c] > 0)
                {
                    sum += SectorCatalogue.CompanyMarketing[c];
                    n++;
                }
            }
            long most = n > 0 ? sum / n : 0;
            return Math.Min(most, Math.Max(0L, money) * RivalryCatalogue.ChestSpend / 1000 / RivalryCatalogue.ChestDays);
        }

        /// <summary>
        /// The niche's pool after the rivals' pull is worked out and the player's products added: every rival bound to a niche added, then its view set (the buyers of the niche and
        /// the pull of everyone who sells there, fading in from the market's). A rival's customers move towards its share of that view.
        /// </summary>
        public static void Views(RivalryWorld w, int day)
        {
            int weight = Weight(day, w.Rec.Since);
            List<RivalProduct> rivals = w.Sector.Rivals;
            for (int i = 0; i < rivals.Count; i++)
            {
                if (rivals[i].Niche >= 0)
                {
                    w.Pool.AddRival(rivals[i].Niche, rivals[i].Market, rivals[i].Attraction);
                }
            }
            for (int i = 0; i < rivals.Count; i++)
            {
                RivalProduct r = rivals[i];
                if (r.Niche < 0 || r.Frame == null)
                {
                    continue;
                }
                int m = Clamp(r.Market, 0, w.Sector.Markets.Length - 1);
                SectorMarketDay md = w.Sector.Markets[m];
                long nb = Math.Min(w.NicheBuyers(r.Niche, r.Market), md.Buyers);
                int at = NichePool.At(r.Niche, m);
                long now = w.Pool.Player[at] + w.Pool.Rival[at] + w.Pool.Rest(m, md.All) * NicheRules.RivalShare(nb, md.Buyers) / 1000;
                SectorMarketDay v = r.View ?? (r.View = new SectorMarketDay());
                v.Clear();
                v.Who = md.Who;
                v.Spend = md.Spend;
                v.Buyers = r.Frame.Buyers;
                v.All = Math.Max(r.Attraction, Blend(md.All, Math.Max(now, r.Attraction), weight));
            }
        }

        /// <summary>
        /// The pull of everyone in the niches a product of the player's sells into, for its share: the products and rivals there in full, the rest of the market by the niche's share
        /// of its rivals, fading in from the old way (the market's pull less its own, by that share).
        /// </summary>
        public static long PlayerAll(RivalryWorld w, int served, int market, long marketAll, long own, long ownMarket, long nicheBuyers, long marketBuyers, int day)
        {
            int m = Clamp(market, 0, NichePool.Markets - 1);
            int share = NicheRules.RivalShare(nicheBuyers, marketBuyers);
            long old = own + Math.Max(0L, marketAll - ownMarket) * share / 1000;
            long now = w.Pool.Rest(m, marketAll) * share / 1000;
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if ((served & (1 << n)) != 0)
                {
                    now += w.Pool.Player[NichePool.At(n, m)] + w.Pool.Rival[NichePool.At(n, m)];
                }
            }
            return Blend(old, Math.Max(own, now), Weight(day, w.Rec.Since));
        }

        /// <summary>A rival's share of its niche's buyers, thousandths; 0 for a rival not bound to a niche.</summary>
        public static int ShareOf(RivalProduct r)
        {
            long buyers = r.View != null ? r.View.Buyers : 0L;
            return buyers > 0 ? (int)Math.Min(1000L, Math.Max(0L, r.Customers) * 1000 / buyers) : 0;
        }

        // ---------------------------------------------------------------- the rivals' companions

        /// <summary>True when a rival can be counted as having money behind it: a listed company's, one that came into a niche, or a startup past its seed with months of runway.</summary>
        public static bool Funded(RivalryWorld w, RivalProduct r, RivalryRec rec)
        {
            if (rec != null && (rec.Backer >= 0 || rec.Entrant))
            {
                return true;
            }
            if (!r.Startups || w.Venture == null)
            {
                return false;
            }
            Startup s = w.Venture.StartupOf(r.Startup);
            return s != null && s.Alive && s.Stage >= (int)RoundStage.Seed && VentureRules.Runway(s.Cash, s.Burn, s.Mrr) >= RivalryCatalogue.FundedRunway;
        }

        /// <summary>
        /// The rival that comes for the people of a firm that sells into the niches in <paramref name="served"/>: a funded rival in one of them, the one with most behind it.
        /// How hard it pulls (thousandths for TalentRules.Approached) comes out, 1000 with none.
        /// </summary>
        public static RivalProduct Poacher(RivalryWorld w, int served, out int pull)
        {
            pull = 1000;
            RivalProduct best = null;
            for (int i = 0; w.Sector != null && i < w.Sector.Rivals.Count; i++)
            {
                RivalProduct r = w.Sector.Rivals[i];
                if (r.Niche < 0 || (served & (1 << r.Niche)) == 0)
                {
                    continue;
                }
                RivalryRec rec = w.Rec.RecOf(r.Id);
                if (!Funded(w, r, rec))
                {
                    continue;
                }
                int p = rec != null && (rec.Backer >= 0 || rec.Entrant) ? RivalryCatalogue.PoachBacked : RivalryCatalogue.PoachFunded;
                if (p > pull || (p == pull && best != null && r.Customers > best.Customers))
                {
                    pull = p;
                    best = r;
                }
            }
            return best;
        }
    }
}
