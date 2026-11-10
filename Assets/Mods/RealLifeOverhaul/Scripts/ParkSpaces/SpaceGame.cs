using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Rivals;
using Buildings;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Parking.UndergroundParking;
using RealLifeOverhaul.Economy;
using Streets;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>What the parking spaces ask of the rest of the mod, each with a default; Wiring fills them in.</summary>
    public static class SpaceBridges
    {
        /// <summary>How many rival tycoons there are. Default: none.</summary>
        public static Func<int> RivalCount;
        /// <summary>A tycoon's worth while they are in play and the rivals are not resting; under nothing otherwise. Default: not in play.</summary>
        public static Func<int, long> RivalWorth;
        /// <summary>The worth a tycoon never spends below. Default: nothing.</summary>
        public static Func<int, long> RivalFloor;
        /// <summary>A sum added to (or, under nothing, taken from) a tycoon's worth. Default: nothing happens.</summary>
        public static Action<int, long> RivalMoney;
        public static Func<int, string> RivalName;

        public static void Reset()
        {
            RivalCount = null;
            RivalWorth = null;
            RivalFloor = null;
            RivalMoney = null;
            RivalName = null;
        }
    }

    /// <summary>
    /// The one file of the parking spaces that touches the game: the clock, the city's buildings with a car park under them, the
    /// player's cars standing in one, the player's cash through the mod's one booking call, and the notices. Nothing per frame: the
    /// buildings are walked at a load, once a day and when the page opens, the cars once a game hour.
    /// </summary>
    internal static class SpaceGame
    {
        public const string BuyKey = SpaceDay.BuyKey;
        public const string SoldKey = SpaceDay.SoldKey;
        public const string RentKey = SpaceDay.RentKey;
        public const string RefundKey = SpaceDay.RefundKey;
        public const string GoneKey = SpaceDay.GoneKey;
        private const string CarParkStreet = "ba:street_parking";   // the game's street of a car park's vehicles (UndergroundParkingEntrance.Address)

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null;

        public static int Day => Save != null ? Save.Day : 0;

        public static int Now => Save != null ? Save.Day * 24 + Save.Hour : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        /// <summary>The player's cash in whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Mathf.Max(0f, Mathf.Min(Save.Money, 1e12f))) : 0L;

        /// <summary>The property market's level in thousandths, where the property business runs; its long-run level otherwise.</summary>
        public static int Market
        {
            get
            {
                EstateDirector estate = EstateDirector.Instance;
                EstateState state = estate != null && estate.Running ? estate.State : null;
                return state != null ? state.Real : EstateCatalogue.RealStart;
            }
        }

        /// <summary>
        /// The city's buildings with a car park under them, the game's and the mod's own copies alike, into <paramref name="into"/>
        /// (cleared), each with its spaces, the city's share, today's rent and price and its owner. One walk of the city's list.
        /// </summary>
        public static void Parks(List<SpacePark> into)
        {
            into.Clear();
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            int year = DaysPerYear, market = Market;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                UndergroundParkingEntrance entrance = c != null ? c.undergroundParkingEntrance : null;
                Building b = c != null ? c.building : null;
                if (entrance == null || b == null || entrance.parkingNumber <= 0 || SpaceRules.Find(into, entrance.parkingNumber) != null)
                {
                    continue;
                }
                BuildingRegistration reg = b.GetRegistration();
                string district = b.Neighbourhood ?? string.Empty;
                int income = Mathf.RoundToInt(CityEconomy.DistrictIncome(district) * 10f);
                var p = new SpacePark
                {
                    Number = entrance.parkingNumber,
                    Name = reg != null && reg.Address != null ? reg.Address.ToFormattedString() : (b.StreetName ?? string.Empty).GetLocalization() + " " + b.StreetNumber,
                    District = string.IsNullOrEmpty(district) ? string.Empty : district.GetLocalization(),
                    Capacity = SpaceRules.Capacity(EstateGame.UseOf(b.BuildingType), b.totalSqm),
                    CityShare = SpaceRules.CityShare(StreetRate(district)),
                    RentMonth = SpaceRules.RentMonth(EstateRules.DistrictValue(income)),
                    Owner = OwnerOf(reg),
                    OwnerName = reg != null && !reg.BuildingOwnedByPlayer && !string.IsNullOrEmpty(reg.buildingOwnerRivalId) ? reg.buildingOwnerRivalId.GetRivalName() ?? string.Empty : string.Empty,
                    Source = b
                };
                p.Price = SpaceRules.Price(p.RentMonth, year, market);
                into.Add(p);
            }
            into.Sort(ByNumber);
        }

        private static readonly Comparison<SpacePark> ByNumber = (a, b) => a.Number.CompareTo(b.Number);

        private static SpaceOwner OwnerOf(BuildingRegistration reg)
        {
            return reg != null && reg.BuildingOwnedByPlayer ? SpaceOwner.Player : reg != null && !string.IsNullOrEmpty(reg.buildingOwnerRivalId) ? SpaceOwner.Rival : SpaceOwner.City;
        }

        /// <summary>
        /// Who owns the building of each car park where somebody holds a bought space, read again from the building itself (no walk of
        /// the city): a building the player has bought since the last look is seen within the second. True if one became the player's.
        /// </summary>
        public static bool OwnersChanged(IList<SpacePark> parks, SpaceState s)
        {
            bool became = false;
            for (int i = 0; i < s.Held.Count; i++)
            {
                SpaceHolding h = s.Held[i];
                SpacePark p = h.Rented ? null : SpaceRules.Find(parks, h.Park);
                var b = p != null ? p.Source as Building : null;
                if (b == null)
                {
                    continue;   // not there, or the game's building has gone
                }
                SpaceOwner now = OwnerOf(b.GetRegistration());
                became |= now == SpaceOwner.Player && p.Owner != SpaceOwner.Player;
                p.Owner = now;
            }
            return became;
        }

        // What an hour at the kerb costs in a district, as the game's data has it; nothing for a district it does not know.
        private static double StreetRate(string district)
        {
            if (string.IsNullOrEmpty(district))
            {
                return 0.0;
            }
            try
            {
                NeighborhoodData data = NeighborhoodHelper.GetData(district);
                return data != null ? data.parkingPrice : 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// The player's cars the game charges this hour for standing in a car park, by the game's own test (ParkingSimulator.RunHourly):
        /// parked legally at the car park's number, not the one being driven and not a thing carried by hand. Their vehicles' ids are
        /// added to <paramref name="into"/>, so what is paid back can be tied to what the game really takes.
        /// </summary>
        public static void CarsIn(int number, List<string> into)
        {
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleInstance v = all[i];
                if (v == null || string.IsNullOrEmpty(v.id) || v.parkingState != ParkingState.Legal || v.streetName != CarParkStreet || v.streetNumber != number || v.id == save.ActiveVehicleId)
                {
                    continue;
                }
                VehicleType type = v.VehicleType;
                if (type != null && !type.spawnInPlayerObject)
                {
                    into.Add(v.id);
                }
            }
        }

        /// <summary>True while a vehicle is in the save: one sold or scrapped (VehicleHelper.Delete) before the morning's charges were taken was never charged.</summary>
        public static bool HasVehicle(string id)
        {
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && id != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Money out of the player's cash, whole money, through the mod's one call; false, nothing taken, if the cash does not cover it.</summary>
        public static bool Pay(long amount, string key, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (amount > Cash || amount > int.MaxValue / 2)
            {
                return false;
            }
            return BizBooks.Book(-(float)amount, Info(key, name), null, null, force: false, showNotification: false);
        }

        /// <summary>Money into the player's cash, whole money: a space sold back, the game's charges paid back, a car park gone.</summary>
        public static void Receive(long amount, string key, string name)
        {
            if (amount > 0)
            {
                BizBooks.Book((float)Math.Min(amount, int.MaxValue / 2), Info(key, name), null, null, force: true);
            }
        }

        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
        }

        public static string Money(long amount) => ((float)amount).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 10f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
