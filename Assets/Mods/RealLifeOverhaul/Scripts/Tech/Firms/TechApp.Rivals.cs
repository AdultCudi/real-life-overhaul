using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Competitors page of each of the player's firms, a page of the Firms tab (opened from the firm's page; the tab
    /// takes the player back): in the table every competitor the rule gives (Tech/Firms/CompetitorRules: the city's tech
    /// companies, its startups, the other firms the player and other players run), what each is, where the two meet, how
    /// many of the firm's products have it as their rival, the council's tenders both bid for, and where it is (a company's
    /// tower and floor, a business's district). Picked, a competitor's page: the products against it, the firm's against
    /// theirs (features, its share of the market), the tenders with their bids once decided, and where it is, with the
    /// game's own Set destination. Written when it is shown, from what the director reads then.
    /// </summary>
    public sealed partial class TechApp
    {
        public const string RivalsRow = "rivals:", RivalRow = "rival:";
        private const int RivalLines = 3;   // products and tenders listed on a competitor's page, then "and so many more"

        private readonly List<FirmPlace> _rivalFirms = new List<FirmPlace>(8);
        private readonly StringBuilder _words = new StringBuilder(160);

        /// <summary>What to show when a competitor's page closes: the firm's list of competitors; null for anything else.</summary>
        private static string RivalsBack(string picked)
        {
            string rival = Picked(picked, RivalRow);
            int bar = rival != null ? rival.IndexOf('|') : -1;
            return bar > 0 ? RivalsRow + rival.Substring(0, bar) : null;
        }

        private TableSpec WriteRivals(InfoBox box, FirmsFile f, int day)
        {
            string place = Picked(_picked, RivalsRow), pick = null;
            if (place == null)
            {
                string rival = Picked(_picked, RivalRow);
                int bar = rival != null ? rival.IndexOf('|') : -1;
                place = bar > 0 ? rival.Substring(0, bar) : null;
                pick = bar > 0 ? rival.Substring(bar + 1) : null;
            }
            FirmPlace firm = place != null ? _director.FirmOf(place) : null;
            if (firm == null)
            {
                _picked = null;
                return WriteFirms(box, f, day);
            }
            IReadOnlyList<Competitor> rivals = _director.RivalsOf(firm);
            Competitor picked = null;
            int companies = 0, startups = 0, firms = 0;
            for (int i = 0; i < rivals.Count; i++)
            {
                Competitor c = rivals[i];
                bool company = c.Kind == (int)RivalKind.Company;
                TableRow row = Row(RivalRow + place + "|" + c.Key);
                row.Set(0, c.Name);
                row.Set(1, RivalKindWords(c));
                row.Set(2, MeetWords(firm.File.Kind, c));
                int products = company ? ProductsAgainst(f, place, c.Company) : 0;
                int tenders = company ? TendersBoth(f, firm.File, c.Company) : 0;
                row.Set(3, company ? products.ToString() : "-", products, products > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, company ? tenders.ToString() : "-", tenders);
                row.Set(5, WhereShort(c));
                row.Kind = company ? TechColumns.RivalCompanies : c.Kind == (int)RivalKind.Startup ? TechColumns.RivalStartups : TechColumns.RivalFirms;
                companies += company ? 1 : 0;
                startups += c.Kind == (int)RivalKind.Startup ? 1 : 0;
                firms += c.Kind == (int)RivalKind.Firm ? 1 : 0;
                picked = pick != null && c.Key == pick ? c : picked;
            }
            if (picked != null)
            {
                WriteRival(box, f, firm, picked, day);
            }
            else
            {
                _picked = RivalsRow + place;
                WriteRivalsBox(box, firm, companies, startups, firms);
            }
            return TechColumns.Rivals;
        }

        // The page of the list: whose competitors, how many of each, what the firm makes, how they are found.
        private void WriteRivalsBox(InfoBox box, FirmPlace firm, int companies, int startups, int firms)
        {
            Firm m = firm.File;
            box.Title(TechText.T("reallife_tech_rivals_title", "firm", m.Name));
            _rivalFirms.Clear();
            IReadOnlyList<FirmPlace> all = _director.FirmPlaces;
            for (int i = 0; i < all.Count; i++)
            {
                _rivalFirms.Add(all[i]);
            }
            int at = _rivalFirms.IndexOf(firm);
            if (_rivalFirms.Count > 1 && at >= 0 && box.HasSelector)
            {
                box.Selector(_rivalFirms.Count, at, i => _rivalFirms[i].File.Name, i => Set(() => _picked = RivalsRow + _rivalFirms[i].File.Place));
            }
            box.Line(TechText.Fill("reallife_tech_rivals_count", "companies", companies.ToString(), "startups", startups.ToString(), "firms", firms.ToString()));
            box.Row("reallife_tech_rivals_what", OfferWords(CompetitorRules.Makes(m.Kind), CompetitorRules.Takes(m.Kind)));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", () => Set(() => _picked = FirmRow + m.Place));
            box.Note(TechText.T("reallife_tech_rivals_how"));
            Meta.Guide.Point(box, SectorGuide);
        }

        // A competitor's page: what it is, where the two meet, the products against it, the tenders both bid for, where it is.
        private void WriteRival(InfoBox box, FirmsFile f, FirmPlace firm, Competitor c, int day)
        {
            Firm m = firm.File;
            string back = RivalsRow + m.Place;
            box.Title(c.Name);
            box.Subtitle(RivalKindWords(c));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_tech_col_meet", MeetWords(m.Kind, c));
            Transform buttons;
            switch ((RivalKind)c.Kind)
            {
                case RivalKind.Company:
                    RivalProducts(box, f, m, c.Company);
                    RivalTenders(box, f, m, c.Company, day);
                    box.Line(c.Street != null
                        ? TechText.Fill("reallife_tech_rival_at", "floor", c.Floor.ToString(), "tower", c.Building ?? string.Empty, "address", FirmsGame.AddressWords(c.Street, c.Number))
                        : TechText.T("reallife_tech_rival_no_office"));
                    break;
                case RivalKind.Startup:
                    Startup s = _director.Ventures != null ? _director.Ventures.StartupOf(c.Startup) : null;
                    if (s != null)
                    {
                        box.Row("reallife_vc_col_sector", TechText.VentureName((Venture)s.Sector));
                        box.Row("reallife_vc_col_stage", TechText.RoundName(s.R));
                    }
                    box.Line(TechText.T("reallife_tech_rival_startup_where"));
                    break;
                default:
                    box.Row("reallife_tech_col_kind", TechText.FirmName((FirmKind)Mathf.Clamp(c.Firm, 0, (int)FirmKind.Count - 1)));
                    box.Line(c.Street != null
                        ? TechText.Fill("reallife_tech_rival_firm_at", "address", FirmsGame.AddressWords(c.Street, c.Number), "district", c.District ?? "-")
                        : TechText.T("reallife_tech_rival_no_office"));
                    break;
            }
            buttons = box.ButtonRow();
            if (c.Kind == (int)RivalKind.Startup)
            {
                int id = c.Startup;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_open_startup", () => Set(() =>
                {
                    _tab = TechDirector.TabStartups;
                    _picked = StartupRow + id;
                }));
            }
            else if (c.Street != null)
            {
                Competitor at = c;
                InfoBox.Button(buttons, InfoBox.Positive, "common_setdestination", () => Do(() => _director.RivalDestination(at)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_roles_button_back", () => Set(() => _picked = back));
            Meta.Guide.Point(box, SectorGuide);
        }

        // The firm's products that have the company as their rival: features against theirs and the share of the market.
        private void RivalProducts(InfoBox box, FirmsFile f, Firm m, int company)
        {
            int shown = 0, more = 0;
            for (int i = 0; i < f.Products.Count; i++)
            {
                Product p = f.Products[i];
                if (p.Place != m.Place || p.Stage >= 2 || CompetitorRules.RivalOf(p) != company)
                {
                    continue;
                }
                if (shown >= RivalLines)
                {
                    more++;
                    continue;
                }
                shown++;
                string share = TechText.Share(CompetitorRules.SharePpm(p));
                box.Line(p.K == ProductKind.Game
                    ? TechText.Fill("reallife_tech_rival_game", "product", p.Name, "sold", TechText.Whole(p.Users), "share", share)
                    : TechText.Fill("reallife_tech_rival_product", "product", p.Name, "ours", p.Features.ToString(), "theirs", p.RivalFeatures.ToString(), "share", share));
            }
            if (more > 0)
            {
                box.Line(TechText.T("reallife_tech_rival_more", "count", more.ToString()));
            }
            if (shown == 0 && (CompetitorRules.CompanyMakes[company] & CompetitorRules.Makes(m.Kind)) != 0)
            {
                box.Line(TechText.T("reallife_tech_rival_no_products"));
            }
        }

        // The council's tenders the firm and the company both bid for: open, bid, or decided with both bids.
        private void RivalTenders(InfoBox box, FirmsFile f, Firm m, int company, int day)
        {
            int shown = 0, more = 0;
            for (int i = f.Contracts.Count - 1; i >= 0; i--)
            {
                Contract t = f.Contracts[i];
                if (!CompetitorRules.BothBid(f.Seed, t, m, company))
                {
                    continue;
                }
                if (shown >= RivalLines)
                {
                    more++;
                    continue;
                }
                shown++;
                if (CompetitorRules.Decided(t))
                {
                    long theirs = FirmsRules.RivalBid(f.Seed, t, CompetitorRules.BidderOf(f.Seed, t, company), FirmsRules.FairOf(t));
                    box.Line(TechText.Fill("reallife_tech_rival_tender_decided", "work", TechText.Work(t.K), "stage", TechText.StageName(t.At),
                        "theirs", TechText.Cents(Level(theirs)), "yours", TechText.Cents(Level(t.Bid))));
                }
                else
                {
                    box.Line(TechText.Fill(t.At == WorkStage.Tender ? "reallife_tech_rival_tender_open" : "reallife_tech_rival_tender_bid", "work", TechText.Work(t.K), "day", t.End.ToString()));
                }
            }
            if (more > 0)
            {
                box.Line(TechText.T("reallife_tech_rival_more", "count", more.ToString()));
            }
            if (shown == 0 && (CompetitorRules.CompanyTakes[company] & CompetitorRules.Takes(m.Kind)) != 0)
            {
                box.Line(TechText.T("reallife_tech_rival_no_tenders"));
            }
        }

        private static int ProductsAgainst(FirmsFile f, string place, int company)
        {
            int n = 0;
            for (int i = 0; i < f.Products.Count; i++)
            {
                Product p = f.Products[i];
                n += p.Place == place && p.Stage < 2 && CompetitorRules.RivalOf(p) == company ? 1 : 0;
            }
            return n;
        }

        private static int TendersBoth(FirmsFile f, Firm m, int company)
        {
            int n = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                n += CompetitorRules.BothBid(f.Seed, f.Contracts[i], m, company) ? 1 : 0;
            }
            return n;
        }

        private static string RivalKindWords(Competitor c)
        {
            switch ((RivalKind)c.Kind)
            {
                case RivalKind.Company: return TechText.T("reallife_tech_rival_company");
                case RivalKind.Startup: return TechText.T("reallife_tech_rival_startup");
                default: return TechText.T(c.Theirs ? "reallife_tech_rival_theirs" : "reallife_tech_rival_yours");
            }
        }

        // Where a firm of a kind and a competitor meet: the products and the work both have, or a startup's sector.
        private string MeetWords(int kind, Competitor c)
        {
            switch ((RivalKind)c.Kind)
            {
                case RivalKind.Company:
                    int meet = CompetitorRules.Meet(kind, CompetitorRules.CompanyMakes[c.Company], CompetitorRules.CompanyTakes[c.Company]);
                    return OfferWords(meet & 0xFF, meet >> 8);
                case RivalKind.Startup:
                    return TechText.VentureName((Venture)c.Sector);
                default:
                    int both = CompetitorRules.Meet(kind, CompetitorRules.Makes(c.Firm), CompetitorRules.Takes(c.Firm));
                    return OfferWords(both & 0xFF, both >> 8);
            }
        }

        // The kinds of product and of work in two sets of bits, in words: "Subscription service, Systems integration".
        private string OfferWords(int products, int work)
        {
            _words.Clear();
            for (int k = 0; k < (int)ProductKind.Count; k++)
            {
                if ((products & (1 << k)) != 0)
                {
                    _words.Append(_words.Length > 0 ? ", " : string.Empty).Append(TechText.ProductName((ProductKind)k));
                }
            }
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if ((work & (1 << k)) != 0)
                {
                    _words.Append(_words.Length > 0 ? ", " : string.Empty).Append(TechText.Work((WorkKind)k));
                }
            }
            return _words.Length > 0 ? _words.ToString() : "-";
        }

        // Where it is, short, for the table: a company's tower and floor, a business's district.
        private static string WhereShort(Competitor c)
        {
            if (c.Kind == (int)RivalKind.Company)
            {
                return c.Building != null ? TechText.Fill("reallife_tech_rival_floor", "tower", c.Building, "floor", c.Floor.ToString()) : "-";
            }
            return c.Kind == (int)RivalKind.Firm && c.District != null ? c.District : "-";
        }
    }
}
