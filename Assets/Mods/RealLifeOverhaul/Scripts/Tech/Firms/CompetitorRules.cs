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

    /// <summary>A business of the player's or another player's, as read from the game for the rule.</summary>
    public struct FirmSeen
    {
        public int Kind;          // CompetitorRules: a FirmKind, or CompetitorRules.Academy
        public string Place;      // its place key (TalentGame.Place)
        public string Name;
        public bool Theirs;       // another player's, in a Going Public session
    }

    /// <summary>One competitor of a kind of firm, as the rule lists it; where it is is added from the game.</summary>
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
        // where it is: read from the game when the Tech app shows it, never saved
        public string Street;                   // null: nowhere to go
        public int Number;
        public string Building;                 // a company's tower
        public int Floor;                       // a company's floor of its tower, from 1
        public string District;                 // a business's district, in words

        /// <summary>Its id among a firm's competitors: the kind and what it is.</summary>
        public string Key =>
            Kind == (int)RivalKind.Company ? "c" + Company : Kind == (int)RivalKind.Startup ? "s" + Startup : Kind == (int)RivalKind.School ? "k" + Name : "f" + Place;

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
    /// Who competes with a kind of firm (a <see cref="FirmKind"/> or the academy): one rule for BizMan's start screen and the
    /// Tech app's Competitors page. A tender's rival bidders come from its id and the save's seed, the same on every machine.
    /// </summary>
    public static class CompetitorRules
    {
        /// <summary>The training academy, after the tech sector's kinds.</summary>
        public const int Academy = (int)FirmKind.Count;

        public const int Kinds = Academy + 1;

        /// <summary>Each company's id on the city's stock market (Markets/MarketCatalogue), in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyStock = { 106, 107, 108, 109, 110, 111, 112, 101, 103, 17, 43, 45 };

        private const int Saas = 1 << (int)ProductKind.Saas, App = 1 << (int)ProductKind.App, Licence = 1 << (int)ProductKind.Licence,
            Game = 1 << (int)ProductKind.Game, Model = 1 << (int)ProductKind.Model;
        private const int Website = 1 << (int)WorkKind.Website, Integration = 1 << (int)WorkKind.Integration, Managed = 1 << (int)WorkKind.Managed,
            Audit = 1 << (int)WorkKind.Audit, Incident = 1 << (int)WorkKind.Incident;

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

        /// <summary>True for the kinds of the mod's own six types, whose start screen cards give their competitors in the city.</summary>
        public static bool InCity(int kind) => (kind >= 0 && kind <= (int)FirmKind.AiLab) || kind == Academy;

        /// <summary>What a kind makes, one bit per ProductKind (the academy makes none).</summary>
        public static int Makes(int kind) => kind >= 0 && kind < (int)FirmKind.Count ? FirmsCatalogue.MakesProducts[kind] : 0;

        /// <summary>What a kind takes on, one bit per WorkKind (the academy takes none).</summary>
        public static int Takes(int kind) => kind >= 0 && kind < (int)FirmKind.Count ? FirmsCatalogue.TakesWork[kind] : 0;

        /// <summary>True when two kinds make or take on something the same; an academy competes with an academy alone.</summary>
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

        public static bool CompanyCompetes(int kind, int company)
        {
            return company >= 0 && company < CompanyMakes.Length && kind >= 0 && kind < Academy
                   && ((CompanyMakes[company] & Makes(kind)) != 0 || (CompanyTakes[company] & Takes(kind)) != 0);
        }

        public static bool StartupCompetes(int kind, int sector) =>
            kind >= 0 && kind < Kinds && sector >= 0 && sector < (int)Venture.Count && (StartupSectors[kind] & (1 << sector)) != 0;

        /// <summary>The company whose stock is this id, or -1.</summary>
        public static int CompanyOfStock(int stock) => Array.IndexOf(CompanyStock, stock);

        /// <summary>The company that is a product's rival (FirmsCatalogue.RivalStock), or -1.</summary>
        public static int RivalOf(Product p) => p != null ? CompanyOfStock(FirmsCatalogue.RivalStock(p.K, p.S)) : -1;

        /// <summary>What a kind and a competitor meet on: products both make in the low byte, work both take on above it.</summary>
        public static int Meet(int kind, int makes, int takes) => (Makes(kind) & makes) | ((Takes(kind) & takes) << 8);

        /// <summary>A kind's competitors in the city, the firm at <paramref name="skip"/> left out; each goes into <paramref name="into"/> unless null.</summary>
        public static int Gather(int kind, IList<Startup> startups, IList<string> schools, IList<FirmSeen> firms, string skip, List<Competitor> into,
            List<Competitor> pool)
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

        public static int Takers(WorkKind kind)
        {
            int n = 0;
            for (int c = 0; c < CompanyTakes.Length; c++)
            {
                n += (CompanyTakes[c] & (1 << (int)kind)) != 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>A tender's rival bidder r (0 to Rivals - 1), from a start the seed and the tender's id choose; -1 when no company takes it.</summary>
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
        public static bool Decided(Contract t) =>
            t != null && t.Council && (t.Stage == (int)WorkStage.Lost || (t.Stage >= (int)WorkStage.Active && t.Stage <= (int)WorkStage.Failed)) && t.Bid > 0;

        /// <summary>True for a tender the company bids for and the firm bid on, or could while it is open.</summary>
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

        /// <summary>A product's share of its market, parts per million: customers and free users of the segment's, or a game's copies of its players.</summary>
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
