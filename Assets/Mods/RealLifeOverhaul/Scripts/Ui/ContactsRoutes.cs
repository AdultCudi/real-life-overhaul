namespace RealLifeOverhaul.Ui
{
    /// <summary>Where a press on the kit's Contacts goes: the game's own action it stands for, or nowhere.</summary>
    public enum ContactsRoute : byte
    {
        None = 0,
        Group,      // the game's list of a category
        Row,        // the game's own contact button, so its messages are read as the game reads them
        Map,        // the game's View on map: the full menu closes and the map opens on the place
        Call,       // the game's Call: its own call, in its own view, for a business, a person or an employee
        Service,    // one of the header's buttons (manage, schedule, candidates, taxes, clear chat)
        MarkRead,   // the game's Mark all as read
        DeleteAll,  // the game's Delete all, which asks in its own box first
        Classic,    // the game's own view for a message only it can answer
        Search      // typing into the search box
    }

    /// <summary>
    /// The kit's Contacts presses, each to the game's action it stands for. Beta 3's View on map and Call carried numbers
    /// no press answered (Call even read as "show every group"), so every route is named here once, the screen acts on
    /// the route alone, and EconomyCheck proves that every target the Contacts screen lays out has one. Game-free.
    /// </summary>
    public static class ContactsRoutes
    {
        public const int MapArg = 0, CallArg = 1;           // the hero's actions by what they do, not where they stand: the map's button may be hidden
        public const int MarkReadArg = 0, DeleteAllArg = 1;  // the side's actions in the order the template draws them

        /// <summary>The route of a target's command and argument.</summary>
        public static ContactsRoute Route(int command, int arg)
        {
            switch (command)
            {
                case UiListDetail.Group: return ContactsRoute.Group;
                case UiListDetail.Row: return ContactsRoute.Row;
                case UiListDetail.Action: return arg == MapArg ? ContactsRoute.Map : arg == CallArg ? ContactsRoute.Call : ContactsRoute.None;
                case UiListDetail.Service: return ContactsRoute.Service;
                case UiListDetail.SideAction: return arg == MarkReadArg ? ContactsRoute.MarkRead : arg == DeleteAllArg ? ContactsRoute.DeleteAll : ContactsRoute.None;
                case UiListDetail.Classic: return ContactsRoute.Classic;
                case UiListDetail.Search: return ContactsRoute.Search;
                default: return ContactsRoute.None;
            }
        }
    }
}
