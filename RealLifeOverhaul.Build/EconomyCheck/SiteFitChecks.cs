using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// A plot's land against what its site needs (docs/features/construction.md, "Land too small for a site"; Scripts/Civic: PlotRules.Fit,
// SiteRefusals; Scripts/Construction: LandRules.SiteFit, ConstructionDirector.Fit): the one answer for the land's size, every size
// the district's grid sells taking a site, the first step refusing land that cannot (before a design, a plan, a permit, a quote or a
// hire is paid for), a project stuck on small land cancelled with every fee paid given back, the city's own picks stopping at the
// same answer, and the words: plain, with the figures, and never a pointer to Player.log. Each check was made to fail once.
internal static partial class Program
{
    private static void SiteFitChecks()
    {
        SiteFitRuleChecks();
        SiteFitStepChecks();
        SiteFitRefundChecks();
        SiteFitCityChecks();
        SiteFitTextChecks();
    }

    // ------------------------------------------------------------------ the rule

    private static void SiteFitRuleChecks()
    {
        // The one answer: the land's size and nothing else, the same as the developer's sentence says it.
        bool same = true;
        for (int width = 0; width <= 70 && same; width++)
        {
            for (int depth = 0; depth <= 70 && same; depth++)
            {
                same &= (PlotRules.Fit(width, depth, out _) == PlotFit.Fits) == (PlotRules.Unfit(width, depth, 1) == null);
                same &= (LandRules.SiteFit(width, depth, out int need) == PlotRules.Fit(width, depth, out int need2)) && need == need2;
            }
        }
        Check(same, "site fit: the land's answer is one, from 0 to 70 m each way: the plot's own and the construction game's agree, with the same figure needed");

        // The district's grid: a space is 7 m, the standard lowest lot; every site of one to seven spaces on each depth the plan's strips have takes a site.
        Check(PlotRules.NarrowMin == SpacesRules.SpaceWidth, "site fit: the narrowest land a site is set out on is the grid's one space (" + PlotRules.NarrowMin + " m)");
        bool grid = true;
        string wrong = null;
        int[] depths = { SpacesRules.ShallowDepth, SpacesRules.StreetDepth, SpacesRules.AvenueDepth };
        for (int spaces = 1; spaces <= BuildCatalogue.SiteSpacesMost; spaces++)
        {
            for (int d = 0; d < depths.Length; d++)
            {
                int width = spaces * SpacesRules.SpaceWidth;
                if (LandRules.SiteFit(width, depths[d], out _) != PlotFit.Fits)
                {
                    grid = false;
                    wrong = wrong ?? spaces + " space(s), " + width + " by " + depths[d] + " m";
                }
            }
        }
        Check(grid, "site fit: every site the Land tab sells, one to seven spaces on a street's, an avenue's or a shallow strip, can have its site set out (" + (wrong ?? "all") + ")");
        PlotRules.Frame(SpacesRules.SpaceWidth, SpacesRules.StreetDepth, out float minX, out float maxX, out float minZ, out float maxZ);
        Check(maxX - minX >= PlotRules.MinFrame && maxZ - minZ >= PlotRules.MinFrame && minX >= -SpacesRules.SpaceWidth * 0.5f && maxX <= SpacesRules.SpaceWidth * 0.5f,
            "site fit: a one-space site's frame stands on its own land, at least " + PlotRules.MinFrame + " m each way");
        PlotRules.YardSpots(SpacesRules.SpaceWidth, SpacesRules.StreetDepth, out PlotSpot cabin, out PlotSpot toilet);
        var stacks = new List<PlotSpot>();
        PlotRules.StackSpots(SpacesRules.SpaceWidth, SpacesRules.StreetDepth, stacks);
        var spoil = new List<PlotSpot>();
        PlotRules.SpoilSpots(SpacesRules.SpaceWidth, SpacesRules.StreetDepth, spoil);
        var hoarding = new List<PlotSpot>();
        PlotRules.HoardingSpots(SpacesRules.SpaceWidth, SpacesRules.StreetDepth, hoarding);
        bool onLand = Math.Abs(cabin.X) < SpacesRules.SpaceWidth * 0.5f && Math.Abs(toilet.X) < SpacesRules.SpaceWidth * 0.5f && cabin.Z > maxZ && toilet.Z > maxZ && stacks.Count >= 1
                      && hoarding.Count >= 1;
        foreach (PlotSpot s in stacks.Concat(spoil).Concat(hoarding))
        {
            onLand &= Math.Abs(s.X) <= SpacesRules.SpaceWidth * 0.5f && Math.Abs(s.Z) <= SpacesRules.StreetDepth * 0.5f;
        }
        Check(onLand, "site fit: a one-space site's cabin, toilet, stacks, spoil and hoarding all stand on its own land");

        // What falls short, and by how much.
        int least;
        Check(LandRules.SiteFit(6, 35, out least) == PlotFit.TooNarrow && least == PlotRules.NarrowMin, "site fit: land 6 m wide is too narrow, and says it needs " + PlotRules.NarrowMin + " m");
        Check(LandRules.SiteFit(7, 19, out least) == PlotFit.TooShallow && least == PlotRules.NarrowDepth, "site fit: a narrow site 19 m deep is too shallow, and says it needs " + PlotRules.NarrowDepth + " m");
        Check(LandRules.SiteFit(21, 15, out least) == PlotFit.TooShallow && least == PlotRules.MinDepth, "site fit: a wide site 15 m deep is too shallow, and says it needs " + PlotRules.MinDepth + " m");
        Check(LandRules.SiteFit(61, 30, out least) == PlotFit.TooWide && least == PlotRules.MostLand && LandRules.SiteFit(30, 61, out least) == PlotFit.TooDeep && least == PlotRules.MostLand,
            "site fit: land over " + PlotRules.MostLand + " m one way is too big, and says the most");
        Check(LandRules.SiteFit(0, 0, out _) == PlotFit.TooNarrow && LandRules.SiteFit(-5, 35, out _) == PlotFit.TooNarrow && LandRules.SiteFit(7, 20, out _) == PlotFit.Fits
              && LandRules.SiteFit(20, 20, out _) == PlotFit.Fits && LandRules.SiteFit(21, 16, out _) == PlotFit.Fits && LandRules.SiteFit(60, 60, out _) == PlotFit.Fits,
            "site fit: the edges, none and less than none, the least of each kind of site and the most");
        // The words wanted: one for each way it falls short.
        bool keys = LandRules.FitKeys.Length == Enum.GetValues(typeof(PlotFit)).Length && LandRules.FitBuilderKeys.Length == LandRules.FitKeys.Length && LandRules.FitKeys[0] == null
                    && LandRules.FitBuilderKeys[0] == null;
        for (int i = 1; keys && i < LandRules.FitKeys.Length; i++)
        {
            keys &= !string.IsNullOrEmpty(LandRules.FitKeys[i]) && !string.IsNullOrEmpty(LandRules.FitBuilderKeys[i]) && LandRules.IsFitKey(LandRules.FitKeys[i]) && LandRules.IsFitKey(LandRules.FitBuilderKeys[i]);
        }
        Check(keys && !LandRules.IsFitKey("reallife_bld_answer_no_lot") && !LandRules.IsFitKey(null), "site fit: every way land falls short has its words, for the player's step and for a builder's refusal");
    }

