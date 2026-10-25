using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Utilities;

// The utilities with no office to visit (a player owning every building that could hold it): the app takes the desk's
// work by phone on the desk's own rules, every text that sends the player to the office has a twin that sends them to
// the app, and the office is looked for again so it comes back once a building is free.
internal static partial class Program
{
    private static void UtilitiesPhone()
    {
        // ---------------------------------------------------------------- what the desk, or the phone, can do
        Check(UtilitiesRules.Offer(null) == AccountOffer.None, "utilities by phone: no account, nothing to offer");
        var t = UtilitiesRules.Temporary("Dock%20Road~3", Service.Power, PlaceKind.Retail, 30);
        Check(UtilitiesRules.Offer(t) == AccountOffer.Apply, "utilities by phone: a temporary supply is offered an account");
        UtilitiesRules.Cut(t, 33);
        Check(UtilitiesRules.Offer(t) == AccountOffer.Apply, "utilities by phone: a temporary supply run out and cut is still offered an account, with no fee");
        var d = UtilitiesRules.Rented("Main%20Street~12", Service.Water, PlaceKind.Home, 10);
        Check(UtilitiesRules.Offer(d) == AccountOffer.None, "utilities by phone: a supplied account is offered nothing");
        d.Owed = 40;
        UtilitiesRules.Cut(d, 20, true);
        Check(UtilitiesRules.Offer(d) == AccountOffer.OwedFirst, "utilities by phone: cut for a bill not paid, the bill comes first");
        UtilitiesRules.Paid(d, 21);
        Check(UtilitiesRules.Offer(d) == AccountOffer.Reconnect, "utilities by phone: paid, it is offered reconnection");
        Check(UtilitiesRules.Apply(d, 21) && UtilitiesRules.Offer(d) == AccountOffer.None && d.ConnectDay == 21 + UtilitiesCatalogue.ConnectDays,
            "utilities by phone: applied for, it connects as at the desk and is offered nothing more");
        var net = UtilitiesRules.Temporary("Dock%20Road~3", Service.Internet, PlaceKind.Retail, 30);
        Check(UtilitiesRules.Offer(net) == AccountOffer.None, "utilities by phone: the internet is bought from its provider, never at the desk");
        Check(UtilitiesRules.Where("reallife_util_toast_cut", false) == "reallife_util_toast_cut" && UtilitiesRules.Where("reallife_util_toast_cut", true) == "reallife_util_toast_cut_app",
            "utilities by phone: a text sends the player to the office, or its twin to the app");

        // ---------------------------------------------------------------- every text that names the office has its twin
        var keys = new List<string>();
        foreach (string file in new[] { "Utilities/UtilitiesDirector.cs", "Utilities/UtilitiesDirector.Bills.cs", "Utilities/UtilitiesApp.cs", "Wiring/UtilityLinks.cs", "Home/HomeHealth.cs", "Tech/TechText.cs" })
        {
            string source = TextsSource(file);
            foreach (Match m in Regex.Matches(source, "Where\\(\"(reallife_[a-z_]+)\""))
            {
                if (!keys.Contains(m.Groups[1].Value))
                {
                    keys.Add(m.Groups[1].Value);   // chosen by Where outright; those chosen in a ternary, or by HomeTidy.CutKey, are named below
                }
            }
        }
        foreach (string k in new[] { "reallife_util_toast_first_owned", "reallife_util_toast_bought", "reallife_util_toast_temporary_over", "reallife_util_toast_cut",
                     "reallife_util_box_office", "reallife_util_box_apply_temporary", "reallife_util_box_apply_cut", "reallife_util_closed_apply", "reallife_util_plan_temporary_what",
                     "reallife_home_no_power", "reallife_home_no_water", "reallife_tech_idle_no_power" })
        {
            if (!keys.Contains(k))
            {
                keys.Add(k);
            }
        }
        Check(keys.Count >= 12, "utilities by phone: the texts that send the player to the office are found in the sources (" + keys.Count + ")");
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            foreach (string k in keys)
            {
                string office = LocaleText(code, k), phone = LocaleText(code, k + UtilitiesRules.PhoneKey);
                bool fits = office != null && phone != null && phone.Length <= 240;
                foreach (Match m in Regex.Matches(phone ?? string.Empty, "\\{[a-z]+\\}"))
                {
                    fits &= office != null && office.Contains(m.Value);   // the twin is filled with the same names
                }
                Check(fits, "utilities by phone: " + k + " and its twin for the app are in " + code + ", the twin with only the names the office's text has, at most 240 characters");
            }
            if (code == "en")
            {
                foreach (string k in keys)
                {
                    string phone = LocaleText(code, k + UtilitiesRules.PhoneKey) ?? string.Empty;
                    Check(!phone.Contains("in person") && phone.Contains("app"), "utilities by phone: " + k + "'s twin sends the player to the app, never in person");
                }
            }
            foreach (string k in new[] { "reallife_util_box_apply", "reallife_util_box_reconnect", "reallife_util_app_in_person", "reallife_util_toast_office_none", "reallife_util_toast_office_back",
                         "reallife_util_test_phone", "reallife_util_test_phone_pass", "reallife_util_test_phone_fail", "reallife_util_test_phone_none" })
            {
                string text = LocaleText(code, k);
                Check(!string.IsNullOrEmpty(text) && text.Length <= 240, "utilities by phone: " + k + " is in " + code + ", at most 240 characters");
            }
        }

        // ---------------------------------------------------------------- the paths: the app's phone line, the desk, the office coming back
        string app = TextsSource("Utilities/UtilitiesApp.cs"), desk = TextsSource("Utilities/UtilitiesDesk.cs"), bills = TextsSource("Utilities/UtilitiesDirector.Bills.cs");
        string officeSource = TextsSource("Utilities/UtilitiesOffice.cs"), director = TextsSource("Utilities/UtilitiesDirector.cs");
        Check(app.Contains("UtilitiesRules.Offer(a)") && desk.Contains("UtilitiesRules.Offer(a)"), "utilities by phone: the app and the desk offer an account by the same rule");
        Check(app.Contains("phone && (offer == AccountOffer.Apply || offer == AccountOffer.Reconnect)") && app.Contains("_director.ApplyByPhone(a, out string why)"),
            "utilities by phone: the app's button is there only while there is no office, and goes through the phone's path");
        int byPhone = bills.IndexOf("public bool ApplyByPhone(", StringComparison.Ordinal);
        string phonePath = byPhone >= 0 ? bills.Substring(byPhone, Math.Min(400, bills.Length - byPhone)) : string.Empty;
        Check(phonePath.Contains("if (!ByPhone)") && phonePath.Contains("return Apply(a, out why, true);"),
            "utilities by phone: the phone takes an application only while there is no office, at the desk's prices (the same Apply)");
        Check(officeSource.Contains("if (Searched && Building == null)") && officeSource.Contains("LookAgainSeconds") && officeSource.Contains("public bool ByPhone => !Usable;"),
            "utilities by phone: with no building, the office is looked for again, so it comes back once one is free");
        Check(director.Contains("_office.Tick();   // looked for before the first notices") && director.Contains("OfficeWatch();"),
            "utilities by phone: the office is looked for before the first notices, and a change between office and phone is told");
    }
}
