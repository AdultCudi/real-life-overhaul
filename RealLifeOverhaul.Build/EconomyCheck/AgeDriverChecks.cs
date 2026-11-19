using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// Two players' suggestions (15 and 13 Nov 2026), checked here.
//
// The age. "It would be nice to be able to know the age of someone when talking to them." A talk with a person now says in its
// header how old they are: the exact age (the one People shows and street talk's "ask their age" gives) for somebody the
// player knows or has asked, else the ten years they look to be, from the person's own age in the mod's data and never from the
// model. The game's panel has a header label that the mod's pages leave empty; Guard.Entry gives a page of a talk with a person
// the header's key and the values go in the label a frame later (Social/TalkAgeGame.cs). The kit names the person as it always
// did and says the age under the name (Ui/Dialog/DialogScreen.cs).
//
// The driver. "I mainly use my phone for my private driver: could a button be added for him?" The bar of the RLO interface has
// a private driver button in its pill of small tools while the player has a contract, with a key of its own in Settings, and
// the driver's state on it (Ui/HudScreens/HudDriver.cs, HudDriverRules.cs).
//
internal static partial class Program
{
    private static void AgeDriver()
    {
        TalkAgeRuleChecks();
        TalkAgeSourceChecks();
        TalkAgeMoreChecks();
        DriverRuleChecks();
        DriverSourceChecks();
        DriverOldChecks();
        AgeDriverTextChecks();
    }

    // ------------------------------------------------------------------------------------------------- the age

    private static void TalkAgeRuleChecks()
    {
        var seen = new HashSet<string>();
        bool bands = true, tenYears = true;
        for (int age = 18; age <= 99; age++)
        {
            string key = TalkAgeRules.BandKey(age);
            bands &= key != null && key.StartsWith("reallife_talk_age_", StringComparison.Ordinal);
            seen.Add(key);
            tenYears &= age < 20 ? key == "reallife_talk_age_teens" : TalkAgeRules.BandKey(age - age % 10) == key;
        }
        Check(bands && tenYears && seen.Count == 9, "age header: every age from 18 to 99 falls in one of nine bands, each ten years but the late teens (" + seen.Count + ")");
        Check(TalkAgeRules.BandKey(29) == "reallife_talk_age_twenties" && TalkAgeRules.BandKey(30) == "reallife_talk_age_thirties" && TalkAgeRules.BandKey(70) == "reallife_talk_age_seventies"
              && TalkAgeRules.BandKey(85) == "reallife_talk_age_eighties" && TalkAgeRules.BandKey(0) == null && TalkAgeRules.BandKey(-3) == null,
            "age header: the band is the age's own decade and nothing for an age not known");
        Check(TalkAgeRules.Exact(true, false) && TalkAgeRules.Exact(false, true) && TalkAgeRules.Exact(true, true) && !TalkAgeRules.Exact(false, false),
            "age header: the exact age shows for somebody the player knows or who has said how old they are, a band for a stranger");
        Check(TalkAgeRules.Sentence("in their twenties") == "In their twenties" && TalkAgeRules.Sentence(string.Empty) == string.Empty && TalkAgeRules.Sentence(null) == null,
            "age header: the kit's line under the name starts with a capital");
    }

