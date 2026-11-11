using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Where the player is sent for an employer's business: a help-wanted board, the building trades' works site or floor, or nowhere (the phone).</summary>
    public enum OfficeWhere
    {
        Phone = 0,   // no place stands: everything is done by phone
        Works = 1,   // the building trades' works site or practice floor, named by its address
        Board = 2    // the employer's help-wanted board on the pavement outside a shop or service building
    }

    /// <summary>
    /// Where an employer's office is, as rules checked offline by JobOfficeCheck and EconomyCheck (MeetingPlaceChecks). A text sending
    /// the player there has twins: its key plus "_at" naming a works site's address, its key plus "_board" naming the board, the
    /// shop it stands outside and the address; the plain one is for an employer with nowhere to go in this city.
    /// </summary>
    public static class CareerOfficeRules
    {
        public const string Suffix = "_at";         // the twin of a text that names a works site's address
        public const string BoardSuffix = "_board"; // the twin of a text that names the board, the shop it stands outside and the address
        public const string AddressField = "address";   // the placeholder the twins carry
        public const string PlaceField = "place";       // ...and the board's twin the name of the shop or service over the door beside it
        public const string NoneSuffix = "_none";       // the twin of a refusal that sends the player to a place which does not stand in this city
        public const string NoOfficeKey = "reallife_trades_no_office";   // the refusals of a job worked at a place (the practice's desk, a works site)...
        public const string NoSiteKey = "reallife_trades_no_site";
        public const string NoIndoorsKey = "reallife_job_no_indoors";    // ...and the one an office trade gets for being indoors anywhere else
        public const string AtDepotKey = "reallife_career_at_depot";     // the refusal of a desk or lead shift clocked on away from the office
        public const float BoardReach = 25f;   // metres: the destination marker on a board comes off once the player is this near it
        public const float WorksReach = 8f;    // metres: ...and on a works site or the practice's door, which the player comes right up to

        /// <summary>
        /// Where an employer's business is held now. A board counts only while it stands: placed in this city (<paramref name="board"/> is an
        /// index below <paramref name="boards"/>) and with its building still there. An employer without a board (the building trades) is
        /// at its works site or floor when it has one; otherwise, or when its board is gone, the phone is the place.
        /// </summary>
        public static OfficeWhere Where(int board, int boards, bool boardStands, bool worksAddress)
        {
            if (board >= 0)
            {
                return ShowMeOffered(board, boards) && boardStands ? OfficeWhere.Board : OfficeWhere.Phone;
            }
            return worksAddress ? OfficeWhere.Works : OfficeWhere.Phone;
        }

        /// <summary>The key for a message sending the player to the place: the twin for a board or a works site, the plain phone text for neither.</summary>
        public static string Key(string key, OfficeWhere where)
        {
            return where == OfficeWhere.Board ? key + BoardSuffix : where == OfficeWhere.Works ? key + Suffix : key;
        }

        /// <summary>
        /// Where a performance review is held: face to face at the board, else by phone. A works site is no place for a meeting (nobody
        /// waits there), so a review for the trades is by phone as it always was.
        /// </summary>
        public static OfficeWhere ReviewWhere(OfficeWhere where)
        {
            return where == OfficeWhere.Board ? OfficeWhere.Board : OfficeWhere.Phone;
        }

        /// <summary>Whether the review can be held now: by phone always when no board stands, at a standing board only face to face.</summary>
        public static bool CanReview(OfficeWhere where, bool inPerson)
        {
            return ReviewWhere(where) == OfficeWhere.Phone || inPerson;
        }

        /// <summary>The key for a message sending the player to the office: the twin naming the address if there is one.</summary>
        public static string Key(string key, string address)
        {
            return Has(address) ? key + Suffix : key;
        }

        /// <summary>True when there is an address to name (the employer has a board in this city).</summary>
        public static bool Has(string address)
        {
            return !string.IsNullOrWhiteSpace(address);
        }

        /// <summary>Whether "Show me the office" is offered: only for an employer whose board is placed.</summary>
        public static bool ShowMeOffered(int board, int boards)
        {
            return board >= 0 && board < boards;
        }

        /// <summary>The employers that have a board, in catalogue order: the city map's Job offices row.</summary>
        public static void Offices(int[] depot, int boards, List<int> employers)
        {
            employers.Clear();
            for (int e = 0; e < depot.Length; e++)
            {
                if (ShowMeOffered(depot[e], boards))
                {
                    employers.Add(e);
                }
            }
        }

        /// <summary>
        /// The refusal that sends the player to the place a job is worked, from what the job or the street gave: the practice's desk, a works
        /// site, or an office trade's indoors anywhere but the practice (<paramref name="officeWork"/>). Null for every other refusal, which
        /// has nothing to do with where the player stands.
        /// </summary>
        public static string PlaceRefusal(string reasonKey, bool officeWork)
        {
            if (reasonKey == NoOfficeKey || reasonKey == NoSiteKey)
            {
                return reasonKey;
            }
            return officeWork && reasonKey == NoIndoorsKey ? NoOfficeKey : null;
        }

        /// <summary>
        /// The text of a place refusal for where the employer's work is now: the twin that names the address where a works site or the
        /// practice stands, the "none" twin where none does, so the player is never sent to a place the city has not got.
        /// </summary>
        public static string PlaceKey(string placeRefusal, OfficeWhere where)
        {
            return where == OfficeWhere.Works ? placeRefusal + Suffix : where == OfficeWhere.Phone ? placeRefusal + NoneSuffix : placeRefusal;
        }

        /// <summary>True for a refusal text that names where the work is done, so the way there can be offered with it (Show me the office).</summary>
        public static bool NamesPlace(string reasonKey)
        {
            return reasonKey == NoOfficeKey + Suffix || reasonKey == NoSiteKey + Suffix || reasonKey == AtDepotKey + Suffix || reasonKey == AtDepotKey + BoardSuffix;
        }

        /// <summary>Metres from the place within which the destination marker for Show me the office comes off, the player having come to it.</summary>
        public static float MarkReach(OfficeWhere where)
        {
            return where == OfficeWhere.Board ? BoardReach : WorksReach;
        }

        /// <summary>True when an employer's board is no longer one: placed (an index) but past the boards the city has. An employer with no board never is.</summary>
        public static bool BoardGone(int board, int boards)
        {
            return board >= 0 && board >= boards;
        }

        /// <summary>
        /// Whether the destination marker for Show me the office comes off now: its time is up, its board has gone, or the player is within
        /// <paramref name="reach"/> metres of the place (<paramref name="flatSqr"/> is the flat distance squared). An employer without a
        /// board (the building trades) has no board to lose: its marker stays up until the player comes to its place or the time is up.
        /// </summary>
        public static bool MarkOff(float now, float until, bool boardGone, float flatSqr, float reach)
        {
            return now >= until || boardGone || flatSqr < reach * reach;
        }

        /// <summary>The boss's welcome on being taken on: the practice's twin, which names its address and floor, where it stands; else the plain one.</summary>
        public static string WelcomeKey(string welcome, EmployerPlace place, OfficeWhere where)
        {
            return place == EmployerPlace.Practice && where == OfficeWhere.Works ? welcome + Suffix : welcome;
        }

        /// <summary>The Work contact's line on where the job is done: the board, the nearest works site or the practice; null where nothing stands (the phone is the place).</summary>
        public static string WhereKey(EmployerPlace place, OfficeWhere where)
        {
            if (where == OfficeWhere.Board)
            {
                return "reallife_career_where_board";
            }
            if (where == OfficeWhere.Works)
            {
                return place == EmployerPlace.Practice ? "reallife_career_where_practice" : "reallife_career_where_sites";
            }
            return null;
        }

        /// <summary>The Career app's line for an employer without a board: its practice's address, its nearest works site's, or that nothing stands for it.</summary>
        public static string WorksListKey(EmployerPlace place, OfficeWhere where)
        {
            if (where != OfficeWhere.Works)
            {
                return "reallife_career_works_none";
            }
            return place == EmployerPlace.Practice ? "reallife_career_works_practice" : "reallife_career_works_sites";
        }
    }
}
