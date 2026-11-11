namespace RealLifeOverhaul.Careers
{
    /// <summary>Where a taken contract of desk work is started from at the place the player stands (<see cref="OwnPlaceRules.StartOf"/>).</summary>
    public enum ContractStart
    {
        Elsewhere = 0,   // nothing starts it here: the Career app says where to go
        App = 1,         // the Career app starts it, where nothing at the place does
        Desk = 2,        // a free desk of an office business of the player's is clicked
        Computer = 3     // the computer at home has the contract's button
    }

    /// <summary>The click that leads to a contract at a free desk of an office business of the player's (<see cref="OwnPlaceRules.ClickFor"/>).</summary>
    public enum DeskClick
    {
        None = 0,   // no click leads to it here
        Own = 1,    // Careers' own click on the free desk
        Tech = 2    // the tech firm's click, or the desk sessions' Work button, carries it
    }

    /// <summary>What a click on a free desk of the player's own tech firm does (<see cref="OwnPlaceRules.ChoiceAt"/>).</summary>
    public enum DeskChoice
    {
        Firm = 0,       // the firm's own work, or the reason it cannot be worked
        Contract = 1,   // the contract of the player's
        Ask = 2         // both can be worked: the player says which
    }

    /// <summary>
    /// Where a freelance contract's work is started, as rules with no game types (checked offline). A contract is worked where the player sits down to it, never
    /// from an app button while something at the place can be clicked: at the computer at home, or at a free desk of an office business of theirs. The Career app
    /// starts it only where nothing at the place does, so there is never a room in which a contract can be worked and nothing starts it. Cost: a few comparisons.
    /// </summary>
    public static class OwnPlaceRules
    {
        /// <summary>
        /// How a taken contract is started where the player stands. way: working for oneself starts contracts at the place (its switch on); trade: worked at the
        /// client's building, which has no desk of the player's; atHome and computer: they are in a home of theirs, whose computer has the contract's button;
        /// inOffice and click: they are inside an office business of theirs where it fits, and a click leads to it there.
        /// </summary>
        public static ContractStart StartOf(bool way, bool trade, bool atHome, bool computer, bool inOffice, bool click)
        {
            if (!way || trade)
            {
                return ContractStart.App;
            }
            if (atHome)
            {
                return computer ? ContractStart.Computer : ContractStart.App;
            }
            if (inOffice)
            {
                return click ? ContractStart.Desk : ContractStart.App;
            }
            return ContractStart.Elsewhere;
        }

        /// <summary>
        /// The click that leads to a contract inside an office business of the player's. contract: one can be worked here; tech and techLive: it is a tech firm,
        /// and its own click (or the desk sessions' Work button) is up, which carries the contract; free: a desk nobody sits at, other than the sessions';
        /// held: another part's click (the bank branch's counter) holds the one place a desk is clicked from. The tech firm's click carries the contract
        /// only while it is up: with the tech sector off, or its firms stood down, the contract is clicked as in any other office business.
        /// </summary>
        public static DeskClick ClickFor(bool contract, bool tech, bool techLive, bool free, bool held)
        {
            if (!contract)
            {
                return DeskClick.None;
            }
            if (tech && techLive)
            {
                return DeskClick.Tech;
            }
            return free && !held ? DeskClick.Own : DeskClick.None;
        }

        /// <summary>What a click on a free desk of the player's own tech firm does: contract is one Careers would have them work here, firm the firm's own work can be offered.</summary>
        public static DeskChoice ChoiceAt(bool contract, bool firm)
        {
            return contract ? (firm ? DeskChoice.Ask : DeskChoice.Contract) : DeskChoice.Firm;
        }

        /// <summary>True if a contract can be worked at a desk or a computer now: taken, with work left, of a profession the game knows, and desk work (a trade is done at the client's).</summary>
        public static bool AtDesk(Contract c)
        {
            if (c == null || c.Stage != (int)ContractStep.Taken || OwnRules.Left(c) <= 0)
            {
                return false;
            }
            Profession p = c.Def;
            return p != null && !OwnRules.IsTrade(p);
        }
    }
}
