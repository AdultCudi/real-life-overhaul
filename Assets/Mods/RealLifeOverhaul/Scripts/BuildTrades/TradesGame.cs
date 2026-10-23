using System;
using Entities;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>The building trades' own calls into the game that no other part of the mod makes for them: an address as the game writes it. No cost of its own.</summary>
    internal static class TradesGame
    {
        /// <summary>An address as the game writes it ("12 Fifth Avenue"); the plain number and street if the game's helper fails.</summary>
        public static string Address(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return null;
            }
            try
            {
                return Streets.AddressHelper.ToFormattedString(new Address(street, number));
            }
            catch (Exception)
            {
                return number + " " + street;
            }
        }
    }
}
