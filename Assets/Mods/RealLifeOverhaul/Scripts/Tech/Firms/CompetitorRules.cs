using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a competitor of a firm is.</summary>
    public enum RivalKind
    {
        Company = 0,    // one of the city's tech companies (Tech/Hiring/HiringCatalogue.RivalFirms), listed on the stock market
        Startup = 1,    // one of the city's startups (Tech/Venture), while it runs
        Firm = 2,       // a business the player runs, or another player in a Going Public session
        School = 3,     // a place that teaches courses: the game's school, the university (the training academy's only)
        Count = 4
    }

    /// <summary>A business of the player's or of another player's, as the game layer read it for the rule. Game-free.</summary>
    public struct FirmSeen
    {
        public int Kind;          // CompetitorRules: a FirmKind, or CompetitorRules.Academy
        public string Place;      // its place key (TalentGame.Place)
        public string Name;
        public bool Theirs;       // another player's, in a Going Public session
    }

    /// <summary>One competitor of a kind of firm, as the rule lists it; where it is is the game layer's to add. Game-free.</summary>
    public sealed class Competitor
    {
        public int Kind;                        // RivalKind
        public int Company = -1;                // a company: its index in HiringCatalogue.RivalFirms
        public int Startup;                     // a startup: its id
        public int Sector;                      // a startup: its Venture
        public int Firm = -1;                   // a business: its kind
        public bool Theirs;                     // a business: another player's
        public string Place = string.Empty;     // a business: its place key
        public string Name = string.Empty;
        // ---- where it is, read from the game when the Tech app shows it, never saved
        public string Street;                   // null: nowhere to go
        public int Number;
        public string Building;                 // a company's tower
        public int Floor;                       // a company's floor of its tower, from 1
        public string District;                 // a business's district, in words

        /// <summary>Its id among a firm's competitors: the kind and what it is.</summary>
        public string Key => Kind == (int)RivalKind.Company ? "c" + Company : Kind == (int)RivalKind.Startup ? "s" + Startup : Kind == (int)RivalKind.School ? "k" + Name : "f" + Place;

        public void Reset()
        {
            Kind = 0;
            Company = -1;
            Startup = 0;
            Sector = 0;
            Firm = -1;
            Theirs = false;
            Place = string.Empty;
            Name = string.Empty;
            Street = null;
            Number = 0;
            Building = null;
            Floor = 0;
            District = null;
        }
    }

    /// <summary>
    /// WHO COMPETES WITH A KIND OF FIRM: one rule for BizMan's start screen (the count on the cards of the mod's own six
    /// types, Tech/Firms/StartCards) and the Tech app's Competitors page (Tech/Firms/TechApp.Rivals). Game-free: EconomyCheck
    /// checks it (CompetitorChecks.cs).
    ///
    /// The kinds are the tech sector's (<see cref="FirmKind"/>: the five of the mod's own types, the game's web development
    /// agency and graphic designer) and the training academy (<see cref="Academy"/>, Careers/AcademyType). A firm competes,
    /// by what each makes and takes on (FirmsCatalogue.MakesProducts and TakesWork), with:
    ///   the companies    the city's twelve tech companies (HiringCatalogue.RivalFirms, the stock market's), each making
    ///                    the products and taking on the work <see cref="CompanyMakes"/> and <see cref="CompanyTakes"/> say;
    ///                    each product kind and segment's rival (FirmsCatalogue.RivalStock) is one of them and makes it
    ///   the startups     the city's startups while they run, by what they do (<see cref="StartupSectors"/>)
    ///   the firms        every business the player runs, or another player in a Going Public session, whose kind makes or
    ///                    takes on something the same (the firm itself left out)
    ///   the schools      for the academy alone: the places that teach courses (the game's Manhattan Business School, the
    ///                    university), and other academies by the rule above
    /// The game's rivals open only the game's own types, so none of theirs is one; the game's own types keep the game's
    /// count of its neighbourhood (<see cref="InCity"/> is false for them).
    ///
    /// A council tender's rival bidders (Contract.Rivals) are companies too: those that take on its kind of work, chosen
    /// from the tender's id and the save's seed (<see cref="TenderRival"/>), so the same tender has the same bidders on
    /// every load and every machine; their bids are FirmsRules.RivalBid, the very ones the tender is decided by.
    ///
    /// Cost: a pass over twelve companies, the startups (twenty at most) and the businesses given; nothing kept.
    /// </summary>
    public static class CompetitorRules
    {
        /// <summary>The training academy, after the tech sector's kinds.</summary>
        public const int Academy = (int)FirmKind.Count;

        /// <summary>Every kind the rule knows.</summary>
        public const int Kinds = Academy + 1;

        /// <summary>Each company's id on the city's stock market (Markets/MarketCatalogue), in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyStock = { 106, 107, 108, 109, 110, 111, 112, 101, 103, 17, 43, 45 };

        private const int Saas = 1 << (int)ProductKind.Saas, App = 1 << (int)ProductKind.App, Licence = 1 << (int)ProductKind.Licence, Game = 1 << (int)ProductKind.Game, Model = 1 << (int)ProductKind.Model;
        private const int Website = 1 << (int)WorkKind.Website, Integration = 1 << (int)WorkKind.Integration, Managed = 1 << (int)WorkKind.Managed, Audit = 1 << (int)WorkKind.Audit,
            Incident = 1 << (int)WorkKind.Incident;

        /// <summary>The products each company makes, one bit per ProductKind, in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyMakes =
        {
            Saas | Licence,   // Codeharbour Software: software for companies, sold by subscription and licence
            0,                // Skyvault Systems: the cloud (the rival clouds of Tech/Infra), and services on it
            Saas,             // Abacus Lane Software: business software
            Saas | App,       // Shopfront Online: online shops for people and their apps
            Game,             // Pixel Harbour Games
            Saas | Model,     // Tallyrow Analytics: analytics for businesses and the models behind it
            App | Model,      // Signpost Search: search, its apps and its AI
            0,                // Circuitry Works: computers
            0,                // Quartzline Semiconductors: chips
            Saas | App,       // Ledgerline Payments: payments for businesses, and its app
            0,                // Signalhill Telecom
            0                 // Fibreway Networks
        };

        /// <summary>The contracts each company takes on, one bit per WorkKind, in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyTakes =
        {
            Website | Integration,   // Codeharbour Software
            Integration | Managed,   // Skyvault Systems
            0,                       // Abacus Lane Software
            Website,                 // Shopfront Online
            0,                       // Pixel Harbour Games
            Integration,             // Tallyrow Analytics
            0,                       // Signpost Search
            Integration | Managed,   // Circuitry Works: fitting and running companies' computers
            0,                       // Quartzline Semiconductors
            Integration,             // Ledgerline Payments: payments built into a client's systems
            Managed | Incident,      // Signalhill Telecom: managed networks and their security
            Managed | Audit          // Fibreway Networks: managed networks, and their audits
        };

        /// <summary>The startups each kind competes with, one bit per Venture, by kind (FirmKind, then the academy).</summary>
        public static readonly int[] StartupSectors =
        {
            (1 << (int)Venture.Software) | (1 << (int)Venture.Fintech) | (1 << (int)Venture.Health) | (1 << (int)Venture.Marketplace) | (1 << (int)Venture.DevTools),
            1 << (int)Venture.Games,
            (1 << (int)Venture.Software) | (1 << (int)Venture.DevTools),
            1 << (int)Venture.Security,
            1 << (int)Venture.Ai,
            1 << (int)Venture.Marketplace,
            0,
            0
        };

        /// <summary>True for the kinds of the mod's own six types: BizMan's start screen says their competitors in the city. The game's own two keep the game's count.</summary>
        public static bool InCity(int kind) => (kind >= 0 && kind <= (int)FirmKind.AiLab) || kind == Academy;

        /// <summary>What a kind makes, one bit per ProductKind (the academy makes none).</summary>
        public static int Makes(int kind) => kind >= 0 && kind < (int)FirmKind.Count ? FirmsCatalogue.MakesProducts[kind] : 0;

        /// <summary>What a kind takes on, one bit per WorkKind (the academy takes none).</summary>
        public static int Takes(int kind) => kind >= 0 && kind < (int)FirmKind.Count ? FirmsCatalogue.TakesWork[kind] : 0;

        /// <summary>True when two kinds of firm compete: they make a kind of product or take on a kind of work the same; an academy with an academy alone.</summary>
        public static bool Competes(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Kinds || b >= Kinds)
            {
                return false;
            }
            if (a == Academy || b == Academy)
            {
                return a == b;
            }
            return (Makes(a) & Makes(b)) != 0 || (Takes(a) & Takes(b)) != 0;
        }

        /// <summary>True when a company competes with a kind of firm.</summary>
        public static bool CompanyCompetes(int kind, int company)
        {
            return company >= 0 && company < CompanyMakes.Length && kind >= 0 && kind < Academy
                   && ((CompanyMakes[company] & Makes(kind)) != 0 || (CompanyTakes[company] & Takes(kind)) != 0);
        }

        /// <summary>True when a startup of a sector competes with a kind of firm.</summary>
        public static bool StartupCompetes(int kind, int sector) => kind >= 0 && kind < Kinds && sector >= 0 && sector < (int)Venture.Count && (StartupSectors[kind] & (1 << sector)) != 0;

        /// <summary>The company whose stock is this id, or -1.</summary>
        public static int CompanyOfStock(int stock) => Array.IndexOf(CompanyStock, stock);

        /// <summary>The company that is a product's rival (FirmsCatalogue.RivalStock), or -1.</summary>
        public static int RivalOf(Product p) => p != null ? CompanyOfStock(FirmsCatalogue.RivalStock(p.K, p.S)) : -1;

        /// <summary>What a firm of a kind and a competitor meet on: the products both make and the work both take on, as bits (products in the low byte, work above it).</summary>
        public static int Meet(int kind, int makes, int takes) => (Makes(kind) & makes) | ((Takes(kind) & takes) << 8);

        /// <summary>
        /// The competitors of a kind of firm in the city, in order: the companies (in the catalogue's order), the startups
        /// that run (in their order), the schools (the academy's), then the businesses given, but the one at
        /// <paramref name="skip"/> (the firm itself). Returns how many; each is put in <paramref name="into"/> unless it is
        /// null (BizMan's count), from <paramref name="pool"/> where there is one spare.
        /// </summary>
        public static int Gather(int kind, IList<Startup> startups, IList<string> schools, IList<FirmSeen> firms, string skip, List<Competitor> into, List<Competitor> pool)
        {
            into?.Clear();
            if (kind < 0 || kind >= Kinds)
            {
                return 0;
            }
            int n = 0;
            for (int c = 0; c < CompanyMakes.Length; c++)
            {
                if (!CompanyCompetes(kind, c))
                {
                    continue;
                }
                n++;
                Competitor made = Take(into, pool);
                if (made != null)
                {
                    made.Kind = (int)RivalKind.Company;
                    made.Company = c;
                    made.Name = c < HiringCatalogue.RivalFirms.Length ? HiringCatalogue.RivalFirms[c] : string.Empty;
                }
            }
            for (int i = 0; startups != null && i < startups.Count; i++)
            {
                Startup s = startups[i];
                if (s == null || !s.Alive || !StartupCompetes(kind, s.Sector))
                {
                    continue;
                }
                n++;
                Competitor made = Take(into, pool);
                if (made != null)
                {
                    made.Kind = (int)RivalKind.Startup;
                    made.Startup = s.Id;
                    made.Sector = s.Sector;
                    made.Name = s.Name ?? string.Empty;
                }
            }
            for (int i = 0; kind == Academy && schools != null && i < schools.Count; i++)
            {
                n++;
                Competitor made = Take(into, pool);
                if (made != null)
                {
                    made.Kind = (int)RivalKind.School;
                    made.Name = schools[i] ?? string.Empty;
                }
            }
            for (int i = 0; firms != null && i < firms.Count; i++)
            {
                FirmSeen f = firms[i];
                if (!Competes(kind, f.Kind) || (skip != null && f.Place == skip))
                {
                    continue;
                }
                n++;
                Competitor made = Take(into, pool);
                if (made != null)
                {
                    made.Kind = (int)RivalKind.Firm;
                    made.Firm = f.Kind;
                    made.Theirs = f.Theirs;
                    made.Place = f.Place ?? string.Empty;
                    made.Name = f.Name ?? string.Empty;
                }
            }
            return n;
        }

        private static Competitor Take(List<Competitor> into, List<Competitor> pool)
        {
            if (into == null)
            {
                return null;
            }
            Competitor c;
            if (pool != null && pool.Count > 0)
            {
                c = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                c.Reset();
            }
            else
            {
                c = new Competitor();
            }
            into.Add(c);
            return c;
        }

        // ---------------------------------------------------------------- the council's tenders

        /// <summary>How many companies take on a kind of work.</summary>
        public static int Takers(WorkKind kind)
        {
            int n = 0;
            for (int c = 0; c < CompanyTakes.Length; c++)
            {
                n += (CompanyTakes[c] & (1 << (int)kind)) != 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A council tender's rival bidder r (from 0 to Rivals less one): one of the companies that take on its work, in a
        /// turn that starts at a place chosen from the save's seed and the tender's id. -1 when no company takes it.
        /// </summary>
        public static int TenderRival(int seed, Contract t, int r)
        {
            if (t == null || r < 0)
            {
                return -1;
            }
            int takers = Takers(t.K);
            if (takers == 0)
            {
                return -1;
            }
            int at = (TechRandom.Range(seed, t.Id, 83, 0, 0, takers - 1) + r) % takers;
            for (int c = 0; c < CompanyTakes.Length; c++)
            {
                if ((CompanyTakes[c] & (1 << t.Kind)) != 0 && at-- == 0)
                {
                    return c;
                }
            }
            return -1;
        }

        /// <summary>Which of a tender's rival bidders a company is, or -1 when it does not bid on it.</summary>
        public static int BidderOf(int seed, Contract t, int company)
        {
            for (int r = 0; t != null && t.Council && r < t.Rivals; r++)
            {
                if (TenderRival(seed, t, r) == company)
                {
                    return r;
                }
            }
            return -1;
        }

        /// <summary>True for a tender that is decided: its bids are known.</summary>
        public static bool Decided(Contract t) => t != null && t.Council && (t.Stage == (int)WorkStage.Lost || (t.Stage >= (int)WorkStage.Active && t.Stage <= (int)WorkStage.Failed)) && t.Bid > 0;

        /// <summary>
        /// True for a council tender a firm and a company both bid for: the company is one of its rival bidders, and the
        /// firm bid on it, or it is still open and the firm takes on its kind of work.
        /// </summary>
        public static bool BothBid(int seed, Contract t, Firm firm, int company)
        {
            if (t == null || firm == null || !t.Council || BidderOf(seed, t, company) < 0)
            {
                return false;
            }
            if (t.Stage == (int)WorkStage.Tender)
            {
                return FirmsRules.Takes(firm.K, t.K);
            }
            return (t.Stage == (int)WorkStage.Bid || Decided(t)) && t.Place == firm.Place;
        }

        // ---------------------------------------------------------------- a product's market

        /// <summary>
        /// A product's share of its market, parts per million: its customers (and a consumer service's free users) of those
        /// its segment can have (FirmsCatalogue.Market); a game's copies sold of the players there are.
        /// </summary>
        public static int SharePpm(Product p)
        {
            if (p == null)
            {
                return 0;
            }
            long market = p.K == ProductKind.Game ? FirmsCatalogue.GamePlayers : FirmsCatalogue.Market[TalentCatalogue.Clamp(p.Segment, 0, 2)];
            long have = p.K == ProductKind.Game ? p.Users : p.Users + p.Free;
            return (int)Math.Max(0L, Math.Min(1000000L, have * 1000000L / Math.Max(1L, market)));
        }
    }
}