    private static void TalkAgeSourceChecks()
    {
        string game = ModSource(@"Social\TalkAgeGame.cs"), guard = ModSource(@"Core\GuardCalls.cs"), screen = ModSource(@"Ui\Dialog\DialogScreen.cs");
        string street = ModSource(@"Street\StreetDirector.Talk.cs"), person = ModSource(@"Social\PersonDialog.cs"), scene = ModSource(@"Social\SceneDialog.cs"), talk = ModSource(@"Social\SocialTalk.cs");
        string mark = MemberText(game, "public static void Mark(DialogEntry page)"), fill = MemberText(game, "private static bool Fill(");
        Check(guard.Contains("TalkAgeGame.Mark(entry);") && guard.IndexOf("TalkAgeGame.Mark(entry);", StringComparison.Ordinal) < guard.IndexOf("if (Ours(entry.OnConfirm))", StringComparison.Ordinal),
            "age header: every page of the mod's dialogs passes the header's mark in Guard.Entry, before it is shown");
        Check(mark.Contains("page.Template != DialogEntry.TemplateType.Text") && mark.Contains("!string.IsNullOrEmpty(page.headerKey)") && mark.Contains("page.headerKey = TalkAgeRules.HeadKey;")
              && mark.Contains("Guard.Routine(Put(") && mark.Contains("?? _making"),
            "age header: only a text page with no header of its own is headed, in the talk of the controller or of the constructor still showing its first page");
        Check(fill.Contains("ReferenceEquals(GameContract.DialogEntryNow.Field.GetValue(dialogs), page)") && fill.Contains("label.Arguments = new Dictionary<string, string>(2)")
              && fill.Contains("\"who\", who") && fill.Contains("\"age\", age") && game.Contains("private const int Frames = 10;"),
            "age header: the values go into the label of this very page, within ten frames, once");
        Check(street.Contains("class StreetDialog : Dialog, ITalkAge") && person.Contains("partial class PersonDialog : Dialog, ITalkAge") && scene.Contains("class SceneDialog : Dialog, ITalkAge")
              && street.Contains("using var talk = TalkAgeGame.Making(this);") && person.Contains("using var age = TalkAgeGame.Making(this);") && scene.Contains("using var age = TalkAgeGame.Making(this);"),
            "age header: street talk, a call with somebody known and a meeting with them are the three talks with a person, each heading its first page too");
        string words = MemberText(street, "public string AgeWords()");
        Check(words.Contains("TalkAgeRules.Exact(known, c.Memo.AgeTold)") && words.Contains("AgeShown(c)") && words.Contains("TalkAgeRules.BandKey(years)")
              && MemberText(street, "private static int AgeShown(StreetChat c)").Contains("known >= 0 ? known : c.Who.Age"),
            "age header: a street stranger's age is the one asking gives (AgeShown, the People page's for somebody known), exact once known or asked, else the band of that same figure");
        string social = MemberText(talk, "public static string AgeWords(SocialPerson person)");
        Check(social.Contains("int years = AgeOf(person);") && social.Contains("TalkAgeRules.Exact(person.Met, false)") && social.Contains("TalkAgeRules.BandKey(years)")
              && person.Contains("SocialTalk.AgeWords(_p)") && scene.Contains("SocialTalk.AgeWords(_p)"),
            "age header: a person's age in a call or a meeting is People's (SocialTalk.AgeOf), exact once met, else a band of it");
        string head = MemberText(screen, "private void Head(string speaker, int mood, string age)");
        Check(screen.Contains("string age = TalkAgeGame.Of(_ui.dialog);") && screen.Contains("age == null ? Words(c.Find(\"HeaderLabel\")) ?? speaker : speaker;")
              && head.Contains("TalkAgeRules.Sentence(age)"),
            "age header: the kit names the person as before, never from the header that now carries the age, and says the age under the name");
        string all = game + guard + street + person + scene + talk;
        Check(!all.Contains("ageInDays") && !game.Contains("Find(\"Model\")") && !mark.Contains("Update(") && !game.Contains("FindObject") && !game.Contains("GetComponentsIn"),
            "age header: read from the mod's own data, never from the figure's model, and nothing searched or run a frame");
    }


