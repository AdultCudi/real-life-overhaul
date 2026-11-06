using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

// Internet speed (Core/NetSpeedRules): the one curve and where its tiers fall among the providers' plans, a tech firm's
// day's work by its office's plan (Tech/Firms), an online shop's orders a day by its building's plan (Economy/Online),
// the grace for shops when speed first counts and its line in the save, the sources that read the speed once on the
// day's or the hour's step, and the texts.
internal static partial class Program
{
    private static void NetSpeed()
    {
        NetSpeedCurve();
        NetSpeedFirms();
        NetSpeedShops();
        NetSpeedGrace();
        NetSpeedSources();
        NetSpeedTexts();
    }

    private static void NetSpeedCurve()
    {
        Check(NetSpeedRules.Floors.Length == NetSpeedRules.Work.Length && NetSpeedRules.Work[NetSpeedRules.UsualTier] == 1000 && NetSpeedRules.Floors[NetSpeedRules.UsualTier] == 300,
            "net speed: a figure for each tier, and the usual business plan (300 Mbps) works at the usual pace");
        bool rising = true, bounded = true;
        foreach (int lean in new[] { 0, NetSpeedRules.Light, NetSpeedRules.Usual, NetSpeedRules.Heavy, NetSpeedRules.Heaviest, NetSpeedRules.Shop, 5000 })
        {
            int last = NetSpeedRules.Factor(1, lean);
            for (int mbps = 1; mbps <= 3000; mbps++)
            {
                int f = NetSpeedRules.Factor(mbps, lean);
                rising &= f >= last;
                bounded &= f >= NetSpeedRules.Least && f <= NetSpeedRules.Most;
                last = f;
            }
            bounded &= NetSpeedRules.Factor(300, lean) == 1000 && NetSpeedRules.Factor(int.MaxValue, lean) <= NetSpeedRules.Most;
        }
        Check(rising && bounded, "net speed: a faster line never works less, at every lean, within the bounds, the usual plan always at 1000");
        Check(NetSpeedRules.Factor(NetSpeedRules.Unknown, NetSpeedRules.Heaviest) == 1000 && NetSpeedRules.Factor(NetSpeedRules.Grace, NetSpeedRules.Light) == 1000
              && NetSpeedRules.Factor(0, NetSpeedRules.Usual) == 0 && NetSpeedRules.Factor(5, 0) == 1000,
            "net speed: nothing known or in the grace reads as before speed counted, no connection reads nothing, no lean leaves it alone");
        Check(NetSpeedRules.Factor(25, NetSpeedRules.Usual) == 700 && NetSpeedRules.Factor(75, NetSpeedRules.Usual) == 800 && NetSpeedRules.Factor(150, NetSpeedRules.Usual) == 900
              && NetSpeedRules.Factor(1000, NetSpeedRules.Usual) == 1050 && NetSpeedRules.Factor(2000, NetSpeedRules.Usual) == 1080
              && NetSpeedRules.Factor(25, NetSpeedRules.Heaviest) == 400 && NetSpeedRules.Factor(2000, NetSpeedRules.Heaviest) == 1160
              && NetSpeedRules.Factor(25, NetSpeedRules.Light) == 850 && NetSpeedRules.Factor(2000, NetSpeedRules.Light) == 1040 && NetSpeedRules.Factor(100, NetSpeedRules.Heavy) == 850,
            "net speed: the tiers' figures as the Guide gives them, stretched by the lean");
        Check(NetSpeedRules.NextFloor(25) == 50 && NetSpeedRules.NextFloor(150) == 300 && NetSpeedRules.NextFloor(300) == 1000 && NetSpeedRules.NextFloor(2000) == 0
              && NetSpeedRules.NextFloor(0) == 300 && NetSpeedRules.NextFloor(NetSpeedRules.Unknown) == 0 && NetSpeedRules.Tier(0) == -1 && NetSpeedRules.Tier(99999) == 5,
            "net speed: the plan that lifts a line a tier, the usual one for no line, none past the fastest");

        // Every plan the providers sell falls in a tier, and each step up a provider's list is a tier up: every price buys something.
        bool placed = true;
        var lists = new List<int[]>();
        foreach (Provider p in InternetCatalogue.Providers)
        {
            lists.Add(p.Mbps);
        }
        lists.Add(BroadbandCatalogue.Mbps);
        foreach (int[] mbps in lists)
        {
            for (int i = 0; i < mbps.Length; i++)
            {
                placed &= NetSpeedRules.Tier(mbps[i]) >= 0 && (i == 0 || NetSpeedRules.Tier(mbps[i]) > NetSpeedRules.Tier(mbps[i - 1]));
            }
        }
        bool used = true;
        for (int t = 0; t < NetSpeedRules.Floors.Length; t++)
        {
            bool sold = false;
            foreach (int[] mbps in lists)
            {
                foreach (int m in mbps)
                {
                    sold |= NetSpeedRules.Tier(m) == t;
                }
            }
            used &= sold;
        }
        Check(placed && used, "net speed: every plan on sale falls in a tier, each step up a provider's list is a tier up, and every tier has a plan in it");
    }

