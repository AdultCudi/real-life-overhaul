using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.ParkSpaces;
using RealLifeOverhaul.Ui;

// The smaller business asks' samples: a building's car park on the Property app and the suppliers on the Car Showroom's Market tab. Both pages are the shipped ones
// (SpacePages, CarSupplyPages) over a sample state made by the shipped rules, so nothing a live page works out is typed here; a press is kept by a recorder the checks read.
internal static partial class UiSamples
{
    /// <summary>What the car park's page was last asked to do.</summary>
    internal sealed class SpacePressed : ISpaceActions
    {
        public string Last = string.Empty;
        public int Park, Slot;
        public long Back;
        public bool Ending;

        public void Buy(int park, int slot) => Set("buy", park, slot);

        public void Rent(int park, int slot) => Set("rent", park, slot);

        public void Sell(int park, int slot, long back)
        {
            Set("sell", park, slot);
            Back = back;
        }

        public void SetEnding(int park, int slot, bool ending)
        {
            Set("ending", park, slot);
            Ending = ending;
        }

        private void Set(string what, int park, int slot)
        {
            Last = what;
            Park = park;
            Slot = slot;
        }
    }

    /// <summary>What the suppliers' page was last asked to do.</summary>
    internal sealed class SupplyPressed : ICarSupplyActions
    {
        public string Last = string.Empty, Place = string.Empty;
        public int Kind, Value;

        public void SetSupplier(int kind, int supplier)
        {
            Last = "supplier";
            Kind = kind;
            Value = supplier;
        }

        public void SetKeep(string place, int kind, int count)
        {
            Last = "keep";
            Place = place;
            Kind = kind;
            Value = count;
        }

        public void ShowYard(string place)
        {
            Last = "yard";
            Place = place;
        }
    }

    internal static readonly SpacePressed SpacePress = new SpacePressed();
    internal static readonly SupplyPressed SupplyPress = new SupplyPressed();

    private const int SpaceDay = 214;

    /// <summary>A car park of a rival's office tower in a dear district, with the player's two spaces in it (one bought, one rented), or one with every space taken.</summary>
    internal static SpaceState SpaceWorld(bool full, out SpacePark park)
    {
        var s = new SpaceState { Seed = 4127, LastDay = SpaceDay, LastHour = SpaceDay * 24 + 9 };
        park = new SpacePark
        {
            Number = 3, Name = "13 3rd Avenue", District = "Midtown", Capacity = SpaceRules.Capacity(2, 2400), CityShare = full ? 1000 : SpaceRules.CityShare(12.0),
            RentMonth = SpaceRules.RentMonth(1120), Owner = SpaceOwner.Rival, OwnerName = "Hudson Realty"
        };
        park.Price = SpaceRules.Price(park.RentMonth, 120, 1040);
        if (!full)
        {
            int first = SpaceRules.FreeSlot(s, park, SpaceDay);
            SpaceRules.Take(s, park, first, SpaceHolding.PlayerIs, SpaceDay - 40, park.Price - 2400, 0);
            int second = SpaceRules.FreeSlot(s, park, SpaceDay);
            SpaceHolding rented = SpaceRules.Take(s, park, second, SpaceHolding.PlayerIs, SpaceDay - 12, 0, park.RentMonth);
            if (rented != null)
            {
                rented.PaidTo = SpaceDay + 17;
            }
            int third = SpaceRules.FreeSlot(s, park, SpaceDay);
            SpaceRules.Take(s, park, third, 1, SpaceDay - 5, park.Price, 0);
            for (int i = 0; i < 14; i++)
            {
                SpaceRules.AddHour(s, SpaceDay, park.Number, "sample car");
            }
        }
        return s;
    }

    private static string SpaceDayWords(UiWords t, int day) => Fx(W(t, "reallife_econ_day", "day {day}"), "day", day.ToString(InvD));

    /// <summary>A car park's page: with spaces free and two of the player's, or full with nothing to take.</summary>
    private static void SpaceSample(UiWords t, EstateModel m, List<Func<bool>> runs, bool full)
    {
        SpaceState s = SpaceWorld(full, out SpacePark park);
        long cash = full ? 900 : 250000;
        SpaceRefusal buy = SpaceRules.MayTake(s, park, SpaceDay, true, false, cash, park.Price, out int _);
        SpaceRefusal rent = SpaceRules.MayTake(s, park, SpaceDay, true, false, cash, park.RentMonth, out int slot);
        var mine = new EstateRuns();
        SpacePages.Fill(m, mine, t, s, park, SpaceDay, buy, rent, slot, x => Usd(x), d => SpaceDayWords(t, d), SpacePress);
        runs.AddRange(mine.ToArray());
    }

    /// <summary>The Market tab's row of the sample car park, as the live table has it.</summary>
    internal static UiDataRow SpaceMarketRow(UiWords t)
    {
        SpaceState s = SpaceWorld(false, out SpacePark park);
        return SpacePages.MarketRow(t, s, park, SpaceDay, x => Usd(x));
    }

    /// <summary>The sample showroom's suppliers: economy cars from the importer, sports cars from the prestige house, a standing order at the first showroom.</summary>
    internal static CarSupplyState SupplyWorld(DealerState s)
    {
        var supply = new CarSupplyState();
        supply.Supplier[0] = CarSupplyCatalogue.Importer;
        supply.Supplier[3] = CarSupplyCatalogue.Specialist;
        if (s.Yards.Count > 0)
        {
            CarSupplyKeep keep = supply.KeepOf(s.Yards[0].Place, true);
            keep.Keep[0] = 3;
            keep.Keep[1] = 2;
        }
        supply.Ordered(9001, CarSupplyCatalogue.Importer);
        return supply;
    }

    internal static List<MarketModel> SupplyModels()
    {
        var models = new List<MarketModel>(CarModels.Length);
        foreach (SampleCar c in CarModels)
        {
            models.Add(new MarketModel(c.Name, c.Sold * 1.4f, (int)c.Kind));
        }
        return models;
    }

    /// <summary>The suppliers' page of the Car Showroom's Market tab.</summary>
    private static void SupplySample(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        var mine = new EstateRuns();
        CarSupplyPages.Fill(m, mine, t, s, SupplyWorld(s), SupplyModels(), s.Yards.Count > 0 ? s.Yards[0].Place : null, y => y == s.Yards[0], SupplyPress);
        runs.AddRange(mine.ToArray());
    }
}
