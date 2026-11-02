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
            UiContacts.Lay(scene, UiSamples.ContactsSample(en, state), UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
            for (int n = 0; n < scene.NodeCount; n++)
            {
                ref UiNode node = ref scene.Nodes[n];
                if (node.Command != UiDraw.CloseCommand && node.Command != UiDraw.AppRowCommand && node.Command != UiDraw.AllAppsCommand
                    && ContactsRoutes.Route(node.Command, node.Arg) == ContactsRoute.None)
                {
                    none++;
                    missed = state + ": " + node.Command + "/" + node.Arg;
                }
            }
        }
        UiContacts.Lay(scene, UiSamples.ContactsSample(en, "business"), UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
        int map = scene.FindNode(UiAppWindow.ActionCommand, ContactsRoutes.MapArg), call = scene.FindNode(UiAppWindow.ActionCommand, ContactsRoutes.CallArg);
        Check(none == 0 && map >= 0 && call >= 0 && scene.Nodes[call].Key == UiAction.Alt && scene.Nodes[map].Key == UiAction.Option,
            "ui contacts: every target the Contacts screen draws has a route to the game, View on map on P and Call on F among them (" + none + " with none, " + missed + ")");
        Check(ContactsRoutes.Route(UiAppWindow.NavCommand, 0) == ContactsRoute.Group && ContactsRoutes.Route(UiAppWindow.ActionCommand, ContactsRoutes.CallArg) == ContactsRoute.Call
              && ContactsRoutes.Route(0, 0) == ContactsRoute.None && ContactsRoutes.Route(UiAppWindow.NavCommand, 1) != ContactsRoute.Call
              && ContactsRoutes.Route(UiCallCard.ChoiceCommand, 0) == ContactsRoute.Reply && ContactsRoutes.Route(UiCallCard.ButtonCommand, 0) == ContactsRoute.HangUp,
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
        Check(all && press.Contains("Click(_map)") && press.Contains("Click(_hangUp)") && press.Contains("Click(_choices[arg])") && callNow.Contains("_call.onClick.Invoke()"),
            "ui contacts: the screen acts on every route, View on map, a reply and Hang up through the game's own buttons and Call through the listener the game wires on its call button");
        Check(screen.Contains("\"Contacts (kit): \"") && read.Contains("ContactsKit.StepAside(") && kit.Contains("Host.StepAside(over)") && read.Contains("GameContract.TalkEntryView"),
            "ui contacts: each press says its line in Player.log, a call is read from the game's own line and buttons, and a form the kit does not draw yet is answered in the game's own view until that line is done");
    }
}