    private static void NetSpeedFirms()
    {
        int ai = (int)FirmKind.AiLab, games = (int)FirmKind.Games, soft = (int)FirmKind.Software, consult = (int)FirmKind.Consultancy;
        Check(FirmsCatalogue.NetLean.Length == (int)FirmKind.Count, "net speed firms: a lean for every kind of firm");
        Check(FirmsRules.Connection(ai, 25) < FirmsRules.Connection(games, 25) && FirmsRules.Connection(games, 25) < FirmsRules.Connection(soft, 25)
              && FirmsRules.Connection(soft, 25) < FirmsRules.Connection(consult, 25)
              && FirmsRules.Connection(ai, 2000) > FirmsRules.Connection(games, 2000) && FirmsRules.Connection(games, 2000) > FirmsRules.Connection(soft, 2000)
              && FirmsRules.Connection(soft, 2000) > FirmsRules.Connection(consult, 2000),
            "net speed firms: an AI lab leans on its line most, then a game studio, then software, a consultancy least, both ways");
        bool asBefore = true;
        for (int k = 0; k < (int)FirmKind.Count; k++)
        {
            asBefore &= FirmsRules.Connection(k, 300) == 1000 && FirmsRules.Connection(k, 0) == 1000 && FirmsRules.Connection(k, NetSpeedRules.Unknown) == 1000
                        && FirmsRules.Connection(k, NetSpeedRules.Grace) == 1000 && FirmsRules.Connection(k, 1000) > 1000 && FirmsRules.Connection(k, 100) < 1000;
        }
        Check(asBefore && FirmsRules.Connection(-3, 25) == FirmsRules.Connection(0, 25) && FirmsRules.Connection(99, 25) == FirmsRules.Connection((int)FirmKind.Count - 1, 25),
            "net speed firms: the usual plan, no plan (the utilities close the firm by the hour) and nothing known all work as before; slower less, faster more; any kind read safely");
        var c = new Crew { Engineering = 800, Design = 300, Web = 120, Testing = 250, Product = 90, Selling = 70, Support = 4, Seats = 9, Wages = 12345 };
        Crew same = c;
        FirmsRules.Connected(ref same, 1000);
        Crew slow = c;
        FirmsRules.Connected(ref slow, FirmsRules.Connection(ai, 25));
        Check(same.Engineering == 800 && same.Testing == 250 && slow.Engineering == 320 && slow.Design == 120 && slow.Web == 48 && slow.Testing == 100
              && slow.Product == 90 && slow.Selling == 70 && slow.Support == 4 && slow.Seats == 9 && slow.Wages == 12345,
            "net speed firms: a day's building, design, web and testing points go by the line's share; product, selling, support, seats and wages are left alone");
        // A product built for a month on each line: a slower line finishes fewer features, the usual plan as before.
        int[] features = new int[3];
        int[] lines = { 25, 300, 2000 };
        for (int i = 0; i < lines.Length; i++)
        {
            var firm = new Firm { Place = "N~" + i, Kind = soft, Focus = (int)Focus.Features };
            var product = new Product { Place = firm.Place, Kind = (int)RealLifeOverhaul.Tech.ProductKind.Saas };
            for (int day = 0; day < 30; day++)
            {
                Crew crew = new Crew { Engineering = 40, Testing = 8, Product = 8, SeniorShare = 500, Faults = 100 };
                FirmsRules.Connected(ref crew, FirmsRules.Connection(soft, lines[i]));
                FirmsRules.Build(firm, product, crew, crew.Engineering, crew.Testing);
            }
            features[i] = product.Features;
        }
        Check(features[0] < features[1] && features[1] <= features[2], "net speed firms: a month's product on 25, 300 and 2000 Mbps finishes " + features[0] + ", " + features[1] + " and " + features[2] + " features");
    }

