using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Buildings;
using Entities;
using Helpers;
using Streets;
using UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The one place the city's rents are read from the game (shared: the info view of rents and build mode's Rents layer). The city's
    /// buildings as the estate keeps them give district, use, floor area and units; the save's registrations give who stands empty and
    /// who owns what (the game's rival-owned buildings); the save's list of buildings for sale gives the listings by use. Read in
    /// slices of a frame each, so a day's read never costs more than a few buildings' arithmetic at a time.
    /// </summary>
    internal static partial class EstateGame
    {
        /// <summary>A read of the city's rents under way: which part, where it stands and what it is filling.</summary>
        internal sealed class RentRead
        {
            public RentMapData Into;
            public int Seed, Phase, Cursor;
            public EstateClimate Climate;
            public bool Done => Phase >= 3;
        }

        internal static RentRead BeginRentRead(RentMapData into, int seed, EstateClimate c, int day)
        {
            into.Clear(day, c.DaysPerYear > 0 ? MacroCatalogue.MonthDays(c.DaysPerYear) : 30);
            return new RentRead { Into = into, Seed = seed, Climate = c };
        }

        /// <summary>One slice of the read: at most <paramref name="most"/> items. True when the whole read is done.</summary>
        internal static bool StepRentRead(RentRead r, int most)
        {
            GameInstance save = Save;
            if (r == null || r.Done)
            {
                return true;
            }
            int end;
            if (r.Phase == 0)
            {
                end = Math.Min(City.Count, r.Cursor + most);
                for (; r.Cursor < end; r.Cursor++)
                {
                    CityBuilding cb = City[r.Cursor];
                    if (cb == null || cb.B == null)
                    {
                        continue;
                    }
                    int use = UseOf(cb.B.BuildingType);
                    int sqm = Math.Max(0, cb.B.totalSqm);
                    int cond = cb.Holding != null ? cb.Holding.Cond : EstateRules.StreetCondition(r.Seed, cb.Key);
                    int rent = EstateRules.MarketRent(cb.Base, use, Income(cb.District), cond, cb.Holding != null ? cb.Holding.Level : 0, r.Climate);
                    r.Into.Add(cb.District, use, rent, sqm, Math.Max(1, cb.Units), 0);
                }
                if (r.Cursor >= City.Count)
                {
                    r.Phase = 1;
                    r.Cursor = 0;
                }
                return false;
            }
            if (r.Phase == 1)
            {
                List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
                end = all != null ? Math.Min(all.Count, r.Cursor + most) : 0;
                for (; all != null && r.Cursor < end; r.Cursor++)
                {
                    BuildingRegistration reg = all[r.Cursor];
                    CityBuilding cb = reg != null && !string.IsNullOrEmpty(reg.StreetName) ? FindAt(reg.StreetName, reg.StreetNumber) : null;
                    if (cb == null || cb.B == null)
                    {
                        continue;
                    }
                    int use = UseOf(cb.B.BuildingType);
                    bool empty = reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
                    if (empty)
                    {
                        r.Into.AddEmpty(cb.District, use, Math.Max(1, cb.Units));
                    }
                    if (!string.IsNullOrEmpty(reg.buildingOwnerRivalId))
                    {
                        int cond = EstateRules.StreetCondition(r.Seed, cb.Key);
                        int rent = EstateRules.MarketRent(cb.Base, use, Income(cb.District), cond, 0, r.Climate);
                        r.Into.AddLandlord(reg.buildingOwnerRivalId, use, rent, Math.Max(1, cb.Units));
                    }
                }
                if (all == null || r.Cursor >= all.Count)
                {
                    r.Phase = 2;
                    r.Cursor = 0;
                }
                return false;
            }
            List<BuildingForSale> sale = save != null ? save.buildingsForSale : null;
            end = sale != null ? Math.Min(sale.Count, r.Cursor + most) : 0;
            for (; sale != null && r.Cursor < end; r.Cursor++)
            {
                BuildingForSale f = sale[r.Cursor];
                CityBuilding cb = f != null && f.address != null ? FindAt(f.address.streetName, f.address.streetNumber) : null;
                if (cb != null && cb.B != null && cb.Holding == null && !cb.B.IsHamptonsHouse())
                {
                    r.Into.ForSale[UseOf(cb.B.BuildingType)]++;
                }
            }
            if (sale == null || r.Cursor >= sale.Count)
            {
                r.Phase = 3;
            }
            return r.Done;
        }

        private const string EmptyType = "ba:businesstype_empty";

        /// <summary>A rival's name as the game gives it.</summary>
        internal static string RivalName(string id)
        {
            return string.IsNullOrEmpty(id) ? string.Empty : id.GetRivalName();
        }
    }
}
