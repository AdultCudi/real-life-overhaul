using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using RealLifeOverhaul.Economy;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What Flare asks of the rest of the mod. Each is optional and has a default, so Social/Flare works alone (the
    /// account, the player's own posts and followers); Scripts/Wiring/FlareLinks.cs fills them in. Flare never calls
    /// another feature directly, and the things that happen elsewhere and become drafts (a picture saved, an outing,
    /// a good deed, an arrest) are told to it from there (<see cref="FlareDirector.Happened"/>).
    /// </summary>
    public static class FlareBridges
    {
        /// <summary>The people the player has met in the social life, as Flare reads them, into the list given. Default: nobody.</summary>
        public static Action<List<FlarePerson>> People;

        /// <summary>What the city thinks of the player, 0 to 100 (the law's public reputation). Default: 50.</summary>
        public static Func<int> PublicStanding;

        /// <summary>The election on now: bit 1 the player has voted in it, bit 2 has endorsed in it, with the candidates' names. Default: no election.</summary>
        public static CivicAsk Civic;

        public delegate int CivicAsk(out string voted, out string endorsed);

        /// <summary>The player's employer and rank now (employer * 100 + rank, -1 out of work) and the employer's name. Default: out of work.</summary>
        public static CareerAsk Career;

        public delegate int CareerAsk(out string employer);

        public static void Reset()
        {
            People = null;
            PublicStanding = null;
            Civic = null;
            Career = null;
        }

        internal static int Standing()
        {
            try
            {
                return PublicStanding != null ? PublicStanding() : FlareRules.ReputationStart;
            }
            catch (Exception)
            {
                return FlareRules.ReputationStart;
            }
        }
    }

    /// <summary>
    /// The one place Flare touches the game: the clock, the player's businesses and their takings from the game's own
    /// daily statements (through Economy/Finance/Books, the mod's reader of them), and which of them the player is
    /// standing in. Asked on the hour, the day, a saved picture and a click; never per frame.
    /// </summary>
    public static class FlareGame
    {
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;
        public static int MinuteOfDay => SaveGameManager.Current != null ? SaveGameManager.Current.Hour * 60 + Mathf.FloorToInt(SaveGameManager.Current.Minute) : 0;

        /// <summary>A business's place as the company and the brands write it ("street~number").</summary>
        public static string Place(BuildingRegistration reg)
        {
            return reg == null ? null : Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Name(BuildingRegistration reg)
        {
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";
        }

        /// <summary>The player's businesses that take money, into a list of Flare's own (filled again on each call).</summary>
        public static List<BuildingRegistration> Businesses()
        {
            Books.Businesses(Regs);
            return Regs;
        }

        /// <summary>The player's business at a place, or null.</summary>
        public static BuildingRegistration Business(string place)
        {
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            List<BuildingRegistration> all = Businesses();
            for (int i = 0; i < all.Count; i++)
            {
                if (Place(all[i]) == place)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>What a business took yesterday, from the game's statement of that day; nothing if it has none.</summary>
        public static int Takings(BuildingRegistration reg)
        {
            return reg != null ? Mathf.RoundToInt(Books.SalesYesterday(reg.Address)) : 0;
        }

        /// <summary>The business of the player's they are standing in, or null.</summary>
        public static BuildingRegistration Inside()
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance == null)
            {
                return null;
            }
            BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            return here != null && Books.IsBusiness(here) ? here : null;
        }
    }
}
