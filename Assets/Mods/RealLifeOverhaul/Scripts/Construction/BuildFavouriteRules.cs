using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The rules of the starred designs list in "Choose a building": design ids joined by "|", the newest last, the oldest dropped past <see cref="Most"/>. Game-free
    /// (EconomyCheck's --buildmode-ui); BuildFavourites keeps the list on this computer.
    /// </summary>
    public static class BuildFavouriteRules
    {
        public const int Most = 48;   // stars kept; the oldest goes when another is added

        /// <summary>True when the id is on a list.</summary>
        public static bool Has(string list, string id)
        {
            if (string.IsNullOrEmpty(list) || string.IsNullOrEmpty(id))
            {
                return false;
            }
            foreach (string one in list.Split('|'))
            {
                if (string.Equals(one, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The list with the id put on, or taken off when it is on; an id that would break the list is refused.</summary>
        public static string Toggled(string list, string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0)
            {
                return list ?? string.Empty;
            }
            var ids = new List<string>(string.IsNullOrEmpty(list) ? new string[0] : list.Split('|'));
            if (!ids.Remove(id))
            {
                ids.Add(id);
                while (ids.Count > Most)
                {
                    ids.RemoveAt(0);
                }
            }
            return string.Join("|", ids);
        }

        /// <summary>How many ids are on the list.</summary>
        public static int Count(string list) => string.IsNullOrEmpty(list) ? 0 : list.Split('|').Length;
    }
}
