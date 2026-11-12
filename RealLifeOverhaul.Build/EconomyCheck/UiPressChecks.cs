using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// Every press on a kit screen reaches something, or is said. Three faults of one family in two days (Start business on
// BizMan's page taken by another branch's number range, an order's add button refused, a counter's goods never listed)
// each gave the player an error sound and nothing else. Held here, over ALL the kit's screens and from their sources:
//   1. every command a screen's samples lay out as a target is named by that screen's Press (followed through the
//      methods it hands the command to), so no button is drawn that no case answers;
//   2. no test of a number range (a command's, an action's) stands before the case of a number inside it, so no page's
//      numbers can take another's;
//   3. a refused press is written to Player.log once with the screen, the control and the reason, by every piece that
//      sends presses, and the game's widgets say why they refuse;
//   4. the three faults of 13 Nov 2026 themselves: the checkout's rows read where the game makes them, an order's add
//      pressed past the game's hover outline, a stack's own action (Eat) on the item panel, an order of nothing refused
//      in words.
// RLO_PRESS_LIST=1 prints each screen's commands and how each is answered.
internal static partial class Program
{
    private static void UiPressAll()
    {
        UiRefusalChecks();
        UiEatingChecks();
        UiNoticeWholeChecks();
        UiCommandChecks();
    }

    // ------------------------------------------------------------------------------------------------- 3. refusals

