using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Pure rules for telling the player where an employer's office is (the employer's help-wanted board): which text
    /// to use, what data it takes, whether the "Show me the office" choice is offered, and which employers have a
    /// board to list on the city map. No Unity or game types, so it is checked offline (CatalogueCheck/JobOfficeCheck).
    /// A text that sends the player to the office has a twin with the same key and "_at" that names the address; the
    /// plain one is kept for an employer with no board in this city, which is reached by phone.
    /// Cost: nothing per frame; each call runs once, on an event or a page of the phone.
    /// </summary>
    public static class CareerOfficeRules
    {
        public const string Suffix = "_at";         // the twin of a text that names the office's address
        public const string AddressField = "address";   // the placeholder the twin carries

        /// <summary>The text to use for a message that sends the player to the office: the twin that names the address if there is one, else the plain key.</summary>
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

        /// <summary>The employers that have a board, in catalogue order: what the city map's Job offices row lists, one marker each.</summary>
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
