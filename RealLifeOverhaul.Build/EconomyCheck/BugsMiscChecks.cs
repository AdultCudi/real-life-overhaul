using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;

// The Guide audit's smaller bugs (law, weather, jobs, presets, save tools, texts), each held by a rule or by the source it lives in:
// the police report reaching the insurers, the cold asking what the player wears, a preset's value shown as the page shows it,
// the save tool's report saying what each kind of business became, a hand-over's days in a Going Public session, who owns two
// options, the driving school's hours, the undo text's button, the Guide's three unused texts, the one rule for officers on the
// street, the cart's card and a shift given up with nothing done.
internal static partial class Program
{
    private static void BugsMisc()
    {
        MiscPoliceReport();
        MiscTransactionLabels();
        MiscWardrobe();
        MiscPresetValues();
        MiscSaveReport();
        MiscHandOver();
        MiscOwnership();
        MiscSchoolAndDriving();
        MiscTexts();
        MiscJobs();
    }

    private static Claim MiscClaim(int id, CoverKind kind, ClaimStage stage, int day, int loss, int excess)
    {
        return new Claim
        {
            Id = id, Place = "High%20Street~1", Name = "Corner Shop", Kind = (int)kind, Insurer = 0, Day = day, Loss = loss, Excess = excess,
            Payout = Math.Min(loss, CompanyCatalogue.CoverMost[(int)kind]) - excess, Stage = stage
        };
    }