    // The other talks with a person: the partner's (face to face and on the phone) and the flatmate's.
    private static void TalkAgeMoreChecks()
    {
        string family = ModSource(@"Family\FamilyTalk.cs"), phone = ModSource(@"Family\FamilyPhone.cs"), links = ModSource(@"Wiring\FamilyLinks.cs");
        string talk = ModSource(@"Home\Roommates\RoommateTalk.cs"), call = ModSource(@"Home\Roommates\RoommatePhone.cs");
        string head = MemberText(family, "internal static void PartnerHead(out string who, out string age)");
        Check(family.Contains("class FamilyDialog : Dialog, ITalkAge") && phone.Contains("class PartnerDialog : Dialog, ITalkAge")
              && family.Contains("_aboutPartner = partner || with != null;") && family.Contains("using var head = TalkAgeGame.Making(this);") && phone.Contains("using var head = TalkAgeGame.Making(this);"),
            "age header: the family's face-to-face talk with the partner (a character beside them, a wedding, a ring, a date) and the partner's call are headed with the partner's age");
        Check(head.Contains("TalkAgeGame.Partner()") && head.Contains("TalkAgeGame.Exact(Partner.LookAge)") && links.Contains("TalkAgeGame.Partner = PartnerAgeWords;") && links.Contains("TalkAgeGame.Partner = null;")
              && links.Contains("SocialTalk.AgeWords(p)"),
            "age header: the partner's age is People's through the family bridge, and their look's age where Social keeps none");
        foreach (string file in new[] { @"Family\WeddingDay.cs", @"Family\Errands.cs" })
        {
            Check(ModSource(file).Contains("partner: true"), "age header: the talks about the partner in " + file + " are marked as with them");
        }
        Check(talk.Contains("class RoommateDialog : Dialog, ITalkAge") && call.Contains("class RoommateCallDialog : Dialog, ITalkAge")
              && talk.Contains("internal static Flatmate Mate => RoommateDirector.Instance?.State?.Mate;") && talk.Contains("TalkAgeGame.Exact(RoommateTalk.Mate != null ? RoommateTalk.Mate.Person.Age : 0)"),
            "age header: the flatmate's call and talk at the flat are headed with the age the mod keeps for them");
        Check(TalkAgeRules.BandKey(1) == "reallife_talk_age_teens", "age header: a rolled age under the adults' is still a band, never nothing");
    }

    // ------------------------------------------------------------------------------------------------- the driver

    private static void DriverRuleChecks()
    {
        Check(HudDriverRules.State(false, false, false, false) == DriverState.None && HudDriverRules.State(false, true, true, true) == DriverState.None
              && HudDriverRules.State(true, false, false, false) == DriverState.Idle && HudDriverRules.State(true, false, true, true) == DriverState.Idle
              && HudDriverRules.State(true, true, false, false) == DriverState.Coming && HudDriverRules.State(true, true, false, true) == DriverState.Waiting
              && HudDriverRules.State(true, true, true, false) == DriverState.Driving && HudDriverRules.State(true, true, true, true) == DriverState.Driving,
            "private driver button: none without a contract, at rest with none called, then on its way, waiting by the player and driving");
        Check(HudDriverRules.Press(DriverState.Idle, 1, false) == DriverPress.Call && HudDriverRules.Press(DriverState.Idle, 1, true) == DriverPress.Card
              && HudDriverRules.Press(DriverState.Idle, 0, false) == DriverPress.Card && HudDriverRules.Press(DriverState.Idle, 2, false) == DriverPress.Card
              && HudDriverRules.Press(DriverState.Coming, 1, false) == DriverPress.Card && HudDriverRules.Press(DriverState.Waiting, 3, true) == DriverPress.Card
              && HudDriverRules.Press(DriverState.Driving, 1, false) == DriverPress.Card,
            "private driver button: the garage's only car is called at once, anything else opens or shuts the card with the game's own choices");
        var tints = new HashSet<UiRole>();
        foreach (DriverState s in new[] { DriverState.Idle, DriverState.Coming, DriverState.Waiting, DriverState.Driving })
        {
            tints.Add(HudDriverRules.Tint(s));
        }
        Check(tints.Count == 4 && HudDriverRules.Tint(DriverState.Idle) == UiRole.None && HudDriverRules.Tint(DriverState.None) == UiRole.None,
            "private driver button: each state a call can be in has its own colour on the glyph, the driver at rest the plain one");
        var notes = new HashSet<string>();
        foreach (DriverState s in new[] { DriverState.Idle, DriverState.Coming, DriverState.Waiting, DriverState.Driving })
        {
            notes.Add(HudDriverRules.NoteKey(s, 1));
        }
        notes.Add(HudDriverRules.NoteKey(DriverState.Idle, 2));
        notes.Add(HudDriverRules.NoteKey(DriverState.Idle, 0));
        Check(notes.Count == 6, "private driver button: six words on hover, one for each state and each way a press answers at rest");
    }

