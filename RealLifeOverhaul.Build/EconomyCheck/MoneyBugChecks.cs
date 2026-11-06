using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;

// Faults the Guide audit found in the money and the economy: the finance minimums written as bare numbers beside every
// other minimum that follows prices; the taxi contact writing the name of an option instead of reading it; the franchise
// refusal speaking of one brand when the limit counts every brand's shops; the City Ledger speaking of kiosks with the
// option off; a trading licence renewal charged on top of the Company app's licences; a catch sold at the start's prices;
// and a policy text writing a score the rule has its own figure for. (The council's rent policy read the wrong way round
// is checked with the rents, in EstateChecks.cs.)
internal static partial class Program
{
    private static void MoneyBugs()
    {
        FinanceLeastFollowsPrices();
        MoneyBugWords();
        MoneyBugSources();
    }

    private static void FinanceLeastFollowsPrices()
    {
        Func<int> was = Prices.Factor;
        try
        {
            Prices.Factor = null;
            Check(ProductCatalogue.CarLeast == 500 && ProductCatalogue.HomeLeast == 5000 && ProductCatalogue.CardDrawLeast == 20
                  && ProductTerms.FinanceLeast(ProductKind.CarFinance) == 500 && ProductTerms.FinanceLeast(ProductKind.Mortgage) == 5000,
                "finance minimums: at the start's prices a car's finance starts at 500, a mortgage at 5,000 and a card draw at 20");
            int level = 1500;
            Prices.Factor = () => level;
            Check(ProductCatalogue.CarLeast == PriceRules.At(500, level) && ProductCatalogue.HomeLeast == PriceRules.At(5000, level) && ProductCatalogue.CardDrawLeast == PriceRules.At(20, level)
                  && ProductTerms.FinanceLeast(ProductKind.CarFinance) == 750 && ProductTerms.FinanceLeast(ProductKind.Mortgage) == 7500 && ProductCatalogue.CardDrawLeast == 30
                  && ProductCatalogue.CardLeast == PriceRules.At(300, level),
                "finance minimums: with prices half as high again the least finance on a car, on a home and the least card draw move with them, as the card's own least does");
        }
        finally
        {
            Prices.Factor = was;
        }
    }

    private static string[] MoneyBugLocales = { "en.json", "pt.json", "zh-cn.json" };

    private static void MoneyBugWords()
    {
        var words = new Dictionary<string, Dictionary<string, string>>();
        foreach (string file in MoneyBugLocales)
        {
            words[file] = LoadWords(System.IO.Path.Combine(DesignLocales, file));
        }
        bool loaded = true;
        foreach (var pair in words)
        {
            loaded &= pair.Value != null;
        }
        Check(loaded, "money fixes: the three locales are where the checks look for them");
        if (!loaded)
        {
            return;
        }
        bool taxi = true, full = true, ledger = true, licences = true, region = true;
        foreach (string file in MoneyBugLocales)
        {
            Dictionary<string, string> w = words[file];
            taxi &= w["reallife_taxi_refuse_off"].Contains("{option}") && w.ContainsKey("reallife_transport_taxi")
                    && !w["reallife_taxi_refuse_off"].Contains(w["reallife_transport_taxi"]);
            full &= w["reallife_brand_answer_full"].Contains("{most}");
            licences &= w["reallife_biz_why_company_licences"].Contains("{issue}") && w["reallife_biz_why_company_licences"].Contains("{business}");
            region &= w["reallife_pol_field_region_replaces_what"].Contains("{score}") && !w["reallife_pol_field_region_replaces_what"].Contains("50");
            ledger &= w["reallife_ledger_desk_offer_plain"].Contains("{address}") && w["reallife_ledger_desk_offer_plain"].Contains("{price}") && !w["reallife_ledger_desk_offer_plain"].Contains("{copy}")
                      && w["reallife_ledger_ad_body_plain"].Contains("{price}")
                      && w["reallife_ledger_desk_no_home_plain"].Length > 0;
        }
        Dictionary<string, string> en = words["en.json"], pt = words["pt.json"], zh = words["zh-cn.json"];
        Check(taxi, "taxi: the refusal for the option being off takes the option's name from the options page and does not write it");
        Check(full, "brands: the refusal for the franchise limit says the limit");
        Check(licences, "business: the licence renewal's refusal in a test names the issue and the business");
        Check(region, "policy: the regional manager's text takes the score from the rule, not from the words");
        Check(ledger, "City Ledger: the words without kiosks have the address and price and say nothing of copies sold at kiosks");
        Check(!en["reallife_ledger_desk_offer_plain"].Contains("kiosk") && !en["reallife_ledger_desk_no_home_plain"].Contains("kiosk") && !en["reallife_ledger_ad_body_plain"].Contains("kiosk")
              && !pt["reallife_ledger_desk_offer_plain"].Contains("banca") && !pt["reallife_ledger_desk_no_home_plain"].Contains("banca") && !pt["reallife_ledger_ad_body_plain"].Contains("banca")
              && !zh["reallife_ledger_desk_offer_plain"].Contains("报亭") && !zh["reallife_ledger_desk_no_home_plain"].Contains("报亭") && !zh["reallife_ledger_ad_body_plain"].Contains("报亭"),
            "City Ledger: the words for kiosks switched off never mention a kiosk, in any of the three languages");
        Check(en["reallife_brand_answer_full"].Contains("brands together") && en["reallife_taxi_refuse_off"].Contains("\"{option}\""),
            "money fixes: the franchise limit is said to count every brand together, and the taxi's refusal quotes the option");

        // What the words are filled with, and that each other language lost the key it could no longer read.
        Check(LedgerCatalogue.OfferKey(true) == "reallife_ledger_desk_offer" && LedgerCatalogue.OfferKey(false) == "reallife_ledger_desk_offer_plain"
              && LedgerCatalogue.NoHomeKey(true) == "reallife_ledger_desk_no_home" && LedgerCatalogue.NoHomeKey(false) == "reallife_ledger_desk_no_home_plain"
              && LedgerCatalogue.AdBodyKey(true) == "reallife_ledger_ad_body" && LedgerCatalogue.AdBodyKey(false) == "reallife_ledger_ad_body_plain"
              && en.ContainsKey(LedgerCatalogue.OfferKey(false)) && en.ContainsKey(LedgerCatalogue.NoHomeKey(false)) && en.ContainsKey(LedgerCatalogue.AdBodyKey(false)),
            "City Ledger: with the kiosks option off the desk's offer, its no-home line and the front page's notice are the words without kiosks, and they exist");
    }

