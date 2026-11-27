using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Who may be a client of a tech firm: never a business in the firm's own line of work. Software development, IT, web and graphic design businesses (the game's web
    /// development agencies and graphic designers, the sector's five kinds of firm) and the listed companies of the software sector are rivals or colleagues, not customers.
    /// Everything here goes by business type or by sector, never by a name. Game-free.
    /// </summary>
    public static class ClientFilter
    {
        public const string WebAgencyType = "ba:businesstype_webdevelopmentagency";
        public const string DesignType = "ba:businesstype_graphicdesigner";

        /// <summary>The sector's five business types (TechTypes.All, which a check holds to this list).</summary>
        public static readonly string[] SectorTypes =
        {
            "reallife:businesstype_softwarestudio", "reallife:businesstype_gamestudio", "reallife:businesstype_itconsultancy", "reallife:businesstype_cybersecurity",
            "reallife:businesstype_ailab"
        };

        /// <summary>The business type a listed company competes with, as MarketCatalogue writes it, for the two trades a tech firm is in.</summary>
        public static readonly string[] SectorTrades = { "webdevelopment", "graphicdesign" };

        /// <summary>True for a business type that is software development, IT, web or graphic design: a firm of the player's sector or one of the game's own in that trade.</summary>
        public static bool TypeExcluded(string businessType)
        {
            if (string.IsNullOrEmpty(businessType))
            {
                return false;
            }
            if (businessType == WebAgencyType || businessType == DesignType)
            {
                return true;
            }
            for (int i = 0; i < SectorTypes.Length; i++)
            {
                if (SectorTypes[i] == businessType)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a business of this type may put work out to a tech firm: it has a type, is no empty plot and is not in the firm's own line of work.</summary>
        public static bool TypeAllowed(string businessType) => !string.IsNullOrEmpty(businessType) && businessType != "ba:businesstype_empty" && !TypeExcluded(businessType);

        /// <summary>Whether a listed company of the city may be a client of a tech firm: not a software company, and not one that competes with the game's web or design businesses.</summary>
        public static bool Allows(CompanyDef c)
        {
            if (c == null || c.Sector == MarketSector.Software)
            {
                return false;
            }
            for (int i = 0; i < SectorTrades.Length; i++)
            {
                if (c.Trade == SectorTrades[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether a data centre may let racks to a listed company: a client in the sense above that is on the market from the start, no tycoon's holding and not too small to
        /// take a few racks. In the catalogue's order.
        /// </summary>
        public static bool ColoOpen(CompanyDef c) => Allows(c) && !c.Holding && !c.Pipeline && c.Size != MarketSize.Small;

        private static CompanyDef[][] _pools;

        /// <summary>The listed companies of a trade that may be clients of a tech firm, in the catalogue's order. Made once.</summary>
        public static CompanyDef[] Pool(ClientTrade trade)
        {
            if (trade < 0 || trade >= ClientTrade.Count)
            {
                return System.Array.Empty<CompanyDef>();
            }
            if (_pools == null)
            {
                _pools = new CompanyDef[(int)ClientTrade.Count][];
                for (int t = 0; t < _pools.Length; t++)
                {
                    var list = new List<CompanyDef>(16);
                    for (int i = 0; i < MarketCatalogue.All.Length; i++)
                    {
                        CompanyDef c = MarketCatalogue.All[i];
                        if (Allows(c) && !c.Holding && !c.Pipeline && ClientCatalogue.InTrade(c, (ClientTrade)t))
                        {
                            list.Add(c);
                        }
                    }
                    _pools[t] = list.ToArray();
                }
            }
            return _pools[(int)trade];
        }
    }
}