    // The police report: the desk handed it to a bridge nobody set. Now it reaches the insurers.
    // A transaction is booked as a key and the game shows its category through the key + "_label" (CashFlow, ConstructionGame): a key with no label
    // shows as a raw code in BizMan's money pages. Every key a booking names in the Scripts has a label in every language.
    private static void MiscTransactionLabels()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts"));
        var keys = new SortedSet<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path), "\"(reallife_transaction_[a-z0-9_]+)\""))
            {
                if (!m.Groups[1].Value.EndsWith("_label"))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
        }
        string[] codes = { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" };
        foreach (string code in codes)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            var lacking = new List<string>();
            foreach (string key in keys)
            {
                string label;
                if (code != "en" && code != "pt" && code != "zh-cn" && !words.ContainsKey(key))
                {
                    continue;   // a language still behind on the key reads English for the key and its label
                }
                if (!words.TryGetValue(key + "_label", out label) || string.IsNullOrWhiteSpace(label))
                {
                    lacking.Add(key);
                }
            }
            Check(keys.Count > 400 && lacking.Count == 0, "transaction labels: every booked reallife_transaction key has a _label in " + code + (lacking.Count > 0 ? " (lacking " + lacking.Count + ", first " + lacking[0] + ")" : ""));
        }
    }

    private static void MiscPoliceReport()
    {
        var claims = new List<Claim>
        {
            MiscClaim(1, CoverKind.Theft, ClaimStage.Open, 10, 900, 200),
            MiscClaim(2, CoverKind.Fire, ClaimStage.Open, 12, 4000, 400),
            MiscClaim(3, CoverKind.Theft, ClaimStage.Paid, 12, 500, 100),
            MiscClaim(4, CoverKind.Theft, ClaimStage.Filed, 13, 1500, 300)
        };
        Claim newest = CoverRules.NewestTheft(claims, 14, 7);
        Check(newest != null && newest.Id == 4, "police report: it is about the newest break-in, so the newest theft claim still to be paid is the one that takes it");
        Check(CoverRules.NewestTheft(claims, 40, 7) == null, "police report: a claim older than the report's days is not touched");
        Check(CoverRules.Reported(newest) && newest.Excess == 0 && newest.Payout == 1500, "police report: with the number the theft claim pays in full, the excess too");
        Check(!CoverRules.Reported(newest), "police report: a second report changes nothing");
        Check(!CoverRules.Reported(claims[1]) && claims[1].Excess == 400, "police report: a fire claim has nothing to do with a break-in");
        Check(!CoverRules.Reported(claims[2]) && claims[2].Excess == 100, "police report: a theft claim already paid is not changed");
        Claim big = MiscClaim(5, CoverKind.Theft, ClaimStage.Open, 14, 50000, 300);
        CoverRules.Reported(big);
        Check(big.Payout == CompanyCatalogue.CoverMost[(int)CoverKind.Theft], "police report: the cover's most still holds");
        Check(!CoverRules.Reported(null), "police report: no claim, nothing changes");

        Check(ReportedRepair.Payout(1000f, 100f, 0.8f, false) == 720f && ReportedRepair.Payout(1000f, 100f, 0.8f, true) == 800f,
            "police report: a car's repair is paid without the excess once a report is in hand");
        Check(ReportedRepair.Payout(50f, 100f, 0.8f, false) == 0f && ReportedRepair.Payout(50f, 100f, 0.8f, true) == 40f, "police report: a small bill under the excess is paid with a report");
        Check(ReportedRepair.Fresh(20, 20, 7) && ReportedRepair.Fresh(20, 27, 7) && !ReportedRepair.Fresh(20, 28, 7) && !ReportedRepair.Fresh(0, 5, 7) && !ReportedRepair.Fresh(20, 19, 7),
            "police report: a car's report is good for the report's days and not before it was handed over");

        string all = string.Empty;
        foreach (string file in System.IO.Directory.GetFiles(DesignScripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            all += System.IO.File.ReadAllText(file);
        }
        Check(Occurs(all, "Law.Bridges.PoliceReport =") >= 2 && all.Contains("Law.Bridges.PoliceReport = Handed;"), "police report: the bridge the desk calls is set, and put back");
        string links = ModSource(@"Wiring\Wiring.ReportLinks.cs");
        Check(links.Contains("CompanyDirector.Instance?.PoliceReport(Law.Bridges.ReportDays)") && links.Contains("CarGarage.Reported("),
            "police report: a break-in's report goes to the company's insurer, a car's to the car's");
        Check(ModSource(@"Wiring\Wiring.cs").Contains("ReportLinks.Connect") && ModSource(@"Wiring\Wiring.cs").Contains("ReportLinks.Disconnect"), "police report: the pairing is connected and disconnected with the others");
        Check(ModSource(@"Transport\CarOwnership.cs").Contains("CarGarage.Waives(") && ModSource(@"Transport\CarOwnership.cs").Contains("CarGarage.Spend()"),
            "police report: the car's repair uses the report once");
        string garage = ModSource(@"Transport\CarGarage.cs");
        Check(garage.Contains("\"reallife:carreport\"") && ModSource(@"Meta\FeatureList.Life.cs").Contains("\"reallife:carreport\""), "police report: the car's report is saved under a key its feature owns");
    }

    // The cold's energy cost asks what the player wears; nobody had set the answer.
    private static void MiscWardrobe()
    {
        Check(RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("MaleSweater") && RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("FemHoodie") && RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("SuitJacket")
              && RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("HighNeckShirt") && RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("top_m_bomber_navy"),
            "wardrobe: the game's warm tops and the mod's bomber are warm");
        Check(!RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("MaleTShirt") && !RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("top_m_tee_white") && !RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop("dress_f_summer_dots")
              && !RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop(null) && !RealLifeOverhaul.Seasons.WarmthRules.IsWarmTop(string.Empty), "wardrobe: a tee, a summer dress and nothing are not warm");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        Check(links.Contains("Events.Weather.Wardrobe = PlayerWardrobe;") && links.Contains("Events.Weather.Wardrobe = null;") && links.Contains("CharacterCatalogue.WearingWarmTop()"),
            "wardrobe: the weather is told what the player wears, and the answer is taken back when the pairing is");
        Check(ModSource(@"Events\Weather.cs").Contains("Weather.Wardrobe.WearingCoat ? 0f : 2f"), "wardrobe: a coat is what the cold's cost reads");
        string coats = ModSource(@"Seasons\SeasonGame.Coats.cs");
        Check(coats.Contains("WarmthRules.WarmTops") && !coats.Contains("private static readonly string[] WarmTops"), "wardrobe: the street's people and the player are dressed warm by one list");
        Check(coats.Contains("LookIntact(look)") && coats.Contains("head.sharedMesh != null") && coats.Contains("look.data == null"),
            "wardrobe: an outfit is put back only on a look whose head part and mesh the city unload has not destroyed (no NullReferenceException in UpdateHeadBlendshapes)");
    }

    // A preset's value as the options page shows it.
    private static void MiscPresetValues()
    {
        Func<string, string> words = key => key == "reallife_opt_value_percent" ? "{value}%" : key == "reallife_opt_value" ? "{value}" : "<" + key + ">";
        string[] handling = { "reallife_car_handling_0", "reallife_car_handling_1", "reallife_car_handling_2", "reallife_car_handling_3" };
        Check(RealLifeOverhaul.Meta.OptionValueRules.Choice(handling, 2, words) == "<reallife_car_handling_2>", "preset values: a drop-down shows its choice's own text, not its index");
        Check(RealLifeOverhaul.Meta.OptionValueRules.Choice(handling, 9, words).Length == 0 && RealLifeOverhaul.Meta.OptionValueRules.Choice(null, 0, words).Length == 0,
            "preset values: a value the drop-down has no choice for gives nothing");
        Check(RealLifeOverhaul.Meta.OptionValueRules.Slider("reallife_opt_value_percent", 75, words) == "75%" && RealLifeOverhaul.Meta.OptionValueRules.Slider(null, 7, words) == "7",
            "preset values: a slider keeps its unit, a plain one its number");
        string show = MemberText(ModSource(@"Meta\Presets.cs"), "internal static string Show(");
        Check(show.Contains("OptionValueRules.Choice(info.ChoiceKeys") && show.Contains("OptionValueRules.Slider(info.ValueLabelKey"), "preset values: the presets' table and the Guide print them through the page's own rule");
        string table = ModSource(@"Meta\SettingsTable.cs");
        Check(table.Contains("info.ChoiceKeys = dropdown.ChoiceKeys;") && table.Contains("info.ValueLabelKey = slider.ValueLabelKey;"), "preset values: the page's choices and unit are taken with its label and range");
        Check(ModSource(@"Meta\OptionsLayout.Book.cs").Contains("OptionValueRules.Choice(dropdown.ChoiceKeys") && ModSource(@"Meta\OptionsLayout.Book.cs").Contains("OptionValueRules.Slider(slider.ValueLabelKey"),
            "preset values: the Settings app words a value by the same rule");
    }

    // Prepare for removing the mod: each kind of business says what it became.
    private static void MiscSaveReport()
    {
        string tools = ModSource(@"Meta\SaveTools.cs"), steps = ModSource(@"Meta\SaveTools.Steps.cs");
        Check(!steps.Contains("BusinessTypes") && !tools.Contains("removed.BusinessTypes"), "save report: the four kinds of business are no longer one count");
        Check(steps.Contains("removed.TechTypes += Tech.TechTypes.Strip(save)") && steps.Contains("removed.Academies += Careers.AcademyType.Strip(save)")
              && steps.Contains("removed.Branches += Economy.BranchType.Strip(save)") && steps.Contains("removed.ShopKinds += Shops.ShopsGame.Strip(save)"),
            "save report: each strip has its own count");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string[] keys = { "reallife_meta_report_business_types", "reallife_meta_report_types_academies", "reallife_meta_report_types_branches", "reallife_meta_report_types_shops" };
        string[] wording = { "web development agencies", "recruitment agencies", "gift shops", "fast food restaurants, coffee shops and liquor stores" };
        for (int i = 0; i < keys.Length; i++)
        {
            Check(en.TryGetValue(keys[i], out string text) && text.Contains(wording[i]) && text.Contains("{count}") && tools.Contains("\"" + keys[i] + "\""),
                "save report: " + keys[i] + " says what it became (" + wording[i] + ")");
        }
        Check(ModSource(@"Tech\Firms\TechTypes.cs").Contains("webdevelopmentagency") && ModSource(@"Careers\AcademyType.cs").Contains("recruitmentagency")
              && ModSource(@"Economy\OwnBank\Branch\BranchType.cs").Contains("giftshop"), "save report: the words match the types each strip really sets");
    }

    // A hand-over blocked in a Going Public session does not run its deadline.
    private static void MiscHandOver()
    {
        Check(ListingBook.HandOverDeadline(100, false) == 100 && ListingBook.HandOverDeadline(100, true) == 101, "hand-over: a day in a session moves the deadline a day on");
        int by = 107;
        for (int day = 100; day < 130; day++)
        {
            by = ListingBook.HandOverDeadline(by, true);
            Check(!ListingBook.HandOverLate(2, day, by, true), "hand-over: a business is never paid for in a session (day " + day + ")");
        }
        Check(!ListingBook.HandOverLate(2, 130, by, false), "hand-over: on leaving a long session the buyer still has the days they had");
        Check(ListingBook.HandOverLate(2, by + 1, by, false) && !ListingBook.HandOverLate(0, by + 1, by, false) && !ListingBook.HandOverLate(2, by, by, false),
            "hand-over: outside a session the deadline is the seventh day as before, and nothing pending is never late");
        string own = ModSource(@"Markets\MarketDirector.Own.cs");
        Check(own.Contains("ListingBook.HandOverDeadline(f.HandOverBy, session)") && own.Contains("ListingBook.HandOverLate(f.HandOver.Count, day, f.HandOverBy, session)"),
            "hand-over: the director asks the rule with the session");
    }

    // Two options a feature's page lists must be its own.
    private static void MiscOwnership()
    {
        string list = ModSource(@"Meta\FeatureList.cs"), life = ModSource(@"Meta\FeatureList.Life.cs");
        string character = list.Substring(list.IndexOf("Features.Add(\"character\"", StringComparison.Ordinal));
        character = character.Substring(0, character.IndexOf(".Objects(", StringComparison.Ordinal));
        Check(character.Contains("Settings.CharacterSkinId"), "ownership: the character feature owns the skin finish");
        string nav = life.Substring(life.IndexOf("Features.Add(\"navigation\"", StringComparison.Ordinal));
        nav = nav.Substring(0, nav.IndexOf(".Objects(", StringComparison.Ordinal));
        Check(nav.Contains("Settings.SpecialNamesId") && nav.Contains("Settings.MapPlacesShownId"), "ownership: navigation owns the special building names and the places on the map");
    }

    // The driving school's hours come from its constants; a suspended driver's second catch is a named rule.
    private static void MiscSchoolAndDriving()
    {
        string enforcement = ModSource(@"Licences\Driving\DrivingEnforcement.cs");
        Check(enforcement.Contains("LicenceState.CountCaught(day) >= LicenceLaw.SuspendedArrestFromCount") && !enforcement.Contains("CountCaught(day) >= 2"), "driving: the suspended driver's arrest count is a named rule");
        Check(ModSource(@"Licences\LicenceState.cs").Contains("SuspendedArrestFromCount = 2"), "driving: and it is the Guide's second time");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en["reallife_school_no_hours"].Contains("{from}") && en["reallife_school_no_hours"].Contains("{to}") && !en["reallife_school_no_hours"].Contains("7:00"), "school hours: the closed text takes the school's hours");
        string school = ModSource(@"Licences\Driving\DrivingSchool.cs"), desk = ModSource(@"Licences\SchoolDesk.cs");
        Check(school.Contains("Licences.T(reasonKey, \"from\", Hour(OpensAt), \"to\", Hour(ClosesAt))") && school.Contains("status = DrivingSchool.ReasonText(reason);")
              && desk.Contains("DrivingSchool.ReasonText(reason)") && desk.Contains("DrivingSchool.Hour(DrivingSchool.OpensAt)"), "school hours: every place that shows a reason fills them in from the constants");
    }

    // The undo text names the button; the Guide's three old texts are gone.
    private static void MiscTexts()
    {
        string[] languages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        foreach (string language in languages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, language + ".json"));
            Check(words["reallife_meta_preset_undo"].Contains(words["reallife_meta_import_button"]), "undo text (" + language + "): it names the button as the page does, not an old word for it");
            foreach (string key in new[] { "reallife_guide_window_title", "reallife_guide_search_hint", "reallife_guide_search" })
            {
                Check(!words.ContainsKey(key), "unused guide text (" + language + "): " + key + " is gone");
            }
        }
        foreach (string file in System.IO.Directory.GetFiles(DesignScripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file);
            Check(!code.Contains("\"reallife_guide_window_title\"") && !code.Contains("\"reallife_guide_search_hint\"") && !code.Contains("\"reallife_guide_search\""),
                "unused guide text: nothing in " + System.IO.Path.GetFileName(file) + " reads the old window and search texts");
        }
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(DesignLocales, @"..\..\..\.."));
        foreach (string doc in new[] { "INSTALL.md", @"docs\contributing\getting-started.md" })
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(root, doc));
            Check(!text.Contains("starts the mod twice"), "install notes: " + doc + " no longer says the game starts the mod twice (it loads neither)");
        }
    }

    // One rule for the officers on the street, the cart's card, a shift given up with nothing done.
    private static void MiscJobs()
    {
        Check(JobCatalogue.OfficersAbout(true, 5) && !JobCatalogue.OfficersAbout(false, 5) && !JobCatalogue.OfficersAbout(true, 0) && !JobCatalogue.OfficersAbout(false, 0),
            "street police: officers are about with the police on and some presence, and not otherwise");
        string busker = ModSource(@"Jobs\Kinds\BuskerJob.cs"), calling = ModSource(@"Jobs\Kinds\VendorJob.Calling.cs");
        Check(busker.Contains("JobCatalogue.OfficersAbout(Settings.Police, Settings.PolicePresence)") && calling.Contains("JobCatalogue.OfficersAbout(Settings.Police, Settings.PolicePresence)"),
            "street police: the busker's move-on and the cart's warning ask one rule");
        Check(!busker.Contains("Settings.Police &&") && !calling.Contains("Settings.PolicePresence > 0"), "street police: neither keeps a rule of its own");

        string figures = ModSource(@"Jobs\ShiftFigures.cs"), vendor = ModSource(@"Jobs\Kinds\VendorJob.cs");
        string first = MemberText(figures, "public static string FirstLine(");
        Check(first.Contains("GigWords.List(ctx.Offer)") && first.Contains("reallife_job_card_gig_offer") && vendor.Contains("ShiftFigures.FirstLine(Ctx)") && !vendor.Contains("\"reallife_job_card_gig\""),
            "cart card: the pavement cart's job card shows the day's offer like the other jobs'");

        // Giving up with nothing done is a failed shift, by the Guide ("most of the work undone"), for the kinds that can fail.
        foreach (string[] kind in new[] { new[] { "Cleaner", "CleanerJob.cs" }, new[] { "Flyer", "FlyerJob.cs" }, new[] { "Stocker", "StockerJob.cs" }, new[] { "Vendor", "VendorJob.cs" }, new[] { "Courier", "CourierJob.cs" } })
        {
            string code = ModSource(@"Jobs\Kinds\" + kind[1]);
            int at = code.IndexOf("QuitIsFailure =>", StringComparison.Ordinal);
            string line = at >= 0 ? code.Substring(at, code.IndexOf(';', at) - at) : string.Empty;
            Check(line.Length > 0 && !line.Contains("> 0 &&") && !line.Contains("Step.Round"), "give up: " + kind[0] + " with nothing done is a failed shift (" + line + ")");
        }
        Check(!ModSource(@"Jobs\Kinds\BuskerJob.cs").Contains("QuitIsFailure"), "give up: busking never fails");
    }
}
