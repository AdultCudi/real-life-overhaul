using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.Economy;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// The only file of parties and company events that touches the game: the world the rules ask (<see cref="IPartyWorld"/>),
    /// answered from the game's clock and money, the social life's cast, the player's home and businesses and the venues the
    /// social life meets people at. Money moves only through BizBooks.Book. Every answer is asked on a booking, a press, the
    /// director's look once a second or the day's round; nothing a frame. A venue's door is looked up among the city's
    /// buildings once a party and kept while that party is the one in hand.
    /// </summary>
    internal sealed class PartyGame : IPartyWorld
    {
        public static IModLogger Logger;

        private const float DoorReach = 25f;        // metres from a venue's door, on the flat, at which the host is there
        private const float VenueReach = 2500f;     // metres from the player within which a venue is looked for: a little past what the social life agrees an outing at
        private const string Residential = "ba:buildingtype_residential";   // the game's building type of a home (as HomeTidy reads it)

        private string _doorStreet;
        private int _doorNumber;
        private Vector3 _door;
        private bool _doorFound;

        /// <summary>Told when a notice went out, so the page is written again.</summary>
        public Action Changed;

        /// <summary>What a click on a notice opens.</summary>
        public Action Open;

        public bool On => Settings.LivingPopulation && Settings.LivingParties;

        public bool Guest => GoingPublic.IsGuest;

        public int Now => TimeHelper.CurrentDay * 1440 + TimeHelper.CurrentHour * 60 + Mathf.Clamp((int)TimeHelper.CurrentMinute, 0, 59);

        public int Day => TimeHelper.CurrentDay;

        public int Level => Prices.Level;

        public long Money => SaveGameManager.Current != null ? (long)Mathf.Floor(SaveGameManager.Current.Money) : 0L;

        public long Floor
        {
            get
            {
                try
                {
                    Func<long> floor = PartyBridges.Floor;
                    return floor != null ? Math.Max(0L, floor()) : 0L;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    return 0L;
                }
            }
        }

        public bool Pay(int amount, string key, string place)
        {
            if (amount <= 0)
            {
                return true;
            }
            Address address = null;
            if (place != null)
            {
                BuildingRegistration reg = BusinessGame.Held(place);
                if (reg == null)
                {
                    return false;
                }
                address = reg.Address;
            }
            if (Money < amount)
            {
                return false;
            }
            return BizBooks.Book(-amount, new TransactionInfo(key), null, address, force: false, showNotification: false);
        }

        public void Refund(int amount, string key, string place)
        {
            if (amount <= 0)
            {
                return;
            }
            BuildingRegistration reg = place != null ? BusinessGame.Held(place) : null;
            BizBooks.Book(amount, new TransactionInfo(key), null, reg != null ? reg.Address : null, force: true);
        }

        public SocialPerson Person(int index) => Settings.Social ? SocialState.Get(index) : null;

        public int People => Settings.Social && SocialState.Loaded ? SocialState.Count : 0;

        public void Change(SocialPerson person, float familiarity, float affection, float trust) => SocialLife.Change(person, familiarity, affection, trust);

        public void Remember(SocialPerson person, MemoryCode code) => SocialLife.Remember(person, code);

        public PlaceFacts Place(PartyPlace place, string street, int number, int start, int minutes)
        {
            var facts = new PlaceFacts { District = -1 };
            try
            {
                if (place == PartyPlace.Home)
                {
                    BuildingRegistration home = HomeAt(street, number);
                    if (home != null)
                    {
                        facts.Found = facts.Open = true;
                        facts.Street = home.StreetName;
                        facts.Number = home.StreetNumber;
                        facts.Name = PartyText.T("reallife_party_place_home_name");
                        facts.Sqm = BuildingHelper.GetBuildingSquareMeters(home.Address);
                        facts.District = DistrictOf(GameRegistrations.District(home));
                    }
                    return facts;
                }
                if (!string.IsNullOrEmpty(street) && SocialPlaces.Resolve(street, number, out Venue venue) && venue.Building != null && Suits(venue, place))
                {
                    Fill(ref facts, venue, start, minutes);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                facts.Found = false;
            }
            return facts;
        }

        public PlaceFacts FindVenue(PartyPlace place, int start, int minutes)
        {
            var facts = new PlaceFacts { District = -1 };
            try
            {
                if (place != PartyPlace.Home && SocialPlaces.Find(PartyCatalogue.KindOf(place), PlayerHelper.GetCityPosition(), 0f, VenueReach, out Venue venue, null, start)
                    && venue.Building != null)
                {
                    Fill(ref facts, venue, start, minutes);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                facts.Found = false;
            }
            return facts;
        }

        private static void Fill(ref PlaceFacts facts, Venue venue, int start, int minutes)
        {
            BuildingRegistration reg = venue.Building.buildingRegistration;
            facts.Found = true;
            facts.Street = venue.Street;
            facts.Number = venue.Number;
            facts.Name = venue.Name;
            facts.Own = reg != null && GoingPublic.Mine(reg);
            facts.Open = OpenThrough(reg, start, minutes);
            facts.Sqm = venue.Building.building != null ? BuildingHelper.GetBuildingSquareMeters(venue.Building.building.Address) : 0;
            facts.District = DistrictOf(GameRegistrations.District(reg));
        }

        // A venue is still one of its kind: the business there may have changed since the party was booked.
        private static bool Suits(Venue venue, PartyPlace place)
        {
            BuildingRegistration reg = venue.Building.buildingRegistration;
            string[] match = SocialCatalogue.Outing(PartyCatalogue.KindOf(place)).Match;
            string type = reg != null ? reg.businessTypeName : null;
            for (int i = 0; match != null && type != null && i < match.Length; i++)
            {
                if (type.IndexOf(match[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Open for every hour a party covers, by the business's own week of hours, and not closed by its owner or the city.
        private static bool OpenThrough(BuildingRegistration reg, int start, int minutes)
        {
            if (reg == null || reg.scheduleDays == null || reg.scheduleDays.Count == 0)
            {
                return true;
            }
            return !reg.temporarilyClosed && OpenHours(reg, start, minutes) == HoursCovered(start, minutes);
        }

        private static int HoursCovered(int start, int minutes) => (start + Math.Max(1, minutes) - 1) / 60 - start / 60 + 1;

        // The hours a stretch of time covers that fall in a business's opening hours.
        private static int OpenHours(BuildingRegistration reg, int start, int minutes)
        {
            int open = 0, first = start / 60, last = (start + Math.Max(1, minutes) - 1) / 60;
            for (int h = first; h <= last; h++)
            {
                DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(Mathf.Max(1, h / 24));
                for (int i = 0; i < reg.scheduleDays.Count; i++)
                {
                    ScheduleDay day = reg.scheduleDays[i];
                    if (day != null && day.day == weekday)
                    {
                        open += day.isOpen && day.IsOpenAtHour(h % 24) ? 1 : 0;
                        break;
                    }
                }
            }
            return open;
        }

        private static int DistrictOf(string neighbourhood)
        {
            List<string> all = NeighborhoodHelper.Neighborhoods;
            return all != null && neighbourhood != null ? all.IndexOf(neighbourhood) : -1;
        }

        // The home a party is booked at, by its address; with none given, the home the player lives in.
        private static BuildingRegistration HomeAt(string street, int number)
        {
            BuildingRegistration home = HomeTidy.Home();
            if (string.IsNullOrEmpty(street) || (home != null && home.StreetName == street && home.StreetNumber == number))
            {
                return home;
            }
            BuildingRegistration reg = BusinessGame.Held(BusinessState.PlaceKey(street, number));
            return reg != null && GameRegistrations.BuildingType(reg) == Residential ? reg : null;
        }

        public HomeFacts Home()
        {
            BuildingRegistration home = HomeTidy.Home();
            return new HomeFacts { Tidy = home != null ? Mathf.RoundToInt(HomeTidy.ReadOf(home)) : -1, Dark = HomeTidy.Dark(home), Dry = HomeTidy.Dry(home) };
        }

        public bool There(PartyPlace place, string street, int number)
        {
            try
            {
                if (BuildingManager.IsInsideBuilding)
                {
                    BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                    Address at = manager != null && manager.building != null ? manager.building.Address : null;
                    return at != null && at.streetName == street && at.streetNumber == number && (place != PartyPlace.Home || HomeTidy.AtHome());
                }
                if (place == PartyPlace.Home)
                {
                    return false;   // the host of a party at home is in it
                }
                if (_doorStreet != street || _doorNumber != number)
                {
                    _doorStreet = street;
                    _doorNumber = number;
                    _doorFound = SocialPlaces.Resolve(street, number, out Venue venue);
                    _door = _doorFound ? venue.Door : Vector3.zero;
                }
                Vector3 player = PlayerHelper.GetCityPosition();
                float dx = player.x - _door.x, dz = player.z - _door.z;
                return _doorFound && dx * dx + dz * dz <= DoorReach * DoorReach;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        public void Mess(int points)
        {
            BuildingRegistration home = HomeTidy.Home();
            if (home != null && points > 0)
            {
                HomeTidy.Change(home, -points);
            }
        }

        public int Staff(string place, List<StaffHand> into)
        {
            into.Clear();
            BuildingRegistration reg = place != null ? BusinessGame.Held(place) : null;
            if (reg == null || !BusinessGame.IsBusiness(reg))
            {
                return -1;
            }
            int level = PartyBridges.StaffLevel != null ? PartyBridges.StaffLevel() : 1;
            float gap = CompanyCatalogue.RaiseGap[CompanyCatalogue.Level(level)] / 1000f;
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                StaffFile file = File(e.id);
                into.Add(new StaffHand
                {
                    Id = e.id, Name = BusinessGame.NameOf(e), Mood = Mathf.Clamp(Mathf.RoundToInt(e.satisfaction), 0, 100), Sick = e.isAbsent, File = file,
                    Underpaid = file != null && file.Going > 0 ? StaffRules.Underpaid(file, level) : BusinessGame.WageGap(e) >= gap
                });
            }
            return into.Count;
        }

        public void Mood(string id, float points)
        {
            EmployeeInstance e = BusinessGame.Employee(id);
            if (e != null && points > 0f)
            {
                BusinessGame.Mood(e, points);
            }
        }

        public StaffFile File(string id)
        {
            try
            {
                Func<string, StaffFile> file = PartyBridges.StaffFile;
                return file != null && !string.IsNullOrEmpty(id) ? file(id) : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        public int Shut(string place, int day, int hour, int minutes)
        {
            BuildingRegistration reg = BusinessGame.Held(place);
            if (reg == null || reg.scheduleDays == null || reg.scheduleDays.Count == 0)
            {
                return 0;
            }
            int start = PartyRules.Stamp(day, hour), open = OpenHours(reg, start, minutes);
            if (open == 0 || reg.temporarilyClosed)
            {
                return open;   // outside its hours; or shut already, by its owner or the city, so the staff are free for it
            }
            int hours = Mathf.Max(1, Mathf.CeilToInt((start + minutes - Now) / 60f));
            try
            {
                Func<BuildingRegistration, int, bool> close = PartyBridges.Close;
                return close != null && close(reg, hours) ? open : -1;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return -1;
            }
        }

        public bool Manager(string place)
        {
            try
            {
                Func<string, bool> manager = PartyBridges.Manager;
                return manager != null && manager(place);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        public string Business(string place)
        {
            BuildingRegistration reg = place != null ? BusinessGame.Held(place) : null;
            return reg != null ? BusinessGame.Name(reg) : string.Empty;
        }

        public string Why(PartyRefusal why) => PartyText.T(PartyText.RefusalKey(why));

        public void Tell(PartyTone tone, string key, params string[] pairs)
        {
            try
            {
                NotificationType type = tone == PartyTone.Good ? NotificationType.Success : tone == PartyTone.Warn ? NotificationType.Warning
                    : tone == PartyTone.Bad ? NotificationType.Error : NotificationType.Info;
                Notifications.Show(type, key, Data(pairs), 11f, null, Open != null ? Guard.Wrap(Open) : null, true, false);
                Logger?.Info("Parties: " + PartyText.Fill(key, pairs));
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public bool Text(SocialPerson person, string key, params string[] pairs)
        {
            return SocialLife.SendText(person, key, Data(pairs));
        }

        public string Cash(int amount) => ((float)amount).ToCurrencyFormat();

        public string When(int day, int hour) => PartyText.When(day, hour, TimeHelper.CurrentDay);

        // A dictionary each: the game keeps a notice's and a message's data.
        private static Dictionary<string, string> Data(string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs != null ? pairs.Length / 2 : 0);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        /// <summary>The player's businesses as places, in the save's own order, for the page: nothing is kept between asks.</summary>
        public static void Businesses(List<string> places)
        {
            places.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (BusinessGame.IsBusiness(all[i]))
                {
                    places.Add(BusinessGame.Place(all[i]));
                }
            }
        }

        /// <summary>
        /// The time passes to a party's end with the game's own time skip, as an outing's time inside does. Not in a Going
        /// Public session, where the clock is everyone's: the party then runs in the session's own time. False if no skip began.
        /// </summary>
        public static bool Skip(int untilMinute, string captionKey)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (GoingPublic.ClockShared || uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || PlayerActivity.PlayerActivityUI.IsPanelOpen)
            {
                return false;
            }
            int now = TimeHelper.CurrentDay * 1440 + TimeHelper.CurrentHour * 60 + Mathf.Clamp((int)TimeHelper.CurrentMinute, 0, 59);
            if (untilMinute <= now)
            {
                return false;
            }
            uis.timeMachine.StartTimeMachine(new Timestamp(untilMinute / 1440, untilMinute % 1440 / 60, untilMinute % 60), false, captionKey, true, false);
            return true;
        }
    }
}
