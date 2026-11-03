namespace RealLifeOverhaul.Ui
{
    /// <summary>Where a press on the kit's Contacts goes: the game's own action it stands for, or nowhere.</summary>
    public enum ContactsRoute : byte
    {
        None = 0,
        Group,      // the game's list of a category
        Row,        // the game's own contact button, so its messages are read as the game reads them
        Map,        // the game's View on map: the full menu closes and the map opens on the place
        Call,       // the game's Call: its own call, for a business, a person or an employee
        Service,    // one of the header's buttons (manage, schedule, delivery, candidates, rival, taxes, clear chat), or another mod's
        MarkRead,   // the game's Mark all as read
        DeleteAll,  // the game's Delete all, which asks in its own box first
        Search,     // typing into the search box
        Choice,     // an offer's button in the thread, or the running conversation's (confirm, the second option, cancel)
        Field,      // typing an amount into the conversation's form
        Less,       // the form's field a step down, the game's own decrease button
        More,       // a step up, its increase button
        Reply,      // a call's reply (the game's confirm or second option)
        HangUp,     // a call's way out (the game's cancel: Hang up, Decline)
        Clear,      // an empty search cleared, the kit's own search box
        Waiting     // the foot's count of contacts with unread messages: the list filtered to them, or the one sender opened
    }

    /// <summary>
    /// The kit's Contacts presses, each to the game's action it stands for. Beta 3's View on map and Call carried numbers
    /// no press answered (Call even read as "show every group"), so every route is named here once, the screen acts on
    /// the route alone, and EconomyCheck proves that every target the Contacts screen lays out has one. The head's buttons
    /// are drawn by their place (the window numbers them 0, 1 from the left) and routed by what they do: a contact with
    /// no map button, which is every contact without an address (Mom, a friend, the tow service, Speedy Bites), has Call
    /// in place 0, so a press is read through the head's own list (<see cref="Head"/>), never as the number alone. Game-free.
    /// </summary>
    public static class ContactsRoutes
    {
        public const int MapArg = 0, CallArg = 1;           // the head's actions by what they do, not where they stand: the map's button may be hidden
        public const int MarkReadArg = 0, DeleteAllArg = 1, WaitingArg = 2;  // the sidebar foot's actions by what they do (Waiting is drawn first)

        /// <summary>
        /// The route of a target's command and argument. <paramref name="head"/> is the head's buttons as the screen laid
        /// them out: the window hands a head button's place, not what it does, so the place is looked up in this list.
        /// </summary>
        public static ContactsRoute Route(int command, int arg, UiActionModel[] head)
        {
            switch (command)
            {
                case UiAppWindow.NavCommand: return ContactsRoute.Group;
                case UiMessageList.RowCommand: return ContactsRoute.Row;
                case UiMessageList.SearchCommand: return ContactsRoute.Search;
                case UiMessageList.ClearCommand: return ContactsRoute.Clear;
                case UiAppWindow.ActionCommand: return Head(head, arg);
                case UiContacts.ActionCommand: return ContactsRoute.Service;
                case UiContacts.SideCommand:
                    return arg == MarkReadArg ? ContactsRoute.MarkRead : arg == DeleteAllArg ? ContactsRoute.DeleteAll
                        : arg == WaitingArg ? ContactsRoute.Waiting : ContactsRoute.None;
                case UiConversation.ChoiceCommand: return ContactsRoute.Choice;
                case UiConversation.FormCommand: return ContactsRoute.Field;
                case UiConversation.FormLess: return ContactsRoute.Less;
                case UiConversation.FormMore: return ContactsRoute.More;
                case UiCallCard.ChoiceCommand: return ContactsRoute.Reply;
                case UiCallCard.ButtonCommand: return ContactsRoute.HangUp;
                default: return ContactsRoute.None;
            }
        }

        /// <summary>The place the head draws the button that does <paramref name="what"/> (<see cref="MapArg"/>, <see cref="CallArg"/>), or -1 where it draws none.</summary>
        public static int PlaceOf(UiActionModel[] head, int what)
        {
            for (int i = 0; head != null && i < head.Length; i++)
            {
                if (head[i].Arg == what)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>What the head's button in this place does: its own <c>Arg</c> says (<see cref="MapArg"/>, <see cref="CallArg"/>), None for a place with no button.</summary>
        public static ContactsRoute Head(UiActionModel[] head, int place)
        {
            if (head == null || place < 0 || place >= head.Length)
            {
                return ContactsRoute.None;
            }
            int what = head[place].Arg;
            return what == MapArg ? ContactsRoute.Map : what == CallArg ? ContactsRoute.Call : ContactsRoute.None;
        }
    }
}
