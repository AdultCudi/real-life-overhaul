using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Every app's one glyph, settled once and read everywhere an app is shown: the phone, the status bar's app row, All
    /// apps and each app window's title strip. The game's eight apps, the mod's, the five folders, All apps and other
    /// mods' tab; an app's colour is its glyph's own (UiIconTints.Of), so one glyph is one colour on every screen. Two
    /// apps never share a glyph, and no two in one folder or beside each other on the home screen look alike
    /// (EconomyCheck, UiChecks.Phone). Another mod's app has no glyph: it shows its own picture. Game-free. Cost: a
    /// search of a short table by name.
    /// </summary>
    public static class PhoneLooks
    {
        /// <summary>Each app by the name of its button: the game's by its AppName, the mod's by its app id.</summary>
        public static readonly string[] Ids =
        {
            "Persona", "Contacts", "MyEmployees", "BizMan", "EconoView", "VoogleMaps", "MarketInsider", "Rivals",
            "RealLifePlanner", "RealLifeFlare", "RealLifeCalendar", "RealLifeCamera", "RealLifeGallery", "RealLifeHealth", "RealLifeNewsstand", "RealLifeLedger",
            "RealLifeCareer", "RealLifeCampus", "RealLifeOpenRoles", "RealLifeMarketplace",
            "RealLifeFinance", "RealLifeBanks", "RealLifeGiving", "RealLifeOwnBank", "RealLifeStocks", "RealLifeEconomy",
            "RealLifeCompany", "RealLifeBrands", "RealLifeServices", "RealLifeVentures", "RealLifeOnline", "RealLifeEstate", "RealLifeDealer",
            "RealLifeBuilders", "RealLifeTechSector", "RealLifeOverhaulUtilities",
            "RealLifeGuide", "RealLifeSettings", "RealLifeStaff"
        };

        /// <summary>Each app's glyph, in the order of <see cref="Ids"/>.</summary>
        public static readonly UiIcon[] Glyphs =
        {
            UiIcon.IdCard, UiIcon.AddressBook, UiIcon.People, UiIcon.Briefcase, UiIcon.ChartPie, UiIcon.MapTrifold, UiIcon.Store, UiIcon.Flag,
            UiIcon.Clock, UiIcon.Fire, UiIcon.Calendar, UiIcon.Camera, UiIcon.Image, UiIcon.Heart, UiIcon.Newspaper, UiIcon.Receipt,
            UiIcon.Trophy, UiIcon.Cap, UiIcon.Handshake, UiIcon.Package,
            UiIcon.Wallet, UiIcon.Bank, UiIcon.Percent, UiIcon.PiggyBank, UiIcon.ChartLineUp, UiIcon.Globe,
            UiIcon.Building, UiIcon.Tag, UiIcon.Headset, UiIcon.Rocket, UiIcon.ShoppingCart, UiIcon.Home, UiIcon.Car,
            UiIcon.Hammer, UiIcon.Cpu, UiIcon.Lightning,
            UiIcon.Lightbulb, UiIcon.Gear, UiIcon.IdBadge
        };

        /// <summary>The five folders' glyphs, in the folders' order: money, work and study, social and life, business, settings.</summary>
        public static readonly UiIcon[] FolderGlyphs = { UiIcon.Coin, UiIcon.Factory, UiIcon.Coffee, UiIcon.Presentation, UiIcon.Wrench };

        /// <summary>The status bar's last tile, which raises every app.</summary>
        public const UiIcon AllApps = UiIcon.Grid;

        /// <summary>All apps' tab for other mods' buttons.</summary>
        public const UiIcon OtherMods = UiIcon.Code;

        /// <summary>The key of the tab of other mods' buttons.</summary>
        public const string OtherModsKey = "reallife_phone_other_mods";

        /// <summary>An app's glyph by its button's name; None for another mod's (its own picture stands in).</summary>
        public static UiIcon Glyph(string id)
        {
            int at = IndexOf(id);
            return at >= 0 ? Glyphs[at] : UiIcon.None;
        }

        /// <summary>An app's glyph and its colour; false for another mod's, which shows its own picture.</summary>
        public static bool Of(string id, out UiIcon icon, out UiRole tint)
        {
            icon = Glyph(id);
            tint = icon != UiIcon.None ? UiIconTints.Of(icon) : UiRole.None;
            return icon != UiIcon.None;
        }

        /// <summary>A folder's glyph and colour (0 money to 4 settings); false for no folder.</summary>
        public static bool Folder(int folder, out UiIcon icon, out UiRole tint)
        {
            bool known = folder >= 0 && folder < FolderGlyphs.Length;
            icon = known ? FolderGlyphs[folder] : UiIcon.None;
            tint = known ? UiIconTints.Of(icon) : UiRole.None;
            return known;
        }

        /// <summary>The place of a button's name in <see cref="Ids"/>; -1 for another mod's.</summary>
        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }
            for (int i = 0; i < Ids.Length; i++)
            {
                if (string.Equals(Ids[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
