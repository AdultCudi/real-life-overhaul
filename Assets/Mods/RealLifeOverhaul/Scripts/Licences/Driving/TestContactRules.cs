namespace RealLifeOverhaul
{
    /// <summary>What the examiner marks for one contact of the school's car.</summary>
    public enum ContactMark
    {
        None,        // not the car hitting anything, or too soft to be a touch
        Touched,     // a minor fault
        Collision,   // a serious fault
        RunInto,     // the car was standing or crawling: what hit it hit it, and a line crossed just after is not the driver's
        Same         // within moments of a fault already marked: the same knock
    }

    /// <summary>
    /// The examiner's marking of a contact, in plain numbers (NavCheck's CarContactCheck runs it): only a contact that
    /// CarContactRules calls a hit is marked, by its impact speed along the contact's normal; the ground, a kerb, the
    /// car's own load and triggers never are. Nothing allocated.
    /// </summary>
    public static class TestContactRules
    {
        public const float TouchFrom = 1f;        // m/s of impact along the normal that is a touch
        public const float CollisionFrom = 3.5f;  // ...and a collision
        public const float OwnMove = 1.5f;        // m/s: slower than this, what hit the car hit it
        public const float SameKnock = 2.5f;      // seconds after a marked contact in which another is the same knock

        /// <summary>The mark for a contact, given the car's own speed (m/s) and the seconds since the last contact was marked.</summary>
        public static ContactMark Mark(in CarContact contact, float carSpeed, float sinceMarked)
        {
            if (!contact.Counts(TouchFrom))
            {
                return ContactMark.None;
            }
            if (carSpeed < OwnMove)
            {
                return ContactMark.RunInto;
            }
            if (sinceMarked < SameKnock)
            {
                return ContactMark.Same;
            }
            return contact.Impact >= CollisionFrom ? ContactMark.Collision : ContactMark.Touched;
        }
    }
}
