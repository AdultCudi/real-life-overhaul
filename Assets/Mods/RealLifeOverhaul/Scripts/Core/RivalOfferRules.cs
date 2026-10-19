namespace RealLifeOverhaul
{
    /// <summary>
    /// When a rival's offer to one of the player's employees keeps its buttons (Core/RivalOfferGuard takes them away).
    ///
    /// THE GAME'S OFFER. EmployeeInstance.PoachByRival texts the offer from the employee's own chat, with Decline and
    /// Accept buttons whose calls are methods of that employee (DeclinePoachPayRaise, AcceptPoachPayRaise), kept in the
    /// message (TextMessage.additionalData.contextButtonData). When the rival gives up, StopPoachingRivalSurrender
    /// texts again with two more, the employee's own calls as well. The game takes a message's buttons away only when
    /// one of them is pressed (ContextButton.SetUp empties the list), and the chat draws them on the newest message of
    /// a chat whatever has happened since (ContactsApp.ShowMessage); the save keeps them too, as calls on that employee
    /// (Odin keeps a call's object and method). So an offer whose employee has gone another way keeps live buttons:
    /// fired (MyEmployees, its mass action), resigned over a complaint, sold with a business, replaced by the
    /// headhunter (the same employee object, renamed and started afresh), moved to another player by Going Public, or
    /// let go by this mod (business pressure, the company, the tech sector). Nothing new comes into the chat on most of
    /// these ways, and a message deleted with its cross brings an older offer back to the bottom. Decline then sends
    /// somebody to the rival again (MoveEmployeeToRival: another copy of them among the rival's staff, one of the
    /// rival's own let go, the day's wage paid again and a second resignation, or the headhunter's fee for a
    /// replacement that never comes); Accept sets the wage of whoever that employee object is now.
    ///
    /// THE RULE. The buttons stay while the very employee they act on is one of the player's employees, under the name
    /// the message was sent to (the chat's), is not being replaced and has not resigned, and the offer is still open
    /// and is this message's own (sent on the day it was made). The rival's giving up keeps its buttons on the same
    /// terms, until a newer offer is made. Otherwise they go, as a press takes them.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, RivalOfferChecks.cs). Cost: a few comparisons
    /// a message.
    /// </summary>
    public static class RivalOfferRules
    {
        // The game's two rival messages with buttons: the offer (EmployeeInstance.PoachByRival) and the rival's
        // giving up (EmployeeInstance.StopPoachingRivalSurrender).
        public const string OfferKey = "ba:messagetype_rivals_poach_employee_offer";
        public const string SurrenderKey = "ba:messagetype_rivals_poach_employee_rival_surrender";

        /// <summary>Which of the game's rival messages a message is.</summary>
        public enum Kind
        {
            None = 0,
            Offer = 1,       // a rival's offer: Decline sends the employee to the rival, Accept matches the wage
            Surrender = 2    // the rival has given up: the employee asks for the raise anyway
        }

        /// <summary>What a rival message's buttons are now. Only <see cref="Live"/> keeps them.</summary>
        public enum State
        {
            Live = 0,        // the offer is the player's to answer
            Gone = 1,        // not one of the player's employees: fired, resigned, sold with a business, moved away
            Replaced = 2,    // the headhunter is replacing them, or has: the same employee object under another name
            Leaving = 3,     // resigned: no satisfaction left, which the game ends at their next hour, unanswerable
            Closed = 4,      // the offer was matched, ran out or the rival gave up
            Older = 5        // an older offer: the one open now came on another day
        }

        /// <summary>What the game knows of the employee a rival's message is about, as it is looked at.</summary>
        public struct Facts
        {
            public bool Employed;        // that very employee is in the player's list of employees
            public bool SameName;        // and has the name the message was sent to (its chat's)
            public bool BeingReplaced;   // EmployeeInstance.isBeingReplaced: the headhunter replaces them
            public float Satisfaction;   // EmployeeInstance.satisfaction
            public string RivalId;       // EmployeeInstance.poachedByRivalId: empty once matched or started afresh
            public bool Known;           // the offer's two private fields below and the message's day could be read
            public int DeadlineDays;     // poachDeadlineDays: the days the open offer runs; 0 once answered or over
            public int OfferDay;         // lastPoachDay: the day of the offer open now, or of the last one made
            public int MessageDay;       // the day the message came (TextMessage.timestamp.Day)
        }

        /// <summary>The kind of a message by its key; <see cref="Kind.None"/> for every other message.</summary>
        public static Kind KindOf(string key)
        {
            return key == OfferKey ? Kind.Offer : key == SurrenderKey ? Kind.Surrender : Kind.None;
        }

        /// <summary>
        /// Whether the employee is still the player's to keep: the part of the rule about the employee alone, which
        /// business pressure's watch on its own poaching reads too (Business/BusinessIssues.Watch), so that it never
        /// says an employee has left while their offer's buttons stay, or the other way round.
        /// </summary>
        public static State Standing(bool employed, bool sameName, bool beingReplaced, float satisfaction)
        {
            if (!employed)
            {
                return State.Gone;
            }
            if (!sameName || beingReplaced)
            {
                return State.Replaced;
            }
            return satisfaction > 0f ? State.Live : State.Leaving;
        }

        /// <summary>What a rival message's buttons are now, by the rule above.</summary>
        public static State Of(Kind kind, Facts f)
        {
            if (kind == Kind.None)
            {
                return State.Live;   // not a rival's message: never this rule's to judge
            }
            State standing = Standing(f.Employed, f.SameName, f.BeingReplaced, f.Satisfaction);
            if (standing != State.Live)
            {
                return standing;
            }
            if (kind == Kind.Offer && string.IsNullOrEmpty(f.RivalId))
            {
                return State.Closed;   // matched (AcceptPoachPayRaise clears the rival), or the employee started afresh
            }
            if (!f.Known)
            {
                return State.Live;     // the offer's own fields could not be read: the employee alone is judged
            }
            if (kind == Kind.Surrender)
            {
                return f.MessageDay >= f.OfferDay ? State.Live : State.Older;   // an offer made since is newer
            }
            if (f.DeadlineDays <= 0)
            {
                return State.Closed;   // answered, run out or given up: the game leaves no days on it
            }
            return f.MessageDay == f.OfferDay ? State.Live : State.Older;
        }

        /// <summary>True when the employee is no longer the player's: business pressure says they have left.</summary>
        public static bool Lost(State state)
        {
            return state == State.Gone || state == State.Replaced || state == State.Leaving;
        }

        /// <summary>True when the employee is still the player's and the offer was matched: they stay.</summary>
        public static bool Kept(State standing, string rivalId)
        {
            return standing == State.Live && string.IsNullOrEmpty(rivalId);
        }

        /// <summary>Why the buttons went, for Player.log.</summary>
        public static string Why(State state)
        {
            switch (state)
            {
                case State.Gone:
                    return "they are no longer one of the player's employees (fired, resigned, sold or let go)";
                case State.Replaced: return "the headhunter is replacing them, or has, under another name";
                case State.Leaving: return "they have resigned and leave at their next hour";
                case State.Closed: return "the offer was matched, ran out or the rival gave up";
                case State.Older: return "a newer offer is the open one";
                default: return "the offer is still open";
            }
        }
    }
}
