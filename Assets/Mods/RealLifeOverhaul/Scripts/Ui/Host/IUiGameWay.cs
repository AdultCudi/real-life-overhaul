namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// How a game screen of the overlay kind takes its input, for a panel of the game's that stands over the running world
    /// and answers keys of its own (the checkout, the vehicle purchase, the clothes screen): an overlay draws over the
    /// world without the full menu's fade or a box's place, but such a panel's keys are the kit's while it shows. A screen
    /// that does not carry this takes the host's way for its kind. Game side; read once, as the host binds.
    /// </summary>
    public interface IUiGameWay
    {
        /// <summary>The kit reads the keys (and the pad) while the screen shows; false: the pointer alone, as the notices.</summary>
        bool Keys { get; }

        /// <summary>
        /// The game answers the same keys on this panel (Interact orders, Cancel closes): the host takes them from the game
        /// each frame after it read them, so one press does one thing.
        /// </summary>
        bool Exclusive { get; }

        /// <summary>The kit holds every click it does not take itself off the world under it, as the game's panel does.</summary>
        bool Blocks { get; }
    }

    /// <summary>A screen that takes Cancel only while it can answer it: false leaves the key to the game, so Escape is never eaten for nothing.</summary>
    public interface IUiGameBack
    {
        /// <summary>True while the screen can answer Cancel now.</summary>
        bool TakesBack { get; }
    }

    /// <summary>The game's screen over which an overlay of the kit answers keys; only Interact is taken (see <see cref="IUiGameBack"/> for Cancel).</summary>
    public interface IUiGameInteract
    {
    }
}
