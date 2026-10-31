using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Insure all, in the Garage phone contact: one way to buy a policy, shared by a car's page and Insure all, so the premium
// and the dates cannot differ; Insure all insures every uninsured car or none; the summary opens only with two cars or
// more and one uninsured; and its words. The dialogue is the game's, so what it does is checked in its source.
internal static partial class Program
{
    private static void InsureAll()
    {
        InsureAllOneWay();
        InsureAllOrNone();
        InsureAllWords();
    }

    private static string TransportSource(string file)
    {
        string path = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Transport", file);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    // Every source file of Scripts in which a text stands, with how often.
    private static List<string> ScriptsWith(string text)
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts"));
        var found = new List<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            int n = Regex.Matches(System.IO.File.ReadAllText(path), Regex.Escape(text)).Count;
            if (n > 0)
            {
                found.Add(System.IO.Path.GetFileName(path) + (n > 1 ? " x" + n : string.Empty));
            }
        }
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static void InsureAllOneWay()
    {
        string phone = TransportSource("CarPhone.cs");
        string insure = MemberText(phone, "public static float Insure(VehicleInstance car, CarRecord record, bool notify)");
        Check(insure.Contains("PremiumOf(car, record)") && insure.Contains("reallife_transaction_car_insurance") && insure.Contains("force: false")
              && insure.Contains("record.InsuredUntil = TimeHelper.CurrentDay + CarRules.PolicyDays - 1;")
              && insure.IndexOf("BizBooks.Book", StringComparison.Ordinal) < insure.IndexOf("record.InsuredUntil =", StringComparison.Ordinal),
            "insure all: CarPhone.Insure books the car's own premium first and sets the policy's dates only once it is paid");
        List<string> premiums = ScriptsWith("CarRules.PremiumOf(");
        Check(string.Join(", ", premiums) == "CarOwnership.Tests.cs, CarPhone.cs",
            "insure all: a car's premium is worked out in CarPhone.PremiumOf alone (and the test's figures), so the page, Insure all and the renewal charge alike (" + string.Join(", ", premiums) + ")");
        List<string> dates = ScriptsWith("+ CarRules.PolicyDays - 1");
        Check(string.Join(", ", dates) == "CarOwnership.cs, CarPhone.cs" && Regex.Matches(phone, Regex.Escape("+ CarRules.PolicyDays - 1")).Count == 1,
            "insure all: a policy bought is dated in CarPhone.Insure alone, a renewal in CarOwnership's new day (" + string.Join(", ", dates) + ")");
        List<string> booked = ScriptsWith("\"reallife_transaction_car_insurance\"");
        Check(string.Join(", ", booked) == "CarOwnership.cs, CarPhone.cs" && Regex.Matches(phone, "\"reallife_transaction_car_insurance\"").Count == 1,
            "insure all: a premium is booked in CarPhone.Insure and the renewal only, one line a car each (" + string.Join(", ", booked) + ")");
        string renewal = TransportSource("CarOwnership.cs");
        Check(renewal.Contains("float premium = CarPhone.PremiumOf(instance, record);"), "insure all: the monthly renewal charges CarPhone.PremiumOf, the premium the Garage page shows");
        Check(phone.Contains("entry.OnConfirm = () => CarPhone.Insure(car, record, true) < 0f"), "insure all: a car's own page buys its policy through CarPhone.Insure");
    }

    private static void InsureAllOrNone()
    {
        string phone = TransportSource("CarPhone.cs");
        string all = MemberText(phone, "public static DialogEntry InsureAll(GameInstance save)");
        int short_ = all.IndexOf("if (save.Money < total)", StringComparison.Ordinal);
        int first = all.IndexOf("Insure(car, record, false)", StringComparison.Ordinal);
        Check(short_ > 0 && first > short_ && all.Contains("\"reallife_garage_all_short\""),
            "insure all: the money is counted against every premium before the first car is insured, and short of it no car is");
        Check(all.Contains("Uninsured(save, today,") && all.Contains("if (!Runs(car))") && all.Contains("if (record.InsuredUntil >= today)"),
            "insure all: it insures exactly the Garage's cars (Runs over the save's own list) that have no policy today");
        Check(all.Contains("Meta.Dev.Verbose") && all.Contains("Car check (insure all)") && all.Contains("PASS") && all.Contains("\"reallife_garage_all_part\""),
            "insure all: one check line a press with Developer diagnostics on, and a part done (the money moved) said so to the player");
        string uninsured = MemberText(phone, "public static int Uninsured(GameInstance save, int today, out int cars, out float total)");
        Check(uninsured.Contains("save.VehicleInstances") && uninsured.Contains("if (!Runs(car))") && uninsured.Contains("total += PremiumOf(car, record);"),
            "insure all: the summary counts the save's own cars as the pages do (in a Going Public session another player's car is not in the save) at the premium charged");
        string first2 = MemberText(phone, "private static DialogEntry First()");
        Check(first2.Contains("if (cars < 2 || uninsured == 0)") && first2.Contains("return Page(0);") && first2.Contains("SecondOptionTextOverride = \"reallife_garage_go_through\"")
              && first2.Contains("OnConfirm = () => CarPhone.InsureAll(SaveGameManager.Current)") && first2.Contains("OnCancel = PhoneContacts.HangUp"),
            "insure all: the summary opens only with two cars or more and one uninsured; its three buttons are Insure all, the cars one by one and hang up");
        Check(phone.Contains("Settings.CarOwnership ? First() :"), "insure all: the summary is behind the car ownership option, as the pages are");
    }

    private static void InsureAllWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("insure all", langs, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_garage_summary", "reallife_garage_insure_all", "reallife_garage_go_through", "reallife_garage_all_short", "reallife_garage_all_done",
            "reallife_garage_all_part", "reallife_garage_all_none"
        };
        var missing = new List<string>();
        bool plain = true;
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
            for (int l = 0; l < langs.Length; l++)
            {
                plain &= langs[l].TryGetValue(key, out string t) && !t.Contains("!") && !t.Contains("—") && !t.Contains("–") && t.Length <= 240;
            }
        }
        Check(missing.Count == 0, "insure all: its texts are in every language with the same {names} (" + string.Join(", ", missing) + ")");
        Check(plain, "insure all: its texts have no dash or exclamation mark and keep to 240 characters");
        Check(langs[0]["reallife_garage_summary"].Contains("{amount}") && langs[0]["reallife_garage_insure_all"].Contains("{amount}")
              && langs[0]["reallife_garage_all_short"].Contains("{more}"),
            "insure all: the summary and its button give the total, and the not-enough line how much more is needed");
        bool guide = true;
        string[] mention = { "Insure all", "Fazer seguro de todos", "全部投保" };
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            guide &= langs[l].TryGetValue("reallife_guide_transport", out string g) && g.Contains(mention[l]);
        }
        Check(guide, "insure all: the Guide's car page names Insure all in English, Portuguese and Chinese");
    }
}
