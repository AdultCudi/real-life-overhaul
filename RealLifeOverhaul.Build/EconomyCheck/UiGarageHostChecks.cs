using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The Garage's lists on the RLO interface (13 Nov 2026): the custom shop, the workshop, "My vehicles" and a stand's rack as the garage strip, read
// from the list window and pressed through the list's own pick. What decides a card is game-free and laid out here; what reads the game is checked in
// its source, as its neighbours are.
internal static partial class Program
{
    private static void UiGarageHostChecks()
    {
        UiGarageHostRules();
        UiGarageHostStrip();
        UiGarageHostWiring();
        UiGarageHostKeys();
    }

    private static UiGarageRow GarageRow(string name, string key, bool enabled = true, bool note = false, int colour = -1) =>
        new UiGarageRow { Name = name, Detail = "detail", ButtonKey = key, Enabled = enabled, Note = note, Colour = colour };

    private static void UiGarageHostRules()
    {
        Func<string, string> words = k => "<" + k + ">";
        UiGarageCard pay = GarageCardRules.Card(GarageRow("Pay $900", GarageCardRules.PayKey), UiGarageKind.Shop, true, words);
        UiGarageCard done = GarageCardRules.Card(GarageRow("Done", GarageCardRules.DoneKey), UiGarageKind.Shop, true, words);
        Check(pay.Main && pay.Button == "<reallife_garage_list_pay>" && pay.Enabled && done.Main && done.Button == "<reallife_customshop_done>",
            "garage host: Pay, and Done when the bill is nothing, are the shop's one main action, with the list's own button words");
        UiGarageCard back = GarageCardRules.Card(GarageRow("Custom shop", GarageCardRules.BackKey), UiGarageKind.Shop, false, words);
        Check(back.Back && back.Button == null && back.Enabled, "garage host: the way back is a row of its own with its glyph, and a press");
        UiGarageCard opens = GarageCardRules.Card(GarageRow("Paint", GarageCardRules.ChooseKey), UiGarageKind.Shop, true, words);
        UiGarageCard figures = GarageCardRules.Card(GarageRow("Figures", GarageCardRules.ReadKey), UiGarageKind.Shop, true, words);
        UiGarageCard option = GarageCardRules.Card(GarageRow("Gloss", GarageCardRules.ChooseKey), UiGarageKind.Shop, false, words);
        Check(opens.Opens && opens.Button == null && figures.Opens && !option.Opens && option.Button == "<reallife_garage_list_choose>",
            "garage host: on the shop's first page Choose and Read open a page, on every other page Choose picks an option and keeps its button");
        UiGarageCard chosen = GarageCardRules.Card(GarageRow("Gloss", GarageCardRules.ChosenKey, false), UiGarageKind.Shop, false, words);
        Check(chosen.Chosen && !chosen.Enabled && chosen.Button == "<reallife_garage_list_chosen>",
            "garage host: the option the cart holds says so with its own word and cannot be pressed again");
        UiGarageCard refused = GarageCardRules.Card(GarageRow("Spoiler", "reallife_garage_list_fit", false), UiGarageKind.Shop, false, words);
        Check(!refused.Chosen && !refused.Enabled && refused.Button == "<reallife_garage_list_fit>" && !refused.Opens,
            "garage host: an option the car cannot take keeps its button, greyed and refused, and is not shown as chosen");
        UiGarageCard bay = GarageCardRules.Card(GarageRow("Custom shop", "reallife_garage_list_open"), UiGarageKind.Workshop, true, words);
        UiGarageCard stand = GarageCardRules.Card(GarageRow("Scooter", "reallife_bike_button_buy"), UiGarageKind.Stand, true, words);
        UiGarageCard wrongKind = GarageCardRules.Card(GarageRow("Row", GarageCardRules.ChooseKey), UiGarageKind.Stand, true, words);
        Check(bay.Button == "<reallife_garage_list_open>" && !bay.Main && !bay.Opens && stand.Button == "<reallife_bike_button_buy>" && wrongKind.Button != null && !wrongKind.Opens,
            "garage host: the workshop's, My vehicles' and a stand's rows are plain buttons with the list's own words, whatever key they use");
        UiGarageCard note = GarageCardRules.Card(GarageRow("Power", null, true, true), UiGarageKind.Shop, false, words);
        Check(note.Note && note.Button == null && !note.Enabled, "garage host: a note of the figures page is words alone");
        UiGarageCard tile = GarageCardRules.Card(GarageRow("Red", "reallife_garage_list_next", true, false, 0xC0282D), UiGarageKind.Shop, false, words);
        UiGarageCard bare = GarageCardRules.Card(GarageRow("Red", "reallife_garage_list_next"), UiGarageKind.Shop, false, words);
        Check(tile.Swatch == UiDraw.SwatchKey(0xC0282D) && bare.Swatch == null, "garage host: a paint's row carries its colour on a tile and a row without one carries none");
        var lines = new string[GarageCardRules.MostInfo];
        int n = GarageCardRules.Lines("You have $5. This visit: $0.\n\n 0-100 km/h: 5.4 s \n Hold the throttle key.", lines);
        Check(n == 3 && lines[0] == "You have $5. This visit: $0." && lines[1] == "0-100 km/h: 5.4 s" && lines[2] == "Hold the throttle key." && GarageCardRules.Lines(null, lines) == 0,
            "garage host: the headline's lines are the list's own, one to a line break, trimmed, with no empty one");
        Check(GarageCardRules.Lines(string.Join("\n", Enumerable.Repeat("x", 20).ToArray()), lines) == GarageCardRules.MostInfo,
            "garage host: no more lines than the band holds are read");
    }

