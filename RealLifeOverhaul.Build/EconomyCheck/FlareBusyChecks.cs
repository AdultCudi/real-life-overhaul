using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;

// Checks for one campaign at a time at a business (CampaignRules.Busy, HiredOnFlare, EndEarly) and what Flare's
// Business tab and hiring page make of it (FlareRules.IsDeal, BusyKey, BusyWhenKey): a hire at one shop leaves
// another free, a campaign booked from the Company app is named with its days, a deal the player ends frees the
// business, and the words are in all three languages.
internal static partial class Program
{
    private static void FlareBusyChecks()
    {
        FlareBusyRule();
        FlareBusyEnd();
        FlareBusyRows();
        FlareBusySave();
        FlareBusyWords();
    }

    private static void FlareBusyRule()
    {
        var hell = new ShopFile { Place = "Hell%20Street~12" };
        var midtown = new ShopFile { Place = "Midtown%20Avenue~40" };
        int kind, from, to;
        bool started;
        Check(CampaignRules.CanBook(hell) && !CampaignRules.Busy(hell, 30, out kind, out from, out to, out started) && kind == -1,
            "campaigns: a business with nothing booked can book, and nothing stands in the way");

        CampaignRules.Book(hell, (int)CampaignKind.Giveaway, 500, 10, 30);
        Check(!CampaignRules.CanBook(hell) && CampaignRules.CanBook(midtown) && !CampaignRules.Busy(midtown, 30, out kind, out from, out to, out started),
            "campaigns: an influencer hired for one shop leaves another shop free to hire (the reported case)");
        Check(CampaignRules.Busy(hell, 30, out kind, out from, out to, out started) && kind == (int)CampaignKind.Giveaway && from == 31 && to == 32 && !started,
            "campaigns: a giveaway hired on day 30 is booked for days 31 to 32, not started yet (" + kind + ", " + from + " to " + to + ")");
        Check(CampaignRules.Busy(hell, 31, out kind, out from, out to, out started) && started && CampaignRules.Busy(hell, 32, out kind, out from, out to, out started) && started,
            "campaigns: it is running on its first and its last day");
        Check(CampaignRules.Over(hell, 33) && !CampaignRules.Over(hell, 32), "campaigns: it is over the day after its last day, not before");
        CampaignRules.Close(hell);
        Check(CampaignRules.CanBook(hell) && !CampaignRules.Busy(hell, 33, out kind, out from, out to, out started) && hell.PushEnded[(int)CampaignKind.Giveaway] == 33,
            "campaigns: once closed the business is free again, and the repeat counts from the day it ended");

        CampaignRules.Book(midtown, (int)CampaignKind.DiscountDay, 0, 12, 33);
        Check(CampaignRules.Busy(midtown, 33, out kind, out from, out to, out started) && kind == (int)CampaignKind.DiscountDay && from == 34 && to == 34 && !started
              && !CampaignRules.HiredOnFlare(kind) && FlareRules.BusyKey(false) == "reallife_flare_busy_company" && FlareRules.BusyWhenKey(33, from, to) == "reallife_flare_busy_booked_day",
            "campaigns: a discount day booked from the Company app on day 33 is named as booked for day 34, from the Company app");
        Check(FlareRules.BusyWhenKey(34, from, to) == "reallife_flare_busy_running", "campaigns: on its day it is named as running");

        bool every = true;
        for (int a = 0; a < CompanyCatalogue.CampaignKinds; a++)
        {
            var s = new ShopFile { Place = "x~1" };
            CampaignRules.Book(s, a, 100, 5, 10);
            every &= !CampaignRules.CanBook(s) && CampaignRules.Busy(s, 10, out kind, out from, out to, out started) && kind == a && from == 11
                     && to == 10 + CompanyCatalogue.CampaignDays[a] && to >= from && !started;
        }
        Check(every, "campaigns: every kind, booked, stands in the way of a second booking at that business until its last day, which is its length on from tomorrow");

        bool flare = true;
        for (int k = 0; k < CompanyCatalogue.CampaignKinds; k++)
        {
            flare &= CampaignRules.HiredOnFlare(k) == (k >= FlareCast.FirstDealKind);
        }
        Check(flare && !CampaignRules.HiredOnFlare((int)CampaignKind.Sponsored) && !CampaignRules.HiredOnFlare(-1) && !CampaignRules.HiredOnFlare(CompanyCatalogue.CampaignKinds),
            "campaigns: Flare's deals are hired on Flare; flyers, radio, a discount day, a refit and a sponsored post are booked from the Company app");
    }

    private static void FlareBusyEnd()
    {
        int kind, from, to;
        bool started;
        var s = new ShopFile { Place = "a~1" };
        Check(!CampaignRules.EndEarly(s, 10) && s.Push == -1, "campaigns: ending early with nothing booked does nothing");

        CampaignRules.Book(s, (int)CampaignKind.Ambassador, 900, 4, 10);
        Check(CampaignRules.EndEarly(s, 10) && CampaignRules.CanBook(s) && s.PushEnded[(int)CampaignKind.Ambassador] == 0 && s.PushCost == 0,
            "campaigns: a deal ended on the day it was hired, before it began, frees the business at once and counts as no repeat");

        CampaignRules.Book(s, (int)CampaignKind.Ambassador, 900, 4, 10);
        Check(!CampaignRules.EndEarly(s, 15) && CampaignRules.Busy(s, 15, out kind, out from, out to, out started) && from == 11 && to == 15 && started,
            "campaigns: an ambassador ended on day 15 of its four weeks runs to the end of that day only");
        Check(CampaignRules.Over(s, 16) && CampaignRules.Footfall(s, 15) == 4 && CampaignRules.Footfall(s, 16) == 0, "campaigns: it adds nothing from the next day and is over then");
        CampaignRules.Close(s);
        Check(CampaignRules.CanBook(s) && s.PushEnded[(int)CampaignKind.Ambassador] == 16, "campaigns: closed, the business is free and the repeat counts from the day it ended");

        CampaignRules.Book(s, (int)CampaignKind.Flyers, 100, 5, 10);
        int end = s.PushEnd;
        CampaignRules.EndEarly(s, 40);
        Check(s.PushEnd == end, "campaigns: ending early never makes a campaign longer");
    }

