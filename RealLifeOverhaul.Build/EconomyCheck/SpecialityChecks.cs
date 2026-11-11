using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Ui;

// The construction company's speciality (Scripts/Construction: FirmRules.SpecialityWait and ChooseSpeciality, ConstructionDirector.SetSpeciality, the Company tab's speciality page in the
// old app and on the kit): the first choice has no wait and any of the five can be the first; a later one waits the rest from the last; asking for what the company is known for spends
// nothing; the page lists every other speciality as a choice of its own and asks in the game's question box, never stepping from one speciality to the next; founding opens that page; the
// texts (dotnet EconomyCheck.dll --builders runs them with the construction game's).
internal static partial class Program
{
    private static void SpecialityChecks()
    {
        SpecialityRuleChecks();
        SpecialityPageChecks();
        SpecialityTextChecks();
    }

    private static void SpecialityRuleChecks()
    {
        int rest = BuildCatalogue.SpecialityRest;
        bool first = true;
        for (int day = 1; day <= 400; day += 3)
        {
            first &= FirmRules.SpecialityWait(0, day) == 0;
            for (int s = 1; s < (int)Speciality.Count; s++)
            {
                first &= FirmRules.ChooseSpeciality(Speciality.None, 0, (Speciality)s, day) == SpecialityChoice.Set;
            }
        }
        Check(first, "speciality: a company that has chosen nothing may take any of the five at once, on any day: the first choice has no wait");
        Check(FirmRules.ChooseSpeciality(Speciality.None, 0, Speciality.None, 12) == SpecialityChoice.Same && FirmRules.ChooseSpeciality(Speciality.Roads, 40, Speciality.Roads, 41) == SpecialityChoice.Same
              && FirmRules.SpecialityWait(0, 12) == 0,
            "speciality: asking for the speciality the company has already sets nothing and starts no wait, so the first choice is still its own after it");
        bool counted = true;
        for (int day = 10; day < 10 + rest; day++)
        {
            counted &= FirmRules.SpecialityWait(10, day) == rest - (day - 10) && FirmRules.ChooseSpeciality(Speciality.Homes, 10, Speciality.Offices, day) == SpecialityChoice.Waits;
        }
        Check(counted && FirmRules.SpecialityWait(10, 10 + rest) == 0 && FirmRules.ChooseSpeciality(Speciality.Homes, 10, Speciality.Offices, 10 + rest) == SpecialityChoice.Set
              && FirmRules.SpecialityWait(10, 10 + rest + 200) == 0 && FirmRules.SpecialityWait(10, 3) == rest,
            "speciality: a change waits " + rest + " days from the last choice, a day less each day, is free on the day the wait ends, and is never made to wait longer than that");
        // Why a list and not arrows: arrows set each speciality they stepped to. From a new company the first step set Homes and began the wait, and the second, to Offices, was
        // refused, so the fifth speciality was out of reach for the days of the wait; picked from a list it is the first choice, and is set.
        Speciality now = Speciality.None;
        int since = 0, reached = 0;
        for (int step = 1; step < (int)Speciality.Count; step++)
        {
            var next = (Speciality)step;
            if (FirmRules.ChooseSpeciality(now, since, next, 20) != SpecialityChoice.Set)
            {
                break;
            }
            now = next;
            since = 20;
            reached = step;
        }
        Check(reached == 1 && now == Speciality.Homes && FirmRules.ChooseSpeciality(Speciality.None, 0, Speciality.Demolition, 20) == SpecialityChoice.Set,
            "speciality: stepping from the first to the next stops at Homes inside the wait, where a pick of the fifth is the first choice and is set");
    }

