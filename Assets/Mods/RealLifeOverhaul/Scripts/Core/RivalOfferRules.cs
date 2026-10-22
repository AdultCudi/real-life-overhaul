namespace RealLifeOverhaul
{
    /// <summary>
    /// When a rival's offer to one of the player's employees keeps its buttons. The game takes them away only on a press
    /// and keeps them in the save as calls on the employee, so an offer to someone fired, resigned, sold with a business,
    /// replaced by the headhunter (the same object renamed) or let go by the mod keeps live buttons, and Decline would
    /// send somebody to the rival again. The buttons stay while that very employee is the player's under the chat's
    /// name, not being replaced or resigned, and the offer is open and this message's own (sent on its day); the
    /// rival's giving up keeps them until a newer offer. Game-free; a few comparisons a message.
    /// </summary>
    public static class RivalOfferRules
    {
        // The game's two rival messages with buttons (EmployeeInstance.PoachByRival, StopPoachingRivalSurrender).
        public const string OfferKey = "ba:messagetype_rivals_poach_employee_offer";
        public const string SurrenderKey = "ba:messagetype_rivals_poach_employee_rival_surrender";

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

        /// <summary>What the game knows of the employee a rival's message is about.</summary>
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

        public static Kind KindOf(string key)
        {
            return key == OfferKey ? Kind.Offer : key == SurrenderKey ? Kind.Surrender : Kind.None;
        }

        /// <summary>
        /// The part of the rule about the employee alone, which business pressure's poaching watch reads too, so the two
        /// always agree on whether an employee has left.
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

        /// <summary>What a rival message's buttons are now.</summary>
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

        /// <summary>
        /// Whether the game's own offer to an employee is open: a rival named and days left on it. Without the days
        /// (<paramref name="known"/> false) while a rival is named, which at worst keeps the mod's own offers waiting.
        /// </summary>
        public static bool Open(string rivalId, bool known, int deadlineDays)
        {
            return !string.IsNullOrEmpty(rivalId) && (!known || deadlineDays > 0);
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

        /// <summary>Why the buttons went, for the log.</summary>
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
