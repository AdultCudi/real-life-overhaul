using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the flats ask of the game (<see cref="FlatRules"/>): the residential buildings the player does not own or rent, as the listing rule
    /// wants them, the building's registration rented by the player at no rent when a flat is bought (as a rented apartment is the player's home),
    /// and that rent put to the market's when the flat is sold. Nothing of it is saved, and nothing runs in a Going Public session.
    /// </summary>
    internal static partial class EstateGame
    {
        public const string FlatBuyKey = "reallife_transaction_flat_buy";          // the deposit, or the whole price in cash, of a flat
        public const string FlatSoldKey = "reallife_transaction_flat_sold";        // what a flat sold brought in
        public const string FlatFoldKey = "reallife_transaction_flat_fold";        // a flat folded into a building bought whole: its share of the price given back
        public const string FlatServiceKey = "reallife_transaction_flat_service";  // a month's service charge to the building's owner
        public const string FlatTaxKey = "reallife_transaction_flat_tax";          // the unit's share of the building's property tax for a month
        public const string FlatRentKey = "reallife_transaction_flat_rent";        // a tenant's rent for a flat the player lets

        private static readonly Dictionary<(string, int), BuildingRegistration> FlatRegs = new Dictionary<(string, int), BuildingRegistration>(2048);

        /// <summary>The game's property tax on a building for a year (RealEstate.TaxesAmount's own arithmetic); 0 for a building it has no data for.</summary>
        private static int TaxYearOf(Building b)
        {
            try
            {
                float tax = b.totalSqm * BuildingTypeHelper.GetData(b.BuildingType).buildingPriceMultiplier * NeighborhoodHelper.GetData(b.Neighbourhood).realEstateMultiplier * 3.5f;
                return Mathf.RoundToInt(Mathf.Clamp(tax, 0f, int.MaxValue / 4f));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// The residential buildings the flats' rule may split: not the Hamptons' houses, not the player's (owned or rented) and none in a
        /// session. One walk of the city and one of the save's registrations, when a listing is rebuilt: never per frame.
        /// </summary>
        public static void ReadFlatBuildings(EstateState s, int index, List<FlatBuilding> into)
        {
            into.Clear();
            GameInstance save = Save;
            if (save == null || GoingPublic.InSession)
            {
                return;
            }
            FlatRegs.Clear();
            List<BuildingRegistration> regs = save.BuildingRegistrations;
            for (int i = 0; regs != null && i < regs.Count; i++)
            {
                BuildingRegistration r = regs[i];
                if (r != null && !string.IsNullOrEmpty(r.StreetName))
                {
                    FlatRegs[(r.StreetName, r.StreetNumber)] = r;
                }
            }
            for (int i = 0; i < City.Count; i++)
            {
                CityBuilding c = City[i];
                if (c.B == null || c.Holding != null || c.Units < FlatCatalogue.MinUnits || UseOf(c.B.BuildingType) != (int)EstateUse.Homes || c.B.IsHamptonsHouse())
                {
                    continue;
                }
                BuildingRegistration reg;
                FlatRegs.TryGetValue((c.B.StreetName, c.B.StreetNumber), out reg);
                if (reg != null && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer))
                {
                    continue;
                }
                into.Add(new FlatBuilding
                {
                    Place = c.Place, Name = c.B.Address.ToFormattedString(), District = c.District, Value = Value(c, s, index), Cond = EstateRules.StreetCondition(s.Seed, c.Key),
                    Income = Income(c.District), Units = c.Units, Sqm = c.Sqm, TaxYear = c.TaxYear,
                    Landlord = reg != null && !string.IsNullOrEmpty(reg.buildingOwnerRivalId) ? RivalName(reg.buildingOwnerRivalId) : string.Empty
                });
            }
            FlatRegs.Clear();
        }

        /// <summary>A building's facts for a flat the player holds, read again from the city each morning; false while the city does not have it.</summary>
        public static bool ReadFlat(OwnedFlat f, EstateState s)
        {
            CityBuilding c = Find(f.Place);
            if (c == null || c.B == null)
            {
                f.Seen = false;
                return false;
            }
            f.Seen = true;
            f.Name = c.B.Address.ToFormattedString();
            f.District = c.District;
            f.TaxYear = c.TaxYear;
            f.Hold.District = c.District;
            f.Hold.Name = f.Name;
            int[] w = FlatRules.Weights(f.Place, f.Units, f.Hold.Income);
            f.Sqm = FlatRules.Sqm(c.Sqm, w, f.Slot);
            f.Share = FlatRules.Share(w, f.Slot);
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(c.B.Address);
            f.Landlord = reg != null && !reg.BuildingOwnedByPlayer && !string.IsNullOrEmpty(reg.buildingOwnerRivalId) ? RivalName(reg.buildingOwnerRivalId) : string.Empty;
            return true;
        }

        /// <summary>The key a mortgage on a whole building the player owns is secured by (the game's address, as the credit desk keeps it).</summary>
        public static string BuildingKey(string place)
        {
            Address address = AddressOf(place);
            return address != null ? Books.Key(address) : string.Empty;
        }

        /// <summary>The building's value now (the market, its district, its condition), or 0 where the city does not have it.</summary>
        public static int BuildingValue(string place, EstateState s, int index)
        {
            CityBuilding c = Find(place);
            return c != null ? Value(c, s, index) : 0;
        }

        /// <summary>The condition a building the player does not own stands at, from the seed and its address.</summary>
        public static int BuildingCondition(string place, EstateState s)
        {
            CityBuilding c = Find(place);
            return c != null ? EstateRules.StreetCondition(s.Seed, c.Key) : EstateCatalogue.ConditionNew;
        }

        public static int BuildingIncome(string place)
        {
            CityBuilding c = Find(place);
            return c != null ? Income(c.District) : EstateCatalogue.IncomeMiddle;
        }

        /// <summary>Whether the player owns the building whole now (the game's own record).</summary>
        public static bool OwnsBuilding(string place)
        {
            return Estate(place) != null;
        }

        /// <summary>What the game was paid for the building the player owns, 0 for none: the price of the whole building a flat of it folds into.</summary>
        public static int PurchasePrice(string place)
        {
            RealEstate e = Estate(place);
            return e != null ? (int)Math.Min(int.MaxValue / 4, Math.Max(0.0, e.purchasePrice)) : 0;
        }

        /// <summary>
        /// The flat becomes the player's home as a rented apartment does (the game's own RentBuilding, as its console rents one): the building's
        /// registration is the player's at no rent, so the home needs, the household and the bills read it as any home. False, with nothing
        /// done, if the building is rented or owned already or is not there.
        /// </summary>
        public static bool TakeHome(string place)
        {
            Address address = AddressOf(place);
            Building b = address != null ? BuildingHelper.GetBuilding(address) : null;
            if (b == null || GoingPublic.InSession)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(address);
            if (reg == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer)
            {
                return false;
            }
            BuildingHelper.RentBuilding(b, 0f, 0f);
            return true;
        }

        /// <summary>
        /// A flat is sold: a player still living in it pays the market's rent for it from now, as the game leaves a unit a player still uses
        /// when it sells the building (<see cref="Release"/>); nothing that is inside is taken. False if the building is not there.
        /// </summary>
        public static bool FlatLeft(string place)
        {
            Address address = AddressOf(place);
            Building b = address != null ? BuildingHelper.GetBuilding(address) : null;
            BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            if (b == null || reg == null)
            {
                return false;
            }
            if (reg.RentedByPlayer && !reg.BuildingOwnedByPlayer)
            {
                reg.RentPerDay = b.GetBuildingDailyMarketRent();
            }
            return true;
        }
    }
}