    private static void SpecialityPageChecks()
    {
        string actions = ModSource(@"Construction\ConstructionDirector.Actions.cs");
        string setter = MethodText(actions, "public string SetSpeciality(Speciality s)");
        Check(setter.Contains("f.Founded <= 0") && setter.Contains("FirmRules.ChooseSpeciality(f.Speciality, f.SpecialSince, s, day)") && setter.Contains("f.SpecialSince = day;")
              && setter.Contains("case SpecialityChoice.Waits:") && !setter.Contains("SpecialityRest"),
            "speciality sources: the director sets it through the one rule, only for a founded company and from the day asked, and holds no wait of its own");
        string[] pages = { @"Construction\ConstructionApp.Company.cs", @"Construction\Live\ConstructionLive.Company.cs" };
        string[] overviews = { @"Construction\ConstructionApp.Overview.cs", @"Construction\Live\ConstructionLive.Overview.cs" };
        string[] foundButtons = { "\"reallife_bld_button_found\", Found);", "UiAction.Alt, Found));" };
        for (int p = 0; p < pages.Length; p++)
        {
            string who = p == 0 ? "the old app" : "the kit's app";
            string page = ModSource(pages[p]), overview = ModSource(overviews[p]);
            string write = MethodText(page, "private void WriteSpeciality(");
            string ask = MethodText(page, "private void AskSpeciality(Speciality s)");
            string found = MethodText(page, "private void Found()");
            Check(write.Length > 0 && !write.Contains("Selector(") && !write.Contains("SetSpeciality(") && write.Contains("for (int i = 0; i < (int)Speciality.Count; i++)")
                  && write.Contains("i == (int)f.Speciality") && write.Contains("AskSpeciality(chosen)"),
                "speciality sources: " + who + " lists every other speciality as a choice of its own, each asking first, and steps from one to the next in no way");
            Check(ask.Contains("Confirm(\"reallife_bld_confirm_speciality\", \"reallife_bld_confirm_speciality_body\"") && ask.Contains("\"days\", BuildCatalogue.SpecialityRest.ToString()")
                  && ask.Contains(".SetSpeciality(s)") && Occurrences(page, "SetSpeciality(") == 1,
                "speciality sources: " + who + " sets it only after the game's question box that names the speciality and the days of the wait");
            Check(found.Contains("Founded > 0") && found.Contains("\"co:speciality\"") && found.Contains("ConstructionDirector.TabCompany") && Occurrences(page, ".Found()") == 1
                  && Occurrences(overview, ".Found()") == 0 && overview.Contains(foundButtons[p]) && Occurrences(page, "\"reallife_bld_button_found\", Found);") == 1,
                "speciality sources: " + who + " founds the company in one place, from the overview and from the office's page, and opens the speciality's page to make the first choice");
        }
        Check(ModSource(@"Construction\Live\ConstructionLive.Box.cs").Contains("public void Link(string words, Action click) => _lines.Add(_runs.Link(words, click));"),
            "speciality sources: the kit's page can write a line that is pressed");
        ConstructionModel sample = UiSamples.Construction((k, e) => e, "speciality");
        int links = 0, steps = 0;
        foreach (UiFactCardModel card in sample.Cards)
        {
            foreach (UiFactLine line in card.Lines)
            {
                links += line.Kind == UiFactKind.Link ? 1 : 0;
                steps += line.Kind == UiFactKind.Step ? 1 : 0;
            }
        }
        Check(Array.IndexOf(UiSamples.ConstructionStates, "speciality") >= 0 && sample.Shape == ConstructionShape.Detail && links == (int)Speciality.Count - 1 && steps == 0,
            "speciality sources: the kit's sample of the page lays the other " + ((int)Speciality.Count - 1) + " specialities out as lines of their own and has no step in it");
    }

    private static void SpecialityTextChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_bld_row_change_again", "reallife_bld_speciality_first", "reallife_bld_speciality_change", "reallife_bld_confirm_speciality", "reallife_bld_confirm_speciality_body"
        };
        string lacking = string.Empty, styled = string.Empty;
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
            {
                lacking += " " + key;
            }
            else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
            {
                styled += " " + key;
            }
        }
        Check(lacking.Length == 0 && styled.Length == 0, "speciality texts: the page's and the question's words are in en.json, with no exclamation mark or dash (lacking:" + (lacking.Length == 0 ? " none" : lacking)
                                                         + "; styled:" + (styled.Length == 0 ? " none" : styled) + ")");
        en.TryGetValue("reallife_bld_confirm_speciality_body", out string body);
        Check(body != null && body.Contains("{name}") && body.Contains("{days}"), "speciality texts: the question names the speciality and the days of the wait");
        Check(WhatsNewHas(en, "speciality is now chosen from a list of all of them"), "speciality texts: What's new tells of the list, in some row of it (the version step moves the line)");
    }
}
