namespace RealLifeOverhaul
{
    /// <summary>
    /// When the drinks cart is heard, as rules with no game types, checked offline. A customer's voice as their order starts, the
    /// same voice once more if the order is a third from walking off and nothing has been tried, and the till when a drink is
    /// paid for; nothing while the game is paused, the clock is working or the shift is over. A few comparisons on an event.
    /// </summary>
    public static class CartSoundRules
    {
        public const float RemindShare = 1f / 3f;   // of the patience left when an untouched order is voiced once more: late enough not to nag, early enough to serve

        /// <summary>Whether the cart may make a sound now: the option on, the game running, no clock run and the shift not ending.</summary>
        public static bool Audible(bool option, float timeScale, bool skipping, bool ending)
        {
            return option && timeScale > 0f && !skipping && !ending;
        }

        /// <summary>
        /// Whether an order's one reminder is due: still to come (<paramref name="due"/>), no drink tried (one who chose a wrong drink
        /// heard the order), and <see cref="RemindShare"/> of the full patience or less left before <paramref name="patience"/> runs
        /// out from <paramref name="orderedAt"/>, but the customer not yet gone (<paramref name="orderEnds"/>, which a wrong drink brings
        /// forward).
        /// </summary>
        public static bool Remind(bool due, bool tried, float now, float orderedAt, float patience, float orderEnds)
        {
            return due && !tried && now >= orderedAt + patience * (1f - RemindShare) && now < orderEnds;
        }

        /// <summary>
        /// The customer's voice when their character's look cannot be read: by their seeded lease, the same on every machine and
        /// never the game's random sequence. True for the female voice.
        /// </summary>
        public static bool FemaleBySeed(int lease)
        {
            return (lease & 1) == 1;
        }

        /// <summary>The shift's sound self-check: every cue wanted was played. Nothing wanted (the option off) passes too.</summary>
        public static bool AllPlayed(int wanted, int played)
        {
            return wanted == played;
        }
    }
}
