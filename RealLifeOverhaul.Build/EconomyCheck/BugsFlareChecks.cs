using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Seasons;
using RealLifeOverhaul.Texts;

// Faults the Guide audit found in Flare, RLO Texts, the Calendar and the Marketplace. What a rule decides is checked by calling it;
// what only the game's side can do (a button, a notice, a wire) is checked in its source, as the neighbouring checks do.
internal static partial class Program
{
    private static void BugsFlareChecks()
    {
        BugsFlareHirePick();
        BugsFlareDealPosts();
        BugsFlareNotices();
        BugsFlareOpened();
        BugsFlareCap();
        BugsFlareMayor();
        BugsFlareUnusedStrings();
        BugsFlareForget();
        BugsFlareHours();
        BugsFlareCalendar();
        BugsFlareMarketplace();
        BugsFlareWords();
    }

    // A file of Scripts as text, comments and all; empty if it is not there.
    private static string BugsSource(string file)
    {
        string path = System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void BugsFlareHirePick()
    {
        string profile = BugsSource(@"Social\Flare\FlareApp.Profile.cs"), sheet = BugsSource(@"Social\Flare\FlareApp.Sheet.cs");
        Check(profile.Contains("OpenHire(FlareGame.Place(shops[0]), FlareGame.Name(shops[0]), id, shops.Count > 1)") && sheet.Contains("_hirePick")
              && sheet.Contains("\"reallife_flare_hire_business\"") && sheet.Contains("_hirePlace = place;"),
            "Flare: Hire an influencer on a profile offers the player's businesses to choose between, and does not hire for the first alone");
    }

    private static void BugsFlareDealPosts()
    {
        // One rule: the posts a deal is priced for, the posts its page says, and the posts the days make are the same number.
        var kinds = new[] { FlareDeal.Post, FlareDeal.Series, FlareDeal.Ambassador, FlareDeal.Plan, FlareDeal.Launch, FlareDeal.Giveaway };
        bool fee = true, days = true, lateOk = true;
        var f = FlareCastFile(2026);
        FlareInfluencer inf = f.Influencers[0];
        var counts = new List<string>();
        foreach (FlareDeal deal in kinds)
        {
            int kind = FlareCast.FirstDealKind + (int)deal, span = CompanyCatalogue.CampaignDays[kind], posts = FlareCast.PostsOf(deal);
            fee &= FlareCast.Fee(inf, deal) == (int)((long)FlareCast.Price(inf) * posts * (deal == FlareDeal.Ambassador ? 750 : 1000) / 1000);
            var h = new FlareHire { Kind = kind, From = 100, To = 100 + span - 1 };
            int made = 0, madeLate = 0, last = -1;
            for (int day = 90; day < 100 + span + 5; day++)
            {
                if (FlareCast.PostDay(h, day))
                {
                    made++;
                    last = day;
                }
            }
            h.Late = true;
            for (int day = 90; day < 100 + span + 5; day++)
            {
                madeLate += FlareCast.PostDay(h, day) ? 1 : 0;
            }
            counts.Add(deal + " " + made);
            days &= made == posts && last <= h.To && FlareCast.PostDay(h, 100 + span) == false;
            lateOk &= madeLate == posts || madeLate == posts - 1;   // a late one starts a day on, and a post past the last day is not made
        }
        Check(fee && days && lateOk, "Flare: a deal makes as many posts as it is priced for, inside its days (" + string.Join(", ", counts) + "), a late one at most one fewer");
        Check(FlareCast.PostsOf(FlareDeal.Post) == 1 && FlareCast.PostsOf(FlareDeal.Series) == 5 && FlareCast.PostsOf(FlareDeal.Launch) == 3
              && FlareCast.PostsOf(FlareDeal.Ambassador) == 2 * CompanyCatalogue.CampaignDays[FlareCast.FirstDealKind + (int)FlareDeal.Ambassador] / 7,
            "Flare: the deals' texts hold: one post, five in a week's series, two a week for an ambassador, three for a launch");

        // The day's round follows it: a week's series posts five times, not on every one of its seven days.
        FlareFile file = FlareCastFile(2027);
        FlareInfluencer who = file.Influencers[0];
        who.Quit = false;
        var shops = new List<FlareShop>();
        var hire = new FlareHire { Id = 910, Kind = FlareCast.FirstDealKind + (int)FlareDeal.Series, Place = "Gym~1", Name = "Iron Works", From = 50, To = 56 };
        hire.Who.Add(who.Id);
        file.Hires.Add(hire);
        var visits = new List<FlarePost>();
        for (int day = 50; day <= 56; day++)
        {
            FlareCast.Day(file, day, shops, null, null, null, visits);
        }
        int paid = file.Posts.FindAll(p => p.Who == who.Id && p.Source == FlareSource.Hired).Count;
        Check(paid == 5 && hire.Posted == 5 && hire.LastPost == 56, "Flare: a week's series makes its five paid posts over the week (" + paid + " made, " + hire.Posted + " counted)");
    }

    private static void BugsFlareNotices()
    {
        string camera = BugsSource(@"Social\Flare\FlareDirector.Camera.cs"), invites = BugsSource(@"Social\Flare\FlareDirector.Invites.cs"), director = BugsSource(@"Social\Flare\FlareDirector.cs");
        bool quiet = !camera.Contains(" Tell(") && invites.Contains("Notice(NotificationType.Info, \"reallife_flare_note_invite_booked\"")
                     && director.Contains("internal void Notice(") && director.Contains("if (Settings.SocialMediaNotices)");
        Check(quiet, "Flare: the camera's and the invitation's notices go through the Flare notifications option");
    }

    private static void BugsFlareOpened()
    {
        Check(FlareRules.OpenedOn(120, 400) == 120 && FlareRules.OpenedOn(-1, 400) == 400 && FlareRules.OpenedOn(0, 400) == 400,
            "Flare: a launch counts from the day the business opened, and from when Flare saw it only for a business with no opening day");
        Check(BugsSource(@"Social\Flare\FlareGame.cs").Contains("FlareRules.OpenedOn(FoundingGame.Of(all[i])"), "Flare: the shops Flare lists carry the day the business started (the game's own creation day, kept across a move by Core/FoundingGame)");
    }

    private static void BugsFlareCap()
    {
        Check(FlareRules.Footfall(6, 1) == 6 && FlareRules.Footfall(-6, 1) == -5 && FlareRules.Footfall(3, 1) == 4 && FlareRules.Footfall(0, 1) == 1 && FlareRules.Footfall(5, 1) == 6,
            "Flare: the cleaners' point counts inside the six per cent cap, with the talk (6 and 1 make 6, not 7)");
        Check(BugsSource(@"Social\Flare\FlareDirector.Links.cs").Contains("return FlareRules.Footfall("), "Flare: the business pressure's figure comes from that one rule");
    }

    private static void BugsFlareMayor()
    {
        Check(Math.Abs(OfficeRules.PostedApproval(50f, 500) - 51.5f) < 0.001f && Math.Abs(OfficeRules.PostedApproval(50f, 100) - 50.3f) < 0.001f && OfficeRules.PostedApproval(94f, 1500) == 95f,
            "civic: a post about the office lifts approval by up to a point and a half, never past 95");
        string count = BugsSource(@"Civic\CivicOffice.Count.cs");
        Check(count.Contains("State.Holds == OfficeRace.Mayor && Gov.MayorIsPlayer") && count.Contains("Gov.Approval = OfficeRules.PostedApproval(Gov.Approval, weight)"),
            "civic: posts help the approval of a mayor too, which is the city's, not the office file's");
    }

    // Every Flare text is read by something: its key in the code, or the start of a family of keys built there.
    private static void BugsFlareUnusedStrings()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string build = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "RealLifeOverhaul.Build"));
        var used = new HashSet<string>(StringComparer.Ordinal);
        var made = new List<string>();
        var token = new Regex("reallife_flare_[a-z0-9_]*", RegexOptions.Compiled);
        foreach (string root in new[] { scripts, build })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar) || file.Contains(System.IO.Path.DirectorySeparatorChar + "bin" + System.IO.Path.DirectorySeparatorChar))
                {
                    continue;
                }
                string text = File.ReadAllText(file);
                if (text.IndexOf("reallife_flare_", StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                foreach (Match m in token.Matches(text))
                {
                    used.Add(m.Value);
                    if (m.Value.EndsWith("_", StringComparison.Ordinal))
                    {
                        made.Add(m.Value);
                    }
                }
            }
        }
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        var unused = new List<string>();
        if (langs[0] != null)
        {
            foreach (string key in langs[0].Keys)
            {
                if (!key.StartsWith("reallife_flare_", StringComparison.Ordinal) || used.Contains(key))
                {
                    continue;
                }
                bool built = false;
                foreach (string start in made)
                {
                    built |= key.StartsWith(start, StringComparison.Ordinal);
                }
                if (!built)
                {
                    unused.Add(key);
                }
            }
        }
        Check(unused.Count == 0, "Flare: every Flare text is used by the code (" + unused.Count + " not: " + string.Join(", ", unused.GetRange(0, Math.Min(5, unused.Count))) + ")");
    }

    private static void BugsFlareForget()
    {
        Check(TextProviders.Forgets(TextProviders.Of(TextProvider.Ollama)) == TextForget.Nothing && TextProviders.Forgets(TextProviders.Of(TextProvider.LmStudio)) == TextForget.Nothing
              && TextProviders.Forgets(TextProviders.Of(TextProvider.Off)) == TextForget.Nothing,
            "RLO Texts: Forget the key lets go of nothing with Ollama, LM Studio or Off chosen (it signed the player out of ChatGPT)");
        Check(TextProviders.Forgets(TextProviders.Of(TextProvider.ChatGpt)) == TextForget.SignOut && TextProviders.Forgets(TextProviders.Of(TextProvider.OpenAi)) == TextForget.Key
              && TextProviders.Forgets(TextProviders.Of(TextProvider.Gemini)) == TextForget.Key,
            "RLO Texts: ChatGPT chosen signs out, a service with a key forgets its key");
    }

    private static void BugsFlareHours()
    {
        // A look can be several hours after the last when time runs fast: a pass is owed for each, so the gossip's exact hour and the 06:00 read happen.
        bool every = true, within = true;
        for (int hour = 0; hour < 24; hour++)
        {
            for (int jump = 1; jump <= 30; jump++)
            {
                int day = 10 + (hour + jump) / 24, now = (hour + jump) % 24;
                int steps = TextsCityRules.HourSteps(10, hour, day, now);
                var seen = new HashSet<int>();
                for (int back = steps - 1; back >= 0; back--)
                {
                    TextsCityRules.HourBack(day, now, back, out int d, out int h);
                    within &= h >= 0 && h < 24 && d >= 10 && d <= day;
                    seen.Add(d * 24 + h);
                }
                int last = Math.Min(jump, TextsCityRules.HoursMost);
                for (int k = 0; k < last; k++)
                {
                    every &= seen.Contains(10 * 24 + hour + jump - k);   // the hours just gone by, the newest first, a day's worth at most
                }
                every &= steps == last;
            }
        }
        Check(every && within, "RLO Texts: every game hour that went by between two looks gets its pass, a day's worth at most");
        Check(TextsCityRules.HourSteps(10, 5, 10, 8) == 3 && TextsCityRules.HourSteps(10, 22, 11, 7) == 9 && TextsCityRules.HourSteps(10, 8, 10, 8) == 1 && TextsCityRules.HourSteps(10, 8, 9, 20) == 1
              && TextsCityRules.HourSteps(1, 0, 40, 0) == TextsCityRules.HoursMost,
            "RLO Texts: three hours from 05:00 to 08:00 so 06:00 is among them, nine over midnight, one when the clock stands still or runs back");
        string wiring = BugsSource(@"Wiring\Wiring.TextsCity.cs"), city = BugsSource(@"Texts\TextsDirector.City.cs");
        Check(wiring.Contains("v.Hour == WorthHour") && !wiring.Contains("save.Hour == WorthHour") && city.Contains("TextsCityRules.HourSteps(") && city.Contains("Views.Life.Hour = hour;"),
            "RLO Texts: the net worth is read for the hour of the pass, not the hour the game is at");
    }

    private static void BugsFlareCalendar()
    {
        Check(SeasonRules.StandsDown(true, false) && !SeasonRules.StandsDown(true, true) && !SeasonRules.StandsDown(false, false) && !SeasonRules.StandsDown(false, true),
            "seasons: they stand down only with Alcware Seasons loaded and not kept beside it");
        Check(BugsSource(@"Wiring\Wiring.CalendarSeasons.cs").Contains("!SeasonDirector.StandingDown") && BugsSource(@"Seasons\SeasonDirector.cs").Contains("!Settings.Seasons || StandingDown"),
            "seasons: the Calendar lists no season and says none on a day's page while they stand down");
        Check(StreetLifeRules.Sites(2, 1f) == 2 && StreetLifeRules.Sites(3, 1f) == 3 && StreetLifeRules.Sites(3, 0.6f) == 2 && StreetLifeRules.Sites(1, 0.6f) == 1 && StreetLifeRules.Sites(2, 0.6f) == 1
              && StreetLifeRules.Sites(3, 0f) == 0 && StreetLifeRules.Sites(0, 1f) == 0 && StreetLifeRules.Sites(1, 0.7f * 0.9f * 0.8f) == 1,
            "street life: rain thins the sites and never empties the street, a storm empties it");
        Check(BugsSource(@"CityLife\CityLifeDirector.cs").Contains("StreetLifeRules.Sites(") && BugsSource(@"CityLife\CityLifeDirector.cs").Contains("Events.Weather.StreetLifeFactor"),
            "street life: the weather's street life factor sets how many sites the city has out");
    }

    private static void BugsFlareMarketplace()
    {
        HustleState s = new HustleState { Seed = 5 };
        Gig Make(int id, GigStage stage) => new Gig { Id = id, Kind = GigKind.Handyman, Client = "C", Street = "S", Number = 1, Pay = 100, From = 200, Until = 204, Stage = stage };
        Gig late = Make(1, GigStage.Offered);
        bool refused = !HustleRules.Book(late, 204) && late.Stage == GigStage.Offered && !HustleRules.SendHelper(s, late, 204) && late.Stage == GigStage.Offered && late.HelperDone == 0;
        Gig booked = Make(2, GigStage.Booked);
        refused &= !HustleRules.SendHelper(s, booked, 230) && booked.Stage == GigStage.Booked;
        Check(refused, "hustles: a request whose wait is over can be neither booked nor sent a helper");
        Gig open = Make(3, GigStage.Offered), open2 = Make(4, GigStage.Offered);
        Check(HustleRules.Book(open, 203) && open.Stage == GigStage.Booked && HustleRules.SendHelper(s, open2, 150) && open2.Stage == GigStage.Helper && open2.HelperDone == 200 + HustleRules.Hours(HustleCatalogue.Gig(GigKind.Handyman)),
            "hustles: in the wait, a request is booked or sent a helper as before");
        string actions = BugsSource(@"Hustles\HustleDirector.Actions.cs");
        Check(actions.Contains("if (!HustleRules.Book(g, HustleGame.Stamp))") && actions.Contains("if (!HustleRules.SendHelper(_state, g, HustleGame.Stamp))"),
            "hustles: the Marketplace asks the rules, and tells the player when the time has passed");
    }

    private static void BugsFlareWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("bugs flare", langs, codes))
        {
            return;
        }
        string[] keys = { "reallife_flare_hire_business", "reallife_hustle_note_passed", "reallife_flare_deal_about_3", "reallife_flare_deal_about_5", "reallife_flare_hire_posts_from", "reallife_texts_test_language" };
        var missing = new List<string>();
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(new[] { langs[0], langs[1], langs[2] }, new[] { codes[0], codes[1], codes[2] }, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "bugs flare: the new and changed texts are in en, pt and zh-cn with the same {names} (" + string.Join(", ", missing) + ")");
        Check(!langs[0]["reallife_texts_test_language"].Contains("for now"), "RLO Texts: the older pack's notice does not call its English only a matter of 'for now'");
        Check(langs[0]["reallife_hustle_note_passed"].Length <= 240 && langs[0]["reallife_texts_test_language"].Length <= 240, "bugs flare: the new notices are at most 240 characters");
        bool guide = true;
        for (int l = 0; l < 3; l++)
        {
            string g = langs[l]["reallife_guide_flare"];
            guide &= g.Contains("{flare_series_posts}") && g.Contains("{flare_ambassador_posts}") && g.Contains("{flare_plan_posts}") && g.Contains("{flare_launch_posts}") && g.Contains("{flare_giveaway_posts}");
        }
        Check(guide, "bugs flare: the Guide's Flare page says how many posts each deal makes, from the code's own numbers");
    }
}
