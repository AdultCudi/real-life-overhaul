using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// BizMan's offers on the kit, one row for each thing the game's BizManPresentation offers and the condition it offers it under:
// an offer to take a rival's business over (SendOvertakeOffer), an offer for the building from its for-sale or its not-for-sale
// box (SendBuyBuildingOffer), the Hamptons house's own purchase box (the same method), and these beside the lease of a building
// for rent. The game keeps the box that holds each amount and Send shut until its make-an-offer button is pressed, so a page
// that waited for Send to show never offered any. Read from the sources, as the screens are game UI the checker cannot run.
internal static partial class Program
{
    private static void BizManOfferAll()
    {
        // The game's boxes and the buttons that open them, by the conditions of LoadLeftSide.
        Check(BizManRules.BuildingOffer(true, true, false, false) == 1, "bizman offer: a building for sale offers its for-sale box's offer");
        Check(BizManRules.BuildingOffer(false, false, true, true) == 2, "bizman offer: a building not for sale offers its other box's offer");
        Check(BizManRules.BuildingOffer(true, false, false, false) == 0 && BizManRules.BuildingOffer(false, false, true, false) == 0,
            "bizman offer: a Hamptons house (the game hides both buttons) offers nothing from these boxes");
        Check(BizManRules.BuildingOffer(false, true, false, true) == 0, "bizman offer: a box the game keeps hidden (a building the player owns, a mod's claimed building) offers nothing");
        Check(!BizManRules.SendHeld(false, false) && !BizManRules.SendHeld(false, true) && !BizManRules.SendHeld(true, true) && BizManRules.SendHeld(true, false),
            "bizman offer: a Send is held only while it shows and the game will not take the press, never for a Send the game keeps shut");

        // A player's picture of 13 Nov 2026: the takeover's Send read BIZMAN_SEND_OFFER_BUTTON. The game's button is shut until an offer is
        // begun, so its label was never put into words and still held its key; the kit read the key as the button's words.
        Check(MapText.KeyShaped("bizman_send_offer_button") && MapText.KeyShaped("BIZMAN_SEND_OFFER_BUTTON") && MapText.KeyShaped("ba:itemname_apple")
              && !MapText.KeyShaped("Send offer") && !MapText.KeyShaped("Send") && !MapText.KeyShaped("$22,600") && !MapText.KeyShaped("OK") && !MapText.KeyShaped("_") && !MapText.KeyShaped("a_")
              && !MapText.KeyShaped("Elva's_shop") && !MapText.KeyShaped("12:30") && !MapText.KeyShaped(string.Empty) && !MapText.KeyShaped(null),
            "bizman offer: a label still holding its key is told from words (one run of letters, digits, underscores and colons with an underscore inside)");
        string widgets = ModSource(@"Ui\Host\UiWidgets.cs");
        string worded = MemberText(widgets, "private static string Worded(");
        Check(worded.Contains("MapText.KeyShaped(words)") && worded.Contains("LocalizorManager.IsLocalizedKey(words)") && worded.Contains("words.ToLowerInvariant()") && worded.Contains(": words;")
              && widgets.Contains("return Worded(MapText.OneLine(Texts[i].text));") && widgets.Contains("Worded(MapText.Plain(t.text))"),
            "bizman offer: a game label read while it still holds its key is given the key's own words, on every screen that reads a label, and a text that is no key stays as it is");
        Check(WhatsNewHas(LocaleFile("en"), "BIZMAN_SEND_OFFER_BUTTON"), "bizman offer: What's new tells of the Send button's fix");

        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        string contract = ModSource(@"Core\GameContract.BizMan.cs");

        string buy = MemberText(parts, "private void BuyOffer(");
        Check(buy.Contains("BizManRules.BuildingOffer(") && buy.Contains("GameContract.ShowForSaleOffer") && buy.Contains("GameContract.ShowNotForSaleOffer")
              && !buy.Contains("send == null") && buy.Contains("UiWidgets.Calling(pres, \"SendBuyBuildingOffer\")"),
            "bizman offer: the offer for the building is read from the game's boxes and their buttons, not from a Send that shows");
        Check(buy.Contains("Field = BizManPages.FieldBuy") && buy.Contains("pres.buyBuildingAmountInputField.text"),
            "bizman offer: the amount is the game's own buyBuildingAmountInputField");

        string rival = MemberText(parts, "private void Rival(");
        Check(rival.Contains("if (On(pres.sendOvertakeOfferPanel))") && !rival.Contains("&& send != null") && rival.Contains("UiWidgets.Calling(pres, \"SendOvertakeOffer\")")
              && rival.Contains("Field = BizManPages.FieldOffer") && rival.Contains("pres.offerAmountInputField.text") && rival.Contains("BuyOffer(pres, _m.Amount != null)"),
            "bizman offer: a rival's business offers the takeover whenever the game shows its panel, and the offer for its building beside it");

        string rent = Lines(parts, "if (On(rent))", 5);
        Check(rent.Contains("Rent(rent);") && rent.Contains("BuyOffer(pres, false);"), "bizman offer: a building for rent offers the offer for the building beside the lease");

        string villa = MemberText(parts, "private void Villa(");
        Check(villa.Contains("villa.offerInputField.text") && villa.Contains("BizManRules.SendHeld(") && !villa.Contains("send == null ||"),
            "bizman offer: the Hamptons house's purchase is the game's own field and is held only when the game holds it");

        // Every press reaches the game's method, with the amount in the game's own field.
        Check(press.Contains("Send(send ?? Calling(pres, \"SendBuyBuildingOffer\"), pres.SendBuyBuildingOffer,")
              && press.Contains("Send(Calling(pres, \"SendOvertakeOffer\"), pres.SendOvertakeOffer,"),
            "bizman offer: both Sends press the game's button while it shows, else call SendBuyBuildingOffer or SendOvertakeOffer itself");
        string send = MemberText(press, "private static bool Send(");
        Check(send.Contains("Click(shown, what)") && send.Contains("call();") && send.Contains("HotSpots.Game()"),
            "bizman offer: the call runs as the game's own work, its checks, notices and other mods' patches included");
        Check(press.Contains("return pres != null ? pres.offerAmountInputField : null;") &&
              press.Contains("return On(villa) ? villa.offerInputField : pres != null ? pres.buyBuildingAmountInputField : null;"),
            "bizman offer: a typed amount goes into the game's offerAmountInputField, buyBuildingAmountInputField or the house's offerInputField");

        // The contract names the two buttons the page now reads.
        Check(contract.Contains("\"forSaleOfferButton\"") && contract.Contains("\"notForSaleOfferButton\""), "bizman offer: the contract lists the two make-an-offer buttons");

        // The What's new line, in every language.
        foreach (string code in new[] { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), "an offer to buy its building, also beside the lease of a building for rent."));   // the row that holds it
            Check(words != null && next.Contains("BizMan")
                  && (code != "en" || next.Contains("an offer to buy its building, also beside the lease of a building for rent.")) && !next.Contains('!')
                  && !next.Contains((char)0x2014) && !next.Contains((char)0x2013),
                "bizman offer: What's new says so in " + code);
        }
    }
}