    private static void NetSpeedShops()
    {
        Check(OnlineCatalogue.NetLean == NetSpeedRules.Shop, "net speed shops: the shops' lean is the one the plan picker reads");
        bool asBefore = true;
        for (int q = 0; q < OnlineCatalogue.Qualities; q++)
        {
            asBefore &= OnlineRules.Most(q, NetSpeedRules.Unknown) == OnlineCatalogue.OrdersMost[q] && OnlineRules.Most(q, NetSpeedRules.Grace) == OnlineCatalogue.OrdersMost[q]
                        && OnlineRules.Most(q, 300) == OnlineCatalogue.OrdersMost[q] && OnlineRules.Most(q, 0) == 0;
        }
        Check(asBefore, "net speed shops: nothing known, the grace and the usual plan keep each site's bound as before; no connection, none");
        Check(OnlineRules.Most(0, 25) == 8 && OnlineRules.Most(1, 25) == 16 && OnlineRules.Most(2, 50) == 36 && OnlineRules.Most(1, 100) == 32 && OnlineRules.Most(1, 1000) == 44
              && OnlineRules.Most(2, 2000) == 69,
            "net speed shops: the day's bound by the line, worked out by hand (Basic on copper 8, Professional on fibre 69)");
        bool same = true;
        for (int q = 0; q < OnlineCatalogue.Qualities; q++)
        {
            for (int stars = 1000; stars <= 5000; stars += 2000)
            {
                same &= OnlineRules.PerDay(q, false, 8, 90, 1400, 0, stars, 30, 20, 0, 0, 1) == OnlineRules.PerDay(q, false, 8, 90, 1400, 0, stars, 30, 20, 0, 0, 1, NetSpeedRules.Unknown)
                        && OnlineRules.PerDay(q, false, 8, 90, 1400, 0, stars, 30, 20, 0, 0, 1, 25) <= OnlineRules.Most(q, 25) * 1000;
            }
        }
        Check(same, "net speed shops: with nothing known the orders a day are as before, and a slow line holds them under its bound");

        // A day of orders at each line: never past the line's bound, none with no line or in an outage.
        var w = new OnlineWorld();
        WebShop shop = OnlineShop(w, "N~9", 2, OnlineCatalogue.Courier, 0, 8, 1500, 2500, 100, 2000, out Site site);
        bool held = true;
        int[] speeds = { 0, 25, 75, 150, 300, 1000, 2000 };
        int[] came = new int[speeds.Length];
        for (int i = 0; i < speeds.Length; i++)
        {
            site.Mbps = speeds[i];
            OnlineRules.Plan(shop, site, 1, 0, 0, 0, 1);
            site.PerDay = OnlineCatalogue.OrdersMost[2] * 1000 * 4;   // far over any bound, so the bound is what stops the day
            var day = new WebShop { Place = shop.Place, Quality = shop.Quality, Promise = shop.Promise };
            day.Lines.AddRange(shop.Lines);
            for (int h = 0; h < 24; h++)
            {
                came[i] += OnlineRules.Arrive(w.State, day, site, 24 + h);
            }
            held &= came[i] == Math.Min(OnlineRules.Most(2, speeds[i]), OnlineCatalogue.OpenMost);
        }
        site.Mbps = 300;
        site.Down = true;
        var outage = new WebShop { Place = shop.Place, Quality = shop.Quality };
        outage.Lines.AddRange(shop.Lines);
        int during = OnlineRules.Arrive(w.State, outage, site, 24 + 18);
        site.Down = false;
        Check(held && came[0] == 0 && came[1] < came[4] && came[4] < came[6] && during == 0,
            "net speed shops: a day of orders stops at the line's bound (" + string.Join(", ", came) + " on 0 to 2000 Mbps), none in an outage");
    }

