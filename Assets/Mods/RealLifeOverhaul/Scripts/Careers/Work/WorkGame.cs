using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Rivals;
using Buildings;
using Helpers;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One employer of the city as the vacancies are made from it: a business, an office, a public building.</summary>
    public sealed class EmployerRef
    {
        public string Name = string.Empty, Street = string.Empty, Type = string.Empty, Rival = string.Empty;
        public int Number;
        public EmployerKind Kind;
        public long Professions;   // the professions its type employs (ProCatalogue.EmployedBy), read once a day
    }

    /// <summary>What the working life reads of the city: its employers, a rival's name and whether the player is at work; lists made once a game day.</summary>
    internal static class WorkGame
    {
        private const string Office = "ba:buildingtype_office";
        private const float Reach = 35f;   // metres from the door that count as at work

        private static readonly List<EmployerRef> Public = new List<EmployerRef>(8);
        private static CityManager _publicOf;
        private static readonly Dictionary<string, Vector3> Doors = new Dictionary<string, Vector3>(8);

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The city's businesses that employ a profession, not the player's own: one pass over the save's buildings; the same objects refilled each day.</summary>
        public static void Employers(List<EmployerRef> into, List<EmployerRef> offices)
        {
            into.Clear();
            offices.Clear();
            _made = 0;
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.HasValidAddress || reg.RentedByPlayer || !GameRegistrations.Found(reg))
                {
                    continue;   // (the player's own, and in a session another player's, which Going Public shows as rented by the player: never an employer)
                }
                string type = reg.businessTypeName ?? string.Empty;
                bool office = SafeType(reg) == Office;
                if (office && offices.Count < 64)
                {
                    offices.Add(Ref(reg, EmployerKind.TechFirm));
                }
                if (!reg.HasEstablishedBusiness || type.Length == 0 || type == "ba:businesstype_empty" || !ProCatalogue.AnyEmploys(type))
                {
                    continue;
                }
                into.Add(Ref(reg, string.IsNullOrEmpty(reg.businessOwnerRivalId) ? EmployerKind.City : EmployerKind.Rival));
            }
        }

        private static string SafeType(BuildingRegistration reg)
        {
            try
            {
                return GameRegistrations.BuildingType(reg) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static readonly List<EmployerRef> Made = new List<EmployerRef>(256);   // the employers of the last call, used again by the next
        private static int _made;

        private static EmployerRef Ref(BuildingRegistration reg, EmployerKind kind)
        {
            if (_made == Made.Count)
            {
                Made.Add(new EmployerRef());
            }
            EmployerRef e = Made[_made++];
            e.Name = TalentGame.NameOf(reg);
            e.Street = reg.StreetName ?? string.Empty;
            e.Number = reg.StreetNumber;
            e.Type = reg.businessTypeName ?? string.Empty;
            e.Rival = kind == EmployerKind.Rival ? reg.businessOwnerRivalId ?? string.Empty : string.Empty;
            e.Kind = kind;
            e.Professions = ProCatalogue.EmployedBy(e.Type);
            return e;
        }

        /// <summary>The public sector's buildings: each hospital and clinic, the school and the council's offices. One walk over the city's buildings, once a city.</summary>
        public static IReadOnlyList<EmployerRef> PublicPlaces()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (ReferenceEquals(city, _publicOf))
            {
                return Public;
            }
            _publicOf = city;
            Public.Clear();
            Doors.Clear();
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            for (int i = 0; all != null && i < all.Length && Public.Count < 8; i++)
            {
                Building b = all[i] != null ? all[i].building : null;
                string type = b != null && b.SpecialService != null ? b.SpecialService.businessTypeName : null;
                if (type == ProCatalogue.HospitalType || type == ProCatalogue.ClinicType || type == ProCatalogue.SchoolType || type == ProCatalogue.CityHallType)
                {
                    Public.Add(new EmployerRef { Street = b.Address.streetName, Number = b.Address.streetNumber, Type = type, Kind = EmployerKind.Public });
                }
            }
            return Public;
        }

        /// <summary>The public building a profession works at (one of them, by the roll), or null.</summary>
        public static EmployerRef PublicFor(Profession p, int roll)
        {
            IReadOnlyList<EmployerRef> places = PublicPlaces();
            int count = 0;
            for (int i = 0; i < places.Count; i++)
            {
                count += Fits(p, places[i].Type) ? 1 : 0;
            }
            if (count == 0)
            {
                return null;
            }
            int pick = (roll & int.MaxValue) % count;
            for (int i = 0; i < places.Count; i++)
            {
                if (Fits(p, places[i].Type) && pick-- == 0)
                {
                    return places[i];
                }
            }
            return null;
        }

        private static bool Fits(Profession p, string type)
        {
            switch (p.Place)
            {
                case Workplace.Hospital: return type == ProCatalogue.HospitalType || type == ProCatalogue.ClinicType;
                case Workplace.School: return type == ProCatalogue.SchoolType;
                case Workplace.CityHall: return type == ProCatalogue.CityHallType;
                default: return false;
            }
        }

        public static string RivalName(string id)
        {
            try
            {
                RivalData r = string.IsNullOrEmpty(id) ? null : RivalsHelper.GetRivalData(id);
                return r != null && !string.IsNullOrEmpty(r.rivalName) ? r.rivalName : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A rival's takings over the last week, whole money (the game's own figure); 0 if it cannot be told.</summary>
        public static float RivalWeekly(string id)
        {
            try
            {
                RivalData r = string.IsNullOrEmpty(id) ? null : RivalsHelper.GetRivalData(id);
                return r != null && r.ownedRetailOfficeBusinesses != null ? Mathf.Max(0f, r.WeeklyIncome) : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>The address the game writes for a place.</summary>
        public static string Address(string street, int number)
        {
            return CivicCity.AddressText(street, number);
        }

        /// <summary>
        /// The week's opening hours of the place at an address, into seven masks, Monday first (OpenHoursRules): the game's own
        /// schedule, which its door keeps (BuildingHelper.CanEnterBuilding). False where the door is not the schedule's: no
        /// address, a building the mod holds (walked into as the mod's own), or no hours kept for it. Asked once a day for each post.
        /// </summary>
        public static bool OpenWeek(string street, int number, int[] into)
        {
            for (int i = 0; i < 7; i++)
            {
                into[i] = 0;
            }
            try
            {
                BuildingRegistration reg = Registration(street, number);
                List<ScheduleDay> days = reg != null ? reg.scheduleDays : null;
                for (int w = 0; days != null && w < 7; w++)
                {
                    DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(w + 1);   // day 1 of a game is a Monday
                    for (int d = 0; d < days.Count; d++)
                    {
                        ScheduleDay s = days[d];
                        if (s == null || s.day != weekday || !s.isOpen || s.openingHourSlots == null)
                        {
                            continue;
                        }
                        for (int h = 0; h < 24; h++)
                        {
                            into[w] |= s.IsOpenAtHour(h) ? 1 << h : 0;
                        }
                    }
                }
            }
            catch (Exception)
            {
                for (int i = 0; i < 7; i++)
                {
                    into[i] = 0;   // a building the game has no data for: its hours are not known
                }
            }
            return OpenHoursRules.Known(into);
        }

        /// <summary>True if the game has the place at an address shut now, a temporary closing included, where its door keeps hours (<see cref="OpenWeek"/>).</summary>
        public static bool ShutNow(string street, int number)
        {
            try
            {
                BuildingRegistration reg = Registration(street, number);
                return reg != null && reg.scheduleDays != null && !BusinessHelper.IsBusinessOpen(reg);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The registration whose schedule a post's door keeps; null for none, or a building the mod holds.
        private static BuildingRegistration Registration(string street, int number)
        {
            if (string.IsNullOrEmpty(street) || SpecialBuildings.Filed(street, number))
            {
                return null;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(new Address(street, number));
            return reg != null && !reg.RentedByPlayer ? reg : null;
        }

        /// <summary>True if the player is at work: inside the post's building or by its door (any hospital or the school for a post without an address).</summary>
        public static bool AtWork(Profession p, Opening post)
        {
            try
            {
                if (string.IsNullOrEmpty(post.Street))
                {
                    return p.Place == Workplace.Hospital ? CareersGame.InHospital() : p.Place == Workplace.School && CareersGame.InSchool();
                }
                return CareersGame.InsideAt(post.Street, post.Number) || Near(post.Street, post.Number);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Near(string street, int number)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || BuildingManager.IsInsideBuilding || string.IsNullOrEmpty(street))
            {
                return false;
            }
            string key = street + "#" + number;
            Vector3 door;
            if (!Doors.TryGetValue(key, out door))
            {
                CityBuildingController building = CivicCity.Find(street, number);
                if (building == null || !JobSpots.Front(building, out door))
                {
                    door = new Vector3(float.NaN, 0f, 0f);
                }
                if (Doors.Count > 16)
                {
                    Doors.Clear();
                }
                Doors[key] = door;
            }
            if (float.IsNaN(door.x))
            {
                return false;
            }
            Vector3 off = door - player.transform.position;
            off.y = 0f;
            return off.sqrMagnitude <= Reach * Reach;
        }
    }
}
