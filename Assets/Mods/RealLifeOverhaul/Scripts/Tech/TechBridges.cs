using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the tech sector asks of the rest of the mod. Each is optional and has a default, so Scripts/Tech works on its
    /// own; Scripts/Wiring/TechLinks.cs fills them in. The tech sector never calls another feature directly.
    /// </summary>
    public static class TechBridges
    {
        /// <summary>The player's standing as an employer, -10 to 10 (Standing's workers). Default: 0.</summary>
        public static Func<int> Esteem;

        /// <summary>A rival's name to put to an approach for one of the player's people at a business. Default: one of the city's tech companies.</summary>
        public static Func<BuildingRegistration, string> Rival;

        /// <summary>Told when the player's standing as an employer should move: points, signed (a lay-off, a rise refused, a promotion). Default: nobody.</summary>
        public static Action<int> Standing;

        /// <summary>True while the company already has a rival's offer or a departure in hand for an employee (by the game's id): no second approach is made. Default: never.</summary>
        public static Func<string, bool> Held;

        /// <summary>The economy's books made again for a day once the tech sector has booked its takings for it (GameBooks.Refresh). Default: nothing.</summary>
        public static Action<int> Books;

        /// <summary>News of a listed company moves its share price: the stock market's id and the move in basis points. Default: nothing.</summary>
        public static Action<int, int> Stock;

        /// <summary>Opens a case for a data protection offence against a firm (its name), on a summons; the case's number or 0. Default: none.</summary>
        public static Func<string, int> Prosecute;

        /// <summary>A loss at a business the company's liability cover may meet: the business and the loss in whole money. Default: nobody is told.</summary>
        public static Action<BuildingRegistration, int> Claim;

        /// <summary>The player's standing with the public moves: the part (0 customers, 3 integrity) and the points. Default: nobody is told.</summary>
        public static Action<int, int> Fame;

        /// <summary>
        /// A firm of the player's asks to come to the market: the stock market's listing (Markets/Listing) takes the player's
        /// company there, and this opens its way. True if it is open. Default: none, and the firm is told it is not.
        /// </summary>
        public static Func<bool> Ipo;

        /// <summary>True if a building has the city's power (service 0) or water (1) now. Default: always.</summary>
        public static Func<BuildingRegistration, int, bool> Supplied;

        /// <summary>The speed of a building's internet connection in Mbps; 0 with none or in an outage. Default: unknown (a data centre is taken to be connected).</summary>
        public static Func<BuildingRegistration, int> Mbps;

        /// <summary>A day's power (service 0, thousandths of a kWh) or water (1, thousandths of a cubic metre) at the city's utilities' tariff, cents at today's prices. Default: a plain tariff.</summary>
        public static Func<int, long, long> UtilityCents;

        public static int EsteemNow()
        {
            try
            {
                return Esteem != null ? Math.Max(-10, Math.Min(10, Esteem())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Reset()
        {
            Esteem = null;
            Rival = null;
            Standing = null;
            Held = null;
            Books = null;
            Stock = null;
            Prosecute = null;
            Claim = null;
            Fame = null;
            Ipo = null;
            Supplied = null;
            Mbps = null;
            UtilityCents = null;
        }
    }
}