    // ------------------------------------------------------------------ the first step and every step after it

    private static void SiteFitStepChecks()
    {
        string projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string hire = ModSource(@"Construction\ConstructionDirector.Hire.cs");
        string fit = ModSource(@"Construction\ConstructionDirector.Fit.cs");
        string newPlot = MethodText(projects, "public int NewPlot(");
        Check(newPlot.Length > 0 && newPlot.Contains("LandBlock(lot") && newPlot.IndexOf("LandBlock(lot", StringComparison.Ordinal) < newPlot.IndexOf("new ProjectFile", StringComparison.Ordinal)
              && newPlot.IndexOf("LandBlock(lot", StringComparison.Ordinal) < newPlot.IndexOf("_state.Projects.Add(", StringComparison.Ordinal),
            "site fit: the first step, planning a building on land, asks whether its site can be set out before any project is made");
        // Each step that draws, applies or asks is refused before its fee is taken.
        string[] steps =
        {
            "public string SetUse(", "public string SetDesign(", "public string DrawPlans(", "public string AskEngineer(", "public string Apply(", "public string Appeal("
        };
        string late = null;
        foreach (string step in steps)
        {
            string body = MethodText(projects, step);
            int asked = body.IndexOf("SiteBlock(p)", StringComparison.Ordinal);
            int taken = body.IndexOf("ConstructionGame.Take(", StringComparison.Ordinal);
            if (body.Length == 0 || asked < 0 || (taken >= 0 && taken < asked))
            {
                late = late ?? step;
            }
        }
        Check(late == null, "site fit: the design, the plans, the engineer, City Hall's application and the appeal are each refused before their fee is taken (" + (late ?? "all") + ")");
        string hireBlock = MethodText(hire, "public string HireBlock(");
        string hireBuilder = MethodText(hire, "public string HireBuilder(");
        string startBlock = MethodText(projects, "public string StartBlock(");
        Check(hireBlock.Contains("SiteBlock(p)") && hireBlock.IndexOf("SiteBlock(p)", StringComparison.Ordinal) < hireBlock.IndexOf("Estimate", StringComparison.Ordinal)
              && hireBuilder.IndexOf("HireBlock(p)", StringComparison.Ordinal) >= 0 && hireBuilder.IndexOf("HireBlock(p)", StringComparison.Ordinal) < hireBuilder.IndexOf("ConstructionGame.Take(", StringComparison.Ordinal)
              && hireBuilder.Contains("BuilderRefused(") && startBlock.Contains("SiteBlock(p)"),
            "site fit: the quotes, the hire and the start wait on land a site can be set out on, and a hire is refused before its deposit is taken, in the builder's name");
        string quotesApp = ModSource(@"Construction\ConstructionApp.Projects.cs");
        string writeProject = MethodText(quotesApp, "private void WriteProject(");
        Check(writeProject.Contains("WriteUnfit(box, p)") && writeProject.IndexOf("WriteUnfit(box, p)", StringComparison.Ordinal) < writeProject.IndexOf("WriteQuotes(box, p)", StringComparison.Ordinal)
              && writeProject.IndexOf("WriteUnfit(box, p)", StringComparison.Ordinal) < writeProject.IndexOf("WritePlans(box, state, p)", StringComparison.Ordinal),
            "site fit: a project on land too small shows why, and the way out, in place of its plans, its quotes and its hire");
        string app = ModSource(@"Construction\ConstructionApp.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        Check(MethodText(app, "private void Answer(").Contains("AnswerData(key)") && MethodText(window, "private void Refresh(string key)").Contains("AnswerData(key)"),
            "site fit: a notice that says the land's size carries the figures with it, in the app and in build mode");
        Check(fit.Contains("AnswerData(string key)") && MethodText(fit, "public Dictionary<string, string> AnswerData(").Contains("_answerKey = null"),
            "site fit: a notice's figures are given once and then forgotten");
    }

    // ------------------------------------------------------------------ the way out

    private static void SiteFitRefundChecks()
    {
        var p = new ProjectFile { Id = 7, Kind = ProjectKind.Plot, Stage = ProjectStage.Ready, Spent = 163400L };
        Check(LandRules.Cancellable(p) && LandRules.Refund(p) == 163400L, "site fit: a project ready to build can be cancelled, and gives back every fee paid for it");
        p.Stage = ProjectStage.Design;
        Check(LandRules.Cancellable(p), "site fit: ... and one still at its plans");
        p.Stage = ProjectStage.Planning;
        Check(LandRules.Cancellable(p), "site fit: ... and one at City Hall");
        p.Stage = ProjectStage.Building;
        Check(!LandRules.Cancellable(p), "site fit: a project already building is not cancelled for money");
        p.Stage = ProjectStage.Ready;
        p.SiteId = 12;
        Check(!LandRules.Cancellable(p), "site fit: nor one with a site set out");
        p.SiteId = 0;
        p.Investor = 1;
        Check(!LandRules.Cancellable(p), "site fit: nor one an investor has put money into");
        p.Investor = -1;
        p.FlatsSold = 2;
        Check(!LandRules.Cancellable(p), "site fit: nor one with flats sold off plan");
        p.FlatsSold = 0;
        p.LoanDrawn = 500000L;
        Check(!LandRules.Cancellable(p), "site fit: nor one with a loan drawn");
        p.LoanDrawn = 0;
        p.Stage = ProjectStage.Abandoned;
        Check(!LandRules.Cancellable(p) && !LandRules.Cancellable(null), "site fit: nor one already given up, nor none");
        p.Stage = ProjectStage.Ready;
        p.Spent = -5L;
        Check(LandRules.Refund(p) == 0L && LandRules.Refund(null) == 0L, "site fit: a refund is never less than nothing");

        // The company's own books take the money back out of its costs, never below nothing.
        var state = new BuildState();
        state.Firm.Spent = 250000L;
        state.PayBack(100000L);
        bool partly = state.Firm.Spent == 150000L;
        state.PayBack(900000L);
        bool never = state.Firm.Spent == 0L;
        state.Firm.Spent = 80L;
        state.PayBack(0L);
        state.PayBack(-30L);
        Check(partly && never && state.Firm.Spent == 80L, "site fit: a refund comes off the company's costs, never below nothing, and nothing changes for none");

        string fit = ModSource(@"Construction\ConstructionDirector.Fit.cs");
        string cancel = MethodText(fit, "public string CancelUnfit(");
        int abandon = cancel.IndexOf("Abandon(p)", StringComparison.Ordinal), give = cancel.IndexOf("ConstructionGame.Give(back,", StringComparison.Ordinal);
        Check(cancel.Contains("if (!CanCancel(p))") && cancel.Contains("long back = LandRules.Refund(p);") && abandon > 0 && give > abandon && cancel.Contains("\"reallife_transaction_bld_fees_back\"")
              && cancel.Contains("_state.PayBack(back)") && cancel.Contains("p.Spent = 0L;") && cancel.Contains("reallife_bld_answer_cancelled_refund"),
            "site fit: cancelling gives up the project (the land kept), gives back every fee through the game's own money, takes it off the books and says so");
        Check(MethodText(fit, "public bool CanCancel(").Contains("LandRules.Cancellable(p) && SiteBlock(p) != null"),
            "site fit: only a project whose land cannot take its site is cancelled so: land that can is never a way to get fees back");
        string appProjects = ModSource(@"Construction\ConstructionApp.Projects.cs");
        string unfit = MethodText(appProjects, "private bool WriteUnfit(");
        Check(unfit.Contains("_director.CanCancel(p)") && unfit.Contains("reallife_bld_button_cancel_refund") && unfit.Contains("_director.CancelUnfit(p)") && unfit.Contains("AbandonRow(box, p)")
              && unfit.Contains("GameConfirm") == false && unfit.Contains("Confirm("),
            "site fit: the way out is on the project's page, behind the game's question box, and a project that cannot be cancelled for money can still be given up");
    }

    // ------------------------------------------------------------------ the city's own picks

    private static void SiteFitCityChecks()
    {
        string spaces = ModSource(@"Construction\ConstructionDirector.Spaces.cs");
        string keep = MethodText(spaces, "private static void KeepFitting(");
        Check(keep.Length > 0 && keep.IndexOf("LandRules.SiteFit(site.Width, site.Depth, out _) != PlotFit.Fits", StringComparison.Ordinal) >= 0
              && keep.IndexOf("into.Clear()", StringComparison.Ordinal) > 0 && keep.IndexOf("LandRules.SiteFit(", StringComparison.Ordinal) < keep.IndexOf("OnSpaces(site)", StringComparison.Ordinal),
            "site fit, the city: every list of designs for a site, the player's and the city's, is emptied first for land no site can be set out on");
        string corners = ModSource(@"Construction\ConstructionDirector.Corners.cs");
        string cornerDesigns = MethodText(corners, "public bool CornerDesigns(");
        Check(cornerDesigns.IndexOf("LandRules.SiteFit(site.Width, site.Depth, out _) != PlotFit.Fits", StringComparison.Ordinal) > 0
              && cornerDesigns.IndexOf("LandRules.SiteFit(", StringComparison.Ordinal) < cornerDesigns.IndexOf("AskCorners(", StringComparison.Ordinal),
            "site fit, the city: no corner building is offered on such land either");
        // Every pick the city makes reads the designs through that list.
        var picks = new[]
        {
            new[] { @"Construction\ConstructionDirector.Market.cs", "private bool DevDesign(" },
            new[] { @"Construction\ConstructionDirector.Market.cs", "private bool DesignOf(" },
            new[] { @"Construction\ConstructionDirector.CityRedevelop.cs", "private bool CityDesign(" },
            new[] { @"Construction\ConstructionDirector.Grow.cs", "private bool GrowDesign(" },
            new[] { @"Construction\ConstructionDirector.Projects.cs", "public bool DesignChoices(" },
            new[] { @"Construction\ConstructionDirector.BuildMode.cs", "public bool FootprintDesigns(" }
        };
        string unkept = null;
        foreach (string[] pick in picks)
        {
            string body = MethodText(ModSource(pick[0]), pick[1]);
            if (body.Length == 0 || !body.Contains("ReadDesigns(") || !body.Contains("KeepFitting("))
            {
                unkept = unkept ?? pick[1];
            }
        }
        Check(unkept == null, "site fit, the city: the developers' pick, a redevelopment's, a growth's, the player's and build mode's each read the designs through it (" + (unkept ?? "all") + ")");
        // With no design left, a developer's pick is none: its lot is passed over and nothing is planned on it.
        Check(DevelopmentRules.DesignPick("uptown_1_00_00", 0) < 0 && DevelopmentRules.DesignPick("uptown_1_00_00", -1) < 0 && DevelopmentRules.DesignPick("uptown_1_00_00", 3) >= 0,
            "site fit, the city: a developer with no design left for a lot picks none, so its lot is passed over and no site is laid on it");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs");
        string developments = MethodText(market, "private void Developments(int day)");
        Check(developments.Contains("if (dev.Design == NoDesign)") && developments.IndexOf("if (dev.Design == NoDesign)", StringComparison.Ordinal) < developments.IndexOf("StartDevelopment(", StringComparison.Ordinal)
              && MethodText(market, "private bool DevDesign(").Contains("DevelopmentRules.DesignPick(lot.Id, _choices.Count)"),
            "site fit, the city: a lot with no design is skipped before the developers set a site out on it");
        string redevelop = ModSource(@"Construction\ConstructionDirector.CityRedevelop.cs");
        string start = MethodText(redevelop, "private void CityBuildStart(");
        Check(start.Contains("design == NoDesign") && start.IndexOf("design == NoDesign", StringComparison.Ordinal) < start.IndexOf("StartPlotSite(", StringComparison.Ordinal),
            "site fit, the city: a redevelopment with no design for its ground sets no site out either");
        // The rivals' and the city's sites are started only for a design picked: the one place a plot's site is asked for with a lot's own size.
        string[] starts = { @"Construction\ConstructionDirector.Market.cs", @"Construction\ConstructionDirector.CityRedevelop.cs", @"Construction\ConstructionDirector.Grow.cs" };
        int callers = 0;
        foreach (string file in starts)
        {
            callers += ModSource(file).Split(new[] { "StartPlotSite(" }, StringSplitOptions.None).Length - 1;
        }
        Check(callers == 4, "site fit, the city: the four places the city's builders start a plot's site are the ones above (" + callers + ")");
    }

    // ------------------------------------------------------------------ the words

    private static void SiteFitTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("site fit texts", words, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_bld_answer_land_narrow", "reallife_bld_answer_land_shallow", "reallife_bld_answer_land_big", "reallife_bld_answer_unfit_narrow", "reallife_bld_answer_unfit_shallow",
            "reallife_bld_answer_unfit_big", "reallife_bld_answer_site_off", "reallife_bld_answer_site_host", "reallife_bld_answer_site_there", "reallife_bld_answer_site_refused",
            "reallife_bld_unfit_note", "reallife_bld_unfit_free_note", "reallife_bld_button_cancel_refund", "reallife_bld_confirm_cancel_refund", "reallife_bld_confirm_cancel_refund_body",
            "reallife_bld_answer_cancelled_refund", "reallife_transaction_bld_fees_back", "reallife_bld_next_unfit"
        };
        // Every one is in en, pt and zh-cn, in the style; none sends a player to a log; the others read the English until they are translated.
        for (int l = 0; l < words.Length && l < ModLanguages.Required; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ")
                    || value.Contains("  ") || value.Contains("Player.log") || value.Contains("Player-prev"))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "site fit texts: every text is in " + codes[l] + ".json, in the style, with the English names and no word of the log (" + (missing ?? "all") + ")");
        }
        // No text the construction game tells a player in any language points at the log: its answers, notices, rows, buttons and questions (the test buttons' lines are the owner's).
        var pointing = new List<string>();
        for (int l = 0; l < words.Length; l++)
        {
            foreach (KeyValuePair<string, string> kv in words[l])
            {
                bool told = kv.Key.StartsWith("reallife_bld_answer_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_bld_notice_", StringComparison.Ordinal)
                            || kv.Key.StartsWith("reallife_bld_next_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_bld_confirm_", StringComparison.Ordinal)
                            || kv.Key.StartsWith("reallife_bld_button_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_bld_unfit_", StringComparison.Ordinal)
                            || kv.Key.StartsWith("reallife_park_answer_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_park_notice_", StringComparison.Ordinal);
                if (told && kv.Value.IndexOf("Player.log", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pointing.Add(codes[l] + ":" + kv.Key);
                }
            }
        }
        Check(pointing.Count == 0, "site fit texts: nothing the construction game tells a player points them at Player.log in any language (" + (pointing.Count == 0 ? "none" : string.Join(", ", pointing.Take(4))) + ")");

        // The words filled in, as the player's notice shows them: the builder, the address and the figures, in plain words.
        string[] pairs = { "builder", "Ironbridge Contracting", "address", "2 Grove Street", "width", "6", "depth", "35", "need", "7" };
        string said = FillWith(words[0]["reallife_bld_answer_unfit_narrow"], pairs);
        Check(said.StartsWith("Ironbridge Contracting can't set out the site at 2 Grove Street: the land is 6 m wide, and a site needs at least 7 m.", StringComparison.Ordinal)
              && !said.Contains("Player.log") && !said.Contains('{'),
            "site fit texts: a builder's refusal reads in plain words, with the builder, the address and the size (" + said + ")");
        string first = FillWith(words[0]["reallife_bld_answer_land_narrow"], pairs);
        Check(first.StartsWith("The land at 2 Grove Street is 6 m wide, and a building site needs at least 7 m.", StringComparison.Ordinal) && first.Contains("Join the next space") && !first.Contains('{'),
            "site fit texts: the first step's refusal says the land's size against what a site needs, and what would work (" + first + ")");
        // Every notice fits the game's toast with its names filled, in every language shown; each asks only for the names the call gives it.
        var given = new HashSet<string>(new[] { "{address}", "{width}", "{depth}", "{need}", "{builder}", "{name}", "{amount}" }, StringComparer.Ordinal);
        string tooLong = null, strange = null;
        for (int l = 0; l < words.Length; l++)
        {
            foreach (string key in keys.Where(k => k.StartsWith("reallife_bld_answer_", StringComparison.Ordinal)))
            {
                string filled = FillWith(words[l][key], new[] { "builder", "Calloway Brothers Scaffolding", "address", "1006 Maple Street, Cedar Heights", "width", "49", "depth", "39", "need", "10", "name", "Shops at 1006 Maple Street", "amount", "$1,634,930" });
                if (filled.Length > 240)
                {
                    tooLong = tooLong ?? codes[l] + ":" + key + " (" + filled.Length + ")";
                }
                foreach (string name in NamesOf(words[l][key]).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!given.Contains(name))
                    {
                        strange = strange ?? codes[l] + ":" + key + " asks " + name;
                    }
                }
            }
        }
        Check(tooLong == null && strange == null, "site fit texts: every notice is at most 240 characters with its names filled, and asks only for names it is given (" + (tooLong ?? strange ?? "all") + ")");

        // Why the programme set no site out, in plain words, for every cause it has: none points at the log, and every cause says what happens next.
        string[] whys = { SiteRefusals.Off, SiteRefusals.NotRunning, SiteRefusals.Host, SiteRefusals.Taken, SiteRefusals.NearPrefix + "30 m", "a fault", null, string.Empty };
        string[] want =
        {
            "reallife_bld_answer_site_off", "reallife_bld_answer_site_off", "reallife_bld_answer_site_host", "reallife_bld_answer_site_there", "reallife_bld_answer_site_there",
            "reallife_bld_answer_site_refused", "reallife_bld_answer_site_refused", "reallife_bld_answer_site_refused"
        };
        bool keysRight = true;
        for (int i = 0; i < whys.Length; i++)
        {
            string key = SiteRefusals.Key(whys[i]);
            keysRight &= key == want[i] && words[0].TryGetValue(key, out string text) && !text.Contains("Player.log") && text.Contains("Nothing was charged");
        }
        Check(keysRight, "site fit texts: every cause the city's programme gives for setting out no site is told in plain words, and says nothing was charged");
        // The causes are said in one place, so the words follow them.
        string[] stray =
        {
            @"Civic\CivicWorks.cs", @"Wiring\Wiring.ConstructionLinks.cs", @"Wiring\Wiring.LandmarkLinks.cs", @"Construction\ConstructionDirector.Asks.cs"
        };
        string loose = null;
        foreach (string file in stray)
        {
            string source = ModSource(file);
            if (source.Contains("\"the city's construction sites are off\"") || source.Contains("\"the host of the Going Public session runs the city's sites\"")
                || source.Contains("\"a site is there already, or the building has no street door\"") || source.Contains("\"another site is already within \"")
                || source.Contains("\"the city's construction programme is not running\""))
            {
                loose = loose ?? file;
            }
        }
        Check(loose == null, "site fit texts: the programme's causes for a refusal are written only in SiteRefusals, so each has its plain words (" + (loose ?? "all") + ")");
        string hire = ModSource(@"Construction\ConstructionDirector.Hire.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string demolish = ModSource(@"Construction\ConstructionDirector.Demolish.cs"), refits = ModSource(@"Construction\ConstructionDirector.Refits.cs");
        Check(!hire.Contains("return \"reallife_bld_answer_site_refused\"") && !projects.Contains("return \"reallife_bld_answer_site_refused\"") && !demolish.Contains("return \"reallife_bld_answer_site_refused\"")
              && !refits.Contains("= \"reallife_bld_answer_site_refused\"") && hire.Contains("SiteRefusals.Key(why)") && projects.Contains("SiteRefusals.Key(why)")
              && demolish.Contains("SiteRefusals.Key(refused)") && refits.Contains("SiteRefusals.Key(refused)"),
            "site fit texts: the hire, the start, a demolition and a refit tell the cause that stopped their site, never the one general refusal");
    }

    // A text with its {names} filled from name, value pairs.
    private static string FillWith(string text, string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return text;
    }
}
