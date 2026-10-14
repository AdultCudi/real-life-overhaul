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

        /// <summary>
        /// The internet of a business that cannot open without it (the utilities): 0 working or not needed, 1 no plan,
        /// 2 being installed (<paramref name="when"/>: the day it connects), 3 down for a while (when: the hour it is back).
        /// </summary>
        public delegate int NetAsk(BuildingRegistration reg, out int when);

        /// <summary>Why a business's internet keeps it closed (<see cref="NetAsk"/>). Default: nothing in the way.</summary>
        public static NetAsk Net;

        /// <summary>Opens the Utilities app on its internet page; false if it cannot be. Default: none.</summary>
        public static Func<bool> OpenInternet;

        /// <summary>Opens the Company app's rota with a business picked (its place), to plan its shifts; false if it cannot be. Default: none.</summary>
        public static Func<string, bool> OpenRota;

        /// <summary>The days a new internet plan takes to be installed (the utilities). Default: 2.</summary>
        public static Func<int> NetDays;

        public static int NetDaysNow()
        {
            try
            {
                return NetDays != null ? Math.Max(0, NetDays()) : 2;
            }
            catch (Exception)
            {
                return 2;
            }
        }

        public static int NetOf(BuildingRegistration reg, out int when)
        {
            when = 0;
            try
            {
                return Net != null && reg != null ? Math.Max(0, Math.Min(3, Net(reg, out when))) : 0;
            }
            catch (Exception)
            {
                when = 0;
                return 0;
            }
        }

        public static bool Try(Func<bool> open)
        {
            try
            {
                return open != null && open();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool RotaFor(string place)
        {
            try
            {
                return OpenRota != null && OpenRota(place);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A day's power (service 0, thousandths of a kWh) or water (1, thousandths of a cubic metre) at the city's utilities' tariff, cents at today's prices. Default: a plain tariff.</summary>
        public static Func<int, long, long> UtilityCents;

        /// <summary>What the player's record as a founder adds to a pitch, thousandths (Careers: the rungs reached in a tech profession, the years, their name). Default: nothing.</summary>
        public static Func<int> Founder;

        /// <summary>What a course costs the player's staff, basis points of its fee (Careers: half with an academy of the player's own). Default: the whole fee.</summary>
        public static Func<int> CourseShareBp;

        /// <summary>A degree the business sponsors for a professional: its fee now, whole money, or 0 with the university closed (Careers). Default: none.</summary>
        public static Func<int> DegreeFee;

        /// <summary>
        /// True once the features that take one of the city's buildings before the tech companies' headquarters have chosen
        /// theirs (the utilities office, the courthouse, the police stations, the university: Wiring/TechLinks), so that a
        /// headquarters is never one of them. Default: true.
        /// </summary>
        public static Func<bool> OthersPicked;


        public static int CourseShare()
        {
            try
            {
                return CourseShareBp != null ? Math.Max(0, Math.Min(10000, CourseShareBp())) : 10000;
            }
            catch (Exception)
            {
                return 10000;
            }
        }

        public static int FounderNow()
        {
            try
            {
                return Founder != null ? Math.Max(0, Math.Min(200, Founder())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Thousandths a professional's morale heads higher for the comfort of their workplace, by its place, 0 to 100 (Scripts/Interiors). Default: nothing.</summary>
        public static Func<string, int> Comfort;

        /// <summary>What the player's best office adds to the esteem candidates on the jobs board feel, 0 to 3 (Scripts/Interiors). Default: nothing.</summary>
        public static Func<int> Office;

        /// <summary>Thousandths a firm's bids weigh better for its office, by its place, 0 to 50 (Scripts/Interiors). Default: nothing.</summary>
        public static Func<string, int> OfficeBid;

        public static int ComfortAt(string place)
        {
            try
            {
                return Comfort != null ? Math.Max(0, Math.Min(100, Comfort(place))) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The esteem candidates feel: the player's as an employer and their best office's, -10 to 10.</summary>
        public static int HiringEsteem()
        {
            int office;
            try
            {
                office = Office != null ? Math.Max(0, Math.Min(3, Office())) : 0;
            }
            catch (Exception)
            {
                office = 0;
            }
            return Math.Max(-10, Math.Min(10, EsteemNow() + office));
        }

        public static int OfficeBidAt(string place)
        {
            try
            {
                return OfficeBid != null ? Math.Max(0, Math.Min(50, OfficeBid(place))) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

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
            Comfort = null;
            Office = null;
            OfficeBid = null;
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
            Net = null;
            NetDays = null;
            OpenInternet = null;
            OpenRota = null;
            UtilityCents = null;
            Founder = null;
            CourseShareBp = null;
            DegreeFee = null;
            OthersPicked = null;
        }
    }
}