    private static void MoneyBugSources()
    {
        string taxi = ModSource(@"Transport\Taxi\TaxiPhone.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(taxi.Contains("\"reallife_taxi_refuse_off\", new { option = OptionKey.Localize() }") || taxi.Contains("\"reallife_taxi_refuse_off\", new { option = TaxiPhone.OptionKey.Localize() }"),
            "taxi: the refusal is filled with the option's own text");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(taxi, "Closing\\(\"reallife_taxi_refuse_off\"\\)") && taxi.Contains("OptionKey = \"reallife_transport_taxi\"")
              && mod.Contains("AddToggle(Settings.TaxiCallId, \"reallife_transport_taxi\","),
            "taxi: no path says the refusal without the option, and the refusal reads the label the options page gives the option");

        string brands = ModSource(@"Economy\Brands\BrandsDirector.Franchises.cs"), text = ModSource(@"Economy\Brands\BrandsText.cs");
        Check(brands.Contains("BrandsText.RefusalKeys[(int)why], BrandsText.RefusalData())") && text.Contains("\"most\", BrandCatalogue.FranchisesMost"),
            "brands: both the notice and the page that give the refusal fill in the limit");

        string desk = ModSource(@"Ledger\LedgerDesk.cs"), front = ModSource(@"Ledger\LedgerPage.Front.cs");
        Check(desk.Contains("LedgerCatalogue.OfferKey(Settings.LedgerKiosks)") && desk.Contains("LedgerCatalogue.NoHomeKey(Settings.LedgerKiosks)") && front.Contains("LedgerCatalogue.AdBodyKey(Settings.LedgerKiosks)")
              && !desk.Contains("\"reallife_ledger_desk_offer\"") && !front.Contains("\"reallife_ledger_ad_body\""),
            "City Ledger: every place that speaks of kiosks asks the option first");

        // A trading licence renewal beside the Company app's licences would charge the same thing twice.
        Check(BusinessCatalogue.CompanyKeepsLicences(true, true, true) && !BusinessCatalogue.CompanyKeepsLicences(false, true, true)
              && !BusinessCatalogue.CompanyKeepsLicences(true, false, true) && !BusinessCatalogue.CompanyKeepsLicences(true, true, false),
            "business: the licence renewal stands aside only while the economy, the Company app and its licences are all on");
        string issues = ModSource(@"Business\BusinessIssues.cs");
        Check(issues.Contains("BusinessCatalogue.CompanyKeepsLicences(Settings.Economy, Settings.EconomyCompany, Settings.EconomyCompanyLicences) ? \"reallife_biz_why_company_licences\""),
            "business: whether a licence renewal can start asks that first, for the director's hour and the test alike");

        string fishing = ModSource(@"Leisure\Activities\Fishing.cs");
        Check(fishing.Contains("Mathf.RoundToInt(Prices.At(_grams / 1000f * _fish.ValuePerKilo))") && !fishing.Contains("Mathf.RoundToInt(_grams / 1000f * _fish.ValuePerKilo)"),
            "fishing: a catch is sold at the city's prices, as the bait is bought");
        Check(PriceRules.At(8f * 0.45f, 1500) > PriceRules.At(8f * 0.45f, 1000) && PriceRules.At(0f, 1500) == 0f,
            "fishing: a catch's value moves with prices, and an old boot is still worth nothing");

        string hierarchy = ModSource(@"Economy\Company\CompanyApp.Hierarchy.cs");
        Check(hierarchy.Contains("\"score\", RegionCatalogue.ReplaceScore.ToString()") && RegionCatalogue.ReplaceScore == 50,
            "policy: the text of the regional manager rule is filled with the score the rule uses");
    }
}