    private static void NetSpeedGrace()
    {
        var s = new UtilitiesState();
        InternetRules.BeginGrace(s, new[] { "Hq~1" }, 10);
        int until = s.GraceUntil;
        int added = InternetRules.AddGrace(s, new[] { "Shop~2", "Hq~1", "Shop~2", "" }, 12);
        Check(added == 1 && s.Grace.Count == 2 && s.GraceUntil == Math.Max(until, 12 + InternetCatalogue.GraceDays) && InternetRules.InGrace(s, "Shop~2", 12 + InternetCatalogue.GraceDays - 1)
              && !InternetRules.InGrace(s, "Shop~2", 12 + InternetCatalogue.GraceDays),
            "net speed grace: shops with no connection join the grace for its days from today, one end day for all, each place once");
        var none = new UtilitiesState();
        Check(InternetRules.AddGrace(none, new string[0], 5) == 0 && none.GraceUntil == 0 && InternetRules.AddGrace(none, null, 5) == 0,
            "net speed grace: no shop without a connection, no grace begun");
        s.SpeedMet = 12;
        UtilitiesState back = UtilitiesStore.Read(UtilitiesStore.Write(s), out _);
        UtilitiesState old = UtilitiesStore.Read("v=2~h:40|36|99|7|1000|1000|1000|1000~a:", out _);
        Check(back != null && back.SpeedMet == 12 && back.GraceUntil == s.GraceUntil && back.Grace.Count == 2 && old != null && old.SpeedMet == 0 && old.Seed == 99,
            "net speed grace: the day speed first counted is kept in the save after the providers' months; a line from before reads not yet");
    }

    private static void NetSpeedSources()
    {
        string firms = ModSource(@"Tech\TechDirector.Firms.cs");
        string tech = ModSource(@"Wiring\TechLinks.cs"), online = ModSource(@"Wiring\OnlineLinks.cs"), utility = ModSource(@"Wiring\UtilityLinks.cs");
        string net = ModSource(@"Economy\Online\OnlineDirector.Net.cs"), day = ModSource(@"Economy\Online\OnlineDirector.Day.cs");
        string page = ModSource(@"Tech\Firms\TechApp.Pages.cs"), shopPage = ModSource(@"Economy\Online\OnlineApp.Shops.cs"), picker = ModSource(@"Utilities\UtilitiesApp.Internet.cs");
        int spare = firms.IndexOf("c.Testing = c.Testing * firm.Spare / 1000;", StringComparison.Ordinal), read = firms.IndexOf("firm.Mbps = TechBridges.PlanMbpsAt(firm.Reg);", StringComparison.Ordinal);
        Check(spare > 0 && read > spare && firms.IndexOf("FirmsRules.Connected(ref c, firm.Net);", StringComparison.Ordinal) > read && Occurs(firms, "TechBridges.PlanMbpsAt(") == 1,
            "net speed sources: the firms read their office's plan once, with the day's crew, after the spare hours, and scale the day's points by it");
        Check(tech.Contains("TechBridges.PlanMbps = reg => UtilityLinks.Speed(reg, -1, false);") && online.Contains("OnlineBridges.Mbps = (reg, hour) => UtilityLinks.Speed(reg, hour, true);")
              && utility.Contains("!Settings.Utilities || !Settings.UtilitiesNetSpeed") && Occurs(utility, "d.Speed(reg, hour, shop)") == 1,
            "net speed sources: the firms and the shops read the speed through one function, unknown with the utilities or the switch off");
        Check(day.Contains("NetHour(place, hour);") && day.Contains("place.Site.Mbps = Speed(place.Reg, -1);") && net.Contains("site.Down = plan > 0 && Speed(place.Reg, hour) == 0;"),
            "net speed sources: the shops read the plan with the day's orders and the outage each hour they work through");
        Check(page.Contains("FirmsRules.Connection(kind, mbps)") && shopPage.Contains("OnlineRules.Most(quality, mbps)") && picker.Contains("NetSpeedRules.Factor(mbps, NetSpeedRules.Shop)")
              && picker.Contains("firmWork(place.Reg, mbps)") && picker.Contains("shopMost(place.Reg, mbps)"),
            "net speed sources: the firm's page, the shop's page and the plan picker show the figures from the rules the work goes by");
        Check(!firms.Contains("Update(") && !net.Contains("Update("), "net speed sources: nothing read per frame");
    }

