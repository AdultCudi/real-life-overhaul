using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _contactsChecked;

    // Contacts, every state: laid out by the screen checks in every language and frame, with the commands the window and
    // the call card answer. The rules the screen reads the game by, and its routes to the game, are checked once, beside them.
    static partial void UiCasesContacts(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "contacts")
        {
            return;
        }
        foreach (string state in UiSamples.ContactsStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "contacts (" + st + ")", Title = "Contacts", Commands = ContactsCommands,
                Lay = (s, f, dark, k, t) => UiContacts.Lay(s, UiSamples.ContactsSample(t, st), f, dark, k)
            });
        }
        if (!_contactsChecked)
        {
            _contactsChecked = true;
            ContactsRuleChecks();
            ContactsRouteChecks();
            ContactsBoundChecks();
        }
    }

    private static readonly int[] ContactsCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiMessageList.RowCommand, UiMessageList.SearchCommand, UiMessageList.ClearCommand, UiContacts.SideCommand, UiContacts.ActionCommand,
        UiConversation.ChoiceCommand, UiConversation.FormCommand, UiConversation.FormLess, UiConversation.FormMore, UiCallCard.ChoiceCommand, UiCallCard.ButtonCommand
    };

    // What the screen promises beyond the layout: every group a glyph of its own and the game's own key for its name (the
    // mod's groups by their numbers); a firm's glyph from its kind of business; an offer's negotiation stays in the thread
    // while a call is a card; the game's three buttons as numbered choices with the confirm first; a call's length as a
    // phone shows it; the game's rich text and labels as plain words.
    private static void ContactsRuleChecks()
    {
        var glyphs = new HashSet<UiIcon>();
        foreach (int g in new[] { 0, 1, 2, 3, 4, 5, 6, 101, 102, 103, 104, 105, 106, 107 })
        {
            glyphs.Add(ContactsRules.GroupIcon(g));
        }
        Check(glyphs.Count == 14 && ContactsRules.GroupIcon(999) == UiIcon.Info, "contacts: each of the game's categories and the mod's groups has a glyph of its own, another mod's group the plain one");
        Check(ContactsRules.GroupKey(0) == "contactcategoryname_general" && ContactsRules.GroupKey(3) == "contactcategoryname_furnitureandequipment"
              && ContactsRules.GroupKey(101) == "contactcategoryname_101", "contacts: a group's name is the game's own key for the category, the mod's groups by their numbers");
        Check(ContactsRules.FirmIcon("ba:businesstype_recruitmentagency", 1) == UiIcon.IdBadge && ContactsRules.FirmIcon("ba:businesstype_wholesalestore", 4) == UiIcon.Package
              && ContactsRules.FirmIcon(null, 2) == UiIcon.Bank && ContactsRules.FirmIcon(null, 0) == UiIcon.Building,
            "contacts: a firm wears its kind of business, else its group's glyph, and never a person's mark in General");
        Check(ContactsRules.InThread("CandidateSalaryNegotiationDialog") && ContactsRules.InThread("HealthInsuranceNegotiationDialog") && !ContactsRules.InThread("UncleFredDialog")
              && !ContactsRules.InThread("EmergencyDialog") && !ContactsRules.InThread(null), "contacts: an offer's negotiation stays in the thread, every other conversation on the phone is a call");
        var order = new int[3];
        int n = ContactsRules.ChoiceOrder(true, true, true, true, order);
        bool thread = n == 3 && order[0] == 2 && order[1] == 1 && order[2] == 0;
        n = ContactsRules.ChoiceOrder(true, false, true, false, order);
        bool call = n == 1 && order[0] == 1;
        n = ContactsRules.ChoiceOrder(false, true, false, false, order);
        Check(thread && call && n == 1 && order[0] == 0, "contacts: the game's buttons (cancel, second, confirm) become choices with the confirm first, the cancel last in a thread and the way out of a call");
        Check(ContactsRules.Length(0f) == "0:00" && ContactsRules.Length(65.9f) == "1:05" && ContactsRules.Length(3725f) == "1:02:05", "contacts: a call's length reads as a phone shows it");
        Check(ContactsRules.Plain("<b>Notice</b><br><br>Dear Sir") == "Notice\n\nDear Sir" && ContactsRules.Plain("plain") == "plain"
              && ContactsRules.Label("Hourly wage: ") == "Hourly wage" && ContactsRules.Label("Prime à la signature :  ") == "Prime à la signature",
            "contacts: the game's rich text keeps its line breaks as words, and a label loses its colon");
        Check(ContactsRules.MoodRing(1) == UiRole.Ok && ContactsRules.MoodRing(0) == UiRole.Warn && ContactsRules.MoodRing(-1) == UiRole.Danger && ContactsRules.MoodRing(-2) == UiRole.None,
            "contacts: the ring is the mood the game shows by the line, none where it shows none");
        // A sidebar of more groups than its room scrolls above its foot, and the chosen one is in view.
        var m = UiSamples.ContactsSample(UiWords("en"), "all");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Contacts");
        UiContacts.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        int foot = s.FindNode(UiContacts.SideCommand, ContactsRoutes.DeleteAllArg), group = s.FindNode(UiAppWindow.NavCommand, 0);
        s.NodeBox(foot, out float fx, out float fy, out float fw, out float fh);
        Check(foot >= 0 && group >= 0 && s.Clips[m.GroupsClip].Y + s.Clips[m.GroupsClip].H <= fy, "contacts: the groups scroll in their own frame above the sidebar's foot, never over Delete all");
        UiContactsModel call2 = UiSamples.ContactsSample(UiWords("en"), "call_replies");
        UiContacts.Lay(s, call2, frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(UiCallCard.ChoiceCommand, 0) >= 0 && s.FindNode(UiCallCard.ButtonCommand, 0) >= 0 && s.FindNode(UiAppWindow.NavCommand, 0) < 0
              && s.Nodes[s.FindNode(UiCallCard.ChoiceCommand, 0)].Key == UiAction.Pick1,
            "contacts: a call stands as its card alone, its replies on the number keys and Hang up its way out, the window not reachable under it");
        WaitingChecks();
        ContactPictureChecks();
    }

    // Who is waiting: every contact with unread messages is marked in the list and stands first, the foot's count of them is a control
    // that filters the list to them whatever group is open, and the count is the number of marked rows. Senders are named in a line.
    private static void WaitingChecks()
    {
        UiContactsModel three = UiSamples.ContactsSample(UiWords("en"), "three");
        int marked = 0, firstRead = -1, lastMarked = -1;
        for (int i = 0; i < three.List.RowCount; i++)
        {
            if (three.List.Rows[i].Unread > 0)
            {
                marked++;
                lastMarked = i;
            }
            else if (firstRead < 0)
            {
                firstRead = i;
            }
        }
        Check(marked == 3 && lastMarked < firstRead && three.List.Rows[0].Section != null, "contacts: every contact with unread messages is marked in the list with its count and stands before the read ones");
        UiActionModel button = System.Array.Find(three.Foot.Actions, a => a.Arg == ContactsRoutes.WaitingArg);
        Check(button.Label == "3 waiting" && three.Foot.PlayerLine == "Waiting: Luis Romero and 2 more",
            "contacts: the waiting count is a button that counts the marked rows, and the foot names the senders");
        UiContactsModel only = UiSamples.ContactsSample(UiWords("en"), "waiting");
        bool allMarked = only.List.RowCount == marked;
        for (int i = 0; i < only.List.RowCount; i++)
        {
            allMarked &= only.List.Rows[i].Unread > 0;
        }
        Check(allMarked && System.Array.Find(only.Foot.Actions, a => a.Arg == ContactsRoutes.WaitingArg).Kind == UiButtonKind.Primary,
            "contacts: with the waiting filter on only the marked rows show, and the button reads as pressed");
        var scene = new UiScene();
        UiContacts.Lay(scene, three, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
        int node = scene.FindNode(UiContacts.SideCommand, ContactsRoutes.WaitingArg);
        Check(node >= 0 && ContactsRoutes.Route(UiContacts.SideCommand, ContactsRoutes.WaitingArg, three.HeadActions) == ContactsRoute.Waiting,
            "contacts: a press on the waiting count has a route of its own");
        Check(ContactsRules.InList(true, true, false, true) && !ContactsRules.InList(true, false, true, true) && !ContactsRules.InList(true, true, true, false)
              && ContactsRules.InList(false, false, true, true) && !ContactsRules.InList(false, true, false, true),
            "contacts: the waiting filter shows every sender in any group; without it the group and the search decide");
        var t = UiWords("en");
        Check(ContactsRules.Senders(t, new string[0]) == string.Empty && ContactsRules.Senders(t, new[] { "Ana Ruiz" }) == "Ana Ruiz"
              && ContactsRules.Senders(t, new[] { "Ana Ruiz", "Tom Becker" }) == "Ana Ruiz and Tom Becker"
              && ContactsRules.Senders(t, new[] { "Ana Ruiz", "Tom Becker", "Mom", "Alex" }) == "Ana Ruiz and 3 more",
            "contacts: the senders read as a name, two names, or the first and how many more");
    }

    // A firm's own picture (its logo, as the game's Contacts shows it) in its list row, its head, on the call card and as
    // the speaker of a conversation, each on its tile in place of the icon; a firm the game has no picture for keeps its
    // icon, a person their face. The game side asks the game's own ContactsApp.GetContactIcon once a contact a city.
    private static void ContactPictureChecks()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Contacts");
        UiContacts.Lay(s, UiSamples.ContactsSample(UiWords("en"), "business"), frame, true, UiDefaultKeys.Keyboard);
        bool rowLogo = Pictured(s, "contact:marketing2:7B3FA0"), headLogo = Count(s, UiSamples.RecruitmentLogo) >= 2;   // the agency's row and its head
        UiContacts.Lay(s, UiSamples.ContactsSample(UiWords("en"), "all"), frame, true, UiDefaultKeys.Keyboard);
        bool kept = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            kept |= s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Truck;   // a service the game has no logo for keeps its glyph
        }
        UiContacts.Lay(s, UiSamples.LoanCall(UiWords("en")), frame, true, UiDefaultKeys.Keyboard);
        bool callLogo = Pictured(s, UiSamples.BankLogo);
        var talk = new UiConversationModel { Name = "Midtown Savings Bank", Picture = UiSamples.BankLogo, Title = "Conversation" };
        UiConversation.LayDocked(s, talk, UiFrame.FullMenu(16f / 9f, "Dialogue"), true, UiDefaultKeys.Keyboard);
        bool speakerLogo = Pictured(s, UiSamples.BankLogo);
        Check(rowLogo && headLogo && kept && callLogo && speakerLogo,
            "contacts: a firm's own picture stands on its tile in its list row, its head, the call card and as a conversation's speaker; a firm the game has none for keeps its icon ("
            + rowLogo + ", " + headLogo + ", " + callLogo + ", " + speakerLogo + ", " + kept + ")");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string faces = File.ReadAllText(System.IO.Path.Combine(folder, "Contacts", "ContactsScreen.Faces.cs"));
        string read = File.ReadAllText(System.IO.Path.Combine(folder, "Contacts", "ContactsScreen.Read.cs"));
        string dialog = File.ReadAllText(System.IO.Path.Combine(folder, "Dialog", "DialogScreen.cs"));
        string of = Between(faces, "public static string Of(Contact c)", "private static void On()");
        Check(of.Contains("ContactsApp.GetContactIcon(c)") && of.Contains("Keys.TryGetValue(c.id") && of.Contains("Keys[c.id] = key;") && faces.Contains("Person(c) ? null")
              && read.Contains("Picture = Logo(c)") && read.Contains("h.Picture = Logo(c);") && read.Contains("Picture = _m.Head.Picture") && dialog.Contains("FirmPictures.Of(c)"),
            "contacts: the game's own picture of a firm's contact is asked once a contact a city through the game's Contacts (a firm with none remembered as none), for the list, the head, the call and the conversation, a person never");
    }

    private static bool Pictured(UiScene s, string key) => Count(s, key) > 0;

    private static int Count(UiScene s, string key)
    {
        int n = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Picture && s.Items[i].Text == key)
            {
                n++;
            }
        }
        return n;
    }

    // Every action a sample contact shows is bound to the game's own: the button drawn in a place of the head does what its
    // key says (Call on F, View on map on P), whichever of the two the game shows, so Mom, a friend, the tow service and
    // Speedy Bites, who have no address and so no map button, have a Call that calls. The window hands a press the button's
    // place, so the route is read through the head's own list; read as the place alone, Call in place 0 was View on map and
    // a press on it did nothing.
    private static void ContactsBoundChecks()
    {
        UiWords en = UiWords("en");
        var scene = new UiScene();
        int unbound = 0, drawn = 0;
        string missed = "none";
        foreach (string state in UiSamples.ContactsStates)
        {
            UiContactsModel m = UiSamples.ContactsSample(en, state);
            UiContacts.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
            if (m.Call != null || !m.HasDetail)
            {
                continue;
            }
            int head = 0, aside = 0;
            for (int n = 0; n < scene.NodeCount; n++)
            {
                ref UiNode node = ref scene.Nodes[n];
                if (node.Command == UiAppWindow.ActionCommand)
                {
                    head++;
                    UiAction key = node.Arg >= 0 && node.Arg < m.HeadActions.Length ? m.HeadActions[node.Arg].Key : UiAction.Count;
                    ContactsRoute want = key == UiAction.Alt ? ContactsRoute.Call : key == UiAction.Option ? ContactsRoute.Map : ContactsRoute.None;
                    if (want == ContactsRoute.None || ContactsRoutes.Route(node.Command, node.Arg, m.HeadActions) != want)
                    {
                        unbound++;
                        missed = state + ": head button " + node.Arg;
                    }
                }
                else if (node.Command == UiContacts.ActionCommand)
                {
                    aside++;
                    if (node.Arg < 0 || node.Arg >= m.Actions.Length || ContactsRoutes.Route(node.Command, node.Arg, m.HeadActions) != ContactsRoute.Service)
                    {
                        unbound++;
                        missed = state + ": aside action " + node.Arg;
                    }
                }
            }
            if (head != m.HeadActions.Length || aside != m.Actions.Length)
            {
                unbound++;
                missed = state + ": " + head + " of " + m.HeadActions.Length + " head buttons and " + aside + " of " + m.Actions.Length + " aside actions drawn";
            }
            drawn += head + aside;
        }
        Check(unbound == 0 && drawn > 0, "ui contacts: every action a sample contact shows is bound to the game's own, the head's by what it does and the aside's by its place (" + unbound + " unbound, " + missed + ")");
        bool noMap = true;
        foreach (string state in new[] { "relative", "friend", "service", "restaurant" })
        {
            UiContactsModel m = UiSamples.ContactsSample(en, state);
            UiContacts.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
            int call = scene.FindNode(UiAppWindow.ActionCommand, 0);
            noMap &= m.HasDetail && m.HeadActions.Length == 1 && call >= 0 && scene.FindNode(UiAppWindow.ActionCommand, 1) < 0
                     && ContactsRoutes.Route(scene.Nodes[call].Command, scene.Nodes[call].Arg, m.HeadActions) == ContactsRoute.Call && scene.Nodes[call].Key == UiAction.Alt;
        }
        Check(noMap, "ui contacts: a relative, a friend, a game service and a restaurant, who have no address and so no map button, each have a Call in the first place of the head that routes to Call");
    }

    // The Contacts kit's every button routed to the game's own action, the way beta 3's View on map and Call were not.
    private static void ContactsRouteChecks()
    {
        UiWords en = UiWords("en");
        var scene = new UiScene();
        int none = 0;
        string missed = "none";
        foreach (string state in UiSamples.ContactsStates)
        {
            UiContactsModel sample = UiSamples.ContactsSample(en, state);
            UiContacts.Lay(scene, sample, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
            for (int n = 0; n < scene.NodeCount; n++)
            {
                ref UiNode node = ref scene.Nodes[n];
                if (node.Command != UiDraw.CloseCommand && node.Command != UiDraw.AppRowCommand && node.Command != UiDraw.AllAppsCommand
                    && ContactsRoutes.Route(node.Command, node.Arg, sample.HeadActions) == ContactsRoute.None)
                {
                    none++;
                    missed = state + ": " + node.Command + "/" + node.Arg;
                }
            }
        }
        UiContactsModel business = UiSamples.ContactsSample(en, "business");
        UiContacts.Lay(scene, business, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
        int map = scene.FindNode(UiAppWindow.ActionCommand, ContactsRoutes.PlaceOf(business.HeadActions, ContactsRoutes.MapArg));
        int call = scene.FindNode(UiAppWindow.ActionCommand, ContactsRoutes.PlaceOf(business.HeadActions, ContactsRoutes.CallArg));
        Check(none == 0 && map >= 0 && call >= 0 && scene.Nodes[call].Key == UiAction.Alt && scene.Nodes[map].Key == UiAction.Option,
            "ui contacts: every target the Contacts screen draws has a route to the game, View on map on P and Call on F among them (" + none + " with none, " + missed + ")");
        Check(ContactsRoutes.Route(UiAppWindow.NavCommand, 0, null) == ContactsRoute.Group && ContactsRoutes.Route(UiAppWindow.ActionCommand, 1, business.HeadActions) == ContactsRoute.Call
              && ContactsRoutes.Route(0, 0, null) == ContactsRoute.None && ContactsRoutes.Route(UiAppWindow.NavCommand, 1, null) != ContactsRoute.Call
              && ContactsRoutes.Route(UiCallCard.ChoiceCommand, 0, null) == ContactsRoute.Reply && ContactsRoutes.Route(UiCallCard.ButtonCommand, 0, null) == ContactsRoute.HangUp,
            "ui contacts: Call is its own route, never the group list it was read as in beta 3, and a call's replies and Hang up are routes of their own");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\Contacts"));
        string screen = File.ReadAllText(System.IO.Path.Combine(folder, "ContactsScreen.cs")), read = File.ReadAllText(System.IO.Path.Combine(folder, "ContactsScreen.Read.cs"));
        string kit = File.ReadAllText(System.IO.Path.Combine(folder, "ContactsKit.cs"));
        string press = Between(screen, "public bool Press(int command, int arg)", "private static bool Done(");
        var routed = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(press, @"case ContactsRoute\.(\w+):"))
        {
            routed.Add(match.Groups[1].Value);
        }
        bool all = true;
        foreach (ContactsRoute r in Enum.GetValues(typeof(ContactsRoute)))
        {
            all &= r == ContactsRoute.None || r == ContactsRoute.Search || r == ContactsRoute.Field || routed.Contains(r.ToString());   // the search and a form's figure are the host's typing
        }
        string callNow = Between(screen, "private bool CallNow(Contact open)", "private bool Service(");
        string handed = Between(screen, "private bool Handed(", "private static bool Unrouted(");
        Check(all && press.Contains("Handed(_map,") && press.Contains("Handed(_hangUp,") && press.Contains("Handed(_choices[arg],") && handed.Contains("UiWidgets.Hand(b);")
              && callNow.Contains("UiWidgets.Hand(_call);") && !screen.Contains("onClick.Invoke()"),
            "ui contacts: the screen acts on every route, View on map, a reply and Hang up through the game's own buttons and Call through the listener the game wires on its call button, each handed to the game (UiWidgets.Hand)");
        // A press that cannot be bound to the game's button or entry is answered in the game's own view, with a line, never by nothing.
        string open = Between(screen, "private bool Open(Contact c)", "private ContactModel EntryOf(");
        string unrouted = Between(screen, "private static bool Unrouted(", "// One line a press in Player.log");
        Check(unrouted.Contains("ContactsKit.Classic()") && unrouted.Contains("UiDirector.Logger?.Info(\"Contacts (kit): \"") && press.Contains("default:") && press.Contains("Unrouted(")
              && handed.Contains("Unrouted(what, open)") && callNow.Contains("Unrouted(\"call\", open)") && open.Contains("_app.selectedContact != c") && open.Contains("Unrouted("),
            "ui contacts: a row, a button or a reply the kit cannot bind to the game's own gives the game's own Contacts view for that press, with one line in Player.log");
        Check(screen.Contains("\"Contacts (kit): \"") && read.Contains("ContactsKit.StepAside(") && kit.Contains("Host.StepAside(over)") && read.Contains("GameContract.TalkEntryView"),
            "ui contacts: each press says its line in Player.log, a call is read from the game's own line and buttons, and a form the kit does not draw yet is answered in the game's own view until that line is done");
    }
}