    private static void UiGarageHostStrip()
    {
        UiGarageStripModel one = new UiGarageStripModel { Info = new[] { "You have $5." }, InfoCount = 1 };
        UiGarageStripModel long3 = new UiGarageStripModel
        {
            Info = new[] { "You have $48,210. This visit: $1,840.", "0-100 km/h: 5.4 s, with this build 5.1 s", "Running costs: wears 12% faster, insurance +8%. The exhaust is not street legal." },
            InfoCount = 3
        };
        float inner = UiGarageStrip.Width - UiTokens.S4 * 2f;
        Check(UiGarageStrip.InfoHeight(long3, inner) > UiGarageStrip.InfoHeight(one, inner) + UiTokens.PairRow * 2f - 1f,
            "garage host: the band holds every line the list says, each wrapped to the strip, instead of cutting it at one line");
        Check(UiGarageStrip.InfoHeight(new UiGarageStripModel(), inner) == 0f, "garage host: a list with no lines under its headline has no band");
        string effects = "$2,400. acceleration +12 %, top speed +4 %, braking +6 %, grip +3 %, wet grip +2 %, stiffness +10 %, weight -4 %, shift +5 %, ride -2 cm, wears 12 % faster, "
                         + "insurance +8 %, loud. Heard on the throttle key here";
        var longCard = new UiGarageCard { Name = "Engine: Stage 2 tune", Detail = effects, Button = "Next", Enabled = true };
        var shortCard = new UiGarageCard { Name = "Engine: Stage 2 tune", Detail = "Fitted", Button = "Next", Enabled = true };
        float cw = UiGarageStrip.Width - UiTokens.S4 * 2f - UiTokens.S3, bw = UiGarageStrip.ButtonWidth(new UiGarageStripModel { Cards = new[] { longCard }, CardCount = 1 }, cw);
        float tw = cw - UiTokens.S3 * 2f - bw - UiTokens.S3;
        Check(UiGarageStrip.CardHeight(ref longCard, cw, bw) > UiGarageStrip.CardHeight(ref shortCard, cw, bw)
              && UiTextRules.WrappedHeight(effects, UiStyle.Small, tw, UiGarageStrip.DetailLines) == UiTextRules.WrappedHeight(effects, UiStyle.Small, tw, 40),
            "garage host: a part's whole line of prices and effects stands in its row, the row growing to hold it, none of it cut");

        // A row of the cart's own option stands as a chip with no press of its own, a refused one as a greyed button, a free one as a button.
        UiGarageStripModel m = new UiGarageStripModel
        {
            Title = "Custom shop", Info = new[] { "You have $5." }, InfoCount = 1, Leave = "Cancel",
            Cards = new[]
            {
                new UiGarageCard { Name = "Gloss", Detail = "Fitted", Button = "Chosen", Enabled = false, Chosen = true },
                new UiGarageCard { Name = "Matt", Detail = "$400", Button = "Choose", Enabled = true },
                new UiGarageCard { Name = "Spoiler", Detail = "Does not fit", Button = "Fit", Enabled = false },
            },
            CardCount = 3,
        };
        var s = new UiScene();
        UiGarageStrip.Lay(s, m, UiFrame.FullMenu(16f / 9f, "Garage"), true, UiDefaultKeys.Keyboard);
        int pressable = 0, refused = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == UiGarageStrip.CardCommand)
            {
                pressable += s.Nodes[i].Disabled ? 0 : 1;
                refused += s.Nodes[i].Disabled ? 1 : 0;
            }
        }
        Check(pressable == 1 && refused == 2, "garage host: laid out, the chosen row and the refused row take no press and the free row does (" + pressable + " pressable, " + refused + " refused)");
    }

    private static void UiGarageHostWiring()
    {
        UiScreenDef def = UiScreens.All.FirstOrDefault(d => d.Id == "garage_shop");
        Check(def != null && def.Runner == "GarageShopScreen" && def.Section == UiScreenSwitches.Buy,
            "garage host: the garage strip is a registered screen with a class that runs it and a switch of its own");
        string face = ModSource(@"Ui\Garage\GarageFace.cs"), screen = ModSource(@"Ui\Garage\GarageShopScreen.cs"), live = ModSource(@"Ui\Garage\GarageShopLive.cs");
        string picker = ModSource(@"Ui\World\WorldPicker.cs"), kits = ModSource(@"Ui\World\WorldKits.cs");
        Check(MemberText(picker, "public bool Take(TravelList list)").Contains("GarageFace.Instance.Take(list)")
              && MemberText(picker, "public void Gone(TravelList list)").Contains("GarageFace.Instance.Gone(list)")
              && MemberText(picker, "public static void Give(string why)").Contains("GarageFace.Instance.Give(why)"),
            "garage host: the picker offers each list to the garage strip first, tells it when a list goes, and gives its list back with its own");
        string install = MemberText(kits, "public static void Install()");
        Check(install.Contains("UiScreenSwitches.On(GarageFace.Id)") && install.Contains("GarageFace.Instance.Give(\"its switch is off\")") && install.Contains("WorldPicker.Pause(")
              && MemberText(kits, "public static void Remove(string why)").Contains("WorldPicker.Give(why)") && MemberText(kits, "public static void Rearm()").Contains("GarageFace.Instance.Rearm()"),
            "garage host: the strip's switch keeps the face on without the picker's, gives its list back when it goes off, and a new city tries it again");
        Check(MemberText(face, "public bool Take(TravelList list)").Contains("!UiScreenSwitches.On(Id)") && MemberText(face, "public bool Take(TravelList list)").Contains("_broken")
              && MemberText(face, "public void Give(string why)").Contains("list.Release()") && MemberText(face, "private void Failed(Exception e)").Contains("_broken = true")
              && face.Contains("UiOverCanvas.Make(parent,") && face.Contains("_host.Failed = Failed;"),
            "garage host: the strip is taken only while its switch is on and it has not faulted, stands on the kit's canvas over the window, and a fault hands the window back");
        Check(MemberText(face, "public static bool Owns(TravelList list)").Contains("GarageList.Kind != GarageListKind.None") && face.Contains("\"RealLifeOverhaul Garage\""),
            "garage host: only the Garage's own window is taken, whoever of the Garage has it");
        string press = MemberText(screen, "public bool Press(int command, int arg)");
        Check(press.Contains("_list.Pick(_ids[arg])") && press.Contains("!_m.Cards[arg].Enabled") && press.Contains("_m.Cards[arg].Note") && press.Contains("UiDraw.CloseCommand")
              && MemberText(screen, "public bool Back()").Contains("_list.Close()") && MemberText(screen, "public bool Back()").Contains("_list.Locked"),
            "garage host: a press on a card is the list's own pick, a greyed one refused as its greyed button is, and Back and the close are the window's Cancel");
        Check(screen.Contains("public bool Dirty => _dirty || (_list != null && _list.Version != _seen)") && MemberText(screen, "public bool Press(int command, int arg)").Contains("_dirty = true")
              && MemberText(screen, "public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)").Contains("_list.Version != _seen"),
            "garage host: the strip is laid again after each press and each change the list says (its money, its page)");
        Check(MemberText(screen, "private void Read()").Contains("GarageShopLive.PageKey()") && MemberText(screen, "private void Read()").Contains("_m.Offset = 0f")
              && MemberText(screen, "public void Wheel(int clip, float notches)").Contains("_m.Offset") && MemberText(screen, "public void Reveal(int list, int index)").Contains("_m.Reveal = index"),
            "garage host: the strip keeps its place while a page is worked on, starts another page at its top, scrolls on the wheel and shows the card the keys reach");
        Check(live.Contains("list.Read(i, out int id") && live.Contains("shop.SwatchOf(id)") && live.Contains("GarageCardRules.Card(row, kind, main, UiText.T)") && live.Contains("list.InfoWords")
              && live.Contains("list.TitleWords") && live.Contains("shop.OnMain"),
            "garage host: the strip reads the list's own title, lines, rows, buttons and the shop's colours, and knows the shop's first page");

        // What the Garage says of its list for the kit: whose list it is, the page, a colour for each row.
        string bay = ModSource(@"Garage\GarageBay.cs"), shop = ModSource(@"Garage\GarageShop.cs"), pages = ModSource(@"Garage\GarageShop.Pages.cs");
        Check(bay.Contains("public static GarageListKind Kind =>") && bay.Contains("GarageBay.IsOwner(_owner)") && bay.Contains("GarageSummary.IsOwner(_owner)") && bay.Contains("_owner is TwoWheelers")
              && bay.Contains("public static GarageShop Shop =>"),
            "garage host: the Garage says whose list is in its window: the workshop, the shop, My vehicles or a stand");
        Check(shop.Contains("private void Row(TravelList list, string name, string detail, string button, bool enabled, Action act, int swatch = -1)") && shop.Contains("_swatches.Add(swatch)")
              && shop.Contains("public bool OnMain => _page == Page.Main;") && shop.Contains("public int SwatchOf(int id)") && shop.Contains("_swatches.Clear();"),
            "garage host: the shop keeps a colour beside each row it puts in the list, cleared with the rows, and says whether its first page is up");
        Check(pages.Contains("GarageCar.ToRgb(_fitted.StockColour())") && pages.Contains("() => PickPaint(next), sample);")
              && pages.Contains("\"reallife_garage_list_open\", true, _mix, mixed ? _cart.Paint : -1)") && pages.Contains("}, colour);"),
            "garage host: the factory colour, each family's shade, a mixed colour and each underglow colour carry their tile");
        // The old list is still what each owner fills, with its own calls: the strip changed no rule of the shop, the workshop, My vehicles or a stand.
        Check(shop.Contains("_acts[id]();") && shop.Contains("Fill();") && ModSource(@"Garage\TwoWheelers.Shop.cs").Contains("_acts[id]();") && !live.Contains("Crime.Pay") && !screen.Contains("Crime."),
            "garage host: the money and the work stay the owners' own calls, none of them made by the interface");
    }

    // Every button key a Garage list gives its rows is one the strip's entry names, so the screen's texts are all translated.
    private static void UiGarageHostKeys()
    {
        UiScreenDef def = UiScreens.All.FirstOrDefault(d => d.Id == "garage_shop");
        string[] files = { @"Garage\GarageShop.Pages.cs", @"Garage\GarageShop.cs", @"Garage\GarageBay.cs", @"Garage\GarageSummary.cs", @"Garage\TwoWheelers.Shop.cs", @"Transport\CarOwnership.Garage.cs" };
        var missing = new List<string>();
        int looked = 0;
        foreach (string file in files)
        {
            string code = ModSource(file);
            foreach (Match call in Regex.Matches(code, @"\b(?:list\.Add|Row)\("))
            {
                List<string> args = GarageCallArgs(code, call.Index + call.Length);
                bool row = call.Value.StartsWith("Row");
                if (args.Count < 5 || (row && args[0] != "list"))
                {
                    continue;   // not a row put in a list (the method's own line)
                }
                string button = args[3];   // list.Add(id, name, detail, button, ...) and Row(list, name, detail, button, ...) alike
                foreach (Match key in Regex.Matches(button, "\"([a-z0-9_]+)\""))
                {
                    looked++;
                    if (def == null || Array.IndexOf(def.Keys, key.Groups[1].Value) < 0)
                    {
                        missing.Add(key.Groups[1].Value);
                    }
                }
            }
        }
        Check(looked >= 20 && missing.Count == 0, "garage host: every button key the Garage's lists give their rows is named by the strip's entry (" + looked + " found"
            + (missing.Count > 0 ? ", missing " + string.Join(", ", missing.Distinct()) : string.Empty) + ")");
    }

    // The top-level arguments of a call whose "(" has just been passed, split at the commas outside brackets and quotes.
    private static List<string> GarageCallArgs(string code, int from)
    {
        var args = new List<string>(8);
        int depth = 0, start = from;
        bool quote = false;
        for (int i = from; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '"' && code[i - 1] != '\\')
            {
                quote = !quote;
            }
            if (quote)
            {
                continue;
            }
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
            }
            else if ((c == ')' || c == ']' || c == '}') && depth-- == 0)
            {
                args.Add(code.Substring(start, i - start).Trim());
                return args;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(code.Substring(start, i - start).Trim());
                start = i + 1;
            }
            else if (c == ';' && depth == 0)
            {
                break;
            }
        }
        return args;
    }
}