    private static void NetSpeedTexts()
    {
        string[] keys =
        {
            "reallife_util_net_speed", "reallife_opt_tip_utilities_net_speed", "reallife_tech_firm_net", "reallife_tech_firm_net_lift", "reallife_tech_firm_net_top",
            "reallife_tech_firm_net_none", "reallife_tech_firm_net_grace", "reallife_online_fig_net", "reallife_online_fig_net_lift", "reallife_online_fig_net_none",
            "reallife_online_fig_net_down", "reallife_online_fig_net_grace", "reallife_online_open_net", "reallife_online_open_net_none", "reallife_online_note_no_net",
            "reallife_online_note_net_back", "reallife_net_needed_orders", "reallife_net_box_speed_note", "reallife_net_box_needs_orders", "reallife_net_box_grace_shop",
            "reallife_net_speed_firm", "reallife_net_speed_tech", "reallife_net_speed_shop_here", "reallife_net_speed_shop", "reallife_net_toast_shop_grace",
            "reallife_net_toast_shop_grace_over", "reallife_guide_net_under", "reallife_guide_net_from", "reallife_tech_test_net", "reallife_tech_test_net_pass",
            "reallife_tech_test_net_pass_none", "reallife_tech_test_net_fail", "reallife_online_test_net", "reallife_online_test_net_pass", "reallife_online_test_net_fail"
        };
        Check(AllIn(keys, out string missing), "net speed texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool short240 = true;
        foreach (string k in new[] { "reallife_online_note_no_net", "reallife_online_note_net_back", "reallife_net_toast_shop_grace", "reallife_net_toast_shop_grace_over" })
        {
            short240 &= en.TryGetValue(k, out string v) && v.Length <= 240;
        }
        Check(short240, "net speed texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_guide_utilities", out string util) && util.Contains("{net_speed_work}") && util.Contains("{net_speed_shop}") && util.Contains("Internet speed matters")
              && en.TryGetValue("reallife_guide_tech_sector", out string sector) && sector.Contains("{net_speed_work}")
              && en.TryGetValue("reallife_guide_economy_online", out string shops) && shops.Contains("{net_speed_shop}"),
            "net speed texts: the Guide's pages on utilities, tech firms and online shops give the speeds from the code");
        bool nine = true;
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            nine &= WhatsNewSpeed(code, texts);
        }
        Check(nine, "net speed texts: What's new has the line in all nine languages");
    }

    // The What's new line's own opening words in each language, looked for in any row, so a release that numbers the row keeps it found.
    private static bool WhatsNewSpeed(string code, Dictionary<string, string> texts)
    {
        string words = code switch
        {
            "en" => "Internet speed now matters",
            "pt" => "A velocidade da internet agora importa",
            "zh-cn" => "网速现在有了影响",
            "de" => "Die Internetgeschwindigkeit zählt jetzt",
            "fr" => "La vitesse d'internet compte maintenant ",
            "es" => "La velocidad de internet ahora importa",
            "ru" => "Скорость интернета теперь важна",
            "nl" => "Internetsnelheid telt nu mee",
            "tr" => "İnternet hızı artık önemli",
            _ => null
        };
        return words != null && WhatsNewHas(texts, words);
    }
}
