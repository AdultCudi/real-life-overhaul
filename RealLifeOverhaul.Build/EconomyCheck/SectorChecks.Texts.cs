using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;

// The other tech firms' texts and sources (SectorChecks.cs runs them): every key the screens, the desk sessions, the notices,
// the paper, the tests, the option and the Guide use is in en, pt and zh-cn; a notice is a toast's length with long names in
// it; no session button carries a placeholder; the Guide's page names its option and fills every figure from the code;
// What's new tells of it in all eleven languages; and the sources run the sector under its option, the old rules otherwise.
internal static partial class Program
{
    private const string SectorMod = "reallife";

    private static void SectorTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string links = ModSource(@"Wiring\Wiring.SectorLinks.cs");
        var keys = new List<string>(SectorScreenKeys);
        string desk = SectorMod + "_tech_desk_", mkt = SectorMod + "_tech_mkt_";
        // the sessions' keys: each kind's title, about, steps, choices with their trade-offs and its notice
        var buttons = new List<string>();
        string body = MemberText(links, "internal static SessionDef Kind(int kind)");
        string[] blocks = Regex.Split(body, @"case SectorRules\.Desk|default:");
        var names = new List<string>();
        for (int k = 0; k < SectorRules.Desks; k++)
        {
            names.Add(SectorRules.DeskIds[k].Substring(5));
        }
        int choices = 0;
        for (int b = 1; b < blocks.Length; b++)
        {
            Match head = Regex.Match(blocks[b], @"^(\w+):");
            int k = head.Success ? Array.IndexOf(DeskConstants, head.Groups[1].Value) : SectorRules.DeskPortfolio;
            if (k < 0)
            {
                continue;
            }
            string name = names[k];
            keys.Add(desk + name + "_title");
            keys.Add(desk + name + "_about");
            keys.Add(desk + name + "_done");
            foreach (Match s in Regex.Matches(blocks[b], @"S\(name, (\d)"))
            {
                keys.Add(desk + name + "_step" + s.Groups[1].Value);
            }
            foreach (Match c in Regex.Matches(blocks[b], @"C\(name, ""([a-z]+)"""))
            {
                keys.Add(desk + name + "_" + c.Groups[1].Value);
                keys.Add(desk + name + "_" + c.Groups[1].Value + "_trade");
                buttons.Add(desk + name + "_" + c.Groups[1].Value);
                choices++;
            }
        }
        Check(choices >= 70 && blocks.Length == SectorRules.Desks + 1, "sector texts: every session's choices were read from the wiring (" + choices + " choices in " + (blocks.Length - 1) + " kinds)");
        foreach (string extra in new[] { "pitch_done_bid", "pitch_done_lost", "clientpitch_done_lost", "portfolio_done_award", "capacity_done_cash" })
        {
            keys.Add(desk + extra);
        }
        string sources = ModSource(@"Tech\TechDirector.Sector.cs") + ModSource(@"Tech\TechDirector.SectorDesk.cs") + ModSource(@"Tech\TechDirector.SectorTests.cs")
                         + ModSource(@"Tech\TechDirector.Firms.cs") + ModSource(@"Tech\Sector\TechApp.Sector.cs") + ModSource(@"Tech\Firms\TechApp.Pages.cs");
        foreach (Match m in Regex.Matches(sources, "\"(" + SectorMod + "_tech_(?:mkt|desk)_[a-z0-9_]+[a-z0-9])\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        foreach (Match m in Regex.Matches(sources, "DeskKeys \\+ \"([a-z_]+)\""))
        {
            if (!m.Groups[1].Value.EndsWith("_", StringComparison.Ordinal))
            {
                keys.Add(desk + m.Groups[1].Value);
            }
        }
        keys.Add(SectorMod + "_tech_mkt_enabled");
        keys.Add(SectorMod + "_opt_sec_firms");
        keys.Add(SectorMod + "_opt_tip_living_firms");
        keys.Add(SectorMod + "_meta_title_firms");
        keys.Add(SectorMod + "_guide_firms_about");
        keys.Add(SectorMod + "_guide_firms");
        keys.Add(SectorMod + "_transaction_tech_desk_costs");
        keys.RemoveAll(k => !k.StartsWith(SectorMod + "_", StringComparison.Ordinal));   // the game's own keys (common_close) are the game's
        Check(AllIn(keys.ToArray(), out string missing), "sector texts: every key in en, pt and zh-cn (" + keys.Count + " keys; " + missing + ")");
        // no key of the sector's in en that pt or zh-cn lack
        var own = new List<string>();
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (kv.Key.StartsWith(desk, StringComparison.Ordinal) || kv.Key.StartsWith(mkt, StringComparison.Ordinal))
            {
                own.Add(kv.Key);
            }
        }
        Check(own.Count > 300 && AllIn(own.ToArray(), out string lacking), "sector texts: every text of the sector's in en is in pt and zh-cn too (" + own.Count + ")");
        string over = "none";
        bool fits = true;
        string longName = new string('n', 24);
        foreach (string k in keys)
        {
            if (!en.TryGetValue(k, out string v) || !(k.Contains("_note_") || k.Contains("_test_") || k.Contains("_done") || k.Contains("_why_")))
            {
                continue;
            }
            string filled = Regex.Replace(v, @"\{(business|subject|firm|rival|product|client|name|market|kind|session|choice0|choice1|choice2)\}", longName);
            if (filled.Length > 240)
            {
                fits = false;
                over = k + " (" + filled.Length + ")";
            }
        }
        Check(fits, "sector texts: every notice is 240 characters or fewer with long names in it (" + over + ")");
        bool plain = buttons.Count > 0;
        foreach (string b in buttons)
        {
            plain &= en.TryGetValue(b, out string v) && !v.Contains("{");
        }
        Check(plain, "sector texts: no session button's text has a placeholder, as the game's panel shows a button's key alone");
        string page = en.TryGetValue(SectorMod + "_guide_firms", out string guide) ? guide : string.Empty, facts = ModSource(@"Tech\Sector\SectorGuide.cs");
        bool filledAll = page.Length > 2000;
        int count = 0;
        foreach (Match m in Regex.Matches(page, @"\{([a-z0-9_]+)\}"))
        {
            filledAll &= facts.Contains("\"" + m.Groups[1].Value + "\"");
            count++;
        }
        Check(filledAll && count >= 20 && page.Contains("<b>Living population</b>") && page.Contains("<b>Tech firms rebuilt</b>"),
            "sector texts: the Guide's tech firms page names its options and every one of its " + count + " figures is the code's");
        Check(WhatsNewHas(en, "The other tech firms are rebuilt") && WhatsNewHas(LocaleFile("pt"), "As outras empresas de tecnologia foram refeitas")
              && WhatsNewHas(LocaleFile("zh-cn"), "其他科技公司全面重做") && WhatsNewHas(LocaleFile("de"), "Die übrigen Techfirmen sind neu gebaut")
              && WhatsNewHas(LocaleFile("fr"), "Les autres entreprises tech sont refaites") && WhatsNewHas(LocaleFile("es"), "Las demás empresas tecnológicas se han rehecho")
              && WhatsNewHas(LocaleFile("ru"), "Остальные IT-компании переделаны") && WhatsNewHas(LocaleFile("nl"), "De andere techbedrijven zijn opnieuw opgebouwd")
              && WhatsNewHas(LocaleFile("tr"), "Diğer teknoloji şirketleri yeniden yapıldı") && WhatsNewHas(LocaleFile("pl"), "Pozostałe firmy technologiczne są przebudowane")
              && WhatsNewHas(LocaleFile("ko"), "다른 기술 회사들이 새로 만들어졌습니다"),
            "sector texts: What's new tells of the rebuilt tech firms in all eleven languages (any of its rows)");
        SectorSources();
    }

    // The desk kinds' constants as the wiring's switch names them, in SectorRules' order.
    private static readonly string[] DeskConstants =
    {
        "Sprint", "Outage", "BigCustomer", "Training", "Launch", "Pitch", "Scope", "Incident", "Capacity", "Power", "Failover", "ClientPitch", "Revision", "Portfolio"
    };

    private static void SectorSources()
    {
        string director = ModSource(@"Tech\TechDirector.cs"), firms = ModSource(@"Tech\TechDirector.Firms.cs"), infra = ModSource(@"Tech\TechDirector.Infra.cs");
        string sector = ModSource(@"Tech\TechDirector.Sector.cs"), wiring = ModSource(@"Wiring\Wiring.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(director.Contains("bool markets = _firmsOn && SectorWanted;") && director.Contains("StartSector();") && director.Contains("StopSector(true);")
              && director.Contains("WriteSector(Save.modData);") && sector.Contains("Settings.LivingPopulation && Settings.LivingFirms"),
            "sector sources: the markets run while Living population and Tech firms rebuilt are on, and are saved with the firms");
        Check(firms.Contains("SectorRules.Offers(_firmsFile, _sector, day, cycle, _crews, _clients, _work);") && firms.Contains("FirmsRules.Offers(_firmsFile, day, _crews, _clients, _work);")
              && firms.Contains("SectorRules.Decide(") && firms.Contains(": FirmsRules.Decide(") && firms.Contains("FirmsRules.Sell(_firmsFile, file, p, c, c.Support, day, cycle, HostingRate())")
              && CodeAt(firms, "SectorDayBegin(day, cycle);") > 0 && CodeAt(firms, "SectorDayBegin(day, cycle);") < CodeAt(firms, "SectorProductDay(firm, p, i, points, testing, day, HostingRate(), out _)"),
            "sector sources: under the option the firms' day begins the markets before any product sells, offers and tenders are the city's; off, the firms' own rules stand as before");
        Check(infra.Contains("SectorRules.RacksToLet(_sector, _infra, cycle, share) : InfraRules.ColoWanted(cycle, share)") && infra.Contains("SectorRules.CloudDemand(c, market,")
              && infra.Contains(": InfraRules.CityDemand(c, cycle, Math.Max(0, capacity - ownUnits), false, share, regions)") && infra.Contains("SectorRules.PowerFactor(_sector, day)"),
            "sector sources: data centres let racks and the cloud sells by the city's firms under the option, by the old fixed demand without it");
        Check(sector.Contains("Id = SectorShare.Part") && sector.Contains("SharedCity.Follows(SectorShare.Part)") && sector.Contains("if (!following)")
              && sector.Contains("SectorRules.RivalDay(_sector, day, _rivalNotes);"),
            "sector sources, Going Public: the host steps the rivals and shares them as part R; a guest follows the host's and steps none");
        Check(wiring.Contains("SectorLinks.Connect") && wiring.Contains("SectorLinks.Disconnect") && mod.Contains("AddToggle(Settings.LivingFirmsId, \"" + SectorMod + "_tech_mkt_enabled\"")
              && Regex.Matches(mod, "AddButton\\(\"" + SectorMod + "_tech_mkt_test_").Count == 8 && ModSource(@"Meta\FeatureList.cs").Contains("Tech.SectorGuide.Register();")
              && ModSource(@"Meta\OptionsTree.cs").Contains("Opt(\"living_firms\", \"living_population\")") && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"firms\""),
            "sector sources: the wiring connects it, its option and eight tests are on the options page, its Guide entry and its section are listed, its city line is there");
        string all = ModSource(@"Tech\Sector\SectorRules.cs") + ModSource(@"Tech\Sector\SectorRules.Rivals.cs") + ModSource(@"Tech\Sector\SectorRules.Work.cs") + ModSource(@"Tech\Sector\SectorRules.Infra.cs")
                     + ModSource(@"Tech\Sector\SectorRules.Desk.cs") + ModSource(@"Tech\Sector\SectorViews.cs") + ModSource(@"Tech\Sector\SectorStore.cs");
        Check(!all.Contains("UnityEngine") && !all.Contains("Harmony") && !all.Contains("new System.Random") && !Regex.IsMatch(all, @"(?<![A-Za-z])Random\.Range") && !all.Contains("DateTime"),
            "sector sources: the rules are game-free, patch nothing and roll only the sector's seed, so every machine gets the same");
    }
}
