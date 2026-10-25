using System;
using System.Collections.Generic;
using BA_Packages.Rivals.Scripts;
using Buildings.BuildingTypes.Shared;
using Helpers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What the management ladder reads of the game: the stores of the company the player's job is with and the game's own staff
    /// at each, and names in the game's own way. It only reads: nothing of the game's businesses, their staff or its simulation is
    /// written. A business any player holds (in a Going Public session another player's shows as rented by the player) is never
    /// one of the company's stores. One pass over the save's buildings when a company is seeded, a registration per store once a
    /// game day after.
    /// </summary>
    internal static class LadderGame
    {
        private static GameInstance Save => SaveGameManager.Current;
        private static readonly List<StoreSeed> Seeds = new List<StoreSeed>(CareerLadderRules.StoresMost);
        private static readonly List<BuildingRegistration> Found = new List<BuildingRegistration>(64);
        private static readonly HashSet<string> Ids = new HashSet<string>();

        /// <summary>
        /// The company of the job's workplace: its key (the rival's id, or the business's name and type for one of the city's
        /// chains), its name and its stores, the job's own first, then those of the same district, then by address. False if the
        /// workplace is not one of the city's businesses now.
        /// </summary>
        public static bool Read(Job j, out string key, out string name, out string rival, out List<StoreSeed> stores)
        {
            key = name = rival = string.Empty;
            stores = Seeds;
            Seeds.Clear();
            Found.Clear();
            BuildingRegistration mine = Registration(j.Post.Street, j.Post.Number);
            if (mine == null || !mine.HasEstablishedBusiness)
            {
                return false;
            }
            rival = mine.businessOwnerRivalId ?? string.Empty;
            string type = mine.businessTypeName ?? string.Empty;
            key = rival.Length > 0 ? "r:" + rival : "n:" + mine.BusinessName + "|" + type;
            name = rival.Length > 0 ? WorkGame.RivalName(rival) ?? mine.BusinessName : mine.BusinessName;
            string district = District(mine);
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || ReferenceEquals(reg, mine) || !Theirs(reg, rival, mine.BusinessName, type) || !GameRegistrations.Found(reg))
                {
                    continue;
                }
                Found.Add(reg);
            }
            // the same district first, then by street and number: the same order on every machine
            Found.Sort((a, b) =>
            {
                int da = District(a) == district ? 0 : 1, db = District(b) == district ? 0 : 1;
                if (da != db)
                {
                    return da - db;
                }
                int street = string.CompareOrdinal(a.StreetName, b.StreetName);
                return street != 0 ? street : a.StreetNumber - b.StreetNumber;
            });
            Seeds.Add(Seed(mine));
            for (int i = 0; i < Found.Count && Seeds.Count < CareerLadderRules.StoresMost; i++)
            {
                Seeds.Add(Seed(Found[i]));
            }
            Found.Clear();
            return true;
        }

        // A business of the same company: the same rival, or for the city's own the same name and type, and no player's.
        private static bool Theirs(BuildingRegistration reg, string rival, string name, string type)
        {
            if (!reg.HasValidAddress || !reg.HasEstablishedBusiness || reg.RentedByPlayer || SpecialBuildings.Filed(reg.StreetName, reg.StreetNumber))
            {
                return false;
            }
            string owner = reg.businessOwnerRivalId ?? string.Empty;
            return rival.Length > 0 ? owner == rival : owner.Length == 0 && reg.BusinessName == name && reg.businessTypeName == type;
        }

        private static StoreSeed Seed(BuildingRegistration reg)
        {
            var s = new StoreSeed { Street = reg.StreetName ?? string.Empty, Number = reg.StreetNumber, Name = TalentGame.NameOf(reg), District = District(reg) };
            List<AiBusinessEmployeeData> staff = reg.aiEmployees;
            for (int i = 0; staff != null && i < staff.Count && s.Staff.Count < CareerLadderRules.StaffMost; i++)
            {
                AiBusinessEmployeeData e = staff[i];
                if (e != null && !e.isPoached && !string.IsNullOrEmpty(e.id))
                {
                    s.Staff.Add(new StaffSeed { GameId = e.id, Name = SafeName(e), Skill = (int)Math.Round(e.primarySkillValue) });
                }
            }
            return s;
        }

        private static string SafeName(AiBusinessEmployeeData e)
        {
            try
            {
                return e.GetFullName();
            }
            catch (Exception)
            {
                return NameOf(TechRandom.Of(e.id));
            }
        }

        private static string District(BuildingRegistration reg)
        {
            return GameRegistrations.District(reg) ?? string.Empty;
        }

        // The registration at an address, if it is none of a player's.
        private static BuildingRegistration Registration(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return null;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(new Address(street, number));
            return reg != null && !reg.RentedByPlayer ? reg : null;
        }

        /// <summary>A person's name the game's own way (its first and last names, from a number): the same on every machine.</summary>
        public static string NameOf(int number)
        {
            try
            {
                return new Random(number).GetFullName();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>
        /// The company's stores against the save once a day: a store no longer the company's (shut, sold, taken over, or now a
        /// player's) is let go with its staff, and a member of the game's own staff the game no longer has there (the player
        /// poached them, say) leaves the store, a gap to fill. The player's own store is kept whatever happens, as the job is.
        /// Returns how many left.
        /// </summary>
        public static int Keep(LadderFile f)
        {
            CityCompany c = f.Company;
            int left = 0;
            string rival = c.Rival ?? string.Empty;
            for (int s = c.Stores.Count - 1; s >= 0; s--)
            {
                Outlet st = c.Stores[s];
                BuildingRegistration reg;
                try
                {
                    reg = BuildingHelper.GetBuildingRegistration(new Address(st.Street, st.Number));
                }
                catch (Exception)
                {
                    continue;
                }
                bool still = reg != null && reg.HasEstablishedBusiness && !reg.RentedByPlayer && reg.BusinessName == st.Name
                             && (rival.Length > 0 ? reg.businessOwnerRivalId == rival : string.IsNullOrEmpty(reg.businessOwnerRivalId));
                if (!still && s > 0)
                {
                    CareerLadderRules.DropStore(f, s);
                    continue;
                }
                if (reg == null)
                {
                    continue;
                }
                Ids.Clear();
                List<AiBusinessEmployeeData> staff = reg.aiEmployees;
                for (int i = 0; staff != null && i < staff.Count; i++)
                {
                    if (staff[i] != null && !string.IsNullOrEmpty(staff[i].id) && !staff[i].isPoached)
                    {
                        Ids.Add(staff[i].id);
                    }
                }
                left += CareerLadderRules.KeepStaff(f, s, Ids);
            }
            return left;
        }
    }
}
