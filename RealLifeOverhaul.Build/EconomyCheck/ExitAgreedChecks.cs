using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// A sale agreed at a desk inside the firm (Scripts/Tech/TechDirector.ExitAgreed.cs, Tech/Venture/ExitRules.cs "a sale agreed inside the firm"): a business cannot change hands with its owner in
// it, so accepting at the desk keeps the player's word (the offer, its price and its last day, in the save) and the sale completes through ExitAccept when they step outside, at the first look
// after a load or on the day's pass, names the same refusals if something else is in the way, lapses with a notice when the last day passes, and can be called off on the Exits page of both
// interfaces. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void ExitAgreedChecks()
    {
        ExitAgreedRuleChecks();
        ExitAgreedStoreChecks();
        ExitAgreedSourceChecks();
        ExitAgreedTextChecks();
    }

    private static FirmExit AgreedFirm(out ExitOffer offer, int lapse = 40)
    {
        var e = new FirmExit { Place = "Main~1" };
        offer = new ExitOffer { Id = 7, Kind = ExitRules.KindBuy, Buyer = 0, BuyerId = 3, Name = "Northgate Systems", Price = 123450000, Made = 30, Lapse = lapse };
        e.Offers.Add(offer);
        return e;
    }

    private static void ExitAgreedRuleChecks()
    {
        FirmExit e = AgreedFirm(out ExitOffer o);
        Check(ExitRules.Check(e, 35) == ExitRules.AgreedState.None && !ExitRules.IsAgreed(e, 7), "agreed sale: a firm with no sale agreed has none standing");
        Check(ExitRules.Agree(e, o, 35) && e.Agreed != null && e.Agreed.Offer == 7 && e.Agreed.Price == 123450000 && e.Agreed.Lapse == 40 && e.Agreed.Day == 35 && e.Agreed.Name == "Northgate Systems",
            "agreed sale: the player's word keeps the offer, its price, its last day, the day it was given and the buyer");
        Check(ExitRules.IsAgreed(e, 7) && !ExitRules.IsAgreed(e, 8) && ExitRules.Check(e, 35) == ExitRules.AgreedState.Standing && ExitRules.Check(e, 40) == ExitRules.AgreedState.Standing,
            "agreed sale: it stands through the offer's last day");
        Check(!ExitRules.Agree(e, o, 36), "agreed sale: a second word at a firm that has one agreed changes nothing");
        Check(ExitRules.Check(e, 41) == ExitRules.AgreedState.Lapsed, "agreed sale: past the offer's last day it has lapsed, whether or not the offer was swept");
        e.Offers.Clear();
        Check(ExitRules.Check(e, 38) == ExitRules.AgreedState.Gone && ExitRules.Check(e, 41) == ExitRules.AgreedState.Lapsed, "agreed sale: an offer gone another way ends it quietly, and one past its day still lapses with its notice");
        FirmExit f = AgreedFirm(out ExitOffer p);
        Check(!ExitRules.Agree(f, new ExitOffer { Id = 9, Lapse = 50 }, 35) && !ExitRules.Agree(f, p, 41) && f.Agreed == null && !ExitRules.Agree(null, p, 35) && !ExitRules.Agree(f, null, 35),
            "agreed sale: no word is kept for an offer the firm does not hold, one past its last day, or nothing");
        ExitRules.Agree(f, p, 35);
        Check(ExitRules.CallOff(f) && f.Agreed == null && f.Offers.Count == 1 && f.Offers[0].Price == 123450000 && f.Offers[0].Lapse == 40 && !ExitRules.CallOff(f) && ExitRules.Check(f, 36) == ExitRules.AgreedState.None,
            "agreed sale: called off, the offer stands as it did to its last day, and a second call-off does nothing");
        Check(!f.Empty && f.Offers.Count > 0, "agreed sale: a firm with an offer is not empty");
        var g = new FirmExit { Place = "Main~2" };
        g.Agreed = new ExitAgreed { Offer = 1, Lapse = 9 };
        Check(!g.Empty, "agreed sale: a firm with only an agreed sale is not an empty record, so the save keeps it");
        int ran = 0, wrong = 0;
        for (int lapse = 5; lapse <= 60; lapse++)
        {
            FirmExit h = AgreedFirm(out ExitOffer ho, lapse);
            ExitRules.Agree(h, ho, 5);
            for (int day = 5; day <= lapse + 3; day++)
            {
                ran++;
                ExitRules.AgreedState s = ExitRules.Check(h, day);
                wrong += (day <= lapse ? s == ExitRules.AgreedState.Standing : s == ExitRules.AgreedState.Lapsed) ? 0 : 1;
            }
        }
        Check(ran > 1000 && wrong == 0, "agreed sale: for every last day from 5 to 60 it stands up to it and has lapsed after it (" + ran + " days looked at)");
    }

    private static void ExitAgreedStoreChecks()
    {
        var file = new ExitFile { Seed = 11, LastDay = 30 };
        FirmExit a = file.Exit("Main~1", true);
        a.Offers.Add(new ExitOffer { Id = 5, Name = "Pocketloop", Buyer = 1, BuyerId = 9, Price = 777000, Made = 20, Lapse = 44 });
        a.Offers.Add(new ExitOffer { Id = 6, Name = "Northgate Systems", Price = 999000, Made = 22, Lapse = 46 });
        ExitRules.Agree(a, a.Offers[0], 31);
        FirmExit b = file.Exit("Main~2", true);
        b.Offers.Add(new ExitOffer { Id = 8, Name = "Other", Price = 5, Made = 1, Lapse = 3 });
        string line = ExitStore.Write(file);
        ExitFile back = ExitStore.Read(line, out _);
        FirmExit ra = back.Exit("Main~1", false), rb = back.Exit("Main~2", false);
        Check(ra != null && ra.Agreed != null && ra.Agreed.Offer == 5 && ra.Agreed.Price == 777000 && ra.Agreed.Lapse == 44 && ra.Agreed.Day == 31 && ra.Agreed.Name == "Pocketloop"
              && rb != null && rb.Agreed == null && ra.Offers.Count == 2,
            "agreed sale: it is kept in the save with its offer, price, last day and buyer, and another firm's offers are not touched");
        string without = Regex.Replace(line, "~a:[^~]*", string.Empty);
        ExitFile old = ExitStore.Read(without, out _);
        Check(without != line && old.Exit("Main~1", false) != null && old.Exit("Main~1", false).Offers.Count == 2 && old.Exit("Main~1", false).Agreed == null,
            "agreed sale: the line without its part (a save from before it, or read by an older mod) still loads whole, with no sale agreed");
        Check(line.Contains("~a:") && line.IndexOf("~o:", StringComparison.Ordinal) < line.IndexOf("~a:", StringComparison.Ordinal),
            "agreed sale: its part comes after the offers' and no existing part moves, so an older reader finds each part where it was");
        FirmExit c = AgreedFirm(out ExitOffer co);
        c.Agreed = new ExitAgreed { Offer = 99, Price = 1, Lapse = 40, Day = 3, Name = "Ghost" };
        var lone = new ExitFile();
        lone.Firms.Add(c);
        ExitFile ghost = ExitStore.Read(ExitStore.Write(lone), out _);
        Check(ghost.Exit("Main~1", false) != null && ghost.Exit("Main~1", false).Agreed == null, "agreed sale: a sale whose offer the save no longer holds is not brought back (nothing to complete)");
        Check(ExitStore.Version == 1, "agreed sale: the exits line keeps its version, as only a part was added");
    }

    private static void ExitAgreedSourceChecks()
    {
        string agreed = ModSource(@"Tech\TechDirector.ExitAgreed.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), director = ModSource(@"Tech\TechDirector.cs");
        string keep = MemberText(agreed, "private void KeepAgreed()"), finish = MemberText(agreed, "private bool FinishAgreed(FirmExit e, int day)");
        Check(keep.Contains("EventHold.Keep(ref GlobalEvents.onExitBuilding, _onLeft, ref _leftSeen);") && agreed.Contains("Guard.Wrap<Address>(OnLeft)")
              && MemberText(agreed, "private void OnLeft(Address address)").Contains("_agreeTry = true;"),
            "agreed sale: the game's exit building event sets the flag the one-second timer reads, through the guard, kept on the event as the other features keep theirs");
        Check(director.Contains("KeepMoney();") && Regex.IsMatch(director, @"KeepMoney\(\);\s*if \(_exits != null\)\s*\{\s*KeepAgreed\(\);"),
            "agreed sale: the timer asks only while the exits run, and a look is one flag test while no sale waits");
        Check(finish.Contains("ExitBridges.WhyNotHand(e.Place)") && finish.Contains("ExitAccept(e.Place, offer)") && finish.IndexOf("WhyNotHand", StringComparison.Ordinal) < finish.IndexOf("ExitAccept(", StringComparison.Ordinal)
              && finish.Contains("BusyRefusal") && finish.Contains("InsideRefusal") && finish.Contains("reallife_exit_note_agreed_blocked"),
            "agreed sale: it completes through ExitAccept after the same refusal check, names what stands in the way once, waits a second while the game is busy and says nothing while the player is inside again");
        Check(Regex.IsMatch(exits, @"_agreeTry = AnyAgreed\(\);") && exits.Contains("DropAgreedHandler();") && Regex.IsMatch(exits, @"AgreedDay\(day\);[^\r\n]*\s*FirmExitsDay\(day\);"),
            "agreed sale: a sale agreed before the save was made is looked at first after the load, the handler is let go when the exits stop, and the day's pass lapses it before the offers' own sweep");
        string accept = MemberText(exits, "internal bool ExitAccept(string place, int offerId)");
        Check(accept.Contains("e.Agreed = null;") && Occurs(accept, "ExitRules.IsAgreed(e, offerId)") == 2 && MemberText(exits, "internal void ExitDecline(string place, int offerId)").Contains("ExitRules.IsAgreed(e, offerId)")
              && MemberText(exits, "internal int ExitCounter(string place, int offerId, int step)").Contains("ExitRules.IsAgreed(e, offerId)"),
            "agreed sale: a sale made, an offer that lapsed or whose buyer is gone, or turned down, ends the agreement, and the agreed offer is never countered");
        string session = MemberText(exits, "internal bool ExitSession(string place, string name, int which, int choice, out string key, out string[] pairs)");
        Check(session.Contains("e.Agreed != null") && session.Contains("reallife_tech_desk_exit_already_agreed") && session.Contains("ExitAgree(place, o)") && session.Contains("reallife_tech_desk_exit_offer_agreed"),
            "agreed sale: a second visit to the desk says a sale is already agreed, and the first keeps the player's word");
        string lapse = MemberText(agreed, "private void LapseAgreed(FirmExit e, int day)");
        Check(lapse.Contains("e.Offers.Remove(o);") && lapse.Contains("reallife_exit_note_agreed_lapsed") && MemberText(agreed, "internal bool ExitCallOff(string place)").Contains("reallife_exit_note_agreed_off"),
            "agreed sale: lapsing takes the offer and says so, and calling it off says so");
        Check(!Regex.IsMatch(agreed, @"void (Update|LateUpdate|FixedUpdate)\(") && !agreed.Contains("FindObjectsByType") && !agreed.Contains("FindObjectOfType"),
            "agreed sale: nothing of it runs a frame or searches the scene");
        string kit = ModSource(@"Ui\Tech\TechLive.Exits.cs"), old = ModSource(@"Tech\Venture\TechApp.Exits.cs"), keys = ModSource(@"Ui\UiScreens.TechExits.cs");
        Check(kit.Contains("ExitRules.IsAgreed(e, id)") && kit.Contains("_d.ExitCallOff(place)") && kit.Contains("reallife_exit_agreed_line") && kit.Contains("reallife_exit_button_call_off")
              && old.Contains("ExitRules.IsAgreed(e, id)") && old.Contains("_director.ExitCallOff(place)") && old.Contains("reallife_exit_agreed_line") && old.Contains("reallife_exit_button_call_off")
              && kit.Contains("!ExitRules.IsAgreed(e, first.Id)") && old.Contains("!ExitRules.IsAgreed(e, first.Id)"),
            "agreed sale: both interfaces show it on the firm's Exits page with a call-off button, and neither offers to counter the offer it stands on");
        Check(keys.Contains("\"reallife_exit_agreed_line\"") && keys.Contains("\"reallife_exit_button_call_off\""), "agreed sale: the kit's list of the Exits page's texts has both new texts");
        Check(kit.IndexOf("ExitRules.IsAgreed(e, id)", StringComparison.Ordinal) < kit.IndexOf("reallife_exit_confirm_accept_title", StringComparison.Ordinal)
              && old.IndexOf("ExitRules.IsAgreed(e, id)", StringComparison.Ordinal) < old.IndexOf("reallife_exit_confirm_accept_title", StringComparison.Ordinal),
            "agreed sale: the agreed offer shows its line and call-off in place of Accept and Decline on both interfaces");
    }

    private static void ExitAgreedTextChecks()
    {
        string[] keys =
        {
            "reallife_tech_desk_exit_offer_agreed", "reallife_tech_desk_exit_already_agreed", "reallife_exit_agreed_line", "reallife_exit_button_call_off", "reallife_exit_note_agreed_lapsed",
            "reallife_exit_note_agreed_blocked", "reallife_exit_note_agreed_off"
        };
        int bad = 0;
        string said = string.Empty;
        foreach (string key in keys)
        {
            string text = LocaleText("en", key);
            if (string.IsNullOrEmpty(text) || text.Length > 240 || text.Contains("!") || text.Contains("—") || text.Contains("–") || text.Contains(" - "))
            {
                bad++;
                said += key + " ";
            }
        }
        Check(bad == 0, "agreed sale: every text exists, at most 240 characters, with no dash or exclamation mark (" + said.Trim() + ")");
        Check(LocaleText("en", "reallife_tech_desk_exit_offer_agreed").Contains("{buyer}") && LocaleText("en", "reallife_tech_desk_exit_offer_agreed").Contains("{amount}")
              && LocaleText("en", "reallife_tech_desk_exit_offer_agreed").Contains("{firm}") && LocaleText("en", "reallife_exit_agreed_line").Contains("{day}")
              && LocaleText("en", "reallife_exit_note_agreed_blocked").Contains("{why}") && LocaleText("en", "reallife_exit_note_agreed_lapsed").Contains("{day}"),
            "agreed sale: the texts carry the figures the code fills in");
        string agreed = ModSource(@"Tech\TechDirector.ExitAgreed.cs");
        Check(Regex.IsMatch(agreed, @"Data\(""firm"", firm\.File\.Name, ""buyer"", e\.Agreed\.Name, ""why"", TechText\.T\(why\)\)"), "agreed sale: a refusal at the moment of leaving is the refusal's own words, in the notice");
    }
}