    private static void DriverSourceChecks()
    {
        string keys = ModSource(@"Core\Keys.cs"), top = ModSource(@"Ui\HudScreens\HudTopGame.cs"), reach = ModSource(@"Ui\Phone\PhoneReach.cs"), driver = ModSource(@"Ui\HudScreens\HudDriver.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs"), hud = ModSource(@"Ui\UiScreens.Hud.cs"), kit = ModSource(@"Ui\World\PrivateDriverKit.cs");
        Check(keys.Contains("Driver,") && keys.Contains("new Binding(\"driver\", \"reallife_keys_driver\", KeyCode.Insert),") && tree.Contains("\"reallife_keys_phone\", \"reallife_keys_driver\""),
            "private driver button: a key of its own in Core/Keys (after the Phone key, as the binding table is in the enum's order), with its row in Settings where the Phone key is");
        Check(top.Contains("Driver = 5") && top.Contains("new UiIcon[6]") && top.Contains("new int[6]") && top.Contains("new UiRole[6]") && top.Contains("HudDriver.Look(out DriverState driver, out bool driverOpen)")
              && top.Contains("Tool(Driver, UiIcon.Car, driverOpen, false, HudDriverRules.Tint(driver));") && top.Contains("case Driver:") && top.Contains("return HudDriver.Press();") && top.Contains("return HudDriver.Words();"),
            "private driver button: the bar's pill has the driver after the phone's button, its state in the glyph's colour, the card open as the chosen tone, a press and hover words of its own");
        string frame = MemberText(driver, "internal static void Frame()"), act = MemberText(driver, "private static bool Act()"), read = MemberText(driver, "private static void Read(");
        Check(reach.Contains("PhoneReach.Frame();") && reach.Contains("HudDriver.Frame();") && frame.Contains("!_shown") && frame.Contains("Keys.Down(KeyAction.Driver)")
              && frame.IndexOf("!_shown", StringComparison.Ordinal) < frame.IndexOf("GetKeyDown", StringComparison.Ordinal),
            "private driver button: the key is read in the phone key's guarded frame, one key test, and only while the bar shows the button");
        Check(act.Contains("card.OnClickVehicle(_only);") && act.Contains("UiWidgets.Hand(button);") && act.Contains("card.OnClickPrivateDriverButton();") && act.Contains("GameContract.PhoneDriver"),
            "private driver button: a press is the game's own (the card's car button for the one car, the phone's driver button, or its method where the phone is folded and the button hidden)");
        Check(read.Contains("PrivateDriverHelpers.GetActiveContract() != null") && read.Contains("SmartphonePrivateDriverUI.CurrentVehicle") && read.Contains("TaxiSystem.IsTraveling")
              && !driver.Contains("Find(") && !driver.Contains("FindObject") && !driver.Contains("GetComponentsIn") && !driver.Contains("new List<") && !driver.Contains("Update("),
            "private driver button: its state is read from the game's contract, car and ride with nothing searched, made or run a frame");
        Check(kit.Contains("private const float Near = HudDriverRules.Near;") && kit.Contains("internal static string CarName(VehicleInstance v)"),
            "private driver button: the card and the bar tell 'waiting' by the same distance and name the car the same way");
        Check(hud.Contains("\"reallife_driver_tool_call\"") && hud.Contains("\"reallife_driver_tool_choose\"") && hud.Contains("\"reallife_driver_tool_none\"") && hud.Contains("\"reallife_driver_tool_coming\"")
              && hud.Contains("\"reallife_driver_tool_waiting\"") && hud.Contains("\"reallife_driver_tool_driving\""),
            "private driver button: its six hover texts are in the HUD screen's key list");
        Check(ModSource(@"Ui\HudScreens\HudTopGame.cs").Contains("HudDriver"), "private driver button: the kit's bar draws it from the same reads as the old bar's");
    }


