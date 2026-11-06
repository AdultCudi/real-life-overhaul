using System.Collections.Generic;
using System.Globalization;
using Buildings;
using UI.Notification;

namespace RealLifeOverhaul.Shops
{
    /// <summary>The new kinds of shop's test buttons: are the kinds there, how one of the player's shops is doing today, and a kind's help page.</summary>
    public static partial class ShopsDirector
    {
        private const string TestName = "New shops";
        private static readonly List<BuildingRegistration> Shops = new List<BuildingRegistration>(4);
        private static int _helpNext;

        /// <summary>Whether every kind is registered, trading as its donor and offered as the option says.</summary>
        public static void TestSay()
        {
            ShopsGame.Register(Settings.NewShops);
            ShopsGame.Count(out int registered, out int furnished);
            int all = ShopsCatalogue.Kinds.Length;
            bool ready = registered == all && furnished == all;
            Logger?.Info(TestName + " test (the kinds): " + registered + " of " + all + " registered, " + furnished + " trading as the game's shops they are made like, offered: "
                         + (ShopsGame.Offered ? "yes" : "no") + ": " + (ready && ShopsGame.Offered == Settings.NewShops ? "PASS" : "FAIL"));
            if (!ready)
            {
                ShopsGame.Notice(NotificationType.Warning, ShopsText.SayMissing, new Dictionary<string, string>(2)
                {
                    { "count", furnished.ToString(CultureInfo.InvariantCulture) }, { "all", all.ToString(CultureInfo.InvariantCulture) }
                });
                return;
            }
            ShopsGame.Notice(NotificationType.Success, ShopsGame.Offered ? ShopsText.SayOk : ShopsText.SayHidden, new Dictionary<string, string>(1)
            {
                { "count", all.ToString(CultureInfo.InvariantCulture) }
            });
        }

        /// <summary>The shop the player stands in, or their first of the new kinds: open, its requirements, the game's customers for it today.</summary>
        public static void TestToday()
        {
            if (!ShopsGame.Loaded())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            ShopsGame.Mine(Shops);
            if (Shops.Count == 0)
            {
                Logger?.Info(TestName + " test (today): the player has no business of the new kinds: FAIL, start one in BizMan first.");
                ShopsGame.Notice(NotificationType.Info, ShopsText.NoShop, null);
                return;
            }
            ShopLook look = ShopsGame.Look(Shops[0]);
            Shops.Clear();
            string name = string.IsNullOrEmpty(look.Name) ? ShopsGame.Named(look.Type) : look.Name;
            if (look.Unmet > 0)
            {
                Logger?.Info(TestName + " test (today): " + look.Type + " has " + look.Unmet + " requirements not met (" + look.FirstUnmet + "): FAIL, it cannot trade yet.");
                ShopsGame.Notice(NotificationType.Warning, ShopsText.Unmet, new Dictionary<string, string>(2)
                {
                    { "name", name }, { "need", ShopsGame.Named(look.FirstUnmet) }
                });
                return;
            }
            if (!look.Open)
            {
                Logger?.Info(TestName + " test (today): " + look.Type + " is closed all day today: FAIL, open it in BizMan's schedule.");
                ShopsGame.Notice(NotificationType.Warning, ShopsText.Closed, new Dictionary<string, string>(1) { { "name", name } });
                return;
            }
            Logger?.Info(TestName + " test (today): " + look.Type + " has " + look.Customers + " of the game's customers today, " + look.Wanting
                         + " of them for goods it has on show: " + (look.Wanting > 0 ? "PASS" : "FAIL, nothing it sells is on show or wanted yet"));
            ShopsGame.Notice(look.Wanting > 0 ? NotificationType.Success : NotificationType.Warning, ShopsText.Today, new Dictionary<string, string>(3)
            {
                { "name", name }, { "count", look.Customers.ToString(CultureInfo.InvariantCulture) }, { "wanting", look.Wanting.ToString(CultureInfo.InvariantCulture) }
            });
        }

        /// <summary>The game's help screen at the page of the kind the player stands in, or at each kind's in turn.</summary>
        public static void TestHelp()
        {
            if (!ShopsGame.Loaded())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            ShopsGame.Mine(Shops);
            ShopKind kind = Shops.Count > 0 ? ShopsCatalogue.Of(Shops[0].businessTypeName) : null;
            Shops.Clear();
            if (kind == null)
            {
                kind = ShopsCatalogue.Kinds[_helpNext % ShopsCatalogue.Kinds.Length];
                _helpNext++;
            }
            string slug = ShopsRules.HelpSlug(kind.Type);
            int pages = ShopsGame.Pages();
            bool page = ShopsGame.HasPage(kind.Type), opened = page && ShopsGame.OpenHelp(slug);
            Logger?.Info(TestName + " test (help page): " + pages + " pages in the game's language tables, the game's help opened at " + slug + ": "
                         + (opened ? "PASS" : page ? "FAIL, the help screen is not there" : "FAIL, the game's tables do not hold the page"));
            if (!opened)
            {
                ShopsGame.Notice(NotificationType.Warning, ShopsText.HelpFailed, null);
            }
        }
    }
}
