using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Schemes;
using RealLifeOverhaul.Ui;

// Schemes and inquiries: each page built and every answer's press, each press reaching its call, the quiet buttons where
// the honest action stands, the sources (the game touched only through SchemeGame, money only through its booking,
// nothing on a frame, no roll but the seeded one, every refusal before any money, every rule the checks drive called by
// the mod), the texts in every language, and what a day costs.
internal static partial class Program
{
    private static readonly string[] SchemeFiles =
    {
        "SchemeBridges.cs", "SchemeCatalogue.cs", "SchemeDirector.cs", "SchemeDirector.Acts.cs", "SchemeDirector.Day.cs", "SchemeDirector.Pages.cs", "SchemeDirector.People.cs",
        "SchemeDirector.Tests.cs", "SchemeGame.cs", "SchemeGuide.cs", "SchemeKeys.cs", "SchemePages.cs", "SchemePanel.cs", "SchemeRules.cs", "SchemeRules.Day.cs", "SchemeRules.Money.cs",
        "SchemeRules.People.cs", "SchemeRules.Rivals.cs", "SchemeState.cs", "SchemeStore.cs", "SchemeText.cs"
    };

    private static readonly string[] SchemeLocales = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static string SchemeSource(string name) => ModSource(@"Schemes\" + name);

    /// <summary>The checks' own words: a text is its key and what fills it, so a check reads which text a page used and with what.</summary>
    private static SchemeWords SchemeKeyWords()
    {
        return new SchemeWords { Fill = (key, pairs) => key + (pairs != null && pairs.Length > 0 ? "(" + string.Join(",", pairs) + ")" : string.Empty), Money = n => "$" + n };
    }

    /// <summary>The English texts filled as the game fills them: a placeholder nobody filled stays in sight.</summary>
    private static SchemeWords SchemeRealWords(Dictionary<string, string> words)
    {
        return new SchemeWords
        {
            Fill = (key, pairs) =>
            {
                string text = words != null && words.TryGetValue(key, out string value) ? value : "{missing " + key + "}";
                for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
                {
                    text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
                }
                return text;
            },
            Money = n => "$" + n.ToString("N0", CultureInfo.InvariantCulture)
        };
    }

    // Everything a page shows, in one string: for a placeholder nobody filled, or a text that is not there.
    private static string SchemePageText(SchemePage p)
    {
        var all = new List<string> { p.Title, p.Kicker, p.Body };
        for (int i = 0; i < p.Count; i++)
        {
            all.Add(p.Choices[i]);
            all.Add(p.Hints[i]);
            for (int l = 0; p.Lines[i] != null && l < p.Lines[i].Count; l++)
            {
                all.Add(p.Lines[i][l].Text);
            }
        }
        return string.Join("\n", all);
    }

    private static bool SchemePressIs(SchemePage p, int at, SchemeDo what, string place, int a, int b)
    {
        return at < p.Count && p.Presses[at].Do == what && p.Presses[at].Place == place && p.Presses[at].A == a && p.Presses[at].B == b;
    }

    private static int SchemeTone(SchemePage p, int at, int line)
    {
        return at < p.Count && p.Lines[at] != null && line < p.Lines[at].Count ? p.Lines[at][line].Tone : 99;
    }

    private static string SchemeLine(SchemePage p, int at, int line)
    {
        return at < p.Count && p.Lines[at] != null && line < p.Lines[at].Count ? p.Lines[at][line].Text : string.Empty;
    }

    private static void SchemePageChecks()
    {
        SchemeWords w = SchemeKeyWords();
        string quarter = SchemeWords.Percent(SchemeCatalogue.SurchargePermille);

        SchemePage claim = SchemePages.Claim(w, SchemePlace, "Shop", 1, 8000);
        Check(claim.Count == 3 && claim.Title == "reallife_scheme_claim_title" && claim.Kicker == "Shop" && claim.Body == "reallife_scheme_claim_body(business,Shop,share," + quarter + ")"
              && SchemePressIs(claim, 0, SchemeDo.Claim, SchemePlace, 1, SchemeCatalogue.Small) && SchemePressIs(claim, 1, SchemeDo.Claim, SchemePlace, 1, SchemeCatalogue.Large)
              && SchemePressIs(claim, 2, SchemeDo.Leave, null, 0, 0) && claim.Hints[0] == "reallife_scheme_claim_hint(amount,$2000)" && claim.Hints[1] == "reallife_scheme_claim_hint(amount,$8000)",
            "schemes pages: the claim's page names the business and the surcharge, and offers a small one, a large one on that cover, and the way out");
        Check(SchemeLine(claim, 0, 0) == "reallife_scheme_claim_gain(amount,$2000)" && SchemeTone(claim, 0, 0) == SchemePages.Gain
              && SchemeLine(claim, 0, 1) == "reallife_scheme_risk_small(chance," + SchemeRules.FirstChance(SchemeKind.Claim, SchemeCatalogue.Small, 0, true) + ")" && SchemeTone(claim, 0, 1) == 0
              && SchemeLine(claim, 1, 1) == "reallife_scheme_risk_large" && SchemeTone(claim, 1, 1) == SchemePages.Risk && claim.Lines[2] == null,
            "schemes pages: each size says what it brings and what it risks before the player commits: the odds of a first small one by the rules' own sum, and that a large one is"
            + " always looked into");

        SchemePage expense = SchemePages.Expense(w, 3000, 20);
        Check(expense.Count == 3 && SchemePressIs(expense, 0, SchemeDo.Expense, null, 0, SchemeCatalogue.Small) && SchemePressIs(expense, 1, SchemeDo.Expense, null, 0, SchemeCatalogue.Large)
              && expense.Body == "reallife_scheme_expense_body(percent,20,share," + quarter + ")" && expense.Hints[0] == "reallife_scheme_expense_hint(amount,$15000)"
              && SchemeLine(expense, 0, 0) == "reallife_scheme_expense_gain(amount,$3000)" && SchemeLine(expense, 1, 0) == "reallife_scheme_expense_gain(amount,$12000)"
              && SchemeLine(expense, 0, 1).StartsWith("reallife_scheme_risk_small(chance," + SchemeRules.FirstChance(SchemeKind.Expense, SchemeCatalogue.Small, 0, true), StringComparison.Ordinal),
            "schemes pages: the expense's page says the game's tax rate, the expense each size invents and the tax it saves, by the rules that then pay it");

        SchemePage books = SchemePages.Books(w, SchemePlace, "Shop", 100, 200, 4321);
        Check(books.Count == 3 && SchemePressIs(books, 0, SchemeDo.OpenLift, SchemePlace, 0, 0) && SchemePressIs(books, 1, SchemeDo.OpenWash, SchemePlace, 0, 0)
              && SchemePressIs(books, 2, SchemeDo.Leave, null, 0, 0) && books.Body == "reallife_scheme_books_body(business,Shop,cash,$4321)"
              && books.Hints[0] == "reallife_scheme_books_lift_hint(share,10%)" && books.Hints[1] == "reallife_scheme_books_wash_hint(share,20%)",
            "schemes pages: the second set of books opens the sales that never happened or the till, each with how it stands now, and says the cash in hand");

        SchemePage lift = SchemePages.Lift(w, SchemePlace, "Shop", 0), lifted = SchemePages.Lift(w, SchemePlace, "Shop", 300);
        int littleOdds = SchemeRules.FirstChance(SchemeKind.Books, SchemeCatalogue.LiftSteps[1] * 1000 / SchemeCatalogue.LiftMost, 0, true);
        Check(lift.Count == 3 && SchemePressIs(lift, 0, SchemeDo.Lift, SchemePlace, SchemeCatalogue.LiftSteps[1], 0) && SchemePressIs(lift, 1, SchemeDo.Lift, SchemePlace, SchemeCatalogue.LiftMost, 0)
              && SchemePressIs(lift, 2, SchemeDo.Leave, null, 0, 0) && SchemePressIs(lifted, 2, SchemeDo.Lift, SchemePlace, 0, 0) && lifted.Choices[2] == "reallife_scheme_lift_plain"
              && SchemeLine(lift, 0, 0) == "reallife_scheme_lift_risk_little(chance," + littleOdds + ")" && SchemeTone(lift, 1, 0) == SchemePages.Risk
              && lifted.Body == "reallife_scheme_lift_body(business,Shop,now,30%,share," + quarter + ")",
            "schemes pages: the dressed books offer a little and a lot with the odds of each, and plain books again once they are dressed");

        SchemePage wash = SchemePages.Wash(w, SchemePlace, "Shop", true, 3000, 0, 9000, false, 1000, 20);
        SchemePage washing = SchemePages.Wash(w, SchemePlace, "Shop", true, 3000, 50, 9000, true, 2000, 20);
        SchemePage noTill = SchemePages.Wash(w, SchemePlace, "Shop", false, 3000, 0, 9000, false, 1000, 20), noTillOn = SchemePages.Wash(w, SchemePlace, "Shop", false, 3000, 50, 9000,
            false, 1000, 20);
        Check(wash.Count == 3 && SchemePressIs(wash, 0, SchemeDo.Wash, SchemePlace, SchemeCatalogue.WashSteps[1], 0)
            && SchemePressIs(wash, 1, SchemeDo.Wash, SchemePlace, SchemeCatalogue.WashSteps[2], 0)
              && SchemePressIs(wash, 2, SchemeDo.Leave, null, 0, 0) && wash.Hints[0] == "reallife_scheme_wash_hint(amount,$150)" && wash.Hints[1] == "reallife_scheme_wash_hint(amount,$600)"
              && wash.Body == "reallife_scheme_wash_body(business,Shop,cash,$9000,cut,15%,report,$" + SchemeCatalogue.ReportFrom + ",now,0%,percent,20)" && SchemeTone(wash, 1, 0) == SchemePages.Risk,
            "schemes pages: the till's page says the cash in hand, the cut, the game's tax and the bank's line, and offers its two rates with what each puts through a day");
        Check(SchemePressIs(washing, 2, SchemeDo.Wash, SchemePlace, 0, 0) && washing.Choices[2] == "reallife_scheme_wash_stop" && washing.Body.Contains("report,$" + 2 * SchemeCatalogue.ReportFrom)
              && washing.Body.EndsWith("\n\nreallife_scheme_wash_watched", StringComparison.Ordinal) && noTill.Count == 1 && SchemePressIs(noTill, 0, SchemeDo.Leave, null, 0, 0)
              && noTill.Body.StartsWith("reallife_scheme_wash_body_no_till(", StringComparison.Ordinal) && noTillOn.Count == 1 && SchemePressIs(noTillOn, 0, SchemeDo.Wash, SchemePlace, 0, 0),
            "schemes pages: a till that washes can be stopped, a watched one says so, the bank's line is at today's prices, and a business with no counter is offered no rate");

        SchemePage club = SchemePages.Club(w, SchemePlace, "Shop", true, 1000), standDown = SchemePages.Club(w, SchemePlace, "Shop", false, 1000);
        int clubOdds = SchemeRules.FirstChance(SchemeKind.Club, SchemeCatalogue.Small, SchemeCatalogue.ClubSmall / SchemeCatalogue.ClubKnowsEvery, false);
        Check(club.Count == 3 && SchemePressIs(club, 0, SchemeDo.Club, SchemePlace, 0, SchemeCatalogue.Small) && SchemePressIs(club, 1, SchemeDo.Club, SchemePlace, 0, SchemeCatalogue.Large)
              && club.Body == "reallife_scheme_club_body(business,Shop,stake,$" + SchemeCatalogue.ClubStakeMost + ",share," + quarter + ")"
              && club.Hints[0] == "reallife_scheme_club_hint(count," + SchemeCatalogue.ClubSmall + ")" && SchemeLine(club, 1, 0) == "reallife_scheme_club_gain(amount,$"
              + SchemeCatalogue.ClubLarge * SchemeCatalogue.ClubStakeMost + ")" && SchemeLine(club, 0, 1) == "reallife_scheme_risk_small(chance," + clubOdds + ")",
            "schemes pages: the club's page says the stake, how many households each size asks and the most it can take, with its odds");
        Check(standDown.Count == 1 && SchemePressIs(standDown, 0, SchemeDo.Leave, null, 0, 0) && standDown.Body == "reallife_scheme_note_club_no_people",
            "schemes pages: where the city's people are not this machine's own, the club's page says so and offers nothing: nothing can be pressed that would move money");

        var fund = new SchemeFund { Tycoon = 1, State = FundState.Open, Day = 500, Due = 507, Amount = 12000 };
        SchemePage offer = SchemePages.Fund(w, "Rival", "Holding", fund, 1000);
        fund.Checked = true;
        fund.Sham = true;
        SchemePage looked = SchemePages.Fund(w, "Rival", "Holding", fund, 1000);
        Check(offer.Count == 3 && SchemePressIs(offer, 0, SchemeDo.FundPut, null, 0, 0) && SchemePressIs(offer, 1, SchemeDo.FundLook, null, 0, 0)
            && SchemePressIs(offer, 2, SchemeDo.FundDown, null, 0, 0)
              && offer.Body == "reallife_scheme_fund_body(rival,Rival,holding,Holding,amount,$12000,back,$12960,days," + SchemeCatalogue.FundDays + ",day,507)"
              && SchemeLine(offer, 0, 0) == "reallife_scheme_fund_put_cost(amount,$12000)" && SchemeTone(offer, 0, 0) == UiConsequence.Loss && SchemeTone(offer, 0, 1) == 0
              && SchemeLine(offer, 1, 0) == "reallife_scheme_fund_look_cost(amount,$360)" && SchemeTone(offer, 1, 0) == UiConsequence.Loss,
            "schemes pages: a rival's fund says what goes in, what comes out and when, and offers to put the money in, to have it looked at for its fee, or to turn it down");
        Check(looked.Count == 2 && SchemePressIs(looked, 0, SchemeDo.FundPut, null, 0, 0) && SchemePressIs(looked, 1, SchemeDo.FundDown, null, 0, 0)
              && looked.Body.EndsWith("\n\nreallife_scheme_fund_look_sham(holding,Holding)", StringComparison.Ordinal) && SchemeTone(looked, 0, 1) == SchemePages.Risk,
            "schemes pages: a fund looked at says what it is on its page, is not looked at twice, and a sham's promised return is shown as the risk it is");

        var crowded = new SchemePage();
        for (int i = 0; i < 6; i++)
        {
            crowded.Add("c" + i, "h", SchemePress.Of(SchemeDo.Leave)).Line("l" + i, 0);
        }
        Check(crowded.Count == SchemePage.MostChoices && crowded.Choices[2] == "c2" && crowded.Lines[2].Count == 4 && new SchemePage().Line("x", 0).Count == 0,
            "schemes pages: a page holds three answers at most, and a line with no answer to stand under is not kept");

        // The real English words: every placeholder of every page is filled.
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        SchemeWords real = SchemeRealWords(english);
        fund.Sham = false;
        var pages = new List<SchemePage>
        {
            SchemePages.Claim(real, SchemePlace, "Harbour Bakery", 0, 20000), SchemePages.Expense(real, 3000, 20), SchemePages.Books(real, SchemePlace, "Harbour Bakery", 100, 50, 4321),
            SchemePages.Lift(real, SchemePlace, "Harbour Bakery", 100), SchemePages.Wash(real, SchemePlace, "Harbour Bakery", true, 3000, 50, 9000, true, 1000, 20),
            SchemePages.Wash(real, SchemePlace, "Harbour Bakery", false, 3000, 0, 9000, false, 1000, 20), SchemePages.Club(real, SchemePlace, "Harbour Bakery", true, 1000),
            SchemePages.Club(real, SchemePlace, "Harbour Bakery", false, 1000), SchemePages.Fund(real, "Bald Luthor", "Luthor Everything Co.", fund, 1000),
            SchemePages.Fund(real, "A", "B", new SchemeFund { Tycoon = 0, State = FundState.Open, Due = 9, Amount = 2000 }, 1000)
        };
        bool filled = english != null;
        string unfilled = string.Empty;
        foreach (SchemePage p in pages)
        {
            string text = SchemePageText(p);
            if (text.Contains("{") || text.Contains("}") || p.Title.Length == 0 || p.Body.Length < 40)
            {
                filled = false;
                unfilled = p.Title + ": " + (text.Contains("{") ? text.Substring(text.IndexOf('{'), Math.Min(40, text.Length - text.IndexOf('{'))) : "too short");
            }
        }
        Check(filled, "schemes pages: with the English words every page has its title and its body and no placeholder is left unfilled (" + unfilled + ")");
        Check(Array.IndexOf(UiSamples.SceneStates, "scheme_offer") >= 0 && Array.IndexOf(UiSamples.SceneStates, "scheme_fund") >= 0,
            "schemes pages: a scheme's page and a rival's fund are among the kit's sample scenes, so the screens' own checks lay them out");

        // Each press reaches its call.
        string press = SchemeSource("SchemeDirector.Pages.cs");
        var wanted = new Dictionary<SchemeDo, string>
        {
            { SchemeDo.Claim, "DoClaim(p.Place, p.A, p.B);" }, { SchemeDo.Expense, "DoExpense(p.B);" }, { SchemeDo.OpenLift, "ShowLift(p.Place);" }, { SchemeDo.OpenWash, "ShowWash(p.Place);" },
            { SchemeDo.Lift, "SetLift(p.Place, p.A);" }, { SchemeDo.Wash, "SetWash(p.Place, p.A);" }, { SchemeDo.Club, "DoClub(p.Place, p.B);" }, { SchemeDo.FundPut, "Answer(FundPut());" },
            { SchemeDo.FundLook, "Answer(FundLook());" }, { SchemeDo.FundDown, "Answer(FundTurnDown());" }
        };
        foreach (SchemeDo what in Enum.GetValues(typeof(SchemeDo)))
        {
            if (what == SchemeDo.Leave)
            {
                Check(!press.Contains("case SchemeDo.Leave:"), "schemes pages: leaving a page does nothing");
                continue;
            }
            Check(wanted.ContainsKey(what) && Regex.IsMatch(press, @"case SchemeDo\." + what + @":\s*" + Regex.Escape(wanted[what])),
                "schemes pages: the answer " + what + " reaches its call in the director");
        }
        string panel = SchemeSource("SchemePanel.cs");
        Check(panel.Contains("_press?.Invoke(press);") && panel.Contains("list.Locked = false;") && panel.Contains("Time.unscaledTime - _shownAt < InputDelay")
              && SchemeSource("SchemeDirector.cs").Contains("_panel = new SchemePanel(Press, "),
            "schemes pages: the panel hands an answer's press to the director, is never locked (the player may always walk away), and ignores a double press");

        // The quiet buttons where the honest action stands.
        string acts = SchemeSource("SchemeDirector.Acts.cs"), company = ModSource(@"Economy\Company\CompanyGame.cs");
        Check(acts.Contains("MatterClaim = 0, MatterExpense = 1, MatterBooks = 2, MatterClub = 3") && company.Contains("QuietPolicy = 0, QuietReturn = 1, QuietBooks = 2, QuietCampaign = 3"),
            "schemes pages: the company's four places for a quiet button are the director's four matters, number for number");
        foreach (string[] pair in new[]
                 {
                     new[] { "MatterClaim", "d.ShowClaim(place, cover);", "reallife_scheme_quiet_claim" }, new[] { "MatterExpense", "d.ShowExpense();", "reallife_scheme_quiet_expense" },
                     new[] { "MatterBooks", "d.ShowBooks(place);", "reallife_scheme_quiet_books" }, new[] { "MatterClub", "d.ShowClub(place);", "reallife_scheme_quiet_club" }
                 })
        {
            Check(Regex.IsMatch(acts, @"case " + pair[0] + @":\s*" + Regex.Escape(pair[1])) && Regex.IsMatch(acts, @"case " + pair[0] + @":\s*return [^;]*""" + pair[2] + @""" : null;"),
                "schemes pages: the quiet button of " + pair[0] + " has its words where there is something to act on, and opens its page");
        }
        Check(Regex.Matches(acts, @"if \(!On \|\| d == null").Count == 2, "schemes pages: with the option off no quiet button is shown and none opens a page");
        foreach (string[] place in new[]
                 {
                     new[] { @"Economy\Company\CompanyApp.Cover.cs", "QuietPolicy" }, new[] { @"Economy\Company\CompanyApp.Tax.cs", "QuietReturn" },
                         new[] { @"Economy\Company\CompanyApp.Tax.cs", "QuietBooks" },
                     new[] { @"Economy\Company\CompanyApp.Marketing.cs", "QuietCampaign" }, new[] { @"Ui\Company\CompanyLive.Money.cs", "QuietPolicy" },
                         new[] { @"Ui\Company\CompanyLive.Tax.cs", "QuietReturn" },
                     new[] { @"Ui\Company\CompanyLive.Tax.cs", "QuietBooks" }, new[] { @"Ui\Company\CompanyLive.Money.cs", "QuietCampaign" }
                 })
        {
            Check(ModSource(place[0]).Contains("CompanyBridges." + place[1]), "schemes pages: " + place[0] + " offers the quiet button of " + place[1]);
        }
        Check(ModSource(@"Ui\Company\CompanyLive.Do.cs").Contains("public void Quiet(string place, int matter, int figure) => CompanyBridges.QuietPress(matter, place, figure);")
              && ModSource(@"Ui\Company\CompanyLiveDoes.cs").Contains("open.Quiet(a.S, a.A, a.B);") && company.Contains("Quiet?.Invoke(matter, place, figure);"),
            "schemes pages: the kit's quiet button runs the company's press, which runs whoever put a scheme there");
        string dialog = ModSource(@"Social\PersonDialog.cs");
        Check(dialog.Contains("SocialPhone.QuietLabel(_p)") && dialog.Contains("SocialPhone.QuietAsk(_p)"), "schemes pages: a call's menu offers the quiet word where there is one to be had");
        string file = ModSource(@"Justice\LegalFile.cs");
        Check(file.Contains("Justice.Bridges.FileLines") && file.Contains("Justice.Bridges.FileActs"), "schemes pages: the Legal file lists what hangs over the player and what can be done about it");
    }

    // ------------------------------------------------------------------------------------------------- the sources

    // A method's text, from its name to the next method's summary or the end.
    private static string SchemeMethod(string code, string signature)
    {
        int at = code.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int next = code.IndexOf("\n        }\n", at, StringComparison.Ordinal), nextCr = code.IndexOf("\r\n        }\r\n", at, StringComparison.Ordinal);
        int end = next < 0 ? nextCr : nextCr < 0 ? next : Math.Min(next, nextCr);
        return end < 0 ? code.Substring(at) : code.Substring(at, end - at);
    }

    // True when the pieces stand in a text in this order.
    private static bool SchemeInOrder(string text, params string[] pieces)
    {
        int at = 0;
        foreach (string piece in pieces)
        {
            int found = text.IndexOf(piece, at, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }
            at = found + piece.Length;
        }
        return true;
    }

    private static void SchemeSourceChecks()
    {
        string all = string.Empty;
        foreach (string name in SchemeFiles)
        {
            string code = SchemeSource(name);
            Check(code.Length > 0, "schemes sources: " + name + " is there");
            all += "\n" + code;
        }
        string[] free = { "SchemeBridges.cs", "SchemeCatalogue.cs", "SchemeKeys.cs", "SchemePages.cs", "SchemeRules.cs", "SchemeRules.Day.cs", "SchemeRules.Money.cs",
            "SchemeRules.People.cs", "SchemeRules.Rivals.cs",
            "SchemeState.cs", "SchemeStore.cs" };
        foreach (string name in free)
        {
            Check(!Regex.IsMatch(SchemeSource(name), @"using UnityEngine|SaveGameManager|Notifications\.|MonoBehaviour|GoingPublic|BizBooks|TimeHelper|\.Localize\(|SchemeGame\.|SchemeDirector"),
                "schemes sources: " + name + " touches neither the game nor Unity nor the director: the checks compile and drive it as it ships");
        }
        string links = ModSource(@"Wiring\Wiring.SchemeLinks.cs"), game = SchemeSource("SchemeGame.cs");
        Check(Regex.Matches(all, @"BizBooks\.Book\(").Count == 3 && Regex.Matches(game, @"BizBooks\.Book\(").Count == 3 && !links.Contains("BizBooks.Book(") && !links.Contains("GameBooks."),
            "schemes sources: money moves only through the mod's one booking call, in SchemeGame's three doors, and never into a business's own figures");
        Check(!Regex.IsMatch(all + links, @"void (Update|LateUpdate|FixedUpdate|OnGUI)\(") && Regex.Matches(all, @"StartCoroutine\(").Count == 1
              && SchemeSource("SchemeDirector.cs").Contains("new WaitForSecondsRealtime(1f)") && SchemeSource("SchemeDirector.cs").Contains("if (day == _day)"),
            "schemes sources: nothing runs on a frame: one timer a second on the real clock, comparing the day");
        Check(!Regex.IsMatch(all + links, @"UnityEngine\.Random|System\.Random|new Random\(|DateTime\.(Now|UtcNow)|Guid\.|\.GetHashCode\(\)|Environment\.TickCount"),
            "schemes sources: no roll but the seeded one, and nothing from the clock on the wall");
        Check(SchemeSource("SchemeDirector.cs").Contains("SchemeStore.Load(save.modData, SchemeRules.SeedOf(SchemeGame.SeedText), out int version)")
              && game.Contains("!string.IsNullOrEmpty(Save.seed) ? Save.seed : Save.characterId"),
            "schemes sources: a save's seed is the game's own for the playthrough, so no reload draws anything afresh");
        string cases = ModSource(@"Justice\SchemeCases.cs");
        Check(cases.Contains("opened.Seed = JusticeLaw.Hash(\"scheme\") ^ seed;") && cases.Contains("Justice.Summon(new JusticeReport(charge)")
              && SchemeSource("SchemeDirector.Day.cs").Contains("SchemeBridges.Charge(e.Text, q.Evidence, e.Amount + e.Net, _state.Seed ^ q.Id * 7919)"),
            "schemes sources: a file goes to the mod's own courts on a summons with the evidence the inquiry gathered, and the court's doubt is the save's, never a fresh roll");
        string director = SchemeSource("SchemeDirector.cs");
        Check(director.Contains("public static bool On => Settings.LivingPopulation && Settings.LivingSchemes && Instance != null")
              && director.Contains("if (Settings.LivingPopulation && Settings.LivingSchemes || _state.CarriesOn)") && director.Contains("SaveHook.Keep(_onSave);"),
            "schemes sources: the option off, no new scheme starts, what is open still runs its course, and the line is still written, so nothing is forgotten");

        // Every refusal comes before any money.
        string acts = SchemeSource("SchemeDirector.Acts.cs"), people = SchemeSource("SchemeDirector.People.cs"), day = SchemeSource("SchemeDirector.Day.cs");
        Check(SchemeInOrder(SchemeMethod(acts, "private void DoClub("), "!shop.Mine || !shop.Cash", "SchemeGame.InSession || SchemeBridges.People == null || !SchemeBridges.People()",
                "SchemeRules.MayClub(kept, day)", "SchemeBridges.Club(shop.District", "SchemeRules.ClubSold("),
            "schemes sources: a club is refused in a Going Public session, and wherever the city's people are not this machine's own, before any household is asked for anything");
        Check(SchemeInOrder(SchemeMethod(acts, "public SchemeSaid FundPut("), "SchemeGame.InSession || !SchemeRules.FundStands(_state, day)", "!Affords(f.Amount)",
                "!SchemeGame.Pay(f.Amount, SchemeGame.FundKey)", "SchemeRules.FundPlace(_state, day);", "SchemeBridges.RivalMoney?.Invoke(f.Tycoon, f.Amount);"),
            "schemes sources: money goes into a fund only outside a session, while the offer stands and above the investors' floor, is paid first, and the rival then has exactly it");
        Check(SchemeInOrder(SchemeMethod(acts, "public SchemeSaid FundLook("), "SchemeGame.InSession", "!Affords(fee) || !SchemeGame.Pay(fee, SchemeGame.FundFeeKey)",
            "SchemeRules.FundCheck(_state);"),
            "schemes sources: a fund is looked at once its fee is paid, and never in a session");
        Check(SchemeInOrder(SchemeMethod(acts, "private void DoClaim("), "(shop.Covers & (1 << cover)) == 0", "SchemeRules.MayClaim(kept, day)", "SchemeBridges.Claim(place, cover, loss",
                "SchemeRules.Claimed(_state, place, day, payout, size, id);"),
            "schemes sources: an invented claim needs a cover in force and its wait, is put in by the insurer's own rules, and only then goes on the trail");
        Check(SchemeInOrder(SchemeMethod(acts, "private void DoExpense("), "percent <= 0 || SchemeBridges.Taxed == null || !SchemeBridges.Taxed()", "SchemeRules.MayExpense(_state, day)",
                "SchemeGame.Give(gain, SchemeGame.ExpenseKey);", "SchemeRules.Expensed(_state, day, gain, size);"),
            "schemes sources: an invented expense needs the game's tax to fall due and its wait, and what it pays is what goes on the trail");
        Check(SchemeInOrder(SchemeMethod(acts, "public SchemeSaid Repay("), "SchemeRules.RepayCost(_state, kind, day, unpaid)", "!Affords(cost - cash)",
                "SchemeRules.Repay(_state, kind, day, unpaid, out long fromCash)", "SchemeGame.Pay(paid - fromCash, SchemeGame.RepayKey)", "GiveBack(paid);",
                "SchemeBridges.ClaimRefuse?.Invoke(unpaid[i]);"),
            "schemes sources: paying a file back is refused whole above the investors' floor, pays what the rules say, gives a club's stakes back and withdraws a claim not yet paid");
        Check(SchemeInOrder(SchemeMethod(people, "private FavourAnswer Offered("), "if (!cash && !Affords(price))", "SchemeRules.TemperOf(s.Seed, Office.Contact, contact)",
                "favour == Favour.Vote && SchemeGame.InSession", "theirs == Temper.Takes && !Deliver(favour, person, inquiry, courtCase)",
                "SchemeRules.Offer(s, favour, office, who, price, cash, day)",
                "SchemeGame.Take(price, SchemeGame.FavourKey);"),
            "schemes sources: a favour is refused before any money when the wallet or the investors' floor cannot pay it or it cannot be had, and their own temper is asked before anybody else's");
        Check(people.Contains("if (!SchemeGame.InSession && d.SeatFor() != null)") && SchemeMethod(acts, "private static bool Affords(").Contains("SchemeGame.Cash - amount >= floor")
              && links.Contains("SchemeBridges.Floor = () => Tech.TechDirector.Instance != null ? Tech.TechDirector.Instance.FloorNow : 0L;"),
            "schemes sources: a council vote is not for sale in a Going Public session, and no scheme's money touches what the wallet holds for a firm's investors");
        Check(SchemeInOrder(SchemeMethod(day, "private void Findings("), "SchemeGame.Take(e.Amount - e.FromCash, SchemeGame.FoundKey);", "SchemeGame.Take(e.Net, SchemeGame.SurchargeKey);",
                "GiveBack(e.Amount);", "SchemeBridges.ClaimRefuse?.Invoke(_state.Acts[i].Ref);", "SchemeBridges.Charge(e.Text", "SchemeBridges.Exposed?.Invoke(felony);",
                "SchemeBridges.Story?.Invoke(")
              && SchemeMethod(game, "public static void Take(").Contains("null, null, true, false"),
            "schemes sources: the findings take back what the rules found whatever the wallet holds, give a club's stakes back, end a cheated cover, go to court, and are exposed");
        Check(Regex.IsMatch(day, @"case SchemeEventKind\.Washed:\s*SchemeGame\.Give\(e\.Net, SchemeGame\.WashKey\);")
              && Regex.IsMatch(day, @"case SchemeEventKind\.Caution:\s*SchemeGame\.Take\(e\.Net, SchemeGame\.SurchargeKey\);"),
            "schemes sources: washed cash is booked to the player as the rules net it, and a caution takes its half surcharge");
        Check(SchemeInOrder(SchemeMethod(day, "private void GiveBack("), "SchemeBridges.People != null && SchemeBridges.People()", "SchemeBridges.Restore(place, amount)")
              && links.Contains("PurseRules.Take(s, at, households, id => SchemeRules.ClubDraw(seed, club, id), savings => SchemeRules.ClubStake(savings, level), out joined)")
              && links.Contains("return PurseRules.Give(s, shop != null ? s.DistrictOf(shop.District) : -1, amount);"),
            "schemes sources: a club's stakes leave and reach the households through the people's one purse, by the rules' own draw and stake");
        Check(day.Contains("SchemeBridges.RivalMoney?.Invoke(e.Tycoon, e.Amount);") && day.Contains("SchemeBridges.RivalMoney?.Invoke(e.Tycoon, -e.Amount);")
            && day.Contains("SchemeBridges.RivalMoney?.Invoke(e.Tycoon, -e.Net);")
              && day.Contains("SchemeBridges.RivalBarred?.Invoke(e.Tycoon, SchemeCatalogue.RivalBarDays, SchemeCatalogue.RivalQuietDays);") && day.Contains("SchemeRules.FineOf(charge,"),
            "schemes sources: a rival's scheme, findings, fine and fund move their own worth, and a conviction bars and quiets them");

        // The rivals by the same rules.
        string rivals = SchemeSource("SchemeRules.Rivals.cs");
        foreach (string rule in new[] { "HeatOf(", "Line(seed, r.NewsKind, day, r.Opened)", "Tried(seed, r.Opened, r.Evidence, r.Convictions", "TemperOf(s.Seed, Office.Councillor, seat)", "Spotted(",
                     "SchemeCatalogue.EvidencePace", "SchemeCatalogue.LetterAfter", "SchemeCatalogue.ChargeAfter", "SchemeCatalogue.ChargeFrom", "SchemeCatalogue.SurchargePermille",
                     "SchemeCatalogue.GiveUpAfter", "SchemeCatalogue.EvidenceBase", "JusticeLaw.DaysToCourt(ChargeClass.Felony)" })
        {
            Check(rivals.Contains(rule), "schemes sources: a rival's trail, inquiry and trial use the player's own " + rule.TrimEnd('('));
        }
        Check(people.Contains("SchemeRules.Councillor(_seats[seat])") && rivals.Contains("Councillor(w.Council[Seat(s.Seed, tycoon * 1000 + day, w.Council.Count)])"),
            "schemes sources: a councillor has the same temper whoever asks, the player or a rival: one id for one seat");
        Check(people.Contains("public static bool Hosted(int person)") && people.Contains("SchemeRules.Hosted(d._state, person)"),
            "schemes sources: the one door a party could call later is there: somebody was hosted");

        // Every rule the checks drive is one the mod calls.
        string shipped = string.Empty;
        foreach (string name in SchemeFiles)
        {
            shipped += name.StartsWith("SchemeRules", StringComparison.Ordinal) ? string.Empty : "\n" + SchemeSource(name);
        }
        shipped += links;
        string rules = SchemeSource("SchemeRules.cs") + SchemeSource("SchemeRules.Day.cs") + SchemeSource("SchemeRules.Money.cs") + SchemeSource("SchemeRules.People.cs") + rivals;
        var unused = new List<string>();
        foreach (Match m in Regex.Matches(rules, @"public static [\w<>\[\]]+ (\w+)\("))
        {
            string name = m.Groups[1].Value;
            bool called = shipped.Contains("SchemeRules." + name + "(") || Regex.Matches(rules, @"[^\w.]" + name + @"\(").Count > 1;
            if (!called && !unused.Contains(name))
            {
                unused.Add(name);
            }
        }
        Check(unused.Count == 0, "schemes sources: every rule is called by the director, the pages, the save or the wiring, never by the checks alone (" + string.Join(", ", unused) + ")");

        // On and off again.
        var set = new List<string>();
        foreach (Match m in Regex.Matches(links, @"SchemeBridges\.(\w+) = "))
        {
            set.Add(m.Groups[1].Value);
        }
        string bridges = SchemeSource("SchemeBridges.cs");
        bool reset = set.Count >= 30;
        foreach (string name in set)
        {
            reset &= bridges.Contains("            " + name + " = null;");
        }
        Check(reset && links.Contains("SchemeBridges.Reset();") && Regex.Matches(bridges, @" = null;").Count == Regex.Matches(bridges, @"public static [\w<>, ]+ \w+;").Count,
            "schemes sources: every bridge the wiring fills is emptied again, and every one there is, is reset (" + set.Count + ")");
        foreach (string hook in new[] { "CompanyBridges.QuietLabel = null;", "CompanyBridges.Quiet = null;", "ServicesBridges.BooksShown = null;", "ServicesBridges.SeesThrough = null;",
                     "ServicesBridges.Sold = _soldBefore;", "SocialPhone.QuietLabel = null;", "SocialPhone.QuietAsk = null;", "NetworkBridges.Bought = null;", "NetworkBridges.BoughtUsed = null;",
                     "Justice.Bridges.FileLines = null;", "Justice.Bridges.FileActs = null;", "News.Extra = _paperBefore;" })
        {
            Check(links.Contains(hook), "schemes sources: the hook is taken off again: " + hook);
        }
        string wiring = ModSource(@"Wiring\Wiring.cs");
        Check(wiring.Contains("SchemeLinks.Connect") && wiring.Contains("SchemeLinks.Disconnect"), "schemes sources: the links are put on and taken off with the others");
        Check(ModSource(@"Core\Settings.cs").Contains("public const string LivingSchemesId = \"living_schemes\";")
            && ModSource(@"Core\Settings.cs").Contains("public static bool LivingSchemes = true;")
              && ModSource(@"Meta\OptionsTree.cs").Contains("Opt(\"living_schemes\", \"living_population\")") && ModSource(@"Meta\FeatureList.cs").Contains("Schemes.SchemeGuide.Register();")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Guard.Add<Schemes.SchemeDirector>(_root)"),
            "schemes sources: one option, on by default under Living population, with its Guide entry and its director");
        string guide = SchemeSource("SchemeGuide.cs"), modCode = ModSource(@"Core\RealLifeOverhaulMod.cs");
        foreach (string test in new[] { "reallife_scheme_test_inquiry", "reallife_scheme_test_step", "reallife_scheme_test_fund", "reallife_scheme_test_rival", "reallife_scheme_test_standing" })
        {
            Check(guide.Contains("\"" + test + "\"") && modCode.Contains(".AddButton(\"" + test + "\""), "schemes sources: the test " + test + " is among the Options' tests and the feature's own");
        }
        Check(guide.Contains(".Data(SchemeStore.Key)") && guide.Contains(".Owns(Settings.LivingSchemesId)") && guide.Contains(".ResetData(") && guide.Contains(".Cleanup("),
            "schemes sources: the feature names its option, its save line, its clean-up and its reset to the mod's own list");
        string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features"));
        string page = System.IO.File.Exists(System.IO.Path.Combine(docs, "schemes.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "schemes.md")) : string.Empty;
        string index = System.IO.File.Exists(System.IO.Path.Combine(docs, "README.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "README.md")) : string.Empty;
        Check(page.Length > 2000 && index.Contains("schemes.md"), "schemes sources: its page is under docs/features and the list there names it");
        foreach (int figure in new[] { SchemeCatalogue.TrailDays, SchemeCatalogue.FelonyFrom, SchemeCatalogue.PatternFrom, SchemeCatalogue.ReportFrom, SchemeCatalogue.RivalGainMost,
                     SchemeCatalogue.FundMost, SchemeCatalogue.HeatMost, SchemeCatalogue.HoldDays, SchemeCatalogue.OpenLeast + SchemeCatalogue.OpenSpread - 1 })
        {
            Check(page.Contains(figure.ToString(CultureInfo.InvariantCulture)) || page.Contains(figure.ToString("N0", CultureInfo.InvariantCulture)),
                "schemes sources: the page gives the figure " + figure + " as the catalogue has it");
        }
    }

    // ------------------------------------------------------------------------------------------------- the texts

    private static void SchemeTextChecks()
    {
        string links = ModSource(@"Wiring\Wiring.SchemeLinks.cs");
        string code = links;
        foreach (string name in SchemeFiles)
        {
            code += "\n" + SchemeSource(name);
        }
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(code, "\"(reallife_[a-z_]+)\""))
        {
            string key = m.Groups[1].Value;
            if (!key.EndsWith("_", StringComparison.Ordinal) && !keys.Contains(key))
            {
                keys.Add(key);
            }
        }
        foreach (SchemeKind kind in Enum.GetValues(typeof(SchemeKind)))
        {
            keys.Add("reallife_scheme_news_own_" + kind.ToString().ToLowerInvariant());
        }
        var transactions = new List<string>();
        foreach (Match m in Regex.Matches(SchemeSource("SchemeGame.cs"), "\"(reallife_transaction_scheme_[a-z_]+)\""))
        {
            transactions.Add(m.Groups[1].Value);
            keys.Add(m.Groups[1].Value + "_label");
        }
        keys.AddRange(new[] { "reallife_scheme_enabled", "reallife_opt_tip_living_schemes", "reallife_justice_court_figure_scheme", "reallife_justice_court_figure_laundering",
            "reallife_justice_court_figure_perverting" });
        foreach (string charge in new[] { JusticeLaw.Fraud, JusticeLaw.FalseAccounting, JusticeLaw.MoneyLaundering, JusticeLaw.PervertingJustice })
        {
            keys.Add(JusticeLaw.Find(charge).NameKey);
        }
        Check(keys.Count >= 200 && transactions.Count == 9 && code.Contains("\"reallife_scheme_news_own_\" + kind.ToString().ToLowerInvariant()"),
            "schemes texts: the keys the code names are found: " + keys.Count + ", nine of them the lines of a bank statement");

        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        var notices = new List<string>();
        foreach (string key in keys)
        {
            if (key.Contains("_note_") || key == "reallife_scheme_text_letter" || Regex.IsMatch(key, "^reallife_scheme_test_(no_save|off|.*_(done|open|none)|no_rival)$") || key.Contains("_talk_")
                || key.Contains("_fund_look_") || key.EndsWith("_done", StringComparison.Ordinal) || key.EndsWith("_short", StringComparison.Ordinal)
                || key.EndsWith("_nothing", StringComparison.Ordinal))
            {
                notices.Add(key);
            }
        }
        Check(notices.Count >= 50, "schemes texts: the notices among them are found: " + notices.Count);
        string[] facts = { "scheme_surcharge", "scheme_letter", "scheme_visit", "scheme_findings", "scheme_drop", "scheme_file_days", "scheme_repeat_days", "scheme_felony", "scheme_wash_cut",
            "scheme_wash_most", "scheme_report", "scheme_pattern", "scheme_pattern_days", "scheme_club_stake", "scheme_lift_most", "scheme_hold_days", "scheme_fund_return", "scheme_fund_days",
            "scheme_fund_back" };
        string[] screens = { "reallife_co_tab_cover", "reallife_co_tab_tax", "reallife_co_tab_marketing", "reallife_co_app", "reallife_justice_file_title" };
        foreach (string locale in SchemeLocales)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, locale + ".json"));
            Check(words != null && english != null, "schemes texts: " + locale + ".json is read");
            if (words == null || english == null)
            {
                continue;
            }
            int missing = 0, marks = 0, holes = 0, breaks = 0, same = 0;
            string first = string.Empty;
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Trim().Length > 0;
                string en = english.TryGetValue(key, out string e) ? e : string.Empty;
                if (!there)
                {
                    missing++;
                    first = first.Length == 0 ? key + " is missing" : first;
                    continue;
                }
                if (value.Contains('!') || value.Contains('！') || value.Contains('—') || value.Contains('–') || value.Contains(" - "))
                {
                    marks++;
                    first = first.Length == 0 ? key + " has an exclamation mark or a dash" : first;
                }
                if (Holes(value) != Holes(en))
                {
                    holes++;
                    first = first.Length == 0 ? key + " has other placeholders than the English" : first;
                }
                if (value.Split('\n').Length != en.Split('\n').Length || Regex.Matches(value, "</?b>").Count != Regex.Matches(en, "</?b>").Count)
                {
                    breaks++;
                    first = first.Length == 0 ? key + " has other line breaks or bold marks than the English" : first;
                }
                same += locale != "en" && key.StartsWith("reallife_scheme_", StringComparison.Ordinal) && en.Length > 40 && value == en ? 1 : 0;
            }
            Check(missing == 0 && marks == 0 && holes == 0 && breaks == 0 && same == 0,
                "schemes texts: in " + locale + " every text is there, in its own words, with the English placeholders and line breaks and no exclamation mark or dash (" + missing + " missing, "
                + marks + " marked, " + holes + " with other placeholders, " + breaks + " broken otherwise, " + same + " left in English; first: " + first + ")");
            int longest = 0;
            string longestKey = string.Empty;
            foreach (string key in notices)
            {
                int length = words.TryGetValue(key, out string value) ? value.Length : 0;
                longestKey = length > longest ? key : longestKey;
                longest = Math.Max(longest, length);
            }
            Check(longest <= 240, "schemes texts: every notice is at most 240 characters in " + locale + " (the longest, " + longestKey + ", is " + longest + ")");
            string page = words.TryGetValue("reallife_guide_schemes", out string guide) ? guide : string.Empty;
            bool named = page.Length > 0, bold = true;
            foreach (string fact in facts)
            {
                named &= page.Contains("{" + fact + "}");
            }
            foreach (Match m in Regex.Matches(page, "<b>(.*?)</b>"))
            {
                bool screen = false;
                foreach (string key in screens)
                {
                    screen |= words.TryGetValue(key, out string name) && name == m.Groups[1].Value;
                }
                bold &= screen;
            }
            Check(named && bold && Regex.Matches(page, "(?m)^# ").Count == 5 && Regex.Matches(page, "<b>").Count == 5,
                "schemes texts: the Guide page in " + locale + " has its five parts, every figure from the catalogue, and names the screens by their own names");
            bool labels = true;
            foreach (string key in transactions)
            {
                labels &= words.TryGetValue(key, out string line) && words.TryGetValue(key + "_label", out string label) && line == label;
            }
            Check(labels, "schemes texts: each line of a bank statement and its label are the same words in " + locale);
        }
        string guideCode = SchemeSource("SchemeGuide.cs");
        foreach (string fact in facts)
        {
            Check(guideCode.Contains(".Fact(\"" + fact + "\""), "schemes texts: the Guide's {" + fact + "} is filled from the catalogue");
        }
        Check(Regex.Matches(guideCode, @"\.Fact\(").Count == facts.Length, "schemes texts: every figure the Guide fills is one its page names");

        // Every placeholder of a text is one the code fills where it uses the text.
        if (english != null)
        {
            var person = new List<string> { "name", "first", "job", "district", "amount", "way", "standing" };
            var paper = new List<string> { "rival", "holding", "what", "amount" };
            int open = 0;
            string firstOpen = string.Empty;
            foreach (string key in keys)
            {
                string en = english.TryGetValue(key, out string e) ? e : string.Empty;
                MatchCollection wantedHoles = Regex.Matches(en, @"\{([a-z_]+)\}");
                if (wantedHoles.Count == 0 || key == "reallife_guide_schemes" || key.StartsWith("reallife_justice_court_figure_", StringComparison.Ordinal))
                {
                    continue;
                }
                List<string> given = key.StartsWith("reallife_scheme_ask_", StringComparison.Ordinal) || key.StartsWith("reallife_scheme_taken_", StringComparison.Ordinal)
                                     || key.StartsWith("reallife_scheme_favour_", StringComparison.Ordinal) || key.StartsWith("reallife_scheme_talk_", StringComparison.Ordinal) ? person
                    : key.StartsWith("reallife_scheme_news_rival_", StringComparison.Ordinal) ? paper : null;
                bool ok = false;
                if (given != null)
                {
                    ok = true;
                    foreach (Match h in wantedHoles)
                    {
                        ok &= given.Contains(h.Groups[1].Value);
                    }
                }
                for (int at = code.IndexOf("\"" + key + "\"", StringComparison.Ordinal); !ok && at >= 0; at = code.IndexOf("\"" + key + "\"", at + 1, StringComparison.Ordinal))
                {
                    int from = Math.Max(0, at - 260), end = code.IndexOf(';', at);
                    string around = code.Substring(from, (end < 0 ? code.Length : end) - from);
                    bool every = true;
                    foreach (Match h in wantedHoles)
                    {
                        every &= around.Contains("\"" + h.Groups[1].Value + "\"");
                    }
                    ok = every;
                }
                open += ok ? 0 : 1;
                firstOpen = !ok && firstOpen.Length == 0 ? key : firstOpen;
            }
            Check(open == 0, "schemes texts: every placeholder of a text is filled where the code uses it (" + open + " not; first: " + firstOpen + ")");
            Check(links.Contains("data[\"amount\"] = SchemeText.Money(price);") && links.Contains("data[\"way\"] = ") && links.Contains("data[\"standing\"] = ")
                  && links.Contains("Dictionary<string, string> data = SocialLife.Data(p);") && ModSource(@"Social\SocialLife.cs").Contains("\"first\""),
                "schemes texts: a call's words are filled with who they are, the price, how it would be paid and what a word tells");
            bool neutral = true;
            foreach (string key in SchemeKeys.Kinds)
            {
                neutral &= english.TryGetValue(key, out string kind) && !Regex.IsMatch(kind, @"\b(your|you|my)\b", RegexOptions.IgnoreCase);
            }
            Check(neutral, "schemes texts: what a scheme is called is nobody's own, since the paper says it of a rival too");
            string row = string.Empty;
            foreach (KeyValuePair<string, string> pair in english)
            {
                if (pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal)
                    && pair.Value.Contains("Schemes and inquiries, under Living population"))
                {
                    row = pair.Key;
                }
            }
            Check(row.Length > 0, "schemes texts: What's new tells of the schemes in English, in some row of the changes");
            foreach (string locale in SchemeLocales)
            {
                Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, locale + ".json"));
                string line = words != null && row.Length > 0 && words.TryGetValue(row, out string value) ? value : string.Empty;
                string title = words != null && words.TryGetValue("reallife_scheme_enabled", out string name) ? name : "\u0000";
                Check(title.Length > 2 && line.Contains(title) && !line.Contains('!') && !line.Contains('—')
                      && (locale == "en" || !line.Contains("Schemes and inquiries, under Living population")),
                    "schemes texts: the same row of What's new tells of them in " + locale + ", by the option's own name there (" + title + ")");
            }
        }
    }

    // ------------------------------------------------------------------------------------------------- the cost

    private static void SchemeTimingChecks()
    {
        // A save as busy as one gets: every trail full, an inquiry into each kind, people paid, businesses washing, the rivals at it.
        var busy = new SchemeState { Seed = 31337, StartDay = 1, Day = 400 };
        for (int k = 0; k < SchemeCatalogue.Kinds; k++)
        {
            for (int i = 0; i < SchemeCatalogue.ActsMost; i++)
            {
                busy.Acts.Add(new SchemeAct { Kind = (SchemeKind)k, Day = 200 + i * 8, Gain = 3000, Paid = 500, Size = 250, Knows = 2, Paper = true, Place = "Shop~" + i % 8, Ref = i });
            }
            busy.Heat[k] = 200;
            busy.Cases.Add(new SchemeInquiry { Id = k + 1, Kind = (SchemeKind)k, Opened = 399, Stage = InquiryStage.Quiet });
        }
        busy.NextInquiry = 7;
        busy.Dirty = 100000000;
        for (int i = 0; i < SchemeCatalogue.PaidMost; i++)
        {
            busy.Paid.Add(new SchemePaid { Who = "p" + i, Day = 390, Amount = 800, What = Favour.Gift });
        }
        var w = SchemeCityWorld();
        w.TaxPercent = 20;
        for (int i = 0; i < 8; i++)
        {
            busy.Shops.Add(new SchemeShop { Place = "Shop~" + i, Lift = 300, Wash = 200, WindowFrom = 390 });
            w.Tills.Add(new SchemeTill { Place = "Shop~" + i, Average = 3000, Cash = true });
        }
        string text = SchemeStore.Write(busy);
        const int saves = 200, days = 30;
        var copies = new SchemeState[saves];
        for (int i = 0; i < saves; i++)
        {
            copies[i] = SchemeStore.Read(text, out _);
        }
        var events = new List<SchemeEvent>(64);
        int happened = 0;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < saves; i++)
        {
            for (int d = 401; d <= 400 + days; d++)
            {
                events.Clear();
                SchemeRules.Day(copies[i], d, w, events);
                happened += events.Count;
            }
        }
        watch.Stop();
        double perDay = watch.Elapsed.TotalMilliseconds / (saves * days);
        Check(happened > saves * days && perDay < 0.2, "schemes cost: a day's turn of the busiest save there can be (" + busy.Acts.Count
            + " acts, six inquiries, eight tills, three rivals) takes under 0.2 ms: "
                                                       + perDay.ToString("0.0000", CultureInfo.InvariantCulture) + " ms");
        watch.Restart();
        int length = 0;
        for (int i = 0; i < 200; i++)
        {
            length += SchemeStore.Write(SchemeStore.Read(text, out _)).Length;
        }
        watch.Stop();
        Check(length == 200 * text.Length && watch.Elapsed.TotalMilliseconds / 200 < 2.0, "schemes cost: that save is written and read in under 2 ms: "
                                                                                          + (watch.Elapsed.TotalMilliseconds / 200).ToString("0.000", CultureInfo.InvariantCulture)
                                                                                          + " ms, " + text.Length + " characters");
        SchemeWords words = SchemeKeyWords();
        watch.Restart();
        int answers = 0;
        for (int i = 0; i < 2000; i++)
        {
            answers += SchemePages.Claim(words, SchemePlace, "Shop", 0, 20000).Count + SchemePages.Wash(words, SchemePlace, "Shop", true, 3000, 50, 9000, false, 1000, 20).Count;
        }
        watch.Stop();
        Check(answers == 12000 && watch.Elapsed.TotalMilliseconds / 4000 < 0.1, "schemes cost: a page is written in under 0.1 ms, on the press that opens it: "
                                                                               + (watch.Elapsed.TotalMilliseconds / 4000).ToString("0.0000", CultureInfo.InvariantCulture) + " ms");
    }
}
