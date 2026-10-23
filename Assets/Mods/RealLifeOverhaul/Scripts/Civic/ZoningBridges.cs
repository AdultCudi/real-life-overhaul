using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the council's planning business asks of the builders (Scripts/Construction), with defaults that leave the
    /// council with nothing to hear: set by Wiring/Wiring.ConstructionLinks. Asked when a matter is tabled, voted or shown,
    /// never per frame; a fault there is logged and answered as if unset.
    /// </summary>
    public static class ZoningBridges
    {
        /// <summary>A builder's application decided by the council: the lot, the use, carried or not, the ayes and the noes. Default: nothing.</summary>
        public static Action<string, int, bool, int, int> Decided;

        /// <summary>A motion to allow (true) or stop allowing a use on every lot of a class of the new district carried. Default: nothing.</summary>
        public static Action<int, int, bool> Rezoned;

        /// <summary>True while the player has an interest in the building code: a construction company, or land in the new district. Default: none.</summary>
        public static Func<bool> Interest;

        /// <summary>A lot in words (its address). Default: none.</summary>
        public static Func<string, string> LotWords;

        /// <summary>The uses a class of the new district's lots is zoned for now, bits of the builders' uses. Default: none.</summary>
        public static Func<int, int> ClassZones;

        /// <summary>The uses a class of the new district's lots was zoned for as the district came: the council may not take those away. Default: none.</summary>
        public static Func<int, int> FirstZonesOf;

        /// <summary>The planners' view of a use on a class of lot, in thousandths. Default: an even view.</summary>
        public static Func<int, int, int> Suit;

        /// <summary>True while the new district is one of the city's at this load: its zoning may be moved. Default: no.</summary>
        public static Func<bool> District;

        public static void Reset()
        {
            Decided = null;
            Rezoned = null;
            Interest = null;
            LotWords = null;
            ClassZones = null;
            FirstZonesOf = null;
            Suit = null;
            District = null;
        }

        internal static bool HasInterest()
        {
            try
            {
                return Interest != null && Interest();
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static string Lot(string lot)
        {
            try
            {
                return LotWords != null ? LotWords(lot) ?? string.Empty : string.Empty;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return string.Empty;
            }
        }

        internal static int Zones(int lotClass)
        {
            try
            {
                return ClassZones != null ? ClassZones(lotClass) : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        internal static int FirstZones(int lotClass)
        {
            try
            {
                return FirstZonesOf != null ? FirstZonesOf(lotClass) : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        internal static int SuitOf(int lotClass, int use)
        {
            try
            {
                return Suit != null ? Suit(lotClass, use) : 500;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 500;
            }
        }

        /// <summary>The builders' uses, in their order (homes, shops, offices, warehouses), as words inside a sentence.</summary>
        public static readonly string[] UseKeys =
        {
            "reallife_civic_zoning_use_0", "reallife_civic_zoning_use_1", "reallife_civic_zoning_use_2", "reallife_civic_zoning_use_3"
        };

        /// <summary>The new district's classes of lot, in the builders' order (small, office, big, warehouse), as words inside a sentence.</summary>
        public static readonly string[] ClassKeys =
        {
            "reallife_civic_zoning_class_0", "reallife_civic_zoning_class_1", "reallife_civic_zoning_class_2", "reallife_civic_zoning_class_3"
        };

        public const int Uses = 4, Classes = 4;

        internal static string Use(int use) => CivicText.T(UseKeys[use < 0 ? 0 : use >= Uses ? Uses - 1 : use]);

        internal static string Class(int lotClass) => CivicText.T(ClassKeys[lotClass < 0 ? 0 : lotClass >= Classes ? Classes - 1 : lotClass]);

        /// <summary>The uses of a set of zones in words, joined; "nothing" for none.</summary>
        internal static string UsesWords(int zones)
        {
            var words = new System.Text.StringBuilder(64);
            for (int u = 0; u < Uses; u++)
            {
                if ((zones & (1 << u)) != 0)
                {
                    words.Append(words.Length > 0 ? ", " : string.Empty).Append(Use(u));
                }
            }
            return words.Length > 0 ? words.ToString() : CivicText.T("reallife_civic_zoning_none");
        }

        internal static bool DistrictLives()
        {
            try
            {
                return District != null && District();
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static void TellDecided(string lot, int use, bool passed, int ayes, int noes)
        {
            try
            {
                Decided?.Invoke(lot, use, passed, ayes, noes);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        internal static void TellRezoned(int lotClass, int use, bool add)
        {
            try
            {
                Rezoned?.Invoke(lotClass, use, add);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }
    }
}