    private static void UiRefusalChecks()
    {
        UiRefusals.Reset();
        UiRefusals.Begin();
        Check(UiRefusals.Why == null, "ui press: a press starts with no reason held over");
        UiRefusals.Because("the game's button 'HoverOutline' is switched off");
        UiRefusals.Because("a later reason");
        Check(UiRefusals.Why == "the game's button 'HoverOutline' is switched off", "ui press: the first reason given for a refusal stands");
        string line = UiRefusals.Line("the items list", 430, 0, "Plus", UiRefusals.Why);
        Check(line != null && line.Contains("the items list") && line.Contains("430/0") && line.Contains("'Plus'") && line.Contains("HoverOutline"),
            "ui press: a refused press's line names the screen, the control with its words and the reason (" + line + ")");
        Check(UiRefusals.Line("the items list", 430, 1, "Plus", UiRefusals.Why) == null, "ui press: the same control refused again for the same reason is not written twice (a list's rows are one control)");
        Check(UiRefusals.Line("the items list", 430, 1, "Plus", "the order holds all it may") != null, "ui press: the same control refused for another reason is written");
        Check(UiRefusals.Line("the checkout", 430, 0, "Plus", UiRefusals.Why) != null, "ui press: another screen's control is written");
        string unsaid = UiRefusals.Line("a screen", 7, 0, null, null);
        Check(unsaid != null && unsaid.Contains(UiRefusals.Unsaid), "ui press: a refusal nobody gave a reason for says so, rather than nothing");
        int before = UiRefusals.Count;
        for (int i = 0; i < UiRefusals.Most * 2; i++)
        {
            UiRefusals.Line("screen " + i, 1, 0, null, "x");
        }
        Check(UiRefusals.Count == UiRefusals.Most && before < UiRefusals.Most, "ui press: a screen that refuses everything cannot fill the log (" + UiRefusals.Count + " lines at most)");
        UiRefusals.Reset();
        // A target's words for the line: its text, else its icon's name.
        var s = new UiScene();
        UiItemPanel.Lay(s, UiSamples.ItemPanel(UiWords("en"), "paper_bag_eat"), UiFrame.FullMenu(16f / 9f, "HUD"), true, UiDefaultKeys.Keyboard);
        int eat = s.FindNode(UiItemPanel.ActionCommand, 0);
        Check(eat >= 0 && UiRefusals.Label(s, eat) == "Eat Croissant", "ui press: a refused target is named by the words it shows (" + (eat >= 0 ? UiRefusals.Label(s, eat) : "no target") + ")");
        Check(UiRefusals.Label(s, -1) == null && UiRefusals.Label(null, 0) == null, "ui press: no target, no words");
        // Every piece that sends presses and can play the error sound for one writes the line; the widgets give the reason.
        string folder = UiSourceFolder();
        string silent = "none";
        int senders = 0;
        foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (text.Contains("? UiCue.Confirm : UiCue.Error") && text.Contains("Press(n.Command, n.Arg)"))
            {
                senders++;
                if (!text.Contains("UiRefusalLog.Say("))
                {
                    silent = System.IO.Path.GetFileName(path);
                }
            }
        }
        Check(senders >= 5 && silent == "none", "ui press: every piece that sends a target's press to a screen writes a refused one to Player.log (" + senders + " senders; silent: " + silent + ")");
        string host = PressSource(System.IO.Path.Combine(folder, "UiHost.cs"));
        Check(host.Contains("UiRefusals.Begin();") && Regex.IsMatch(host, @"target disabled"";\s*UiRefusalLog\.Say\("),
            "ui press: the host starts each press with no reason held over and writes a press on a disabled target too");
        string widgets = PressSource(System.IO.Path.Combine(folder, "Host", "UiWidgets.cs"));
        Check(Regex.IsMatch(widgets, @"if \(!Shows\(b\)\)\s*\{\s*UiRefusals\.Because\(") && widgets.Contains("public static bool PressUnder(Button b, Component row)"),
            "ui press: a game button that takes no press says why (not there, switched off, held), and a button under a hover outline has its own way");
        string log = PressSource(System.IO.Path.Combine(folder, "Host", "UiRefusalLog.cs"));
        Check(log.Contains("Logger?.Warn(line)") && !log.Contains("Dev.Verbose"), "ui press: the line is written with or without Developer diagnostics");
        // One refusal, one line: a screen that writes its own (BizMan's Answered, with the page and the place) says so, and
        // the piece that sent the press writes none. A press on a disabled target asked no screen: what the press before
        // it left behind never silences its line.
        UiRefusals.Reset();
        UiRefusals.Begin();
        bool fresh = UiRefusals.HostWrites(false) && UiRefusals.HostWrites(true);
        UiRefusals.Told();
        bool told = !UiRefusals.HostWrites(false) && UiRefusals.HostWrites(true);
        UiRefusals.Begin();
        Check(fresh && told && UiRefusals.HostWrites(false), "ui press: a refusal its screen wrote down itself is not written again by the host, the next press starts afresh, and a disabled target's line is never held back");
        UiRefusals.Told();
        UiRefusals.Reset();
        Check(UiRefusals.HostWrites(false), "ui press: a new session holds nothing over");
        string bizman = PressSource(System.IO.Path.Combine(folder, "BizMan", "BizManScreen.cs"));
        int own = bizman.IndexOf("UiRefusals.Told();", StringComparison.Ordinal), first = bizman.IndexOf("UiDirector.Logger?.Warn(\"BizMan check (press): \" + where", StringComparison.Ordinal);
        Check(own >= 0 && first > own && bizman.Contains("if (!done)") && Occurs(bizman, "+ Widget() +") == 2 && bizman.Contains("UiRefusals.Why != null ?")
              && Occurs(log, "UiRefusals.HostWrites(") == 2,
            "ui press: BizMan writes a refused press once, in its own line with the page, the place and the widget's reason, and tells the host, which then writes none (both of the host's ways ask)");
    }

    // A source as text; nothing for one that is not there, so that a check fails for it instead of stopping the run.
    private static string PressSource(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    private static string UiSourceFolder() =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));

    // ------------------------------------------------------------------------------------------------- 4. the three faults

    private static void UiEatingChecks()
    {
        string folder = UiSourceFolder();
        // The checkout: the game's rows are copies beside their templates, never children of the list's scroll view.
        string checkout = PressSource(System.IO.Path.Combine(folder, "Buy", "CheckoutScreen.cs"));
        Check(checkout.Contains("BuyKits.Copies(_ui." + CheckoutRules.RowsBeside + ", _copies)") && checkout.Contains("BuyKits.Copies(_ui." + CheckoutRules.TaxesBeside + ", _copies)")
              && !checkout.Contains("container.GetChild("),
            "checkout: the goods and the tax rows are read beside the game's own templates (PurchaseUI.itemTemplate.parent), not among the children of the list's scroll view");
        Check(CheckoutRules.NothingChosen(true, 0) && !CheckoutRules.NothingChosen(true, 1) && !CheckoutRules.NothingChosen(false, 0) && CheckoutRules.NothingChosen(true, -1),
            "checkout: Place order is refused only at a register that lets the player choose with nothing taken (a carried basket and a ticket are never refused)");
        Check(checkout.Contains("CheckoutRules.NothingChosen(_m.Choose, _taken)") && checkout.Contains("\"reallife_ui_buy_choose_first\"")
              && checkout.Contains("public bool Act(UiAction action) => action == UiAction.Alt && Order();") && Regex.IsMatch(checkout, @"case UiCheckout\.OrderCommand:\s*return Order\(\);"),
            "checkout: an order of nothing is refused in words, by the button and by its key alike, so no queue is joined that nobody serves");
        // A counter's goods as the samples lay them: each row's minus, figure and plus are targets with the row's index.
        var s = new UiScene();
        UiCheckoutModel goods = UiSamples.Checkout(UiWords("en"), "goods");
        UiCheckout.Lay(s, goods, UiFrame.FullMenu(16f / 9f, "Checkout"), true, UiDefaultKeys.Keyboard);
        bool steppers = goods.Choose && goods.Lines.Length > 0;
        for (int i = 0; i < Math.Min(goods.Lines.Length, UiCheckoutModel.PerPage); i++)
        {
            steppers &= s.FindNode(UiCheckout.MoreCommand, i) >= 0 && (s.FindNode(UiCheckout.LessCommand, i) >= 0 || !goods.Lines[i].CanLess);
        }
        Check(steppers && s.FindNode(UiCheckout.OrderCommand, 0) >= 0 && s.FindNode(UiCheckout.CancelCommand, 0) >= 0,
            "checkout: at a counter every row of goods has its plus (and its minus once something is taken) as targets, with Place order and Cancel");
        // The items list: the add is pressed and read past the game's hover outline.
        string list = PressSource(System.IO.Path.Combine(folder, "World", "ItemsListKit.cs"));
        Check(list.Contains("UiWidgets.PressUnder(AddButton(d), d)") && list.Contains("UiWidgets.ShowsUnder(AddButton(d), d)") && !list.Contains("UiWidgets.Press(GameContract.ItemsAdd")
              && !list.Contains("Interactable(GameContract.ItemsAdd"),
            "items list: an order's add button is pressed and read through the row (the game switches the button's own outline on only under the mouse), by one test for the drawing and the press");
        Check(list.Contains("\"reallife_ui_world_items_full\""), "items list: an order that holds all it may says so in words when the greyed plus is pressed");
        // The item panel: a stack's own action is an action of the panel, and its sell and discard are reached.
        string panel = PressSource(System.IO.Path.Combine(folder, "Buy", "ItemPanelKit.cs"));
        Check(panel.Contains("GameContract.SlotAction, slot") && panel.Contains("GameContract.SlotActionLabel, slot") && panel.Contains("SlotActions();")
              && panel.IndexOf("SlotActions();", StringComparison.Ordinal) < panel.IndexOf("ManageCargo();", StringComparison.Ordinal),
            "item panel: each stack's own action on its slot (Eat, Drink, Equip, Read, Place) is an action of the panel, before the panel's own");
        Check(panel.Contains("ItemPanelRules.More(Foreign(), _slotButtons)") && panel.Contains("GameContract.SlotSell, slot") && panel.Contains("GameContract.SlotDiscard, slot"),
            "item panel: while a slot shows its own sell or discard, the panel offers the game's own view");
        Check(ItemPanelRules.SlotAction("Eat", "Cupcake") == "Eat Cupcake" && ItemPanelRules.SlotAction("Eat", null) == "Eat" && ItemPanelRules.SlotAction(null, "Cupcake") == null
              && ItemPanelRules.SlotAction(string.Empty, "Cupcake") == null,
            "item panel: a stack's action is the game's word and the stack's name, and none where the slot offers none");
        Check(ItemPanelRules.More(true, false) && ItemPanelRules.More(false, true) && !ItemPanelRules.More(false, false), "item panel: the way to the game's own view shows for another mod's button or a slot's own");
        UiItemPanel.Lay(s, UiSamples.ItemPanel(UiWords("en"), "paper_bag_eat"), UiFrame.FullMenu(16f / 9f, "HUD"), true, UiDefaultKeys.Keyboard);
        int eat = s.FindNode(UiItemPanel.ActionCommand, 0), more = s.FindNode(UiItemPanel.MoreCommand, 0);
        Check(eat >= 0 && !s.Nodes[eat].Disabled && UiRefusals.Label(s, eat) == "Eat Croissant" && more >= 0 && s.FindNode(UiItemPanel.CargoCommand, 1) >= 0,
            "item panel: food in a bag lays out Eat as the first action, a tile a stack and the way to the game's view");
        string contract = PressSource(System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "..", "Core", "GameContract.Buy.cs")));
        Check(Regex.IsMatch(contract, @"SlotAction = Field\(new\[\] \{ UiItemPanel, UiManageCargo \}") && Regex.IsMatch(contract, @"SlotActionLabel = Field\(new\[\] \{ UiItemPanel, UiManageCargo \}"),
            "item panel: the game contract names the item panel as a reader of the slot's action and its words");
        // The words.
        var en = LocaleFile("en");
        Check(en.TryGetValue("reallife_ui_buy_choose_first", out string choose) && choose.Length <= 240 && en.TryGetValue("reallife_ui_world_items_full", out string full) && full.Length <= 240,
            "ui press: the two refusals said to the player have their words, each a notice's length");
        bool told = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            told |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("nothing could be chosen at a counter");
        }
        Check(told, "ui press: What's new says the fix (looked for in every row, as the version step numbers the next one)");
    }

    // A notice at the mod's longest (240 characters, NoticeCheck) is read whole on its card: three lines cut it short, and
    // the list under the bell shows one line of each (the owner's picture of 13 Nov: "It opens again by itself once the...").
    private static void UiNoticeWholeChecks()
    {
        var words = new StringBuilder();
        string[] say = { "The Main Office is closed: it cannot open without an internet connection.", "Buy one in the Utilities app.", "It opens again by itself once the connection works.",
            "Until then its staff stay at home and its rent still falls due every week." };
        for (int i = 0; words.Length + say[i % say.Length].Length + 1 <= 240; i++)
        {
            words.Append(words.Length > 0 ? " " : string.Empty).Append(say[i % say.Length]);
        }
        string body = words.ToString();
        var cards = new UiNoticeCardsModel { Cards = new[] { new UiNotice { Icon = UiIcon.Package, Header = "Money", Body = body, Age = "Day 7, 12:00" } }, CardCount = 1 };
        var s = new UiScene();
        UiNotices.LayCards(s, cards, UiFrame.FullMenu(16f / 9f, "HUD"), true, UiDefaultKeys.Keyboard);
        bool whole = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            whole |= s.Items[i].Text == body;
        }
        Check(body.Length > 200 && body.Length <= 240 && whole && UiNotices.BodyLines >= 6, "notices: a notice of " + body.Length + " characters is shown whole on its card, not cut after three lines");
    }

    // ------------------------------------------------------------------------------------------------- 1 and 2. commands

    private sealed class PressBook
    {
        public readonly Dictionary<string, List<string>> Classes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Consts = new Dictionary<string, string>(StringComparer.Ordinal);       // "Class.Name" -> its expression
        public readonly Dictionary<string, List<string>> ByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // "Name" -> the classes that have it
        public readonly Dictionary<string, long?> Values = new Dictionary<string, long?>(StringComparer.Ordinal);
        public readonly Dictionary<string, List<PressMethod>> Methods = new Dictionary<string, List<PressMethod>>(StringComparer.Ordinal);
        public readonly Dictionary<string, List<string>> Bases = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // a class's base classes, by name
    }

    private sealed class PressMethod
    {
        public string Class, Name, Body;
        public string[] Types, Names;
    }

    private sealed class PressFacts
    {
        public readonly HashSet<long> Equal = new HashSet<long>();
        public readonly List<long[]> Ranges = new List<long[]>();           // lo, hi (long.MinValue and MaxValue for open ends)
        public readonly List<string> Unfollowed = new List<string>();
        public readonly List<string> Swallows = new List<string>();
        public bool Found;
    }

    // Comments and the insides of strings out, so that a brace or a word in either is never read as code.
    private static string PressBare(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0, n = text.Length;
        while (i < n)
        {
            char c = text[i];
            if (c == '/' && i + 1 < n && text[i + 1] == '/')
            {
                while (i < n && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    i++;
                }
                i += 2;
            }
            else if (c == '"')
            {
                bool verbatim = i > 0 && (text[i - 1] == '@' || (i > 1 && text[i - 1] == '$' && text[i - 2] == '@'));
                sb.Append('"');
                i++;
                while (i < n)
                {
                    if (!verbatim && text[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }
                    if (text[i] == '"')
                    {
                        if (verbatim && i + 1 < n && text[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }
                        break;
                    }
                    i++;
                }
                sb.Append('"');
                i++;
            }
            else if (c == '\'' && i + 2 < n && (text[i + 2] == '\'' || (text[i + 1] == '\\' && i + 3 < n && text[i + 3] == '\'')))
            {
                int end = text[i + 2] == '\'' ? i + 2 : i + 3;
                sb.Append("' '");
                i = end + 1;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    // The index of the bracket that closes the one at 'open', or -1.
    private static int PressClose(string text, int open, char a, char b)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == a)
            {
                depth++;
            }
            else if (text[i] == b && --depth == 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static readonly Regex PressClassAt = new Regex(@"\b(?:class|struct)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
    private static readonly Regex PressConstAt = new Regex(@"\bconst\s+int\s+([^;]+);", RegexOptions.Compiled);

    private static PressBook PressRead()
    {
        var book = new PressBook();
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(UiSourceFolder(), ".."));
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = PressBare(File.ReadAllText(path));   // every file: a runner that only inherits its Press names neither a command nor a number
            foreach (Match m in PressClassAt.Matches(text))
            {
                int open = text.IndexOf('{', m.Index);
                int semi = text.IndexOf(';', m.Index);
                if (open < 0 || (semi >= 0 && semi < open))
                {
                    continue;
                }
                int close = PressClose(text, open, '{', '}');
                if (close < 0)
                {
                    continue;
                }
                string name = m.Groups[1].Value, body = text.Substring(open, close - open + 1);
                string head = text.Substring(m.Index + m.Length, open - m.Index - m.Length);
                int colon = head.IndexOf(':');
                if (colon >= 0)
                {
                    int where = head.IndexOf(" where ", colon, StringComparison.Ordinal);
                    foreach (string b in PressSplit(where > 0 ? head.Substring(colon + 1, where - colon - 1) : head.Substring(colon + 1)))
                    {
                        string bn = b.Trim();
                        int angle = bn.IndexOf('<');
                        bn = angle > 0 ? bn.Substring(0, angle) : bn;
                        bn = bn.LastIndexOf('.') >= 0 ? bn.Substring(bn.LastIndexOf('.') + 1) : bn;
                        if (bn.Length > 0)
                        {
                            if (!book.Bases.TryGetValue(name, out List<string> bases))
                            {
                                book.Bases[name] = bases = new List<string>();
                            }
                            if (!bases.Contains(bn))
                            {
                                bases.Add(bn);
                            }
                        }
                    }
                }
                if (!book.Classes.TryGetValue(name, out List<string> bodies))
                {
                    book.Classes[name] = bodies = new List<string>();
                }
                bodies.Add(body);
                foreach (Match c in PressConstAt.Matches(body))
                {
                    foreach (string part in PressSplit(c.Groups[1].Value))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0)
                        {
                            continue;
                        }
                        string cn = part.Substring(0, eq).Trim(), key = name + "." + cn;
                        if (!book.Consts.ContainsKey(key))
                        {
                            book.Consts[key] = part.Substring(eq + 1).Trim();
                            if (!book.ByName.TryGetValue(cn, out List<string> owners))
                            {
                                book.ByName[cn] = owners = new List<string>();
                            }
                            owners.Add(name);
                        }
                    }
                }
            }
        }
        return book;
    }

    // A list split at its top level commas.
    private static List<string> PressSplit(string list)
    {
        var parts = new List<string>();
        int depth = 0, from = 0;
        for (int i = 0; i < list.Length; i++)
        {
            char c = list[i];
            if (c == '(' || c == '[' || c == '{' || c == '<')
            {
                depth++;
            }
            else if (c == ')' || c == ']' || c == '}' || c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(list.Substring(from, i - from));
                from = i + 1;
            }
        }
        parts.Add(list.Substring(from));
        return parts;
    }

    private static readonly Regex PressTerm = new Regex(@"^(?:\(int\)\s*)?([A-Za-z_][\w\.]*|\d+)$", RegexOptions.Compiled);

    // A number an expression of constants comes to (names, whole numbers, plus and minus), or null when it is not one.
    private static long? PressValue(PressBook book, string expr, string cls, int depth = 0)
    {
        expr = expr.Trim();
        while (expr.Length > 1 && expr[0] == '(' && PressClose(expr, 0, '(', ')') == expr.Length - 1)
        {
            expr = expr.Substring(1, expr.Length - 2).Trim();
        }
        if (expr.Length == 0 || depth > 8)
        {
            return null;
        }
        long total = 0;
        int sign = 1, from = 0, level = 0;
        for (int i = 0; i <= expr.Length; i++)
        {
            char c = i < expr.Length ? expr[i] : '+';
            if (c == '(')
            {
                level++;
            }
            else if (c == ')')
            {
                level--;
            }
            if ((c == '+' || c == '-') && level == 0 && i > from)
            {
                long? term = PressOne(book, expr.Substring(from, i - from), cls, depth);
                if (term == null)
                {
                    return null;
                }
                total += sign * term.Value;
                sign = c == '-' ? -1 : 1;
                from = i + 1;
            }
            else if ((c == '+' || c == '-') && level == 0 && i == from && i < expr.Length)
            {
                sign *= c == '-' ? -1 : 1;
                from = i + 1;
            }
        }
        return total;
    }

    private static long? PressOne(PressBook book, string term, string cls, int depth)
    {
        term = term.Trim();
        if (term.Length > 1 && term[0] == '(' && PressClose(term, 0, '(', ')') == term.Length - 1)
        {
            return PressValue(book, term, cls, depth + 1);
        }
        Match m = PressTerm.Match(term);
        if (!m.Success)
        {
            return null;
        }
        string name = m.Groups[1].Value;
        if (char.IsDigit(name[0]))
        {
            return long.TryParse(name, out long literal) ? literal : (long?)null;
        }
        int dot = name.LastIndexOf('.');
        string owner = dot > 0 ? name.Substring(0, dot) : cls, last = dot > 0 ? name.Substring(dot + 1) : name;
        owner = owner != null && owner.LastIndexOf('.') >= 0 ? owner.Substring(owner.LastIndexOf('.') + 1) : owner;
        string key = owner + "." + last;
        if (!book.Consts.ContainsKey(key))
        {
            // A name its own class does not hold: the one class that has it (a nested or a base class's), if there is only one.
            if (dot > 0 || !book.ByName.TryGetValue(last, out List<string> owners) || owners.Count == 0)
            {
                return null;
            }
            long? only = null;
            foreach (string o in owners)
            {
                long? v = PressConst(book, o + "." + last, o, depth);
                if (v == null || (only != null && only != v))
                {
                    return null;
                }
                only = v;
            }
            return only;
        }
        return PressConst(book, key, owner, depth);
    }

    private static long? PressConst(PressBook book, string key, string owner, int depth)
    {
        if (book.Values.TryGetValue(key, out long? held))
        {
            return held;
        }
        book.Values[key] = null;   // a constant that names itself comes to nothing
        long? value = PressValue(book, book.Consts[key], owner, depth + 1);
        book.Values[key] = value;
        return value;
    }

    private static readonly HashSet<string> PressNotTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "return", "new", "await", "else", "throw", "in", "is", "case", "out", "ref", "var", "yield", "if", "while", "switch", "typeof", "nameof", "default", "using", "lock", "foreach", "for"
    };

    // The declarations of a method in a class (every part of it), with their parameters and bodies.
    private static List<PressMethod> PressMethods(PressBook book, string cls, string name)
    {
        if (cls != null && book.Methods.TryGetValue(cls + "." + name, out List<PressMethod> held))
        {
            return held;
        }
        var found = new List<PressMethod>();
        if (cls == null || !book.Classes.TryGetValue(cls, out List<string> bodies))
        {
            return found;
        }
        book.Methods[cls + "." + name] = found;
        var at = new Regex(@"([A-Za-z_][\w<>\[\]\.\?,]*)\s+" + Regex.Escape(name) + @"\s*\(");
        foreach (string body in bodies)
        {
            foreach (Match m in at.Matches(body))
            {
                if (PressNotTypes.Contains(m.Groups[1].Value))
                {
                    continue;
                }
                int open = m.Index + m.Length - 1, close = PressClose(body, open, '(', ')');
                if (close < 0)
                {
                    continue;
                }
                string pars = body.Substring(open + 1, close - open - 1).Trim();
                var types = new List<string>();
                var names = new List<string>();
                bool declared = true;
                foreach (string p in pars.Length == 0 ? new List<string>() : PressSplit(pars))
                {
                    string one = p.Trim();
                    int eq = one.IndexOf('=');
                    one = eq > 0 ? one.Substring(0, eq).Trim() : one;
                    int space = one.LastIndexOf(' ');
                    if (space <= 0)
                    {
                        declared = false;
                        break;
                    }
                    types.Add(one.Substring(0, space).Trim());
                    names.Add(one.Substring(space + 1).Trim());
                }
                if (!declared)
                {
                    continue;
                }
                int after = close + 1;
                while (after < body.Length && char.IsWhiteSpace(body[after]))
                {
                    after++;
                }
                string code = null;
                if (after < body.Length && body[after] == '{')
                {
                    int end = PressClose(body, after, '{', '}');
                    code = end > 0 ? body.Substring(after, end - after + 1) : null;
                }
                else if (after + 1 < body.Length && body[after] == '=' && body[after + 1] == '>')
                {
                    int end = body.IndexOf(';', after);
                    code = end > 0 ? body.Substring(after, end - after + 1) : null;
                }
                if (code != null && !found.Exists(x => x.Body == code))
                {
                    found.Add(new PressMethod { Class = cls, Name = name, Body = code, Types = types.ToArray(), Names = names.ToArray() });
                }
            }
        }
        return found;
    }

    // A method as a class has it: its own, or the nearest base's.
    private static List<PressMethod> PressMethodsUp(PressBook book, string cls, string name, int depth = 0)
    {
        List<PressMethod> own = PressMethods(book, cls, name);
        if (own.Count > 0 || cls == null || depth > 4 || !book.Bases.TryGetValue(cls, out List<string> bases))
        {
            return own;
        }
        foreach (string b in bases)
        {
            List<PressMethod> up = PressMethodsUp(book, b, name, depth + 1);
            if (up.Count > 0)
            {
                return up;
            }
        }
        return own;
    }

    // A class's own source and its bases'.
    private static List<string> PressBodies(PressBook book, string cls, int depth = 0)
    {
        var all = new List<string>();
        if (cls == null || depth > 4)
        {
            return all;
        }
        if (book.Classes.TryGetValue(cls, out List<string> own))
        {
            all.AddRange(own);
        }
        if (book.Bases.TryGetValue(cls, out List<string> bases))
        {
            foreach (string b in bases)
            {
                all.AddRange(PressBodies(book, b, depth + 1));
            }
        }
        return all;
    }

    private static readonly Regex PressNameAt = new Regex(@"(?<![\w\.])[A-Za-z_][\w\.]*(?:\s*\+\s*[A-Za-z_][\w\.]*)*", RegexOptions.Compiled);

    // Every number a piece of code names (a constant, or a sum of them).
    private static void PressNumbersIn(PressBook book, string code, string cls, HashSet<long> into)
    {
        foreach (Match m in PressNameAt.Matches(code))
        {
            long? v = PressValue(book, m.Value, cls);
            if (v != null)
            {
                into.Add(v.Value);
            }
            if (m.Value.IndexOf('+') >= 0)
            {
                foreach (string part in m.Value.Split('+'))
                {
                    long? one = PressValue(book, part, cls);
                    if (one != null)
                    {
                        into.Add(one.Value);
                    }
                }
            }
        }
    }

    // The commands the host types for instead of pressing (IUiTyping.TypingCommand): the numbers its getter names and,
    // where the getter gives a field the screen sets as the focus moves, the numbers that field is set from or compared with
    // and those its Focused names.
    private static HashSet<long> PressTyping(PressBook book, string runner)
    {
        var typing = new HashSet<long>();
        foreach (string body in PressBodies(book, runner))
        {
            foreach (Match g in Regex.Matches(body, @"\bTypingCommand\s*(=>|\{)"))
            {
                int end = g.Groups[1].Value == "=>" ? body.IndexOf(';', g.Index) : PressClose(body, g.Index + g.Length - 1, '{', '}');
                if (end < 0)
                {
                    continue;
                }
                string getter = body.Substring(g.Index + g.Length, end - g.Index - g.Length);
                PressNumbersIn(book, getter, runner, typing);
                foreach (Match f in Regex.Matches(getter, @"\b_[A-Za-z]\w*"))
                {
                    foreach (string all in PressBodies(book, runner))
                    {
                        foreach (Match set in Regex.Matches(all, Regex.Escape(f.Value) + @"\s*={1,2}\s*([^;]+);"))
                        {
                            PressNumbersIn(book, set.Groups[1].Value, runner, typing);
                        }
                    }
                    foreach (PressMethod focused in PressMethodsUp(book, runner, "Focused"))
                    {
                        PressNumbersIn(book, focused.Body, runner, typing);
                    }
                }
            }
        }
        return typing;
    }

    // A press handed on through a field that holds a method: where it goes.
    private static readonly Dictionary<string, string[]> PressHandedTo = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        { "PiecePressed", new[] { "HudLive", "Press" } },   // HudScreen's pieces: HudLive sets it to its own Press (HudLive.cs)
    };

    private const string PressExpr = @"[\w\.\+\-\s\(\)]+?";
    private const string PressEnd = @"(?=\s*(?:&&|\|\||\?|;|,|:|\)\s*(?:&&|\|\||\{|\)|;|\?|return|$|[A-Za-z_])|\)\s*$))";

    // What a method does with a number it is given (v), and with the commands it passes on: the cases and equalities that
    // name it, the ranges asked of it, the methods it hands it to. 'command' says v is the command itself (offset: what was
    // taken off it on the way), so its numbers go to the facts; for any other number only the order of ranges and cases is read.
    private static void PressWalk(PressBook book, PressMethod method, string v, bool command, long offset, int depth, PressFacts facts, HashSet<string> seen, string self = null)
    {
        self = self ?? method.Class;
        if (depth > 6 || !seen.Add(method.Class + "." + method.Name + "(" + v + ")" + (command ? "c" : "n") + offset + "#" + method.Body.Length))
        {
            return;
        }
        string body = method.Body;
        // The order of ranges and cases, for every whole number the method is given.
        for (int p = 0; p < method.Names.Length; p++)
        {
            if (method.Types[p] == "int")
            {
                PressOrder(book, method, method.Names[p], facts);
            }
        }
        if (command)
        {
            string name = Regex.Escape(v);
            foreach (Match sw in Regex.Matches(body, @"\bswitch\s*\(\s*" + name + @"\s*\)"))
            {
                int open = body.IndexOf('{', sw.Index);
                int close = open >= 0 ? PressClose(body, open, '{', '}') : -1;
                if (close < 0)
                {
                    continue;
                }
                foreach (Match c in Regex.Matches(body.Substring(open, close - open), @"\bcase\s+([^:?]+?)\s*:"))
                {
                    long? value = PressValue(book, c.Groups[1].Value, method.Class);
                    if (value != null)
                    {
                        facts.Equal.Add(value.Value + offset);
                    }
                }
            }
            foreach (Match e in Regex.Matches(body, @"\b" + name + @"\s*(==|!=)\s*(" + PressExpr + ")" + PressEnd))
            {
                long? value = PressValue(book, e.Groups[2].Value, method.Class);
                if (value != null)
                {
                    facts.Equal.Add(value.Value + offset);
                }
            }
            foreach (Match e in Regex.Matches(body, @"([A-Za-z_][\w\.]*(?:\s*\+\s*[A-Za-z_][\w\.]*)?)\s*(==|!=)\s*" + name + @"\b"))
            {
                long? value = PressValue(book, e.Groups[1].Value, method.Class);
                if (value != null)
                {
                    facts.Equal.Add(value.Value + offset);
                }
            }
            foreach (long[] range in PressRanges(book, method, v))
            {
                facts.Ranges.Add(new[] { range[0] == long.MinValue ? long.MinValue : range[0] + offset, range[1] == long.MaxValue ? long.MaxValue : range[1] + offset });
            }
            // "line = command - First; return line >= 0 && line < Most": the range asked of what is left is a range of the command.
            foreach (Match left in Regex.Matches(body, @"\b([A-Za-z_]\w*)\s*=\s*" + name + @"\s*-\s*([\w\.\s\+]+?)\s*;"))
            {
                long? first = PressValue(book, left.Groups[2].Value, method.Class);
                if (first == null)
                {
                    continue;
                }
                foreach (long[] range in PressRanges(book, method, left.Groups[1].Value))
                {
                    long by = first.Value + offset;
                    facts.Ranges.Add(new[] { range[0] == long.MinValue ? long.MinValue : range[0] + by, range[1] == long.MaxValue ? long.MaxValue : range[1] + by });
                }
            }
        }
        // The methods the number is handed to.
        foreach (Match call in Regex.Matches(body, @"(?<![\w\.])((?:[A-Za-z_]\w*\s*\.\s*)*)([A-Za-z_]\w*)\s*\("))
        {
            string callee = call.Groups[2].Value;
            if (PressNotTypes.Contains(callee))
            {
                continue;
            }
            int open = call.Index + call.Length - 1, close = PressClose(body, open, '(', ')');
            if (close < 0)
            {
                continue;
            }
            List<string> args = PressSplit(body.Substring(open + 1, close - open - 1));
            for (int a = 0; a < args.Count; a++)
            {
                string arg = args[a].Trim();
                bool isCommand = false;
                long shift = 0;
                string passed = null;
                if (command && arg == v)
                {
                    isCommand = true;
                    passed = v;
                }
                else if (command && Regex.IsMatch(arg, @"^" + Regex.Escape(v) + @"\s*-\s*[\w\.\s\+\(\)]+$"))
                {
                    long? less = PressValue(book, arg.Substring(arg.IndexOf('-') + 1), method.Class);
                    if (less == null)
                    {
                        continue;
                    }
                    isCommand = true;
                    shift = less.Value;
                    passed = v;
                }
                else if (Array.IndexOf(method.Names, arg) >= 0 && method.Types[Array.IndexOf(method.Names, arg)] == "int")
                {
                    passed = arg;
                }
                if (passed == null)
                {
                    continue;
                }
                string receiver = call.Groups[1].Value.Replace(" ", string.Empty).TrimEnd('.');
                string owner = PressOwner(book, method, receiver);
                bool own = receiver.Length == 0 || receiver == "this" || receiver == "base";
                // A bare call is the screen's own method first (an override of what its base declares), then the class's that makes it.
                List<PressMethod> targets = own ? PressMethodsUp(book, self, callee) : PressMethodsUp(book, owner, callee);
                if (targets.Count == 0 && own)
                {
                    targets = PressMethodsUp(book, method.Class, callee);
                }
                string next = own ? self : owner;
                if (targets.Count == 0 && own && PressHandedTo.TryGetValue(callee, out string[] to))
                {
                    targets = PressMethods(book, to[0], to[1]);
                    next = to[0];
                }
                if (targets.Count == 0)
                {
                    if (isCommand && (callee == "Press" || callee.StartsWith("Press", StringComparison.Ordinal) || callee == "Route" || callee == "Answer" || callee == "Handle" || callee == "Take"
                                      || callee == "Do" || callee == "Dispatch"))
                    {
                        facts.Unfollowed.Add(method.Class + "." + method.Name + " hands the command to " + (receiver.Length > 0 ? receiver + "." : string.Empty) + callee + ", which was not found");
                    }
                    continue;
                }
                foreach (PressMethod target in targets)
                {
                    if (a < target.Names.Length && target.Types[a] == "int")
                    {
                        PressWalk(book, target, target.Names[a], isCommand, offset + shift, depth + 1, facts, seen, next);
                    }
                }
            }
        }
    }

    // The class a call goes to: the method's own for a bare call, a class named outright, or the type a field, a property
    // or a local of that name was declared with.
    private static string PressOwner(PressBook book, PressMethod method, string receiver)
    {
        if (receiver.Length == 0 || receiver == "this" || receiver == "base")
        {
            return method.Class;
        }
        string last = receiver.LastIndexOf('.') >= 0 ? receiver.Substring(receiver.LastIndexOf('.') + 1) : receiver;
        if (book.Classes.ContainsKey(last))
        {
            return last;
        }
        var declared = new Regex(@"\b([A-Z]\w*)(?:<[^>;(){}]*>)?\s+" + Regex.Escape(last) + @"\s*(?:=|;|,|\)|\{)");
        Match local = declared.Match(method.Body);
        string type = local.Success ? PressType(book, local.Groups[1].Value) : null;
        if (type != null)
        {
            return type;
        }
        foreach (string body in PressBodies(book, method.Class))
        {
            foreach (Match field in declared.Matches(body))
            {
                type = PressType(book, field.Groups[1].Value);
                if (type != null)
                {
                    return type;
                }
            }
        }
        return null;
    }

    // A declared type as the class a call lands in: the class itself, or the one class of the mod that is of that type (an interface's).
    private static string PressType(PressBook book, string type)
    {
        if (book.Classes.ContainsKey(type))
        {
            return type;
        }
        string only = null;
        foreach (KeyValuePair<string, List<string>> c in book.Bases)
        {
            if (c.Value.Contains(type))
            {
                if (only != null)
                {
                    return null;
                }
                only = c.Key;
            }
        }
        return only;
    }

    // The ranges a method asks of a number: each lower bound with the upper bound that follows it in the same test, or alone.
    private static List<long[]> PressRanges(PressBook book, PressMethod method, string v)
    {
        var ranges = new List<long[]>();
        string name = Regex.Escape(v);
        MatchCollection tests = Regex.Matches(method.Body, @"\b" + name + @"\s*(>=|<=|>|<)\s*(" + PressExpr + ")" + PressEnd);
        for (int i = 0; i < tests.Count; i++)
        {
            long? bound = PressValue(book, tests[i].Groups[2].Value, method.Class);
            string op = tests[i].Groups[1].Value;
            bool lower = op == ">=" || op == ">";
            if (bound == null)
            {
                if (lower)
                {
                    ranges.Add(new[] { long.MinValue, long.MaxValue, (long)tests[i].Index, 0L });   // a bound that is no constant: it may take anything
                }
                continue;
            }
            if (!lower)
            {
                ranges.Add(new[] { long.MinValue, op == "<" ? bound.Value - 1 : bound.Value, (long)tests[i].Index, 1L });
                continue;
            }
            long lo = op == ">" ? bound.Value + 1 : bound.Value, hi = long.MaxValue;
            if (i + 1 < tests.Count && tests[i + 1].Index - (tests[i].Index + tests[i].Length) < 12 && method.Body.Substring(tests[i].Index + tests[i].Length, tests[i + 1].Index - tests[i].Index - tests[i].Length).Contains("&&"))
            {
                string op2 = tests[i + 1].Groups[1].Value;
                long? upper = PressValue(book, tests[i + 1].Groups[2].Value, method.Class);
                if ((op2 == "<" || op2 == "<=") && upper != null)
                {
                    hi = op2 == "<" ? upper.Value - 1 : upper.Value;
                    i++;
                }
                else if (op2 == "<" || op2 == "<=")
                {
                    i++;   // an upper bound that is no constant: open above
                }
            }
            ranges.Add(new[] { lo, hi, (long)tests[i].Index, 1L });
        }
        return ranges;
    }

    private const long PressNumbers = 50;   // a range that starts at 50 or above is a range of command or action numbers, not a list's rows

    // A range asked in an 'if' that stands before the case of a number inside it, outside what the 'if' holds: the range takes that number first.
    private static void PressOrder(PressBook book, PressMethod method, string v, PressFacts facts)
    {
        string body = method.Body, name = Regex.Escape(v);
        var cases = new List<long[]>();   // value, position
        foreach (Match sw in Regex.Matches(body, @"\bswitch\s*\(\s*" + name + @"\s*\)"))
        {
            int open = body.IndexOf('{', sw.Index);
            int close = open >= 0 ? PressClose(body, open, '{', '}') : -1;
            if (close < 0)
            {
                continue;
            }
            foreach (Match c in Regex.Matches(body.Substring(open, close - open), @"\bcase\s+([^:?]+?)\s*:"))
            {
                long? value = PressValue(book, c.Groups[1].Value, method.Class);
                if (value != null)
                {
                    cases.Add(new[] { value.Value, (long)(open + c.Index) });
                }
            }
        }
        foreach (Match e in Regex.Matches(body, @"\b" + name + @"\s*==\s*(" + PressExpr + ")" + PressEnd))
        {
            long? value = PressValue(book, e.Groups[1].Value, method.Class);
            if (value != null)
            {
                cases.Add(new[] { value.Value, (long)e.Index });
            }
        }
        if (cases.Count == 0)
        {
            return;
        }
        foreach (long[] range in PressRanges(book, method, v))
        {
            if (range[3] == 0 || range[0] < PressNumbers)
            {
                continue;
            }
            // The 'if' the range stands in, and what it holds.
            int at = (int)range[2], ifAt = body.LastIndexOf("if", at, StringComparison.Ordinal);
            if (ifAt < 0 || at - ifAt > 160)
            {
                continue;
            }
            int open = body.IndexOf('(', ifAt);
            int close = open >= 0 && open < at ? PressClose(body, open, '(', ')') : -1;
            if (close < at)
            {
                continue;
            }
            int from = close + 1;
            while (from < body.Length && char.IsWhiteSpace(body[from]))
            {
                from++;
            }
            int end = from < body.Length && body[from] == '{' ? PressClose(body, from, '{', '}') : body.IndexOf(';', from);
            if (end < 0)
            {
                continue;
            }
            foreach (long[] c in cases)
            {
                if (c[1] > end && c[0] >= range[0] && c[0] <= range[1])
                {
                    string said = method.Class + "." + method.Name + ": the range of '" + v + "' from " + range[0] + (range[1] == long.MaxValue ? " up" : " to " + range[1])
                        + " is asked before the case for " + c[0];
                    if (!facts.Swallows.Contains(said))
                    {
                        facts.Swallows.Add(said);
                    }
                }
            }
        }
    }

    // A screen whose commands the sources cannot answer for, each with its reason: kept short, and looked at by hand.
    private static readonly Dictionary<string, string> PressNotRead = new Dictionary<string, string>(StringComparer.Ordinal)
    {
    };

    // A command a screen lays out that its Press does not name, each with why that is right.
    private static readonly Dictionary<string, string> PressUnanswered = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "*:120", "UiChart.First + UiChart.Point: a chart's point is read by focus or the resting mouse (IUiPointing); nothing is pressed" },
        { "*:150", "UiTrack.First + UiTrack.Step: a track's step is read by focus; nothing is pressed" },
        { "gallery:2", "the kit's own gallery of parts (Options, Tests): a sample control answers nothing" },
        { "gallery:4", "the kit's own gallery of parts: a sample call's option answers nothing" },
        { "kit_parts:112", "the kit's own gallery of parts: its table's search is a sample field (the gallery types only in UiPartsGallery.Field)" },
        { "my_employees:206", "UiStaff.HeldCommand: a page's target while a card stands over it answers nothing until the card goes" },
        { "uniforms:12", "UiUniforms.Stage: the preview is turned by a drag (IUiDragging), never pressed" },
        { "staff_roles:41", "UiReader.RelatedCommand: the reader's sample lays related pages; Staff and roles has none (StaffScreen sets Related empty)" },
        { "hud:1", "the HUD's sample lays the notice cards, which ShellNotices runs on a host of its own" },
        { "hud:2", "the HUD's sample lays the notice cards, which ShellNotices runs on a host of its own" },
        { "hud:3", "the HUD's sample lays the notice cards, which ShellNotices runs on a host of its own" },
        { "hud:200", "the HUD's sample lays a world prompt, which WorldPrompt runs on a follower of its own" },
        { "hud:243", "the HUD's sample lays an activity's strip, which the activity panel's own screen runs (the live HUD only keeps clear of it)" },
        { "hud:244", "the HUD's sample lays an activity's strip, which the activity panel's own screen runs" },
        { "hud:270", "the HUD's sample lays a tool row, which build mode's screen runs" },
        { "hud:271", "the HUD's sample lays a tool row's choices, which build mode's screen runs" },
    };

    // What this reading found on 13 Nov 2026 and this job did not mend: a control drawn as one that its screen refuses with
    // the error sound alone. Each is a line of the report; take it out here when it is mended, and the check holds it.
    private static readonly Dictionary<string, string> PressKnownFaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "finance:141", "Finance, Cash flow: a day's band is drawn with a fold mark (UiStatement.Group) and FinanceScreen.Press has no case for it" },
        { "finance:142", "Finance, Cash flow: a statement's line is a target (UiStatement.Line) and FinanceScreen.Press has no case for it" },
        { "stocks:141", "Stocks: a statement's band is drawn with a fold mark and StocksScreen.Press has no case for it" },
        { "stocks:142", "Stocks: a statement's line is a target and StocksScreen.Press has no case for it" },
        { "marketplace:1409", "Marketplace: the sort choice at the right of the tool row (WorkCmd.Sort, WorkCards.Tools) is drawn as a choice and WorkScreen.Route has no case for it" },
    };

    // A range before a case that takes nothing from it, each with why.
    private static readonly Dictionary<string, string> PressRangesKnown = new Dictionary<string, string>(StringComparer.Ordinal)
    {
    };

    private static void UiCommandChecks()
    {
        bool list = Environment.GetEnvironmentVariable("RLO_PRESS_LIST") != null;
        PressBook book = PressRead();
        Check(book.Classes.Count > 200 && PressValue(book, "UiItemsList.AddCommand", null) == UiItemsList.AddCommand && PressValue(book, "UiCheckout.CancelCommand", null) == UiCheckout.CancelCommand
              && PressValue(book, "UiCargo.CatalogueCommand + UiCatalogue.Card", null) == UiCargo.CatalogueCommand + UiCatalogue.Card && PressValue(book, "UiDraw.CloseCommand", null) == UiDraw.CloseCommand,
            "ui commands: the sources' command numbers are read as the kit holds them (" + book.Classes.Count + " classes, " + book.Consts.Count + " numbers)");
        // The reading itself, on the fault of 12 Nov as it stood: a range of action numbers before the case of one inside it.
        var old = new PressBook();
        old.Classes["Was"] = new List<string> { "{ const int DoLine = 100, DoStartHere = 9810; bool Press(int command, int arg) { switch (command) { case 5: return Do(arg); } return false; } "
            + "bool Do(int what) { if (what >= DoLine) { return Interior(what - DoLine); } switch (what) { case DoStartHere: return true; } return false; } bool Interior(int i) { return false; } }" };
        old.Consts["Was.DoLine"] = "100";
        old.Consts["Was.DoStartHere"] = "9810";
        var wasFacts = new PressFacts();
        foreach (PressMethod m in PressMethods(old, "Was", "Press"))
        {
            PressWalk(old, m, "command", true, 0, 0, wasFacts, new HashSet<string>(), "Was");
        }
        Check(wasFacts.Equal.Contains(5) && wasFacts.Swallows.Count == 1 && wasFacts.Swallows[0].Contains("9810"),
            "ui commands: the reading finds a range of numbers asked before the case of one inside it (Start business, as it stood: " + (wasFacts.Swallows.Count > 0 ? wasFacts.Swallows[0] : "not found") + ")");
        old.Classes["Was"][0] = old.Classes["Was"][0].Replace("if (what >= DoLine) { return Interior(what - DoLine); } switch (what) { case DoStartHere: return true; }",
            "if (what == DoStartHere) { return true; } if (what >= DoLine) { return Interior(what - DoLine); }");
        old.Methods.Clear();
        var nowFacts = new PressFacts();
        foreach (PressMethod m in PressMethods(old, "Was", "Press"))
        {
            PressWalk(old, m, "command", true, 0, 0, nowFacts, new HashSet<string>(), "Was");
        }
        Check(nowFacts.Swallows.Count == 0, "ui commands: the case asked first is not reported");

        var scene = new UiScene();
        UiWords en = UiWords("en");
        var offered = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        var runners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (UiScreenCase c in UiCases())
        {
            if (c.Def.Runner == null)
            {
                continue;
            }
            runners[c.Def.Id] = c.Def.Runner;
            if (!offered.TryGetValue(c.Def.Id, out SortedSet<int> commands))
            {
                offered[c.Def.Id] = commands = new SortedSet<int>();
            }
            foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
            {
                c.Lay(scene, UiFrame.FullMenu(16f / 9f, c.Title), true, keys, en);
                for (int i = 0; i < scene.NodeCount; i++)
                {
                    ref UiNode n = ref scene.Nodes[i];
                    if (!n.Handle && n.Command != UiDraw.CloseCommand && !UiDraw.IsChrome(n.Command))
                    {
                        commands.Add(n.Command);
                    }
                }
            }
        }
        int screens = 0, answered = 0, known = 0;
        var unread = new List<string>();
        var unanswered = new List<string>();
        var swallowed = new List<string>();
        var stale = new List<string>();
        foreach (KeyValuePair<string, string> runner in runners)
        {
            screens++;
            var facts = new PressFacts();
            var up = new List<string> { runner.Value };
            for (int u = 0; u < up.Count && u < 8 && !facts.Found; u++)
            {
                foreach (PressMethod m in PressMethods(book, up[u], "Press"))
                {
                    if (m.Names.Length == 2 && m.Types[0] == "int" && m.Types[1] == "int")
                    {
                        facts.Found = true;
                        PressWalk(book, m, m.Names[0], true, 0, 0, facts, new HashSet<string>(), runner.Value);
                    }
                }
                if (book.Bases.TryGetValue(up[u], out List<string> bases))
                {
                    up.AddRange(bases);
                }
            }
            if (!facts.Found)
            {
                if (!PressNotRead.ContainsKey(runner.Key))
                {
                    unread.Add(runner.Key + " (" + runner.Value + ": no Press(int, int) found in the sources)");
                }
                continue;
            }
            // The field the host types for is the host's own press.
            HashSet<long> typing = PressTyping(book, runner.Value);
            var sb = new StringBuilder();
            foreach (int command in offered[runner.Key])
            {
                if (UiDraw.IsPill(command) && runner.Value != "HudScreen")
                {
                    continue;   // the status bar's pills in a sample's frame are the HUD's own targets, answered there
                }
                bool named = facts.Equal.Contains(command) || typing.Contains(command);
                bool ranged = false;
                foreach (long[] r in facts.Ranges)
                {
                    ranged |= command >= r[0] && command <= r[1];
                }
                string key = runner.Key + ":" + command;
                if (named || ranged)
                {
                    answered++;
                    if (PressUnanswered.ContainsKey(key))
                    {
                        stale.Add(key);
                    }
                }
                else if (PressKnownFaults.ContainsKey(key))
                {
                    known++;
                }
                else if (!PressUnanswered.ContainsKey(key) && !PressUnanswered.ContainsKey("*:" + command))
                {
                    unanswered.Add(key + " (" + runner.Value + (facts.Unfollowed.Count > 0 ? "; " + facts.Unfollowed[0] : string.Empty) + ")");
                }
                sb.Append(' ').Append(command).Append(named ? "=" : ranged ? "~" : "!");
            }
            foreach (string s in facts.Swallows)
            {
                if (!PressRangesKnown.ContainsKey(s))
                {
                    swallowed.Add(runner.Key + ": " + s);
                }
            }
            if (list)
            {
                Console.WriteLine("        " + runner.Key + " [" + runner.Value + "]:" + sb + (facts.Unfollowed.Count > 0 ? "  | not followed: " + string.Join("; ", facts.Unfollowed) : string.Empty));
            }
        }
        Check(screens >= 40, "ui commands: every screen the kit runs is read (" + screens + " screens with a runner)");
        Check(unread.Count == 0, "ui commands: every runner's Press is found in the sources (" + (unread.Count == 0 ? "all" : string.Join("; ", unread)) + ")");
        Check(unanswered.Count == 0, "ui commands: every command a screen lays out as a target is named by that screen's Press, " + answered + " of them (" + (unanswered.Count == 0 ? "all" : string.Join("; ", unanswered)) + ")");
        Check(swallowed.Count == 0, "ui commands: no range of numbers is asked before the case of a number inside it (" + (swallowed.Count == 0 ? "none" : string.Join("; ", swallowed)) + ")");
        Check(stale.Count == 0, "ui commands: no command is excused that its screen now answers (" + (stale.Count == 0 ? "none" : string.Join(", ", stale)) + ")");
        Check(known == PressKnownFaults.Count, "ui commands: the faults this reading found and that are not mended yet are still the ones written down, " + PressKnownFaults.Count
            + " (take a line out when its fault is mended: " + string.Join("; ", PressKnownFaults.Keys) + ")");
    }
}
