using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The new kinds of client of a tech firm (27 Nov 2026): no software, IT, web or graphic design business is ever a client (the colocation pool, the offers of the player's own businesses),
// city departments (large, slow to pay, some public tenders a rival can win), individuals (small, some pay late, some ask for one more change) and the city's companies by trade, each with
// a brief in the client's own voice, what it asks and what it risks; what a developer earns a month held to the old mix's; an old save loads as it was; the option off is today's mix.
// Run alone with --techdeals29.
internal static partial class Program
{
    private static void TechDeals29()
    {
        Td29Filter();
        Td29Colo();
        Td29Withdraw();
        Td29Briefs();
        Td29Offers();
        Td29Needs();
        Td29Tenders();
        Td29Payment();
        Td29Creep();
        Td29Income();
        Td29Saves();
        Td29OptionOff();
        Td29Sources();
    }

    // Every business type of the game, as the game's own texts list them (businesstype_*), and the sector's own.
    private static readonly string[] Td29GameTypes =
    {
        "appliancestore", "autorepairshop", "bank", "bookstore", "cardealership", "casino", "cinema", "clinic", "clothingstore", "coffeeshop", "electronicsstore", "empty", "eventplanningagency",
        "factory", "fastfoodrestaurant", "florist", "fruitandvegetablestore", "furniturestore", "gasstation", "giftshop", "government", "graphicdesigner", "gym", "hairdresser", "headquarters",
        "hospital", "importexport", "interiorinstallationfirm", "irs", "jewelrystore", "lawfirm", "liquorstore", "marketingagency", "movingservice", "nightclub", "officesupplystore",
        "privatedriverservice", "recruitmentagency", "school", "supermarket", "theater", "travelagency", "truckgarage", "warehouse", "webdevelopmentagency", "wholesalestore"
    };

    // ---------------------------------------------------------------- who may be a client

    private static bool Td29PoolHasSector(Func<CompanyDef, bool> allow, out string who)
    {
        who = string.Empty;
        for (int i = 0; i < MarketCatalogue.All.Length; i++)
        {
            CompanyDef c = MarketCatalogue.All[i];
            if (allow(c) && (c.Sector == MarketSector.Software || c.Trade == "webdevelopment" || c.Trade == "graphicdesign"))
            {
                who = c.Id + "/" + c.Sector + "/" + c.Trade;
                return true;
            }
        }
        return false;
    }

    private static void Td29Filter()
    {
        // every business type of the game: only the web development agency and the graphic designer are barred, and none of them has a trade to write a brief for
        int barred = 0;
        bool typesOk = true;
        foreach (string t in Td29GameTypes)
        {
            string type = "ba:businesstype_" + t;
            bool excluded = ClientFilter.TypeExcluded(type);
            barred += excluded ? 1 : 0;
            typesOk &= excluded == (t == "webdevelopmentagency" || t == "graphicdesigner");
            typesOk &= ClientFilter.TypeAllowed(type) == (!excluded && t != "empty");
            typesOk &= !excluded || ClientCatalogue.TradeOf(type) == ClientTrade.None;
        }
        Check(typesOk && barred == 2, "tech deals: of the game's " + Td29GameTypes.Length + " business types only the web development agency and the graphic designer are barred as clients");
        bool sector = true;
        for (int i = 0; i < ClientFilter.SectorTypes.Length; i++)
        {
            sector &= ClientFilter.TypeExcluded(ClientFilter.SectorTypes[i]) && !ClientFilter.TypeAllowed(ClientFilter.SectorTypes[i]);
        }
        string types = ModSource(@"Tech\Firms\TechTypes.cs");
        bool same = true;
        for (int i = 0; i < ClientFilter.SectorTypes.Length; i++)
        {
            same &= types.Contains("\"" + ClientFilter.SectorTypes[i].Substring("reallife:businesstype_".Length) + "\"");
        }
        Check(sector && same && ClientFilter.SectorTypes.Length == 5 && !ClientFilter.TypeExcluded("reallife:businesstype_academy") && !ClientFilter.TypeExcluded("reallife:businesstype_construction")
              && !ClientFilter.TypeExcluded(null) && !ClientFilter.TypeAllowed(string.Empty),
            "tech deals: the sector's five own business types are barred as clients (the list is TechTypes' own), a mod type of another trade is not");
        // every listed company of the city: none of the software sector, none that competes with the game's web or design businesses, in the colocation pool or in any trade's pool
        var colo = new HashSet<int>(ColoRules.Pool());
        bool none = true, kept = false, software = false;
        for (int i = 0; i < MarketCatalogue.All.Length; i++)
        {
            CompanyDef c = MarketCatalogue.All[i];
            bool shouldBar = c.Sector == MarketSector.Software || c.Trade == "webdevelopment" || c.Trade == "graphicdesign";
            software |= shouldBar;
            none &= ClientFilter.Allows(c) == !shouldBar && (!shouldBar || (!colo.Contains(c.Id) && !ClientFilter.ColoOpen(c)));
            kept |= !shouldBar && colo.Contains(c.Id);
        }
        bool pools = true;
        int named = 0;
        for (int t = 0; t < (int)ClientTrade.Count; t++)
        {
            foreach (CompanyDef c in ClientFilter.Pool((ClientTrade)t))
            {
                pools &= ClientFilter.Allows(c) && !c.Holding && !c.Pipeline && ClientCatalogue.InTrade(c, (ClientTrade)t);
                named++;
            }
            pools &= ClientFilter.Pool((ClientTrade)t).Length > 0;
        }
        Check(none && kept && software && pools && !Td29PoolHasSector(c => colo.Contains(c.Id), out string who) && named > 60,
            "tech deals: no listed software, web or graphic design company is a colocation client or in any trade's pool, and the other companies still are (" + colo.Count + " for racks, " + named + " in trades)");
        // the check can fail: a pool that lets everything in has a software company in it
        Check(Td29PoolHasSector(c => true, out string leaked) && leaked.Length > 0, "tech deals: the filter check finds a pool that lets a software company in (" + leaked + ")");
        string filter = ModSource(@"Tech\Clients\ClientFilter.cs");
        Check(filter.Length > 0 && !filter.Contains(".Name") && !filter.Contains("Codeharbour") && !filter.Contains("Signpost") && !filter.Contains("Contains(\"") && !filter.Contains("StartsWith("),
            "tech deals: the filter goes by business type and sector, never by a name");
    }

    private static void Td29Colo()
    {
        // a software company's contract signed before the filter runs to its term's end and is let go then, its racks let again to another client; others renew as before
        int software = -1, bank = 1;
        for (int i = 0; i < MarketCatalogue.All.Length && software < 0; i++)
        {
            software = MarketCatalogue.All[i].Sector == MarketSector.Software ? MarketCatalogue.All[i].Id : -1;
        }
        var f = new InfraFile { Seed = 5 };
        var d = new DataCentre { Place = "dc~t", Tier = 2, Sqm = 6000, Colo = 40, Racks = 100 };
        f.Centres.Add(d);
        f.Colo.Add(new ColoContract { Id = 1, Place = "dc~t", Client = software, Racks = 10, Start = 1, End = 100 });
        f.Colo.Add(new ColoContract { Id = 2, Place = "dc~t", Client = bank, Racks = 10, Start = 1, End = 100 });
        f.NextColo = 3;
        var notes = new List<ColoNote>();
        ColoRules.Turn(f, d, 50, 1000, notes);
        bool running = f.Colo.Count == 2 && notes.Count == 0;   // mid term: both run
        ColoRules.Turn(f, d, 100, 1000, notes);
        bool ended = f.Colo.Count == 1 && f.Colo[0].Client == bank && d.Colo == 40 && notes.Exists(n => n.Kind == ColoNote.Moved && n.Client == software && n.Racks == 10)
                     && notes.Exists(n => n.Kind == ColoNote.Renewed && n.Client == bank);
        ColoRules.Sync(f, d, 101, notes);
        bool again = ColoRules.Held(f, "dc~t") > 10 && ColoRules.Held(f, "dc~t") <= 40 && !f.Colo.Exists(c => c.Client == software);
        Check(software >= 0 && running && ended && again && !ColoRules.ClientOpen(software) && ColoRules.ClientOpen(bank),
            "tech deals: a software client's racks run to the term's end, it is let go with a notice and not renewed, and its racks are let again to a client the sector serves");
        // years of halls: no client of the software sector is ever made
        bool clean = true;
        for (int seed = 1; seed <= 12; seed++)
        {
            var g = new InfraFile { Seed = seed * 313 };
            var h = new DataCentre { Place = "dc~" + seed, Tier = seed % InfraCatalogue.Tiers, Sqm = 6000, Racks = 400 };
            g.Centres.Add(h);
            for (int day = 1; day <= 700; day++)
            {
                h.Colo = Math.Min(h.Colo + 1, 300);
                ColoRules.Sync(g, h, day, null);
                ColoRules.Turn(g, h, day, 900, null);
            }
            foreach (ColoContract c in g.Colo)
            {
                clean &= ColoRules.ClientOpen(c.Client);
            }
            clean &= g.Colo.Count > 3;
        }
        Check(clean, "tech deals: twelve halls over 700 days let racks to no software, web or graphic design company");
    }

    private static void Td29Withdraw()
    {
        // offers from the player's own businesses of a barred type are withdrawn; ones under way run on; others stay
        var f = new FirmsFile { Seed = 9 };
        Func<string, string> typeOf = place => place == "w" ? "ba:businesstype_webdevelopmentagency" : place == "g" ? "ba:businesstype_graphicdesigner" : place == "s" ? "reallife:businesstype_ailab"
            : place == "b" ? "ba:businesstype_bank" : null;
        string[] places = { "w", "g", "s", "b", "n", "w", "w", "w" };
        int[] stages = { (int)WorkStage.Offered, (int)WorkStage.Tender, (int)WorkStage.Bid, (int)WorkStage.Offered, (int)WorkStage.Offered, (int)WorkStage.Active, (int)WorkStage.Done, (int)WorkStage.Declined };
        for (int i = 0; i < places.Length; i++)
        {
            f.Contracts.Add(new Contract { Id = f.Next(), OwnPlace = places[i], Stage = stages[i], End = 99 });
        }
        f.Contracts.Add(new Contract { Id = f.Next(), Stage = (int)WorkStage.Offered, Client = "Elm Street Dental" });
        int gone = TechClientRules.Withdraw(f, typeOf, 40);
        bool right = gone == 3 && f.Contracts[0].Stage == (int)WorkStage.Declined && f.Contracts[1].Stage == (int)WorkStage.Declined && f.Contracts[2].Stage == (int)WorkStage.Declined
                     && f.Contracts[0].End == 40 && f.Contracts[3].Stage == (int)WorkStage.Offered && f.Contracts[4].Stage == (int)WorkStage.Offered
                     && f.Contracts[5].Stage == (int)WorkStage.Active && f.Contracts[6].Stage == (int)WorkStage.Done && f.Contracts[8].Stage == (int)WorkStage.Offered;
        Check(right && TechClientRules.Withdraw(f, typeOf, 41) == 0, "tech deals: offers, tenders and bids of a software, web or graphic design business are withdrawn (" + gone + "), signed contracts run on, and nothing is withdrawn twice");
        string director = ModSource(@"Tech\TechDirector.Clients.cs"), games = ModSource(@"Tech\Firms\FirmsGame.cs");
        int withdraw = director.IndexOf("TechClientRules.Withdraw(", StringComparison.Ordinal), option = director.IndexOf("if (Settings.TechDealKinds)", StringComparison.Ordinal);
        Check(withdraw > option && option > 0 && director.Contains("\"reallife_deal_note_withdrawn\"") && director.Contains("if (gone > 0)")
              && games.Contains("ClientFilter.TypeAllowed(reg.businessTypeName)") && ModSource(@"Tech\TechDirector.Firms.cs").Contains("ClientsOffered(day, offersFrom);"),
            "tech deals: the withdrawal runs each day whatever the option says, with a notice only when some were open, and the player's own businesses of a barred type are never offered as clients");
    }

    // ---------------------------------------------------------------- the briefs

    private static void Td29Briefs()
    {
        Dictionary<string, string> en = LocaleFile("en");
        int[] kinds = new int[(int)ClientKind.Count];
        var ids = new HashSet<int>();
        bool texts = true, names = true, tidy = true, ranges = true;
        foreach (ClientBrief b in ClientCatalogue.Briefs)
        {
            kinds[(int)b.Kind]++;
            texts &= ids.Add(b.Id) && en.TryGetValue(b.Key, out string said) && said.Length >= 120 && said.Length <= 330;
            if (en.TryGetValue(b.Key, out string t))
            {
                tidy &= t.IndexOf('—') < 0 && t.IndexOf('–') < 0 && !t.Contains(" - ") && !t.Contains("!") && !t.Contains(";") && !t.Contains("{") && char.IsUpper(t[0]) && t.EndsWith(".", StringComparison.Ordinal);
            }
            names &= b.Kind == ClientKind.Company ? b.Names == null && b.Trade != ClientTrade.None && ClientFilter.Pool(b.Trade).Length > 0 : b.Names != null && b.Names.Length > 0 && b.Trade == ClientTrade.None;
            ranges &= b.NeedSecurity >= 0 && b.NeedSecurity <= 800 && b.NeedReliability >= 0 && b.NeedReliability <= 800 && (!b.Tender || b.Kind == ClientKind.Government);
            ranges &= b.Id > 0 && ClientCatalogue.BriefOf(b.Id) == b;
        }
        Check(texts && tidy, "tech deals: every one of the " + ClientCatalogue.Briefs.Length + " briefs has its own text in the client's voice, of an email's length, with no dash, no exclamation mark, no semicolon");
        Check(names && ranges, "tech deals: a department or a person writes each brief of those kinds, a company's is named from its trade's pool, and the demands are within what a firm can reach");
        Check(kinds[(int)ClientKind.Government] >= 15 && kinds[(int)ClientKind.Individual] >= 15 && kinds[(int)ClientKind.Company] >= 30,
            "tech deals: many variants of each kind (" + kinds[1] + " departments, " + kinds[2] + " individuals, " + kinds[3] + " companies)");
        // every kind of work a firm can be given has a brief of each kind that can ask for it
        bool cover = true;
        string missing = string.Empty;
        for (int k = 0; k < (int)WorkKind.Count; k++)
        {
            if (k == (int)WorkKind.Incident)
            {
                continue;
            }
            WorkKind w = (WorkKind)k;
            bool gov = TechClientRules.Count(ClientKind.Government, w, ClientTrade.None, false) > 0, ind = TechClientRules.Count(ClientKind.Individual, w, ClientTrade.None, false) > 0
                       || w == WorkKind.Managed, co = TechClientRules.Count(ClientKind.Company, w, ClientTrade.None, false) > 0;
            cover &= gov && ind && co;
            missing += gov && ind && co ? string.Empty : " " + w;
        }
        bool tenders = TechClientRules.Count(ClientKind.Government, WorkKind.Integration, ClientTrade.None, true) >= 3 && TechClientRules.Count(ClientKind.Government, WorkKind.Managed, ClientTrade.None, true) >= 1;
        int trades = 0;
        for (int t = 0; t < (int)ClientTrade.Count; t++)
        {
            int any = 0;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                any += TechClientRules.Count(ClientKind.Company, (WorkKind)k, (ClientTrade)t, false);
            }
            trades += any >= 2 ? 1 : 0;
        }
        Check(cover && tenders && trades == (int)ClientTrade.Count, "tech deals: every kind of work but an attack has a brief from a department, an individual (a monthly service excepted) and a company, the council's tender kinds have public tenders, and every trade has two briefs or more" + missing);
        // the player's own businesses by the game's types: a supermarket is a grocer's brief, a bank a bank's, a web agency none
        Check(ClientCatalogue.TradeOf("ba:businesstype_supermarket") == ClientTrade.Grocer && ClientCatalogue.TradeOf("ba:businesstype_bank") == ClientTrade.Bank
              && ClientCatalogue.TradeOf("ba:businesstype_lawfirm") == ClientTrade.Law && ClientCatalogue.TradeOf("ba:businesstype_webdevelopmentagency") == ClientTrade.None
              && ClientCatalogue.TradeOf("ba:businesstype_headquarters") == ClientTrade.None && ClientCatalogue.TradeOf(null) == ClientTrade.None,
            "tech deals: a business of the player's own is the client of the trade its game type is in");
    }

    // ---------------------------------------------------------------- offers of each kind

    private static void Td29Offers()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var ff = new FirmsFile { Seed = 11 };
        bool all = true, tidy = true;
        int made = 0;
        string why = string.Empty;
        for (int c = (int)ClientKind.Government; c <= (int)ClientKind.Company; c++)
        {
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if (k == (int)WorkKind.Incident)
                {
                    continue;
                }
                for (int roll = 0; roll < 1000; roll += 41)
                {
                    foreach (bool tender in new[] { false, true })
                    {
                        if (tender && c != (int)ClientKind.Government)
                        {
                            continue;
                        }
                        Contract w = TechClientRules.Make(ff, "firm", (ClientKind)c, (WorkKind)k, tender, 100, roll);
                        if (w == null)
                        {
                            continue;
                        }
                        made++;
                        ClientBrief b = ClientCatalogue.BriefOf(w.Brief);
                        bool ok = b != null && w.Deal == c && b.Kind == (ClientKind)c && b.Work == (WorkKind)k && b.Tender == (tender && c == (int)ClientKind.Government) && w.Client.Length > 0
                                  && w.Value > 0 && (w.Monthly || w.Points >= 1) && w.NeedSecurity == b.NeedSecurity && w.NeedReliability == b.NeedReliability
                                  && (tender ? w.Council && w.Stage == (int)WorkStage.Tender && w.Fair == w.Value && w.Rivals > 0 : !w.Council && w.Stage == (int)WorkStage.Offered && w.Place == "firm");
                        if (b != null && b.Kind == ClientKind.Company)
                        {
                            bool inPool = false;
                            foreach (CompanyDef cd in ClientFilter.Pool(b.Trade))
                            {
                                inPool |= cd.Name == w.Client;
                            }
                            ok &= inPool;
                        }
                        else if (b != null)
                        {
                            ok &= Array.IndexOf(b.Names, w.Client) >= 0;
                        }
                        all &= ok;
                        why = ok ? why : "kind " + c + " work " + k + " roll " + roll + " tender " + tender;
                    }
                }
            }
        }
        Check(all && made > 300, "tech deals: every offer built of each kind has its brief, a client of the right pool, a price and points, and a tender is a tender (" + made + " built)" + why);
        // the kinds have their own characters: a department's work is bigger, slower and pays slower than an individual's; a company's is the usual
        long gp = 0, ip = 0, cp = 0;
        int gd = 0, id = 0, cd2 = 0, gu = 0, iu = 0, n = 0;
        for (int roll = 0; roll < 1000; roll += 7)
        {
            Contract g = TechClientRules.Make(ff, "firm", ClientKind.Government, WorkKind.Integration, false, 100, roll);
            Contract i = TechClientRules.Make(ff, "firm", ClientKind.Individual, WorkKind.Integration, false, 100, roll);
            Contract c = TechClientRules.Make(ff, "firm", ClientKind.Company, WorkKind.Integration, false, 100, roll);
            if (g == null || i == null || c == null)
            {
                continue;
            }
            n++;
            gp += g.Points;
            ip += i.Points;
            cp += c.Points;
            FirmsRules.Take(g, "firm", 100);
            FirmsRules.Take(i, "firm", 100);
            FirmsRules.Take(c, "firm", 100);
            gd += g.Due - 100;
            id += i.Due - 100;
            cd2 += c.Due - 100;
            gu += (int)(g.Paid * 1000 / Math.Max(1, g.Value));
            iu += (int)(i.Paid * 1000 / Math.Max(1, i.Value));
        }
        Check(n > 100 && gp > cp * 15 / 10 && cp > ip * 13 / 10 && gd > cd2 * 18 / 10 && id < cd2 && gu / n < iu / n && Math.Abs(gu / n - ClientCatalogue.Upfront[1]) <= 1 && Math.Abs(iu / n - ClientCatalogue.Upfront[2]) <= 1,
            "tech deals: a department's project is about " + gp * 10 / cp / 10.0 + " times a company's in points and " + gd / Math.Max(1, cd2) + " times in days with a small deposit, an individual's is small and quick (" + ip / n + ", " + cp / n + ", " + gp / n + " points; " + id / n + ", " + cd2 / n + ", " + gd / n + " days)");
        // payment terms: a department always pays weeks late, an individual or a company only some
        int govLate = 0, indLate = 0, coLate = 0, indCreep = 0, coCreep = 0, govCreep = 0, total = 0;
        for (int roll = 0; roll < 1000; roll++)
        {
            var file = new FirmsFile { Seed = roll + 5 };
            Contract g = TechClientRules.Make(file, "firm", ClientKind.Government, WorkKind.Integration, false, 100, roll);
            Contract i = TechClientRules.Make(file, "firm", ClientKind.Individual, WorkKind.Website, false, 100, roll);
            Contract c = TechClientRules.Make(file, "firm", ClientKind.Company, WorkKind.Integration, false, 100, roll);
            total++;
            govLate += g.Lag >= ClientCatalogue.GovernmentLagLeast && g.Lag <= ClientCatalogue.GovernmentLagMost ? 1 : 0;
            indLate += i.Lag >= ClientCatalogue.IndividualLagLeast && i.Lag <= ClientCatalogue.IndividualLagMost ? 1 : 0;
            coLate += c.Lag == ClientCatalogue.CompanyTermsDays ? 1 : 0;
            govCreep += g.CreepMille > 0 ? 1 : 0;
            indCreep += i.CreepMille > 0 ? 1 : 0;
            coCreep += c.CreepMille > 0 ? 1 : 0;
        }
        Check(govLate == total && govCreep == 0 && Within(indLate, 280, 60, total) && Within(coLate, 300, 60, total) && Within(indCreep, 350, 60, total) && Within(coCreep, 150, 50, total),
            "tech deals: a department pays every time, " + indLate / 10 + "% of individuals pay late and " + indCreep / 10 + "% ask for a change, " + coLate / 10 + "% of companies pay on terms and " + coCreep / 10 + "% ask for a change");
        // a monthly service of an individual is never made, one of a department or a company is
        Check(TechClientRules.Make(ff, "firm", ClientKind.Individual, WorkKind.Managed, false, 100, 400) == null && TechClientRules.Make(ff, "firm", ClientKind.Government, WorkKind.Managed, false, 100, 400) != null
              && TechClientRules.Make(ff, "firm", ClientKind.Company, WorkKind.Managed, false, 100, 400).Monthly, "tech deals: nobody asks an individual for a monthly service, and a department's or a company's is billed monthly");
    }

    private static bool Within(int count, int perMille, int slack, int total) => count * 1000 >= (perMille - slack) * total && count * 1000 <= (perMille + slack) * total;

    // ---------------------------------------------------------------- what a firm must hold

    private static void Td29Needs()
    {
        // the brief's demands: met when the firm holds exactly that much, short by a bit for each of security and reliability when it holds one less
        bool exact = true, short1 = true, wide = true;
        int demanding = 0;
        foreach (ClientBrief b in ClientCatalogue.Briefs)
        {
            var w = new Contract { NeedSecurity = b.NeedSecurity, NeedReliability = b.NeedReliability };
            var met = new Crew { Security = b.NeedSecurity, Reliability = b.NeedReliability };
            exact &= TechClientRules.Short(w, met) == 0;
            if (b.NeedSecurity > 0)
            {
                short1 &= TechClientRules.Short(w, new Crew { Security = b.NeedSecurity - 1, Reliability = 1000 }) == TechClientRules.ShortSecurity;
            }
            if (b.NeedReliability > 0)
            {
                short1 &= TechClientRules.Short(w, new Crew { Security = 1000, Reliability = b.NeedReliability - 1 }) == TechClientRules.ShortReliability;
            }
            if (b.NeedSecurity > 0 && b.NeedReliability > 0)
            {
                short1 &= TechClientRules.Short(w, new Crew()) == (TechClientRules.ShortSecurity | TechClientRules.ShortReliability);
            }
            wide &= TechClientRules.Short(w, new Crew { Security = 1000, Reliability = 1000 }) == 0;
            demanding += b.NeedSecurity > 0 || b.NeedReliability > 0 ? 1 : 0;
        }
        Check(exact && short1 && wide && demanding > 30, "tech deals: a firm holding what a brief asks can take it and one a point short cannot, for security and for reliability (" + demanding + " briefs ask for one or both)");
        // a firm with nothing (security 200 with no analysts) cannot take the demanding work of a department or a bank, and is offered some it can
        var plain = new Crew { Security = FirmsCatalogue.SecurityBase, Reliability = 300 };
        int govShort = 0, govAll = 0, coShort = 0, coAll = 0, noDemand = 0;
        foreach (ClientBrief b in ClientCatalogue.Briefs)
        {
            var w = new Contract { NeedSecurity = b.NeedSecurity, NeedReliability = b.NeedReliability };
            bool refused = TechClientRules.Short(w, plain) != 0;
            govAll += b.Kind == ClientKind.Government ? 1 : 0;
            govShort += b.Kind == ClientKind.Government && refused ? 1 : 0;
            coAll += b.Kind == ClientKind.Company ? 1 : 0;
            coShort += b.Kind == ClientKind.Company && refused ? 1 : 0;
            noDemand += !refused ? 1 : 0;
        }
        Check(govShort >= govAll / 3 && coShort >= 8 && noDemand > ClientCatalogue.Briefs.Length / 3, "tech deals: a plain firm is refused " + govShort + " of " + govAll + " departments' briefs and " + coShort + " of " + coAll + " companies', and many it can take remain");
        // the director refuses to take or bid with a notice, and the contract's page says what is asked and whether the firm has it
        string director = ModSource(@"Tech\TechDirector.Clients.cs"), app = ModSource(@"Tech\TechDirector.FirmsApp.cs");
        Check(director.Contains("TechClientRules.Short(w, firm.Crew) == 0") && director.Contains("\"reallife_deal_note_need\"") && app.Contains("|| NeedRefused(w, firm))") && app.Contains("|| NeedRefused(t, firm))"),
            "tech deals: taking an offer and bidding for a tender are refused, with a notice, for a firm short of what is asked");
        var lines = new List<DealLine>();
        var tender = new Contract { Deal = (int)ClientKind.Government, Kind = (int)WorkKind.Integration, NeedSecurity = 600, NeedReliability = 400, Points = 100, Value = 1000 };
        ClientViews.Lines(lines, tender, true, new Crew { Security = 200, Reliability = 500 }, "Firm");
        bool warns = lines.Exists(l => l.Warn && l.Key == "reallife_deal_need_both" && l.A == 600 && l.B == 400) && lines.Exists(l => l.Warn && l.Key == "reallife_deal_short" && l.Name == "Firm" && l.A == 200);
        lines.Clear();
        ClientViews.Lines(lines, tender, true, new Crew { Security = 700, Reliability = 500 }, "Firm");
        bool quiet = !lines.Exists(l => l.Warn);
        lines.Clear();
        ClientViews.Lines(lines, new Contract { Deal = 0 }, true, new Crew(), "Firm");
        Check(warns && quiet && lines.Count == 0, "tech deals: the page warns when the firm is short and says nothing of an old client");
    }

    // ---------------------------------------------------------------- public tenders

    private static void Td29Tenders()
    {
        // a department's tender is a public tender with rivals: at a fair bid the player wins some and loses some, a cheaper bid wins more, a dearer one loses
        int fair = 0, cheap = 0, dear = 0, n = 400;
        long fairPrice = 0;
        var firm = new Firm { Place = "firm", Reputation = 500, Kind = (int)FirmKind.Consultancy };
        for (int i = 1; i <= n; i++)
        {
            var f = new FirmsFile { Seed = i };
            Contract t = TechClientRules.Make(f, "firm", ClientKind.Government, WorkKind.Integration, true, 50, i * 7 % 1000);
            fairPrice += t.Fair;
            foreach (int share in new[] { 1000, 800, 1250 })
            {
                var u = new Contract { Id = t.Id, Kind = t.Kind, Value = t.Value, Rivals = t.Rivals, Stage = (int)WorkStage.Bid, Council = true, Fair = t.Fair, Deal = t.Deal, Points = t.Points };
                u.Bid = FirmsRules.BidAt(u, share);
                bool won = FirmsRules.Decide(f, u, firm, 55);
                fair += share == 1000 && won ? 1 : 0;
                cheap += share == 800 && won ? 1 : 0;
                dear += share == 1250 && won ? 1 : 0;
                if (won && share == 1000)
                {
                    u.Stage = (int)WorkStage.Active;
                    if (FirmsRules.FairOf(u) != t.Fair || u.Value != u.Bid)
                    {
                        n = -1;
                    }
                }
            }
        }
        Check(n > 0 && fair > 20 && fair < 380 && cheap > fair && dear < fair, "tech deals: a department's public tender can be lost to a rival (a fair bid won " + fair + " of 400, a cheap one " + cheap + ", a dear one " + dear + ") and its fair price is kept once it is won");
        // the council's old tender kept its fair price from its points; a department's keeps its own
        var old = new Contract { Council = true, Kind = (int)WorkKind.Integration, Points = 300, Stage = (int)WorkStage.Active, Value = 1234 };
        Check(FirmsRules.FairOf(old) == FirmsRules.ValueOf(WorkKind.Integration, 300, true), "tech deals: an old council tender's fair price is worked as before");
        // a day's offers: the council's tender becomes a department's, unless a late public job's audit holds public work back
        var file = new FirmsFile { Seed = 21 };
        file.Firms.Add(new Firm { Place = "firm", Kind = (int)FirmKind.Consultancy, Reputation = 500 });
        file.Contracts.Add(new Contract { Id = file.Next(), Kind = (int)WorkKind.Integration, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, Points = 400, Rivals = 3,
            Value = FirmsRules.ValueOf(WorkKind.Integration, 400, true) });
        TechClientRules.Mix(file, 60, 0, null, null);
        Contract made = file.Contracts[0];
        bool becomes = made.Deal == (int)ClientKind.Government && made.Council && made.Stage == (int)WorkStage.Tender && made.Client != FirmsCatalogue.Council && made.Rivals == 3
                       && ClientCatalogue.BriefOf(made.Brief).Tender && made.Points > 400 * 15 / 10 && made.Fair > 0;
        var held = new FirmsFile { Seed = 21, GovHold = 90 };
        held.Contracts.Add(new Contract { Id = held.Next(), Kind = (int)WorkKind.Integration, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, Points = 400, Rivals = 3, Value = 1000 });
        TechClientRules.Mix(held, 60, 0, null, null);
        var ended = new FirmsFile { Seed = 21, GovHold = 60 };
        ended.Contracts.Add(new Contract { Id = ended.Next(), Kind = (int)WorkKind.Integration, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, Points = 400, Rivals = 3, Value = 1000 });
        TechClientRules.Mix(ended, 60, 0, null, null);
        Check(becomes && held.Contracts.Count == 0 && ended.Contracts.Count == 1 && ended.Contracts[0].Deal == (int)ClientKind.Government,
            "tech deals: the council's tender becomes a department's public tender, none is put out while an audit holds public work back, and the hold ends on its day");
    }

    // ---------------------------------------------------------------- late payment

    // A project of a kind of client taken on day 100, worked at full pace by a firm, to the day it is finished.
    private static Contract Td29Run(ClientKind kind, WorkKind work, int roll, int lateBy, Firm firm, List<WorkNote> notes, out long paidNow, out int finished)
    {
        var f = new FirmsFile { Seed = 3 };
        Contract w = TechClientRules.Make(f, "firm", kind, work, false, 100, roll);
        FirmsRules.Take(w, "firm", 100);
        paidNow = w.Paid;
        finished = 0;
        int target = lateBy > 0 ? w.Due + lateBy : 101;   // 0: finished the day after it was taken, otherwise so many days after its date
        for (int day = 101; day < 100 + 400; day++)
        {
            int give = day < target ? 1 : Math.Max(1, w.Points);
            long paid = FirmsRules.Work(w, firm, give, day, notes);
            paidNow += paid;
            if (w.Stage != (int)WorkStage.Active)
            {
                finished = day;
                break;
            }
        }
        return w;
    }

    private static void Td29Payment()
    {
        // a department's delivered work is owed for weeks, paid on its day and not before; it is kept in the list until it is paid
        var firm = new Firm { Place = "firm", Reputation = 500 };
        var notes = new List<WorkNote>();
        Contract g = Td29Run(ClientKind.Government, WorkKind.Integration, 300, 0, firm, notes, out long paidAtOnce, out int day);
        long upfront = g.Paid;
        bool owed = g.Stage == (int)WorkStage.Done && g.Owed > 0 && g.PayDay == day + g.Lag && g.Lag >= ClientCatalogue.GovernmentLagLeast && paidAtOnce == upfront && g.Paid == upfront
                    && notes.Exists(n => n.Kind == FirmsRules.NoteOwed && n.Amount == g.Owed) && !notes.Exists(n => n.Kind == FirmsRules.NoteDone);
        long before = TechClientRules.Due(g, g.PayDay - 1);
        long total = g.Value;
        long due = g.Owed;
        long on = TechClientRules.Due(g, g.PayDay);
        bool paid = before == 0 && on == due && g.Owed == 0 && g.Paid == total && TechClientRules.Due(g, g.PayDay + 1) == 0;
        Check(owed && paid, "tech deals: a department's delivered work is owed (" + due / 100 + ") until day " + (day + g.Lag) + ", paid then and not before, and only once");
        // late payers: an individual's that pays late is owed for its days, one that pays at once is paid at once
        int late = 0, atOnce = 0, wrong = 0;
        for (int roll = 0; roll < 200; roll++)
        {
            var f = new FirmsFile { Seed = roll + 40 };
            Contract w = TechClientRules.Make(f, "firm", ClientKind.Individual, WorkKind.Website, false, 100, roll);
            FirmsRules.Take(w, "firm", 100);
            var n = new List<WorkNote>();
            long got = 0;
            for (int d = 101; d < 400 && w.Stage == (int)WorkStage.Active; d++)
            {
                got += FirmsRules.Work(w, new Firm { Reputation = 500 }, w.Points, d, n);
            }
            if (w.Lag > 0)
            {
                late++;
                wrong += w.Owed > 0 && w.PayDay > 101 && got == 0 ? 0 : 1;
            }
            else
            {
                atOnce++;
                wrong += w.Owed == 0 && w.Paid == w.Value && got == w.Value - w.Value * ClientCatalogue.Upfront[2] / 1000 ? 0 : 1;
            }
        }
        Check(late > 20 && atOnce > 100 && wrong == 0, "tech deals: of 200 individuals' jobs " + late + " pay late and are owed their days, " + atOnce + " pay at once, and the money adds up");
        // a late job: the penalty of its kind a day, a department's heavier than an individual's; a public job three days late brings an audit; the name moves by kind
        var slow = new Firm { Place = "firm", Reputation = 500 };
        var slowNotes = new List<WorkNote>();
        Contract gl = Td29Run(ClientKind.Government, WorkKind.Integration, 300, 5, slow, slowNotes, out _, out int gDay);
        int gLate = Math.Max(0, gDay - gl.Due);
        long expect = gl.Value - Math.Min(gl.Value / 2, gl.Value * ClientCatalogue.LatePenalty[1] / 1000 * gLate);
        bool pen = gLate >= ClientCatalogue.ScrutinyLateDays && gl.Owed + gl.Paid == expect && gl.Owed + gl.Paid < gl.Value && slowNotes.Exists(n => n.Kind == FirmsRules.NoteScrutiny)
                   && slow.Reputation < 500;
        var ontime = new Firm { Place = "firm", Reputation = 500 };
        Contract go = Td29Run(ClientKind.Government, WorkKind.Integration, 300, 0, ontime, new List<WorkNote>(), out _, out _);
        var indFirm = new Firm { Place = "firm", Reputation = 500 };
        Td29Run(ClientKind.Individual, WorkKind.Website, 300, 0, indFirm, new List<WorkNote>(), out _, out _);
        var coFirm = new Firm { Place = "firm", Reputation = 500 };
        Td29Run(ClientKind.Company, WorkKind.Integration, 300, 0, coFirm, new List<WorkNote>(), out _, out _);
        bool names = ontime.Reputation == 500 + ClientCatalogue.ReputationOnTime[1] && indFirm.Reputation == 500 + ClientCatalogue.ReputationOnTime[2] && coFirm.Reputation == 500 + ClientCatalogue.ReputationOnTime[3]
                     && ClientCatalogue.ReputationOnTime[1] > ClientCatalogue.ReputationOnTime[3] && ClientCatalogue.ReputationOnTime[3] > ClientCatalogue.ReputationOnTime[2];
        Check(pen && names && go.Stage == (int)WorkStage.Done,
            "tech deals: a department's job " + gLate + " days late loses " + ClientCatalogue.LatePenalty[1] / 10.0 + "% a day and brings an audit, and the firm's name moves by +" + ClientCatalogue.ReputationOnTime[1] + ", +" + ClientCatalogue.ReputationOnTime[3] + " and +" + ClientCatalogue.ReputationOnTime[2] + " on time");
        // a project nobody finishes in twice its time fails and costs the name by kind
        var failing = new Firm { Place = "firm", Reputation = 500 };
        var failNotes = new List<WorkNote>();
        var ff = new FirmsFile { Seed = 3 };
        Contract fl = TechClientRules.Make(ff, "firm", ClientKind.Government, WorkKind.Website, false, 100, 200);
        FirmsRules.Take(fl, "firm", 100);
        for (int d = 101; d < 100 + 4 * (fl.Due - fl.Start) && fl.Stage == (int)WorkStage.Active; d++)
        {
            FirmsRules.Work(fl, failing, 0, d, failNotes);
        }
        Check(fl.Stage == (int)WorkStage.Failed && failing.Reputation == 500 - ClientCatalogue.ReputationFailed[1] && failNotes.Exists(n => n.Kind == FirmsRules.NoteFailed),
            "tech deals: a department's job nobody works on fails in twice its time and costs the firm " + ClientCatalogue.ReputationFailed[1] + " of its name");
        // an old record's work is untouched: a classic contract finishes by the rule it always had
        var oldFirm = new Firm { Reputation = 500 };
        var oldNotes = new List<WorkNote>();
        var oldC = new Contract { Id = 1, Kind = (int)WorkKind.Website, Points = 100, Value = 1000000, Start = 10, Due = 30, Stage = (int)WorkStage.Active, Paid = 300000 };
        long oldPaid = FirmsRules.Work(oldC, oldFirm, 100, 25, oldNotes);
        Check(oldPaid == 700000 && oldC.Stage == (int)WorkStage.Done && oldC.Owed == 0 && oldFirm.Reputation == 525 && oldNotes.Count == 1 && oldNotes[0].Kind == FirmsRules.NoteDone,
            "tech deals: a contract of the old kind is paid on the day it is done, as before");
    }

    // ---------------------------------------------------------------- scope creep

    private static void Td29Creep()
    {
        int asked = 0, never = 0, carriers = 0, twice = 0;
        bool grows = true, dateMoves = true, unpaid = true;
        for (int i = 0; i < 300; i++)
        {
            var f = new FirmsFile { Seed = i + 1 };
            Contract w = TechClientRules.Make(f, "firm", ClientKind.Individual, WorkKind.Website, false, 100, i * 13 % 1000);
            FirmsRules.Take(w, "firm", 100 + i);   // the roll is the contract's own: its day and number, so each contract here has its own start
            bool carries = w.CreepMille > 0;
            carriers += carries ? 1 : 0;
            int points = w.Points, due = w.Due;
            long value = w.Value;
            var notes = new List<WorkNote>();
            var firm = new Firm { Reputation = 500 };
            for (int d = w.Start + 1; d < w.Start + 5 + 3 * (w.Due - w.Start) && w.Stage == (int)WorkStage.Active; d++)
            {
                FirmsRules.Work(w, firm, Math.Max(1, w.Points / 12), d, notes);
            }
            int changes = notes.FindAll(n => n.Kind == FirmsRules.NoteChange).Count;
            asked += changes > 0 ? 1 : 0;
            twice += changes > 1 ? 1 : 0;
            never += !carries && changes > 0 ? 1 : 0;
            if (changes > 0)
            {
                grows &= w.Points > points && w.Extra == w.Points - points && w.Points - points >= points / 10 * changes;
                dateMoves &= w.Due > due;
                unpaid &= w.Value == value;
            }
        }
        Check(carriers > 60 && asked > carriers * 4 / 10 && asked < carriers && never == 0 && twice > 0 && grows && dateMoves && unpaid,
            "tech deals: of " + carriers + " individuals who carry the risk " + asked + " ask for one more change (" + twice + " twice), none that does not, each adds a fifth of its points and moves its date and none pays for it");
        // the roll is the contract's own: a reload gives the same answer
        string first = Td29CreepTrace(), again = Td29CreepTrace();
        Check(first == again && first.Length > 0, "tech deals: the same contract asks for its change on the same day after a reload");
        // a carrier's day is not weighed before 40 per cent of the work is done
        var f2 = new FirmsFile { Seed = 8 };
        Contract c = TechClientRules.Make(f2, "firm", ClientKind.Individual, WorkKind.Website, false, 100, 10);
        c.CreepMille = 1000;
        FirmsRules.Take(c, "firm", 100);
        var n2 = new List<WorkNote>();
        FirmsRules.Work(c, new Firm { Reputation = 500 }, c.Points * 3 / 10, 101, n2);
        bool early = !n2.Exists(n => n.Kind == FirmsRules.NoteChange) && c.Changes == 0;
        FirmsRules.Work(c, new Firm { Reputation = 500 }, c.Points * 2 / 10, 102, n2);
        early &= !n2.Exists(n => n.Kind == FirmsRules.NoteChange);   // weighed at the start of a day, with half the work done
        FirmsRules.Work(c, new Firm { Reputation = 500 }, 1, 103, n2);
        Check(early && n2.Exists(n => n.Kind == FirmsRules.NoteChange) && c.Changes == 1, "tech deals: a client weighs its change once the work is 40 per cent done, not before");
    }

    private static string Td29CreepTrace()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            var f = new FirmsFile { Seed = i + 7 };
            Contract w = TechClientRules.Make(f, "firm", ClientKind.Company, WorkKind.Integration, false, 100, i * 17 % 1000);
            w.CreepMille = 700;
            FirmsRules.Take(w, "firm", 100 + i);
            var firm = new Firm { Reputation = 500 };
            for (int d = w.Start + 1; d < w.Start + 300 && w.Stage == (int)WorkStage.Active; d++)
            {
                int before = w.Extra;
                FirmsRules.Work(w, firm, Math.Max(1, w.Points / 10), d, null);
                sb.Append(w.Extra != before ? d.ToString() + "," : string.Empty);
            }
            sb.Append(w.Points).Append(';');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- what a developer earns a month

    private sealed class Td29Result
    {
        public long Cash;
        public int Taken, Done, Failed, Refused;
        public readonly int[] ByKind = new int[(int)ClientKind.Count];
    }

    // A firm of so many builders taking every offer it has people and posture for, bidding the fair price on every tender it takes, working its contracts as the firms' day does and
    // being paid as it is (an upfront share when taken, the rest when done or weeks after, a month's fee for a service): its takings over the last two years of four, in cents.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static Td29Result Td29Sim(FirmKind kind, int builders, int security, int reliability, bool mix, bool sector, int seed, int days)
    {
        var ff = new FirmsFile { Seed = seed };
        var sf = new SectorFile { Seed = seed };
        var firm = new Firm { Place = "c", Kind = (int)kind, Reputation = 500 };
        ff.Firms.Add(firm);
        var c = new Crew
        {
            Engineering = kind == FirmKind.Design ? 0 : builders * FirmsCatalogue.PointsPerDay, Design = kind == FirmKind.Design ? builders * FirmsCatalogue.PointsPerDay : 0,
            Selling = Math.Max(8, builders * 2), Seats = builders + builders / 4, Security = kind == FirmKind.Security ? Math.Min(900, security + 300) : security, Reliability = reliability
        };
        var crews = new List<Crew> { c };
        var notes = new List<WorkNote>();
        var r = new Td29Result();
        int warm = days / 2;
        for (int day = 1; day <= days; day++)
        {
            notes.Clear();
            int from = ff.Contracts.Count;
            if (sector)
            {
                SectorRules.Offers(ff, sf, day, 1, crews, null, notes);
            }
            else
            {
                FirmsRules.Offers(ff, day, crews, null, notes);
            }
            if (mix)
            {
                TechClientRules.Mix(ff, day, from, null, null);
            }
            FirmsRules.Lapse(ff, day, notes);
            long today = 0;
            int load = 0;
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                load += ff.Contracts[i].Stage == (int)WorkStage.Active ? FirmsRules.Need(ff.Contracts[i], day) : 0;
            }
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                if (w.Stage == (int)WorkStage.Offered && FirmsRules.Takes(kind, w.K) && Td29Fits(w, c.Engineering + c.Design - load))   // a firm takes what its people can do in the days it is given
                {
                    if (TechClientRules.Short(w, c) != 0)
                    {
                        w.Stage = (int)WorkStage.Declined;
                        r.Refused += day > warm ? 1 : 0;
                        continue;
                    }
                    today += FirmsRules.Take(w, firm.Place, day);
                    load += FirmsRules.Need(w, day);
                    r.Taken += day > warm ? 1 : 0;
                    r.ByKind[w.Deal] += day > warm ? 1 : 0;
                }
                else if (w.Stage == (int)WorkStage.Tender && FirmsRules.Takes(kind, w.K) && TechClientRules.Short(w, c) == 0)
                {
                    w.Bid = FirmsRules.BidAt(w, 1000);
                    w.Place = firm.Place;
                    w.Stage = (int)WorkStage.Bid;
                }
                else if (w.Stage == (int)WorkStage.Bid && day >= w.End && FirmsRules.Decide(ff, w, firm, day))
                {
                    today += FirmsRules.Take(w, firm.Place, day);
                    r.Taken += day > warm ? 1 : 0;
                    r.ByKind[w.Deal] += day > warm ? 1 : 0;
                }
            }
            int engineering = c.Engineering, design = c.Design, web = c.Web;
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                if (w.Stage != (int)WorkStage.Active)
                {
                    continue;
                }
                int need = FirmsRules.Need(w, day), give = FirmsRules.Give(w.K, need, ref engineering, ref design, ref web);
                today += FirmsRules.Work(w, firm, give, day, null);
                r.Done += day > warm && w.Stage == (int)WorkStage.Done ? 1 : 0;
                r.Failed += day > warm && w.Stage == (int)WorkStage.Failed ? 1 : 0;
            }
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                today += TechClientRules.Due(ff.Contracts[i], day);
            }
            r.Cash += day > warm ? today : 0;
        }
        return r;
    }

    // Whether a firm with so many points a day free can do a contract in the days it is given, with a fifth to spare.
    private static bool Td29Fits(Contract w, int free) =>
        w.Monthly ? free >= FirmsCatalogue.ManagedPoints : free > 0 && (long)w.Points * 10 <= (long)free * TechClientRules.Days(w, FirmsCatalogue.WorkDays[w.Kind]) * 8;

    // Takings a developer a month (30 days), dollars, summed over several firms' seeds.
    private static double Td29PerDev(FirmKind kind, int builders, int sec, int rel, bool mix, bool sector, int days, out Td29Result sum)
    {
        sum = new Td29Result();
        for (int seed = 1; seed <= 8; seed++)
        {
            Td29Result r = Td29Sim(kind, builders, sec, rel, mix, sector, seed * 101, days);
            sum.Cash += r.Cash;
            sum.Taken += r.Taken;
            sum.Done += r.Done;
            sum.Failed += r.Failed;
            sum.Refused += r.Refused;
            for (int k = 0; k < sum.ByKind.Length; k++)
            {
                sum.ByKind[k] += r.ByKind[k];
            }
        }
        return sum.Cash / 100.0 / 8 / builders / (days / 2 / 30.0);
    }

    private static void Td29Income()
    {
        const int days = 1440;
        // a small, a medium and a large firm: the posture they hold grows with them (a security analyst or two, a site reliability person)
        int[] size = { 3, 10, 30 };
        int[] sec = { 250, 350, 500 }, rel = { 250, 350, 500 };
        string[] name = { "small", "medium", "large" };
        FirmKind[] kinds = { FirmKind.Consultancy, FirmKind.Security, FirmKind.Design, FirmKind.WebAgency };
        var line = new StringBuilder();
        bool band = true, mixed = true, bandSector = true;
        double lowest = 9, highest = 0;
        foreach (bool sector in new[] { false, true })
        {
            foreach (FirmKind kind in kinds)
            {
                for (int s = 0; s < size.Length; s++)
                {
                    double before = Td29PerDev(kind, size[s], sec[s], rel[s], false, sector, days, out Td29Result off);
                    double after = Td29PerDev(kind, size[s], sec[s], rel[s], true, sector, days, out Td29Result on);
                    double ratio = before > 0 ? after / before : 0;
                    bool ok = before > 0 && ratio >= 0.88 && ratio <= (s == 0 ? 1.20 : 1.12);   // a young firm gains a little from small quick jobs, the old mix failing big ones it cannot do
                    if (sector)
                    {
                        bandSector &= ok;
                    }
                    else
                    {
                        band &= ok;
                    }
                    lowest = Math.Min(lowest, ratio);
                    highest = Math.Max(highest, ratio);
                    mixed &= off.ByKind[1] + off.ByKind[2] + off.ByKind[3] == 0 && (on.ByKind[1] + on.ByKind[2] + on.ByKind[3] > 0 || on.Taken == 0);
                    if (kind == FirmKind.Consultancy || !ok)
                    {
                        Console.WriteLine("  techdeals29 " + (sector ? "living" : "plain ") + " " + kind + " " + name[s] + " (" + size[s] + " developers): " + before.ToString("0") + " -> " + after.ToString("0")
                            + " a developer a month, x" + ratio.ToString("0.000") + "; taken " + off.Taken + " -> " + on.Taken + " (classic " + on.ByKind[0] + ", departments " + on.ByKind[1] + ", individuals " + on.ByKind[2]
                            + ", companies " + on.ByKind[3] + "), refused " + on.Refused + ", failed " + off.Failed + " -> " + on.Failed);
                    }
                }
            }
        }
        Check(band && bandSector, "tech deals: what a developer earns a month with the new clients is within 12% of the old mix's (a small firm up to 20% more, as quick small jobs suit it) for a small, a medium and a large firm of four kinds, in the old market and the living one (x" + lowest.ToString("0.00") + " to x" + highest.ToString("0.00") + ")");
        Check(mixed, "tech deals: the option off takes no work of the new kinds, and on it takes some from each");
        // the check can fail: individuals at full size and a dearer rate lift what a developer earns out of the band
        int[] points = (int[])ClientCatalogue.PointsScale.Clone(), rate = (int[])ClientCatalogue.RateScale.Clone();
        try
        {
            ClientCatalogue.PointsScale[1] = 5000;
            ClientCatalogue.RateScale[1] = 2200;
            ClientCatalogue.RateScale[2] = 1800;
            double base0 = Td29PerDev(FirmKind.Consultancy, 30, 650, 700, false, false, days, out _), rich = Td29PerDev(FirmKind.Consultancy, 30, 650, 700, true, false, days, out _);
            Check(base0 > 0 && rich / base0 > 1.12, "tech deals: the band check can fail: departments at five times the points and individuals at a dearer rate earn x" + (rich / base0).ToString("0.00") + " of the old mix");
        }
        finally
        {
            Array.Copy(points, ClientCatalogue.PointsScale, points.Length);
            Array.Copy(rate, ClientCatalogue.RateScale, rate.Length);
        }
        // young firms are offered individuals more, known ones departments more, and the old clients keep their share
        int youngInd, youngGov, knownInd, knownGov, c1, c2;
        TechClientRules.Shares(100, WorkKind.Integration, true, out youngGov, out youngInd, out c1);
        TechClientRules.Shares(900, WorkKind.Integration, true, out knownGov, out knownInd, out c2);
        TechClientRules.Shares(500, WorkKind.Managed, true, out int mg, out int mi, out int mc);
        TechClientRules.Shares(500, WorkKind.Integration, false, out int hg, out _, out _);
        Check(youngInd > knownInd && knownGov > youngGov && youngInd + youngGov + c1 + ClientCatalogue.ClassicShare == 1000 && knownInd + knownGov + c2 + ClientCatalogue.ClassicShare == 1000
              && mi == 0 && mg > 0 && mg + mi + mc + ClientCatalogue.ClassicShare == 1000 && hg == 0,
            "tech deals: a young firm is offered more individuals (" + youngInd / 10 + "% against " + knownInd / 10 + "%), a known one more departments (" + knownGov / 10 + "% against " + youngGov / 10
            + "%), a monthly service never an individual's, and no public work during an audit");
    }

    // ---------------------------------------------------------------- saves

    private static string Td29Old(string text)
    {
        // what the writer of before the kinds made of the same file: no hold in the header, and eleven fields fewer on each contract
        string[] parts = text.Split('~');
        for (int p = 0; p < parts.Length; p++)
        {
            if (parts[p].StartsWith("h:", StringComparison.Ordinal))
            {
                parts[p] = parts[p].Substring(0, parts[p].LastIndexOf('|'));
            }
            else if (parts[p].StartsWith("c:", StringComparison.Ordinal))
            {
                string[] records = parts[p].Substring(2).Split(';');
                for (int r = 0; r < records.Length; r++)
                {
                    string[] fields = records[r].Split('|');
                    records[r] = string.Join("|", fields, 0, fields.Length - 11);
                }
                parts[p] = "c:" + string.Join(";", records);
            }
        }
        return string.Join("~", parts);
    }

    private static void Td29Saves()
    {
        var ff = new FirmsFile { Seed = 77, GovHold = 0 };
        ff.Firms.Add(new Firm { Place = "Long%20Rd~7", Kind = (int)FirmKind.Consultancy, Reputation = 640 });
        ff.Contracts.Add(new Contract { Id = ff.Next(), Place = "Long%20Rd~7", Kind = (int)WorkKind.Website, Client = "Elm Street Dental", Points = 120, Value = 1320000, Stage = (int)WorkStage.Offered, End = 15 });
        ff.Contracts.Add(new Contract { Id = ff.Next(), Place = "Long%20Rd~7", Kind = (int)WorkKind.Integration, Client = "Garnet Insurance", OwnPlace = "Short%20St~3", Points = 400, Done = 120, Value = 4400000,
            Paid = 1320000, Start = 3, Due = 60, End = 9, Stage = (int)WorkStage.Active, Satisfaction = 820 });
        ff.Contracts.Add(new Contract { Id = ff.Next(), Kind = (int)WorkKind.Managed, Client = FirmsCatalogue.Council, Council = true, Value = 600000, Rivals = 3, Stage = (int)WorkStage.Tender, End = 20 });
        ff.Contracts.Add(new Contract { Id = ff.Next(), Place = "Long%20Rd~7", Kind = (int)WorkKind.Website, Client = "Copperfield Retail", Points = 80, Done = 80, Value = 880000, Paid = 880000, Stage = (int)WorkStage.Done, Due = 30, End = 9 });
        string current = FirmsStore.Write(ff);
        FirmsFile old = FirmsStore.Read(Td29Old(current), out _);
        bool same = old.Contracts.Count == 4 && old.Seed == 77 && old.GovHold == 0;
        for (int i = 0; same && i < ff.Contracts.Count; i++)
        {
            Contract a = ff.Contracts[i], b = old.Contracts[i];
            same = a.Id == b.Id && a.Place == b.Place && a.Kind == b.Kind && a.Client == b.Client && a.OwnPlace == b.OwnPlace && a.Council == b.Council && a.Points == b.Points && a.Done == b.Done
                   && a.Value == b.Value && a.Paid == b.Paid && a.Start == b.Start && a.Due == b.Due && a.End == b.End && a.Satisfaction == b.Satisfaction && a.Stage == b.Stage && a.Rivals == b.Rivals
                   && b.Deal == 0 && b.Brief == 0 && b.NeedSecurity == 0 && b.NeedReliability == 0 && b.Lag == 0 && b.Owed == 0 && b.PayDay == 0 && b.CreepMille == 0 && b.Changes == 0 && b.Extra == 0 && b.Fair == 0;
        }
        Check(same && Td29Old(current) != current && Td29Old(current).Length < current.Length, "tech deals: a save from before the kinds, with open offers, a contract under way and a tender, loads as it was, each an old client's");
        // old offers are not given a new client by the day that follows the load, and old contracts finish by the old rules
        int before = old.Contracts.Count;
        TechClientRules.Mix(old, 10, before, null, null);
        Check(old.Contracts[0].Deal == 0 && old.Contracts[0].Client == "Elm Street Dental" && old.Contracts[2].Deal == 0 && old.Contracts[2].Client == FirmsCatalogue.Council && old.Contracts.Count == before,
            "tech deals: an old open offer and the council's old tender keep their client when the new clients come in");
        // the new fields round trip
        var g = new FirmsFile { Seed = 5, GovHold = 321 };
        g.Firms.Add(new Firm { Place = "p", Kind = (int)FirmKind.Software, Reputation = 500 });
        Contract d = TechClientRules.Make(g, "p", ClientKind.Individual, WorkKind.Website, false, 30, 222);
        d.Owed = 1234567;
        d.PayDay = 88;
        d.Changes = 1;
        d.Extra = 14;
        d.CreepMille = 700;
        g.Contracts.Add(d);
        Contract t = TechClientRules.Make(g, "p", ClientKind.Government, WorkKind.Integration, true, 30, 111);
        g.Contracts.Add(t);
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(g), out _);
        Contract e = back.Contracts[0], u = back.Contracts[1];
        Check(back.GovHold == 321 && e.Deal == d.Deal && e.Brief == d.Brief && e.NeedSecurity == d.NeedSecurity && e.NeedReliability == d.NeedReliability && e.Lag == d.Lag && e.Owed == 1234567
              && e.PayDay == 88 && e.CreepMille == 700 && e.Changes == 1 && e.Extra == 14 && e.Client == d.Client && u.Fair == t.Fair && u.Fair > 0 && u.Council && u.Brief == t.Brief,
            "tech deals: a department's, an individual's and a company's terms, what is owed, the changes asked for, a tender's fair price and the audit's hold are saved");
        // a record holding a brief or a kind this version does not know reads without harm
        string future = FirmsStore.Write(g).Replace("|" + d.Deal + "|" + d.Brief + "|", "|9|99999|");
        FirmsFile odd = FirmsStore.Read(future, out _);
        Check(odd.Contracts.Count == 2 && odd.Contracts[0].Deal == (int)ClientKind.Count - 1 && odd.Contracts[0].Brief == 99999 && ClientCatalogue.BriefOf(odd.Contracts[0].Brief) == null,
            "tech deals: a kind or a brief a later version wrote reads as the nearest kind and no brief, without a fault");
    }

    // ---------------------------------------------------------------- the option off

    private static string Td29Draw(bool mix, int seed, bool sector)
    {
        var ff = new FirmsFile { Seed = seed };
        var sf = new SectorFile { Seed = seed };
        ff.Firms.Add(new Firm { Place = "a", Kind = (int)FirmKind.Consultancy, Reputation = 400 });
        ff.Firms.Add(new Firm { Place = "b", Kind = (int)FirmKind.Security, Reputation = 700 });
        ff.Firms.Add(new Firm { Place = "c", Kind = (int)FirmKind.Design, Reputation = 500 });
        var crews = new List<Crew> { new Crew { Selling = 24 }, new Crew { Selling = 16 }, new Crew { Selling = 8 } };
        var sb = new StringBuilder();
        for (int day = 1; day <= 500; day++)
        {
            int from = ff.Contracts.Count;
            if (sector)
            {
                SectorRules.Offers(ff, sf, day, 1, crews, null, null);
            }
            else
            {
                FirmsRules.Offers(ff, day, crews, null, null);
            }
            if (mix)
            {
                TechClientRules.Mix(ff, day, from, null, null);
            }
            for (int i = from; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                sb.Append(w.Id).Append(',').Append(w.Kind).Append(',').Append(w.Place).Append(',').Append(w.Stage).Append(',').Append(w.End).Append(',').Append(w.Council ? 1 : 0).Append(';');
                w.Stage = (int)WorkStage.Declined;   // answered at once: the list never fills, so every draw is of the day's own
            }
            ff.Contracts.Clear();
        }
        return sb.ToString();
    }

    private static void Td29OptionOff()
    {
        // the option off is the day's draw of the old rules and nothing else; on, the very same offers (their number, kind, firm and last day) get a client of their own
        bool same = true, onlyOld = true, counts = true;
        foreach (bool sector in new[] { false, true })
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                string off = Td29Draw(false, seed * 17, sector), on = Td29Draw(true, seed * 17, sector);
                string again = Td29Draw(false, seed * 17, sector);
                same &= off == again && off.Length > 200;
                counts &= Td29Fields(off, 0) == Td29Fields(on, 0);
            }
        }
        // with Mix never called a contract is always an old client's
        var ff = new FirmsFile { Seed = 3 };
        ff.Firms.Add(new Firm { Place = "a", Kind = (int)FirmKind.Consultancy, Reputation = 400 });
        var crews = new List<Crew> { new Crew { Selling = 24 } };
        for (int day = 1; day <= 300; day++)
        {
            FirmsRules.Offers(ff, day, crews, null, null);
        }
        foreach (Contract w in ff.Contracts)
        {
            onlyOld &= w.Deal == 0 && w.Brief == 0 && w.NeedSecurity == 0 && w.Lag == 0 && w.CreepMille == 0 && w.Fair == 0;
        }
        Check(same && counts && onlyOld && ff.Contracts.Count > 5,
            "tech deals: with the option off the day's offers are the old rules' own and all of an old client's kind, and with it on the same offers (same number, kind, firm and day) are given their clients");
        // the mix: the share of each kind is by the firm's name, over many days
        var mixFile = new FirmsFile { Seed = 9 };
        mixFile.Firms.Add(new Firm { Place = "a", Kind = (int)FirmKind.Consultancy, Reputation = 500 });
        var mixCrews = new List<Crew> { new Crew { Selling = 40 } };
        int[] kinds = new int[4];
        for (int day = 1; day <= 4000; day++)
        {
            int from = mixFile.Contracts.Count;
            FirmsRules.Offers(mixFile, day, mixCrews, null, null);
            TechClientRules.Mix(mixFile, day, from, null, null);
            for (int i = from; i < mixFile.Contracts.Count; i++)
            {
                if (mixFile.Contracts[i].Stage == (int)WorkStage.Offered)
                {
                    kinds[mixFile.Contracts[i].Deal]++;
                }
                mixFile.Contracts[i].Stage = (int)WorkStage.Declined;
            }
            mixFile.Contracts.Clear();
        }
        int sum = kinds[0] + kinds[1] + kinds[2] + kinds[3];
        Check(sum > 400 && kinds[0] > sum * 10 / 100 && kinds[0] < sum * 20 / 100 && kinds[1] > sum * 6 / 100 && kinds[1] < sum * 20 / 100 && kinds[2] > sum * 17 / 100 && kinds[2] < sum * 30 / 100
              && kinds[3] > sum * 40 / 100,
            "tech deals: at a name of 500 the offers are " + kinds[0] * 100 / sum + "% old clients, " + kinds[1] * 100 / sum + "% departments, " + kinds[2] * 100 / sum + "% individuals and " + kinds[3] * 100 / sum + "% companies");
        // a draw with Mix is the same on every machine and after every reload
        Check(Td29Draw(true, 5, false) == Td29Draw(true, 5, false) && Td29Draw(true, 5, true) == Td29Draw(true, 5, true), "tech deals: the new clients are drawn from the save's seed, the same every time");
    }

    // The nth field of every record of a draw's text.
    private static string Td29Fields(string draw, int field)
    {
        var sb = new StringBuilder();
        foreach (string rec in draw.Split(';'))
        {
            string[] f = rec.Split(',');
            sb.Append(f.Length > 4 ? f[0] + "," + f[1] + "," + f[2] + "," + f[4] + "," + f[5] + ";" : string.Empty);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- sources, texts and the options

    private static void Td29Sources()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), features = ModSource(@"Meta\FeatureList.Economy.cs");
        string director = ModSource(@"Tech\TechDirector.Clients.cs"), day = ModSource(@"Tech\TechDirector.Firms.cs");
        Check(settings.Contains("public const string TechDealKindsId = \"tech_deal_kinds\";") && settings.Contains("public static bool TechDealKinds = true;")
              && settings.Contains("TechDealKinds = Bool(modId, TechDealKindsId, TechDealKinds);") && mod.Contains("AddToggle(Settings.TechDealKindsId, \"reallife_tech_opt_deal_kinds\"")
              && mod.Contains("TestDealKinds()") && tree.Contains(".Opt(\"tech_deal_kinds\", \"tech_sector\")") && features.Contains(".Owns(Settings.TechDealKindsId).Test(\"reallife_deal_test_kinds\")"),
            "tech deals: the option is on by default, saved, shown under the Tech section's tree with its test, and owned by the tech sector's feature");
        string label = en.TryGetValue("reallife_tech_opt_deal_kinds", out string l) ? l : string.Empty, tip = en.TryGetValue("reallife_opt_tip_tech_deal_kinds", out string t) ? t : string.Empty;
        Check(label.Length > 3 && label.Length <= 40 && tip.Length > 40 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.IndexOf('—') < 0 && !tip.Contains("!") && en.ContainsKey("reallife_deal_test_kinds"),
            "tech deals: the option has its label and its tip (" + tip.Length + " characters, " + tip.Split(' ').Length + " words) and its test");
        Check(Occurs(director, "if (Settings.TechDealKinds)") == 1 && director.IndexOf("TechClientRules.Mix(", StringComparison.Ordinal) > director.IndexOf("if (Settings.TechDealKinds)", StringComparison.Ordinal)
              && Occurs(director, "TechClientRules.Mix(") == 1 && day.Contains("int offersFrom = _firmsFile.Contracts.Count;") && day.Contains("ClientsPaid(firm, day, level);"),
            "tech deals: the new clients are made only with the option on, from the day's own offers, and what is owed is paid in the firm's day");
        // every text the new code names exists; no brief is left unused
        bool keys = true;
        string missing = string.Empty;
        string[] files = { @"Tech\Clients\ClientCatalogue.cs", @"Tech\Clients\ClientViews.cs", @"Tech\Clients\ClientWords.cs", @"Tech\Clients\TechClientRules.cs", @"Tech\TechDirector.Clients.cs", @"Tech\Firms\TechApp.Pages.cs", @"Ui\Tech\TechLive.Contracts.cs", @"Tech\TechDirector.Infra.cs" };
        var used = new HashSet<string>();
        foreach (string file in files)
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:deal|infra_note_colo_moved|tech_opt_deal)[a-z0-9_]*)\""))
            {
                string key = m.Groups[1].Value;
                used.Add(key);
                if (!en.ContainsKey(key))
                {
                    keys = false;
                    missing += " " + key;
                }
            }
        }
        int briefKeys = 0;
        foreach (KeyValuePair<string, string> kv in en)
        {
            briefKeys += kv.Key.StartsWith("reallife_deal_brief_", StringComparison.Ordinal) ? 1 : 0;
        }
        Check(keys && used.Count > 25 && briefKeys == ClientCatalogue.Briefs.Length, "tech deals: every text the new code names is written (" + used.Count + " keys and " + briefKeys + " briefs)" + missing);
        // texts: notices at most 240 characters, no dash, no exclamation mark, no semicolon, every placeholder filled by the code that names it
        bool notices = true, style = true;
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (!kv.Key.StartsWith("reallife_deal_", StringComparison.Ordinal) && kv.Key != "reallife_infra_note_colo_moved")
            {
                continue;
            }
            notices &= !kv.Key.Contains("_note_") || kv.Value.Length <= 240;
            style &= kv.Value.IndexOf('—') < 0 && kv.Value.IndexOf('–') < 0 && !kv.Value.Contains(" - ") && !kv.Value.Contains("!") && !kv.Value.Contains(";");
        }
        Check(notices && style, "tech deals: the new notices are at most 240 characters and every new text keeps the house style");
        string notice = en.TryGetValue("reallife_deal_note_need", out string nn) ? nn : string.Empty;
        Check(notice.Contains("{client}") && notice.Contains("{firm}") && notice.Contains("{security}") && notice.Contains("{reliability}") && notice.Contains("{have_security}") && notice.Contains("{have_reliability}")
              && director.Contains("\"have_security\"") && director.Contains("\"have_reliability\"") && en["reallife_infra_note_colo_moved"].Contains("{racks}") && en["reallife_infra_note_colo_moved"].Contains("{name}"),
            "tech deals: the notices' placeholders are filled by the code that tells them");
        // What's new tells of both, in any row; the guide says it with facts the code fills
        Check(WhatsNewHas(en, "Tech firms now get work from city departments") && WhatsNewHas(en, "Fixed: data centres no longer take software, web or graphic design firms as colocation clients"),
            "tech deals: What's new tells of the new clients and of the colocation fix");
        string guide = en.TryGetValue("reallife_guide_tech_sector", out string g) ? g : string.Empty, guideSource = ModSource(@"Tech\TechGuide.cs");
        bool facts = guide.Contains("<b>Clients of every kind</b>");
        foreach (Match m in Regex.Matches(guide, @"\{(tech_deal_[a-z_]+)\}"))
        {
            facts &= guideSource.Contains("\"" + m.Groups[1].Value + "\"");
        }
        Check(facts && Regex.Matches(guide, @"\{tech_deal_").Count >= 8, "tech deals: the Guide's contracts section explains the kinds of client, with every figure a fact of the code");
        // both interfaces show the kind, the brief and the demands from the same words
        string old = ModSource(@"Tech\Firms\TechApp.Pages.cs"), kit = ModSource(@"Ui\Tech\TechLive.Contracts.cs"), glyphs = ModSource(@"Ui\Tech\TechGlyphs.cs");
        Check(old.Contains("ClientWords.WorkTag(w)") && old.Contains("ClientWords.Brief(w)") && old.Contains("ClientViews.Lines(_dealLines") && kit.Contains("ClientWords.WorkTag(w)") && kit.Contains("ClientWords.Brief(w)")
              && kit.Contains("ClientViews.Lines(deal") && kit.Contains("TechGlyphs.Client") && glyphs.Contains("Client = { UiIcon.Handshake, UiIcon.Bank, UiIcon.Person, UiIcon.Store }"),
            "tech deals: the old interface and the kit both show the kind, the brief, the demands and the risks of a contract");
        Check(Td29GlyphCount(glyphs) == (int)ClientKind.Count, "tech deals: the kit has a picture for each kind of client");
        // the colocation pool goes through the filter
        string colo = ModSource(@"Tech\Infra\ColoRules.cs");
        Check(colo.Contains("if (ClientFilter.ColoOpen(c))") && colo.Contains("if (!ClientOpen(c.Client))") && ModSource(@"Tech\TechDirector.Infra.cs").Contains("ColoNote.Moved ? \"reallife_infra_note_colo_moved\""),
            "tech deals: the colocation pool and its renewals go through the filter and the notice is told");
        // the shared files were added to, not rewritten
        Check(ModSource(@"Tech\Firms\FirmsRules.Contracts.cs").Contains("return TechClientRules.Project(w, firm, points, day, notes);") && ModSource(@"Tech\Firms\FirmsRules.Contracts.cs").Contains("&& w.Owed <= 0)"),
            "tech deals: the old contract rules hand a new kind's project to its own rules and keep what is still owed");
    }

    private static int Td29GlyphCount(string glyphs)
    {
        int at = glyphs.IndexOf("Client = {", StringComparison.Ordinal);
        int end = glyphs.IndexOf("};", at, StringComparison.Ordinal);
        return Regex.Matches(glyphs.Substring(at, end - at), "UiIcon\\.").Count;
    }
}
