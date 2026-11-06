using BAModAPI;

namespace RealLifeOverhaul.Shops
{
    /// <summary>
    /// The new kinds of shop: an ice cream parlour, a tea bar and a tobacconist, registered with the game at its start
    /// (a save that holds one must find its type, whatever the option says), furnished once the game's own types are
    /// loaded, and offered when a business is started while the option is on. The game runs them as its own shops from
    /// there: customers, sales, stock, staff and BizMan. Nothing per frame, nothing saved, nothing sent.
    /// </summary>
    public static partial class ShopsDirector
    {
        public static IModLogger Logger;

        /// <summary>At the game's start, before any save is read.</summary>
        public static void Start()
        {
            ShopsGame.Logger = Logger;
            ShopsGame.Listen(true);   // the help pages, after each load of the game's language tables
            ShopsGame.Pages();
            int done = ShopsGame.Register(Settings.NewShops);
            ShopsGame.Count(out int registered, out int furnished);
            Logger?.Info("New shops: " + done + " of " + ShopsCatalogue.Kinds.Length + " business types registered for retail buildings, "
                         + (Settings.NewShops ? "offered when a business is started" : "not offered (the option is off)")
                         + (furnished == registered ? "." : "; furnished as the game's shops they trade like once the game's types are loaded."));
        }

        /// <summary>A city has loaded: the kinds furnished if they were not yet, and with Developer diagnostics on, the check line.</summary>
        public static void CityLoaded()
        {
            ShopsGame.Register(Settings.NewShops);
            int pages = ShopsGame.Pages();
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            ShopsGame.Count(out int registered, out int furnished);
            int all = ShopsCatalogue.Kinds.Length;
            bool shown = ShopsGame.HasPage(ShopsCatalogue.Kinds[0].Type);
            bool pass = registered == all && furnished == all && ShopsGame.Offered == Settings.NewShops && shown;
            Logger?.Info("New shops check (city load): " + registered + " of " + all + " kinds registered, " + furnished
                         + " trading as the game's shops they are made like, offered when a business is started: " + (ShopsGame.Offered ? "yes" : "no")
                         + " (the option says " + (Settings.NewShops ? "yes" : "no") + "), " + pages + " help pages written in the game's tables, the first shown: "
                         + (shown ? "yes" : "no") + ": " + (pass ? "PASS" : "FAIL"));
        }

        /// <summary>The mod is being unloaded: the language handler taken off the game's event.</summary>
        public static void Stop()
        {
            ShopsGame.Listen(false);
        }

        /// <summary>The option switched: the kinds offered or not from now on; shops already open trade on either way.</summary>
        public static void Switch(bool on)
        {
            Settings.NewShops = on;
            int done = ShopsGame.Register(on);
            Logger?.Info("New shops: " + done + " kinds " + (on ? "offered" : "no longer offered") + " when a business is started; shops already open trade on.");
        }
    }
}
