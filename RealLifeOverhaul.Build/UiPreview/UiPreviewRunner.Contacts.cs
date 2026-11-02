using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The game's Contacts app on the kit in every state, over the day street the full menu leaves, with the frame's app row
    // under it; a call over a day street and the night street, as a call comes wherever the player is.
    static partial void AddContacts(List<Screen> screens)
    {
        foreach (string state in UiSamples.ContactsStates)
        {
            string st = state;
            bool call = st.StartsWith("call");
            var screen = new Screen
            {
                Name = "contacts_" + st, AppName = "Contacts", Icon = UiIcon.AddressBook, Backdrop = st == "call_replies" ? "11.jpg" : call ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false), Lay = (s, f, d, k) => UiContacts.Lay(s, UiSamples.ContactsSample(English, st), f, d, k)
            };
            if (call)
            {
                screen.HoverCommand = st == "call_start" ? UiCallCard.ButtonCommand : UiCallCard.ChoiceCommand;
                screen.HoverArg = st == "call_start" ? 0 : 1;
                screen.FocusCommand = st == "call_start" ? UiCallCard.ButtonCommand : UiCallCard.ChoiceCommand;
                screen.Walk = new[] { Down, Down, Up };
            }
            else if (st == "offer" || st == "offer_fr" || st == "wage")
            {
                screen.HoverCommand = UiConversation.ChoiceCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = st == "wage" ? UiConversation.FormCommand : UiConversation.ChoiceCommand;
                screen.Walk = st == "wage" ? new[] { Down, Down, Down, Up, Right, Left, Left } : new[] { Down, Down, Up, Right, Left, Left };
            }
            else if (st == "empty")
            {
                screen.HoverCommand = UiAppWindow.NavCommand;
                screen.HoverArg = 2;
                screen.FocusCommand = UiMessageList.SearchCommand;
                screen.Walk = new[] { Left, Down, Down, Right };
            }
            else if (st == "all")
            {
                screen.HoverCommand = UiMessageList.RowCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = UiMessageList.RowCommand;
                screen.FocusArg = UiSamples.ContactsChosen(st);
                screen.Walk = new[] { Up, Down, Down, Right, Up, Up, Up, Down, Left, Left };
            }
            else
            {
                // A group of one or two: the mouse rests on its own action, the keys go down the short list, across and back.
                screen.HoverCommand = UiContacts.ActionCommand;
                screen.HoverArg = 0;
                screen.FocusCommand = UiMessageList.RowCommand;
                screen.FocusArg = UiSamples.ContactsChosen(st);
                screen.Walk = new[] { Down, Right, Up, Down, Left, Left };
            }
            screens.Add(screen);
        }
    }
}
