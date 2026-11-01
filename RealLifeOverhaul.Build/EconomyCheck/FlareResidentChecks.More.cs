using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

// Flare in the city, its screens, its part of the shared city, its words and its sources: every screen built from a real city with its keys in
// en, pt and zh-cn, the part a guest follows read back as the host wrote it (the same voices, the same names on every machine), the notices within
// 240 characters, the Guide page's figures filled from the code, What's new in all eleven languages, and the code read as text for the rules of the
// overhaul (nothing per frame, the options checked, the attention a source of the population's, Flare's talk out of business pressure's figure while
// it runs, the save's one line, the part a letter of its own).
internal static partial class Program
{
    private static void FlareResidentMore()
    {
        FlareResidentViews();
        FlareResidentShare();
        FlareResidentTexts();
        FlareResidentSources();
    }

    // ---------------------------------------------------------------- the screens

    private static void FlareResidentViews()
    {
        FlareResidentRig rig = FlareResidentCity(42, 5000, (int)FlareTier.Known);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.EatingOut);
        c.Funnels.Add(new FlareFunnel { Post = 4, Kind = FunnelKind.Post, Place = "Cafe~1", Day = s.Day, Seen = 900, Interested = 200, Came = 40, Spent = 56000, Live = 1000 });
        c.Funnels.Add(new FlareFunnel { Post = 1, Kind = FunnelKind.Ad, Place = "Cafe~1", Day = s.Day, Seen = 3000, Interested = 1500, Came = 90, Spent = 126000 });
        FlareCityView account = FlareCityViews.Account(c, 5000, place => "Five Cups");
        Check(account.Kind == FlareViewKind.Account && account.Hero.Count == 2 && account.Hero[0].Value == c.CityFollowers && account.Hero[1].Value == 5000 - c.CityFollowers
              && account.Rows.Count == 2 && account.Rows[0].Subtitle.EndsWith("row_ad", StringComparison.Ordinal) && account.Rows[0].Second == 1260 && account.Empty != null,
            "flare city screens: the account in the city, its followers here and beyond, and what each post and ad did, newest first");
        FlareCityView page = FlareCityViews.Business(c, s, rig.Pass.PersonSlot, b, "Five Cups", "Cafe~1", true);
        Check(page.Kind == FlareViewKind.Business && page.Hero.Count == 3 && page.Hero[0].Value == c.Followers[b] && page.Hero[1].Value == c.Rating(b) && page.Primary != null
              && page.Details.Count == 5 && page.Rows.Count <= FlareCityViews.RowsMost,
            "flare city screens: a business's page, its followers, rating and pull, its newest reviews by customers, what it heard and brought, and one action for the player's own");
        bool reviewers = true;
        foreach (FlareViewRow r in page.Rows)
        {
            reviewers &= r.Person > 0 && r.Value >= 10 && r.Value <= 50;
        }
        Check(reviewers, "flare city screens: each review row is a resident and their stars");
        FlareCityView city = FlareCityViews.Business(c, s, rig.Pass.PersonSlot, b, "Five Cups", "Cafe~1", false);
        Check(city.Primary == null && city.Subtitle.EndsWith("sub_city", StringComparison.Ordinal), "flare city screens: a business of the city's has no action of the player's");
        FlareCityView none = FlareCityViews.Business(c, s, rig.Pass.PersonSlot, -1, "Gone", null, false);
        Check(none.Rows.Count == 0 && none.Details.Count == 0 && none.Empty != null, "flare city screens: a business the residents do not know shows words, never a blank page");
        var ad = new FlareAd { Name = "Five Cups", Place = "Cafe~1", Reach = 3000, Days = 3 };
        FlareCityView quote = FlareCityViews.Ad(ad, 0, 2400, 198000, null), refused = FlareCityViews.Ad(ad, 9, 0, 0, "reallife_flare_ad_why_nobody");
        Check(quote.Primary != null && quote.Empty == null && quote.Hero[2].Value == 1980 && refused.Primary == null && refused.Empty == "reallife_flare_ad_why_nobody"
              && refused.Details[0].Text.EndsWith("target_4", StringComparison.Ordinal),
            "flare city screens: an ad's reach, days and price, with its one action, or why it cannot run in its place");
        FlareCityView funnel = FlareCityViews.Funnel(c.Funnels[0], "Five Cups");
        Check(funnel.Hero.Count == 4 && funnel.Hero[0].Value == 900 && funnel.Hero[3].Value == 560 && funnel.Details[0].Label.EndsWith("fig_followed", StringComparison.Ordinal),
            "flare city screens: a post's funnel, seen, interested, came and spent");
        var used = new List<string>();
        foreach (FlareCityView v in new[] { account, page, city, none, quote, refused, funnel })
        {
            used.Add(v.TitleKey ? v.Title : null);
            used.Add(v.Subtitle);
            used.Add(v.RowsHeader);
            used.Add(v.Primary);
            used.Add(v.Empty);
            used.AddRange(v.Hero.Select(f => f.Label));
            used.AddRange(v.Details.Select(f => f.Label));
            used.AddRange(v.Details.Where(f => f.Format == FlareViewFormat.Key).Select(f => f.Text));
            used.AddRange(v.Rows.Select(r => r.Subtitle));
        }
        bool listed = used.Where(k => k != null).All(k => FlareCityViews.Keys.Contains(k) || k.StartsWith("reallife_flare_ad_why_", StringComparison.Ordinal));
        bool inLocales = AllIn(FlareCityViews.Keys, out string missing);
        Check(listed && inLocales, "flare city screens: every word the screens show is a key of the list, in en, pt and zh-cn (" + FlareCityViews.Keys.Length + "; " + missing + ")");
    }

    // ---------------------------------------------------------------- Going Public: part Q

    private static void FlareResidentShare()
    {
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.Clothing);
        FlareCityRules.AddReview(c, b, s.People.Id[3], 4, s.Day);
        string text = FlareCityShare.Write(c, s, s.Day, rig.Pass.PersonSlot, i => i == b ? "Shop~7" : null);
        var followed = new FlareCityFollowed();
        Check(FlareCityShare.Read(text, followed) && followed.Day == s.Day, "flare city shared: the host's part reads back on a guest (" + text.Length / 1024 + " KB)");
        bool voices = true;
        for (int v = 0; v < c.Voices.Length && voices; v++)
        {
            CityVoice host = FlareCityRules.VoiceOf(c, s, v, s.Day, rig.Pass.PersonSlot);
            CityVoice guest = followed.Voices[v];
            voices = host.Person == guest.Person && host.Age == guest.Age && host.Female == guest.Female && host.Persona == guest.Persona
                     && (host.District < 0 ? followed.VoiceDistricts[v] == string.Empty : followed.VoiceDistricts[v] == s.Districts[host.District].Id);
        }
        Check(voices, "flare city shared: a guest follows the host's voices, each the same resident with the same age, sex, district and manner");
        var hostNames = new HashSet<string>();
        var guestNames = new HashSet<string>();
        bool same = true;
        for (int v = 0; v < 40; v++)
        {
            CityVoice host = FlareCityRules.VoiceOf(c, s, v, s.Day, rig.Pass.PersonSlot);
            FlareCitizen a = FlareLiveRules.ResidentCitizen(c.Seed, v, host.Person, host.Female, host.Age, host.District >= 0 ? s.Districts[host.District].Id : null, host.Persona, hostNames);
            FlareCitizen g = FlareLiveRules.ResidentCitizen(c.Seed, v, followed.Voices[v].Person, followed.Voices[v].Female, followed.Voices[v].Age, followed.VoiceDistricts[v] == string.Empty ? null : followed.VoiceDistricts[v],
                followed.Voices[v].Persona, guestNames);
            same &= a.First == g.First && a.Last == g.Last && a.Handle == g.Handle;
        }
        Check(same, "flare city shared: so the city's people have the same names and handles on every machine");
        Check(followed.Businesses.TryGetValue("Shop~7", out FlareCityFigures fig) && followed.Businesses.Count == 1 && fig.Followers == c.Followers[b] && fig.Rating == c.Rating(b)
              && fig.Attention == c.Attention[b] && fig.Reviewer == s.People.Id[3] && fig.Stars == 4,
            "flare city shared: a guest's business's figures come with it, and nobody else's");
        Check(!FlareCityShare.Read("FR9|1", followed) && !FlareCityShare.Read(null, followed) && !FlareCityShare.Read("garbage", followed)
              && FlareCityShare.Read("FR1|3\nV|5|x|1|30|d|2\nB|a|1\nQ|later", followed) && followed.Day == 3 && followed.Voices[5].Person == 0 && followed.Businesses.Count == 0,
            "flare city shared: another version, a damaged text or a line this version does not know is left out, never a fault");
        Check(FlareCityShare.Part == 'Q' && FlareCityShare.Version == "FR1", "flare city shared: a part of its own, versioned, that an older version never knows and leaves out");
    }

    // ---------------------------------------------------------------- the words

    private static void FlareResidentTexts()
    {
        string scripts = ModScripts();
        var keys = new List<string>();
        foreach (string file in Directory.GetFiles(System.IO.Path.Combine(scripts, "Social", "Flare", "City"), "*.cs")
                     .Concat(new[] { "FlareDirector.Residents.cs", "FlareDirector.Tests.City.cs", "FlareApp.City.cs", "FlareDirector.Creator.cs", "FlareDirector.Studio.cs", "FlareDirector.Trends.cs", "FlareDirector.Sector.cs", "FlareDirector.Desk.cs", "FlareText.cs", "FlareText.Live.cs" }
                                 .Select(f => System.IO.Path.Combine(scripts, "Social", "Flare", f))))
        {
            const string Ours = "flare_city|flare_ad|flare_offer|flare_creator|flare_studio|flare_sector|flare_session|flare_post_deal|guide_flare_city|meta_title_flare_city|transaction_flare";
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(reallife_(?:" + Ours + ")[a-z0-9_]*)\""))
            {
                string k = m.Groups[1].Value;
                if (!k.EndsWith("_", StringComparison.Ordinal) && !keys.Contains(k))
                {
                    keys.Add(k);
                }
            }
        }
        for (int t = 0; t < FlareCityCatalogue.AdTargets; t++)
        {
            keys.Add("reallife_flare_city_target_" + t);
        }
        keys.Add("reallife_opt_tip_living_flare");
        Check(AllIn(keys.ToArray(), out string missing) && keys.Count > 70 && keys.Contains("reallife_flare_studio_crowdfund") && keys.Contains("reallife_flare_post_deal_2")
              && keys.Contains("reallife_flare_sector_news_8"), "flare city texts: every key its screens, sheet, notices and tests name in en, pt and zh-cn (" + keys.Count + "; " + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        string[] notices = { "reallife_flare_city_test_off", "reallife_flare_city_test_wait", "reallife_flare_city_test_day_done", "reallife_flare_city_test_no_business",
                             "reallife_flare_city_test_post_done", "reallife_flare_city_test_ad_done", "reallife_flare_city_test_page_done", "reallife_flare_city_test_offer_done",
                             "reallife_flare_city_test_offer_none", "reallife_flare_offer_note", "reallife_flare_creator_note", "reallife_flare_city_test_trend_done",
                             "reallife_flare_city_test_trend_none" };
        Check(notices.All(k => Fits(en, k)), "flare city texts: every notice is 240 characters or fewer");
        string page = en.TryGetValue("reallife_guide_flare_city", out string p) ? p : string.Empty;
        string guide = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "City", "FlareCityGuide.cs"));
        bool facts = true;
        int count = 0;
        foreach (Match m in Regex.Matches(page, @"\{([a-z_]+)\}"))
        {
            facts &= guide.Contains(".Fact(\"" + m.Groups[1].Value + "\"");
            count++;
        }
        Check(facts && count >= 18 && page.Contains("# Ads") && page.Contains("{flarecity_ad_cpm}") && page.Contains("# Paid posts and income") && page.Contains("# Your game studio")
              && page.Contains("# The market and the council") && page.Contains("{flarecity_offer_fee}") && page.Contains("{flarecity_kicks_most}"), "flare city texts: the Guide page's every figure is filled from the code (" + count + ")");
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LocaleFile(lang);
            string other = words.TryGetValue("reallife_guide_flare_city", out string o) ? o : string.Empty;
            bool sameFacts = Regex.Matches(page, @"\{[a-z_]+\}").Cast<Match>().Select(m => m.Value).OrderBy(x => x).SequenceEqual(Regex.Matches(other, @"\{[a-z_]+\}").Cast<Match>().Select(m => m.Value).OrderBy(x => x));
            Check(sameFacts && other.Split('\n').Length == page.Split('\n').Length, "flare city texts: the Guide page in " + lang + " has the same figures and lines as in English");
        }
        Check(WhatsNewHas(en, "Flare in the city (Living population)") && WhatsNewHas(LocaleFile("pt"), "Flare na cidade (População viva)") && WhatsNewHas(LocaleFile("zh-cn"), "Flare进入城市（鲜活人口）")
              && WhatsNewHas(LocaleFile("de"), "Flare in der Stadt") && WhatsNewHas(LocaleFile("fr"), "Flare dans la ville") && WhatsNewHas(LocaleFile("es"), "Flare en la ciudad")
              && WhatsNewHas(LocaleFile("ru"), "Flare в городе") && WhatsNewHas(LocaleFile("nl"), "Flare in de stad") && WhatsNewHas(LocaleFile("tr"), "Şehirde Flare")
              && WhatsNewHas(LocaleFile("pl"), "Flare w mieście") && WhatsNewHas(LocaleFile("ko"), "도시 속 Flare"),
            "flare city texts: What's new tells of Flare in the city in all eleven languages");
    }

    // ---------------------------------------------------------------- the sources, read as text

    private static void FlareResidentSources()
    {
        string scripts = ModScripts();
        string folder = System.IO.Path.Combine(scripts, "Social", "Flare", "City");
        string city = string.Concat(Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        Check(!Regex.IsMatch(city, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(|HarmonyLib|\[Harmony|FindObjects|GameObject\.Find\(|System\.Linq|UnityEngine"),
            "flare city sources: no Update, no Harmony, no search of the scene, no LINQ and no Unity in Flare in the city's own folder");
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Residents.cs"));
        Check(director.Contains("Settings.LivingPopulation && Settings.LivingFlare") && director.Contains("DaySpread.Shared.Add(") && director.Contains("WorkSlice.Of(FlareCityCatalogue.SliceMs")
              && director.Contains("FlareCityStore.Line(_city, _cityZip)") && director.Contains("ThreadPool.QueueUserWorkItem") && director.Contains("SharedCity.Register(")
              && director.Contains("CityFault(") && !Regex.IsMatch(director, @"\bvoid\s+(?:Update|LateUpdate)\s*\("),
            "flare city sources: the director reads both options, spreads the day over frames in 2 ms slices, reads its line on a worker and saves it on the save's, registers its part and stands down alone on a fault");
        string main = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.cs"));
        Check(main.Contains("CitySecond();") && main.Contains("CitySave();") && main.Contains("CityStop(keep,") && main.Contains("CityShareRegister();") && main.Contains("ForgetCity();"),
            "flare city sources: Flare's director starts, saves, stops and forgets the residents' side with its own");
        string links = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.FlareCityLinks.cs"));
        string wiring = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.cs"));
        Check(links.Contains("PopulationAttention.Register(Source, Attention)") && links.Contains("PopulationAttention.Unregister(Source)") && links.Contains("CityPopulation.DayLived += Lived")
              && links.Contains("CityPopulation.DayLived -= Lived") && links.Contains("!CityPopulation.Following") && wiring.Contains("FlareCityLinks.Connect") && wiring.Contains("FlareCityLinks.Disconnect"),
            "flare city sources: the residents' attention is one of the population's sources, its day heard, a guest following the host left out, all taken off again");
        string footfall = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Links.cs"));
        Check(footfall.Contains("FlareRules.Footfall(d.CityOn ? Math.Min(0, talk) : talk, clean)") && footfall.Contains("int buzz = CityOn ? 0 : FlareRules.BuzzOf(p);"),
            "flare city sources: while the residents' side runs, Flare's talk leaves business pressure's figure and is the population's attention instead; off, it is as before");
        string cases = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Cases.cs"));
        string cast = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Cast.cs"));
        Check(cases.Contains("CityReviewPost(p);") && cases.Contains("int buzz = city ? 0 : FlareLiveRules.ReviewBuzz(p.Arg, p.Reach);") && cases.Contains("if (CityTalk(c.Place, -1,")
              && cast.Contains("if (CityTalk(_visits[i].Place, 1,") && cast.Contains("bool heard = CityTalk(h.Place, -1,"),
            "flare city sources: reviews, announcements, inspections, influencers' visits and scandals are heard by the residents while their side runs");
        string settings = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "Settings.cs"));
        string tree = File.ReadAllText(System.IO.Path.Combine(scripts, "Meta", "OptionsTree.cs"));
        string mod = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs"));
        string cities = File.ReadAllText(System.IO.Path.Combine(scripts, "Meta", "FeatureCities.cs"));
        Check(settings.Contains("public const string LivingFlareId = \"living_flare\";") && settings.Contains("LivingFlare = Bool(modId, LivingFlareId, LivingFlare);")
              && tree.Contains("S(\"flarecity\").For(\"flare_city\").Need(\"social_media\").Opt(\"living_flare\", \"living_population\")") && mod.Contains("Flare.FlareCityOptions.AddFlareCity(o)")
              && cities.Contains("new Line(\"flare_city\", CityScope.NewYork,"),
            "flare city sources: its option is saved, in the options tree under Living population and Flare, on the options page with its tests, and says which city it stands on");
        string store = File.ReadAllText(System.IO.Path.Combine(folder, "FlareCityStore.cs"));
        Check(store.Contains("public const string Key = \"reallife:flare_city\"") && store.Contains("DistrictSaveZip") && store.Contains("zip.Finish(Head)"),
            "flare city sources: one line of the save, compressed in pieces on the save's workers as the population's");
        string game = File.ReadAllText(System.IO.Path.Combine(folder, "FlareCityGame.cs"));
        Check(game.Contains("BizBooks.Book(-cents / 100f, new TransactionInfo(AdKey)") && !Regex.IsMatch(city.Replace(game, string.Empty), @"BizBooks\.Book|GameManager\.ChangeMoney"),
            "flare city sources: an ad's money goes through the mod's one money call, and only from its game file");
    }
}
