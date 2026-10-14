using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What education and careers ask of the rest of the mod. Each is optional and has a default, so Scripts/Careers works
    /// on its own; Scripts/Wiring/CareersLinks.cs fills them in. Careers never calls another feature directly.
    /// </summary>
    public static class CareersBridges
    {
        /// <summary>True if a home of the player's has a working internet connection now (Utilities). Default: yes (nothing to ask).</summary>
        public static Func<bool> HomeInternet;

        /// <summary>The council's policy on education: +1 a tuition subsidy, -1 a surcharge, 0 as the city came (Civic). Default: 0.</summary>
        public static Func<int> Council;

        /// <summary>A driving refresher passed: penalty points to take off the licence; returns how many came off (Licences). Default: none.</summary>
        public static Func<int, int> Refresher;

        /// <summary>The player's standing moves: the part (0 customers, 1 workers, 2 neighbourhood, 3 integrity) and the points. Default: nobody is told.</summary>
        public static Action<int, int> Standing;

        /// <summary>The player's standing as a professional, -10 to 10 (Standing's recognition and integrity, as an employer reads a name). Default: 0.</summary>
        public static Func<int> Reputation;

        /// <summary>The rivals' ads on the jobs board the city shares (Tech/Hiring), into the list given. Default: none.</summary>
        public static Action<List<BoardAd>> Board;

        /// <summary>The player took the post of a rival's ad: the ad is filled (a rival's name, the family and the level). Default: nothing.</summary>
        public static Action<string, int, int> TookAd;

        /// <summary>The labour market's shortage of a family of the jobs board, thousandths (1000 as many seekers as places). Default: 1000.</summary>
        public static Func<int, int> Shortage;

        /// <summary>Friends and close friends the player has (social life). Default: none.</summary>
        public static Func<int> Friends;

        /// <summary>True if the player has a record an employer would mind (the law). Default: no.</summary>
        public static Func<bool> Record;

        /// <summary>The city's economy: 0 boom, 1 steady, 2 slowdown, 3 recession. Default: steady.</summary>
        public static Func<int> Cycle;

        /// <summary>The family's children (Family): into the list given. Default: none.</summary>
        public static Action<List<ChildView>> Children;

        /// <summary>The student loan on the credit file: 0 money borrowed (the amount, whole), 1 paid off (Economy's credit rating). Default: nothing is told.</summary>
        public static Action<int, int> Credit;

        /// <summary>A former employer files a civil claim over a restraint clause: the amount (whole money) and its name; the claim's number or 0 (Justice). Default: none.</summary>
        public static Func<int, string, int> Claim;

        /// <summary>A tech firm of the player's (a business of the tech sector's types) to bring colleagues to: its place key, or null. Default: none.</summary>
        public static Func<string> TechFirm;

        /// <summary>Former colleagues join a firm of the player's: the place, the role, the level and how many; returns how many joined (Tech/People). Default: none.</summary>
        public static Func<string, int, int, int, int> JoinFirm;

        /// <summary>
        /// True once the features that take one of the city's buildings before the university have chosen theirs (the utilities
        /// office, the courthouse: Wiring/CareersLinks), so that the university never takes one of them. Default: true.
        /// </summary>
        public static Func<bool> OthersPicked;

        /// <summary>Where a building is, by the name the city knows it by.</summary>
        public delegate bool PlaceOf(string name, out string street, out int number);

        /// <summary>
        /// A tech company's headquarters (Tech/Sites: one of the city's office buildings, the same on every machine), by the
        /// company's name: where its jobs are. Default: none, and a tech company's job is at an office building given by its name.
        /// </summary>
        public static PlaceOf Headquarters;

        /// <summary>Changes whenever the tech companies' headquarters do (a tower chosen, moved, or its floors shared out again). Default: never.</summary>
        public static Func<long> HeadquartersStamp;

        /// <summary>
        /// The tech company whose floor of a tower the player is on now (Tech/Sites: several companies share a building, a
        /// floor each), or null when the player is not on such a floor. Default: none.
        /// </summary>
        public static Func<string> FloorHere;

        public static long HeadquartersStampNow()
        {
            try
            {
                return HeadquartersStamp != null ? HeadquartersStamp() : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static string FloorHereNow()
        {
            try
            {
                return FloorHere != null ? FloorHere() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool HomeOnline()
        {
            try
            {
                return HomeInternet == null || HomeInternet();
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static int CouncilNow()
        {
            try
            {
                return Council != null ? Math.Max(-1, Math.Min(1, Council())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int ReputationNow()
        {
            try
            {
                return Reputation != null ? Math.Max(-10, Math.Min(10, Reputation())) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int ShortageOf(int family)
        {
            try
            {
                return Shortage != null && family >= 0 ? Math.Max(850, Math.Min(1500, Shortage(family))) : 1000;
            }
            catch (Exception)
            {
                return 1000;
            }
        }

        public static int FriendsNow()
        {
            try
            {
                return Friends != null ? Math.Max(0, Friends()) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static bool RecordNow()
        {
            try
            {
                return Record != null && Record();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static int CycleNow()
        {
            try
            {
                return Cycle != null ? Math.Max(0, Math.Min(3, Cycle())) : 1;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        public static void Reset()
        {
            OthersPicked = null;
            Headquarters = null;
            HeadquartersStamp = null;
            FloorHere = null;
            Board = null;
            Claim = null;
            Credit = null;
            Children = null;
            TechFirm = null;
            JoinFirm = null;
            TookAd = null;
            Shortage = null;
            Friends = null;
            Record = null;
            Cycle = null;
            HomeInternet = null;
            Council = null;
            Refresher = null;
            Standing = null;
            Reputation = null;
        }
    }

    /// <summary>One rival's ad on the jobs board, as careers read it.</summary>
    public struct BoardAd
    {
        public string Rival;
        public int Family, Level, To, PayBp;
    }

    /// <summary>One child of the family, as careers read them.</summary>
    public struct ChildView
    {
        public string Name;
        public float Age, Closeness;
        public bool Grown;
        public int Fund;     // the college fund, whole money
        public int School;   // 0 none yet, 1 a state school, 2 a private one
    }
}
