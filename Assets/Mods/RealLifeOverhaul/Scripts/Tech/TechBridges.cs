using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the tech sector asks of the rest of the mod, filled in by Wiring/TechLinks. Each is optional with a default;
    /// the readers here also take a fault in the other feature as the default.
    /// </summary>
    public static class TechBridges
    {
        /// <summary>The player's standing as an employer, -10 to 10 (Standing's workers). Default: 0.</summary>
        public static Func<int> Esteem;

        /// <summary>A rival's name for an approach to one of the player's people at a business. Default: one of the city's tech companies.</summary>
        public static Func<BuildingRegistration, string> Rival;

        /// <summary>The player's standing as an employer moves by signed points (a lay-off, a rise refused, a promotion). Default: nobody.</summary>
        public static Action<int> Standing;

        /// <summary>True while the company feature has an offer or a departure in hand for an employee (the game's id). Default: never.</summary>
        public static Func<string, bool> Held;

        /// <summary>The economy's books made again for a day once the tech sector has booked its takings. Default: nothing.</summary>
        public static Action<int> Books;

        /// <summary>News of a listed company moves its share price: the stock market's id and the move in basis points. Default: nothing.</summary>
        public static Action<int, int> Stock;

        /// <summary>Opens a case for a data protection offence against a firm (its name), on a summons; the case's number or 0. Default: none.</summary>
        public static Func<string, int> Prosecute;

        /// <summary>A loss at a business the company's liability cover may meet: the business and the loss in whole money. Default: nobody is told.</summary>
        public static Action<BuildingRegistration, int> Claim;

        /// <summary>The player's standing with the public moves: the part (0 customers, 3 integrity) and the points. Default: nobody is told.</summary>
        public static Action<int, int> Fame;

        /// <summary>A firm of the player's asks to list: true if the stock market's listing takes it. Default: not open.</summary>
        public static Func<bool> Ipo;

        /// <summary>True if a building has the city's power (service 0) or water (1) now. Default: always.</summary>
        public static Func<BuildingRegistration, int, bool> Supplied;

        /// <summary>A building's internet speed in Mbps; 0 with none or in an outage. Default: unknown, and a data centre counts as connected.</summary>
        public static Func<BuildingRegistration, int> Mbps;

        /// <summary>A business's internet: 0 working or not needed, 1 no plan, 2 being installed (when: the day), 3 down (when: the hour back).</summary>
        public delegate int NetAsk(BuildingRegistration reg, out int when);

        /// <summary>Why a business's internet keeps it closed (<see cref="NetAsk"/>). Default: nothing in the way.</summary>
        public static NetAsk Net;

        /// <summary>Opens the Utilities app on its internet page; false if it cannot be. Default: none.</summary>
        public static Func<bool> OpenInternet;

        /// <summary>Opens the Company app's rota with a business (its place) picked; false if it cannot be. Default: none.</summary>
        public static Func<string, bool> OpenRota;

        /// <summary>True while the utilities have no office to visit, so power is applied for in the Utilities app. Default: none (the office).</summary>
        public static Func<bool> UtilitiesByPhone;

        /// <summary>The days a new internet plan takes to be installed (the utilities). Default: 2.</summary>
        public static Func<int> NetDays;

        public static int NetDaysNow() => Ask(NetDays, 0, int.MaxValue, 2);

        /// <summary><see cref="Supplied"/> asked: true with no answer, or if the utilities fault.</summary>
        public static bool SuppliedAt(BuildingRegistration reg, int service)
        {
            try
            {
                return Supplied == null || Supplied(reg, service);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>True if a building's internet gives at least <paramref name="mbps"/>; true with no answer, or if the utilities fault.</summary>
        public static bool FastEnough(BuildingRegistration reg, int mbps)
        {
            try
            {
                return Mbps == null || Mbps(reg) >= mbps;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary><see cref="UtilityCents"/> asked; <paramref name="otherwise"/> with no answer, or if the utilities fault.</summary>
        public static long UtilityCentsAt(int service, long thousandths, long otherwise)
        {
            try
            {
                return UtilityCents != null ? UtilityCents(service, thousandths) : otherwise;
            }
            catch (Exception)
            {
                return otherwise;
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

        /// <summary>A day's power (service 0, thousandths of a kWh) or water (1, of a cubic metre), cents at today's prices. Default: a plain tariff.</summary>
        public static Func<int, long, long> UtilityCents;

        /// <summary>What the player's record as a founder adds to a pitch, thousandths (Careers). Default: nothing.</summary>
        public static Func<int> Founder;

        /// <summary>What a course costs the player's staff, basis points of its fee (less with an academy of their own). Default: the whole fee.</summary>
        public static Func<int> CourseShareBp;

        /// <summary>A sponsored degree's fee now, whole money, or 0 with the university closed (Careers). Default: none.</summary>
        public static Func<int> DegreeFee;

        /// <summary>True once the features that take a building before the headquarters have chosen theirs. Default: true.</summary>
        public static Func<bool> OthersPicked;

        /// <summary>The name of the city's tech company the player works for (Careers), or null. Default: none.</summary>
        public static Func<string> Employer;

        public static string EmployerNow() => Ask(Employer);

        /// <summary>The training academy's business type while it runs (Careers). Default: none.</summary>
        public static Func<string> AcademyType;

        /// <summary>The places that teach courses, by name, as an academy's competitors (Careers). Default: none.</summary>
        public static Action<List<string>> Schools;

        public static string AcademyNow() => Ask(AcademyType);

        public static void SchoolsNow(List<string> into)
        {
            into.Clear();
            try
            {
                Schools?.Invoke(into);
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        public static int CourseShare() => Ask(CourseShareBp, 0, 10000, 10000);

        public static int FounderNow() => Ask(Founder, 0, 200, 0);

        /// <summary>Thousandths a professional's morale heads higher for the comfort of their workplace, 0 to 100 (Interiors). Default: nothing.</summary>
        public static Func<string, int> Comfort;

        /// <summary>What the player's best office adds to the esteem candidates feel, 0 to 3 (Interiors). Default: nothing.</summary>
        public static Func<int> Office;

        /// <summary>Thousandths a firm's bids weigh better for its office, 0 to 50 (Interiors). Default: nothing.</summary>
        public static Func<string, int> OfficeBid;

        public static int ComfortAt(string place) => Ask(Comfort, place, 0, 100);

        /// <summary>The esteem candidates feel: the player's as an employer and their best office's, -10 to 10.</summary>
        public static int HiringEsteem()
        {
            int office = Ask(Office, 0, 3, 0);
            return Math.Max(-10, Math.Min(10, EsteemNow() + office));
        }

        public static int OfficeBidAt(string place) => Ask(OfficeBid, place, 0, 50);

        public static int EsteemNow() => Ask(Esteem, -10, 10, 0);

        // A bridge's answer between least and most; otherwise with none, or if it throws.
        private static int Ask(Func<int> ask, int least, int most, int otherwise)
        {
            try
            {
                return ask != null ? Math.Max(least, Math.Min(most, ask())) : otherwise;
            }
            catch (Exception)
            {
                return otherwise;
            }
        }

        // A bridge's answer for a place between least and most; least with none, or if it throws.
        private static int Ask(Func<string, int> ask, string place, int least, int most)
        {
            try
            {
                return ask != null ? Math.Max(least, Math.Min(most, ask(place))) : least;
            }
            catch (Exception)
            {
                return least;
            }
        }

        private static string Ask(Func<string> ask)
        {
            try
            {
                return ask != null ? ask() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Reset()
        {
            UtilitiesByPhone = null;
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
            Employer = null;
            AcademyType = null;
            Schools = null;
        }
    }
}