    private static void DriverOldChecks()
    {
        string old = ModSource(@"Ui\HudScreens\DriverButton.cs"), director = ModSource(@"Ui\UiDirector.cs"), driver = ModSource(@"Ui\HudScreens\HudDriver.cs");
        string tick = MemberText(old, "public static void Tick()"), build = MemberText(old, "private static bool Build()");
        Check(director.Contains("DriverButton.Tick();") && director.Contains("DriverButton.Remove();") && tick.Contains("HudLive.Live") && tick.Contains("HudDriver.Look(out DriverState state, out bool open)")
              && tick.Contains("_failed = true;"),
            "private driver button: the game's own top bar has it too, on the interface director's slow tick, standing down while the kit's HUD is over the bar, gone with the city, a fault removes it");
        Check(build.Contains("UiClone.Need(row, \"HelpButton\"") && build.Contains("UiClone.StripGameScripts(copy, m => m is BasicTooltip)") && build.Contains("UiClone.Rewire(copy.transform, Pressed)")
              && build.Contains("TMPro.TMP_Text") && build.Contains("vehiclePOIIcon") && old.Contains("if (_piece == null && !Build())"),
            "private driver button: built once from the bar's help button, the control and its picture only (no label), pressed through one listener, with the game's own car marker");
        Check(old.Contains("HudDriver.Press()") && old.Contains("HudDriverRules.NoteKey(state, _cars)") && old.Contains("Keys.Label(KeyAction.Driver)") && old.Contains("HudDriver.Frame();")
              && old.Contains("_keys.enabled = shows;") && driver.Contains("_pressedFrame == Time.frameCount")
              && !tick.Contains("Find(") && !tick.Contains("FindObject") && !tick.Contains("GetComponentsIn"),
            "private driver button: the same press, state, tooltip and key as the kit's, the key read only while it shows and answered once a frame, nothing searched on the tick");
    }

    // ------------------------------------------------------------------------------------------------- the texts

    private static void AgeDriverTextChecks()
    {
        string[] keys =
        {
            "reallife_talk_head", "reallife_talk_age_exact", "reallife_talk_age_teens", "reallife_talk_age_twenties", "reallife_talk_age_thirties", "reallife_talk_age_forties",
            "reallife_talk_age_fifties", "reallife_talk_age_sixties", "reallife_talk_age_seventies", "reallife_talk_age_eighties", "reallife_talk_age_nineties",
            "reallife_keys_driver", "reallife_driver_tool_call", "reallife_driver_tool_choose", "reallife_driver_tool_none", "reallife_driver_tool_coming",
            "reallife_driver_tool_waiting", "reallife_driver_tool_driving"
        };
        Dictionary<string, string> en = LocaleFile("en");
        var missing = keys.Where(k => !en.TryGetValue(k, out string v) || string.IsNullOrWhiteSpace(v)).ToList();
        Check(missing.Count == 0, "age and driver texts: every key is in en.json (missing: " + string.Join(", ", missing) + ")");
        bool style = true;
        foreach (string k in keys.Where(k => en.ContainsKey(k)))
        {
            string t = en[k];
            style &= !t.Contains("!") && !t.Contains("—") && !t.Contains("–") && !t.Contains(" - ") && t.Length <= 190;
        }
        Check(style, "age and driver texts: no exclamation marks or dashes, short");
        Check(en["reallife_talk_head"] == "{who}, {age}" && en["reallife_talk_age_exact"].Contains("{age}") && en["reallife_driver_tool_call"].Contains("{key}")
              && en["reallife_driver_tool_coming"].Contains("{car}") && en["reallife_driver_tool_waiting"].Contains("{car}") && en["reallife_driver_tool_driving"].Contains("{car}"),
            "age and driver texts: the header and the hover words carry the values they are filled with");
        // In any row, and as features: other lines of the row may be fixes, so only these two sentences are looked at for a mark of their own.
        Check(AnyChangesRow(en, "in its header how old they are") && AnyChangesRow(en, "private driver button") && !AnyChangesRow(en, "Fixed: A talk with a person") && !AnyChangesRow(en, "Fixed: A private driver"),
            "age and driver texts: What's new says both in a row, as a feature");
    }
}