    private static void FlareBusyRows()
    {
        FlareFile f = FlareRules.Open(3, 1, 0, 50);
        var h = new FlareHire { Id = 1, Kind = (int)CampaignKind.Giveaway, Place = "Hell%20Street~12", Name = "Hell Electronics", From = 31, To = 32 };
        f.Hires.Add(h);
        Check(FlareRules.IsDeal(f, "Hell%20Street~12", (int)CampaignKind.Giveaway, 31), "Flare: the company's campaign that is a deal on Flare has its row as the deal");
        Check(!FlareRules.IsDeal(f, "Midtown%20Avenue~40", (int)CampaignKind.Giveaway, 31) && !FlareRules.IsDeal(f, "Hell%20Street~12", (int)CampaignKind.Giveaway, 40)
              && !FlareRules.IsDeal(f, "Hell%20Street~12", (int)CampaignKind.Radio, 31),
            "Flare: a campaign at another business, from another day or of another kind gets a row of its own");
        h.Ended = true;
        Check(!FlareRules.IsDeal(f, "Hell%20Street~12", (int)CampaignKind.Giveaway, 31), "Flare: a campaign whose deal Flare has ended shows as a campaign, so what keeps the business busy is always on the tab");
        Check(!FlareRules.IsDeal(null, "a~1", 5, 1), "Flare: with no account, no campaign is a deal");
    }

    private static void FlareBusySave()
    {
        var state = new CompanyState();
        var booked = new ShopFile { Place = "Midtown%20Avenue~40" };
        var free = new ShopFile { Place = "Hell%20Street~12" };
        CampaignRules.Book(booked, (int)CampaignKind.Refit, 2000, 3, 50);
        state.Shops.Add(booked);
        state.Shops.Add(free);
        int version;
        CompanyState back = CompanyStore.Read(CompanyStore.Write(state), out version);
        ShopFile b = back != null ? back.Shop("Midtown%20Avenue~40") : null, f = back != null ? back.Shop("Hell%20Street~12") : null;
        Check(b != null && f != null && b.Push == (int)CampaignKind.Refit && b.PushFrom == 51 && b.PushEnd == 71 && !CampaignRules.CanBook(b) && CampaignRules.CanBook(f),
            "campaigns: through the save, the business with a refit booked stays booked and the other stays free");
    }

    private static void FlareBusyWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var langs = new[] { LoadWords(System.IO.Path.Combine(folder, "en.json")), LoadWords(System.IO.Path.Combine(folder, "pt.json")), LoadWords(System.IO.Path.Combine(folder, "zh-cn.json")) };
        Check(langs[0] != null && langs[1] != null && langs[2] != null, "Flare busy: the three locales are where the checks look for them (" + folder + ")");
        if (langs[0] == null || langs[1] == null || langs[2] == null)
        {
            return;
        }
        var need = new Dictionary<string, string[]>
        {
            { FlareRules.BusyKey(false), new[] { "{business}", "{when}", "{campaign}" } },
            { FlareRules.BusyKey(true), new[] { "{business}", "{when}", "{campaign}" } },
            { FlareRules.BusyWhenKey(5, 5, 9), new[] { "{to}" } },
            { FlareRules.BusyWhenKey(4, 5, 5), new[] { "{day}" } },
            { FlareRules.BusyWhenKey(4, 5, 9), new[] { "{from}", "{to}" } },
            { "reallife_flare_row_campaign", new[] { "{business}", "{campaign}" } },
            { "reallife_flare_campaign_company", new string[0] },
            { "reallife_flare_campaign_on_flare", new string[0] },
            { "reallife_flare_campaign_where", new string[0] }
        };
        var missing = new List<string>();
        foreach (KeyValuePair<string, string[]> k in need)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                string text;
                if (!langs[l].TryGetValue(k.Key, out text) || string.IsNullOrEmpty(text))
                {
                    missing.Add(k.Key + " (" + l + ")");
                    continue;
                }
                for (int p = 0; p < k.Value.Length; p++)
                {
                    if (text.IndexOf(k.Value[p], StringComparison.Ordinal) < 0)
                    {
                        missing.Add(k.Key + " " + k.Value[p] + " (" + l + ")");
                    }
                }
            }
        }
        Check(missing.Count == 0, "Flare busy: every key of the busy line and the campaign rows is in en, pt and zh-cn with its names (" + string.Join(", ", missing) + ")");
        string line = langs[0][FlareRules.BusyKey(false)].Replace("{business}", "Midtown Electronics").Replace("{campaign}", langs[0]["reallife_co_campaign_discount"])
            .Replace("{when}", langs[0][FlareRules.BusyWhenKey(33, 34, 34)].Replace("{day}", "34"));
        Check(line == "Midtown Electronics has a campaign booked for day 34: Discount day, booked from the Company app. You can hire once it ends.",
            "Flare busy: the hiring page names the campaign, how it was booked and its day (" + line + ")");
    }
}
