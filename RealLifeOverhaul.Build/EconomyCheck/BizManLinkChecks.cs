using System;
using System.Collections.Generic;

// The Company app's "Open in BizMan" (Scripts/Economy/Company): every business's page in the app has the button, and it
// opens the game's own BizMan on that business the way the game's own buttons do (BizMan.Open with the business's address),
// only for a business of the player's own, with the game's notice when it cannot. Read from the sources, as the app is
// game UI the checker cannot run.
internal static partial class Program
{
    private static void BizManLink()
    {
        string app = CompanySource("CompanyApp.cs");
        string game = CompanySource("CompanyGame.cs");

        // Every business is a row of the Overview that can be picked, and picking one writes its page.
        string overview = CompanyMethod(app, "private string WriteOverview(");
        Check(InOrder(overview, "for (int i = 0; i < shops.Count; i++)", "Pick(\"shop:\" + shop.File.Place", "picked = id == selected ? shop : picked;", "WriteShop(box, state, picked, day);"),
            "bizman link: every business is a row of the Company app that opens its page");

        // The page has the button whatever else is on, before the sale button and the way back.
        string page = CompanyMethod(app, "private void WriteShop(");
        Check(InOrder(page, "BuildingRegistration reg = shop.Reg;", "InfoBox.Button(buttons, InfoBox.Positive, \"reallife_co_open_bizman\", () => OpenBizMan(reg));",
                "if (ServicesDirector.CanSell)", "BackButton(box);"),
            "bizman link: each business's page has Open in BizMan, not behind another part's switch");
        Check(!page.Contains("() => OpenBizMan(shop") && !page.Contains("OpenBizMan(shop.Reg)"),
            "bizman link: the click keeps the business it was written for, not the director's shop that is used again");

        // A click that cannot open BizMan says so with the game's own notice.
        string click = CompanyMethod(app, "private static void OpenBizMan(BuildingRegistration reg)");
        Check(InOrder(click, "if (!CompanyGame.OpenBizMan(reg))", "Notifications.ShowError(\"reallife_co_bizman_cannot\""),
            "bizman link: a business BizMan cannot open gives the game's notice");

        // The open call: the game's own, only for the player's own business, nothing sent.
        string open = CompanyMethod(game, "public static bool OpenBizMan(BuildingRegistration reg)");
        Check(InOrder(open, "try", "reg == null", "!reg.RentedByPlayer", "reg.Address == null", "ui.fullMenu.bizMan == null", "!GoingPublic.Mine(reg)", "return false;",
                "ui.fullMenu.bizMan.Open(reg.Address);", "return true;", "catch (Exception e)", "return false;"),
            "bizman link: BizMan opens on the business's address by the game's BizMan.Open, only for a business of the player's own");
        Check(open.Length > 0 && !open.Contains("ShowApp(") && !open.Contains("Send") && !open.Contains("Save"),
            "bizman link: the open goes through BizMan.Open alone (it opens the app itself) and writes or sends nothing");

        // The texts, in the three languages, and the What's new line.
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null && words.TryGetValue("reallife_co_open_bizman", out string button) && button.Contains("BizMan")
                  && words.TryGetValue("reallife_co_bizman_cannot", out string cannot) && cannot.Length > 0 && cannot.Length <= 240
                  && !button.Contains('!') && !cannot.Contains('!') && !cannot.Contains((char)0x2014) && !cannot.Contains((char)0x2013),
                "bizman link: the button and its notice in " + code);
            Check(words != null && words.TryGetValue("reallife_changes_next", out string next) && next.Contains("BizMan"),
                "bizman link: What's new says so in " + code);
        }
    }
}
