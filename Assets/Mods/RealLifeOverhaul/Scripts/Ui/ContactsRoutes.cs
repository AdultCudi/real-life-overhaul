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
        Clear       // an empty search cleared, the kit's own search box
    }

    /// <summary>
    /// The kit's Contacts presses, each to the game's action it stands for. Beta 3's View on map and Call carried numbers
    /// no press answered (Call even read as "show every group"), so every route is named here once, the screen acts on
    /// the route alone, and EconomyCheck proves that every target the Contacts screen lays out has one. Game-free.
    /// </summary>
    public static class ContactsRoutes
    {
        public const int MapArg = 0, CallArg = 1;           // the head's actions by what they do, not where they stand: the map's button may be hidden
        public const int MarkReadArg = 0, DeleteAllArg = 1;  // the sidebar foot's actions in the order the window draws them

        /// <summary>The route of a target's command and argument.</summary>
        public static ContactsRoute Route(int command, int arg)
        {
            switch (command)
            {
                case UiAppWindow.NavCommand: return ContactsRoute.Group;
                case UiMessageList.RowCommand: return ContactsRoute.Row;
                case UiMessageList.SearchCommand: return ContactsRoute.Search;
                case UiMessageList.ClearCommand: return ContactsRoute.Clear;
                case UiAppWindow.ActionCommand: return arg == MapArg ? ContactsRoute.Map : arg == CallArg ? ContactsRoute.Call : ContactsRoute.None;
                case UiContacts.ActionCommand: return ContactsRoute.Service;
                case UiContacts.SideCommand: return arg == MarkReadArg ? ContactsRoute.MarkRead : arg == DeleteAllArg ? ContactsRoute.DeleteAll : ContactsRoute.None;
                case UiConversation.ChoiceCommand: return ContactsRoute.Choice;
                case UiConversation.FormCommand: return ContactsRoute.Field;
                case UiConversation.FormLess: return ContactsRoute.Less;
                case UiConversation.FormMore: return ContactsRoute.More;
                case UiCallCard.ChoiceCommand: return ContactsRoute.Reply;
                case UiCallCard.ButtonCommand: return ContactsRoute.HangUp;
                default: return ContactsRoute.None;
            }
        }
    }
}
