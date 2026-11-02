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
    }
}
