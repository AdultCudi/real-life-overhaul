using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Rules for telling the player where an employer's office (its help-wanted board) is, checked offline by
    /// CatalogueCheck's JobOfficeCheck. A text that sends the player there has a twin, its key plus "_at", that names the
    /// address; the plain one is for an employer with no board in this city, reached by phone.
    /// </summary>
    public static class CareerOfficeRules
    {
        public const string Suffix = "_at";         // the twin of a text that names the office's address
        public const string AddressField = "address";   // the placeholder the twin carries

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
