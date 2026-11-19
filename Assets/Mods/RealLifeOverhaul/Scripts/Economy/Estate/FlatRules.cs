using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kinds of flat a unit of a residential building can be.</summary>
    public enum FlatKind : byte
    {
        Studio = 0,
        OneBed = 1,
        TwoBed = 2,
        ThreeBed = 3
    }

    /// <summary>Why a flat is not bought, sold or let now; <see cref="None"/> for nothing in the way. Said before any money moves.</summary>
    public enum FlatRefusal : byte
    {
        None = 0,
        Off,        // the part is switched off or not ready
        Session,    // a shared city: who owns what is the host's record
        Gone,       // the unit is not for sale any more
        Most,       // the player holds as many flats as one person may
        Cash,       // not enough cash
        Moved,      // the price changed while the player decided
        NotMine,    // the flat is not the player's
        Let,        // somebody lives in it by a lease the player holds
        Selling,    // a sale is agreed
        Terms       // the bank will not lend on these terms
    }

    /// <summary>
    /// Flats and studios as units of the residential buildings the player does not own (game-free; checked by EconomyCheck's flats checks). A
    /// building's value is split over its units by floor area, a studio being the smallest share; a unit goes on the market by a draw of the
    /// building's own (its units, its district and its condition); a unit costs a long lease of its share. Nothing here allocates a list a day.
    /// </summary>
    public static class FlatRules
    {
        private const int ForKind = 71, ForListed = 73;

        public static string Key(string place, int slot)
        {
            return "flat:" + place + ":" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True for the key of a flat (the security of a mortgage on one, a holding's place).</summary>
        public static bool IsKey(string key) => key != null && key.StartsWith("flat:", StringComparison.Ordinal);

        /// <summary>The window of days a draw of listings stands for.</summary>
        public static int Window(int day) => Math.Max(0, day - 1) / FlatCatalogue.ListWindow;

        /// <summary>The kind of a unit: a draw of the building's own (never the save's), the poorer district's units smaller.</summary>
        public static int KindOf(string place, int slot, int income)
        {
            int roll = EstateRules.Roll(EstateRules.Hash(place), slot, ForKind, 0);
            int off = (EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost) - EstateCatalogue.IncomeMiddle) / FlatCatalogue.StudioPerPoint;
            int studio = FlatCatalogue.Clamp(FlatCatalogue.StudioBase - off, FlatCatalogue.StudioLeast, FlatCatalogue.StudioMost);
            if (roll < studio)
            {
                return (int)FlatKind.Studio;
            }
            int rest = (roll - studio) * 1000 / Math.Max(1, 1000 - studio);
            int[] share = FlatCatalogue.RestShare;
            return rest < share[0] ? (int)FlatKind.OneBed : rest < share[0] + share[1] ? (int)FlatKind.TwoBed : (int)FlatKind.ThreeBed;
        }

        /// <summary>The floor weight of each of a building's units, by its kind.</summary>
        public static int[] Weights(string place, int units, int income)
        {
            var w = new int[Math.Max(1, units)];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = FlatCatalogue.KindWeight[KindOf(place, i, income)];
            }
            return w;
        }

        /// <summary>
        /// A building's value split over its units by floor area (largest remainder, whole dollars): the shares add up to the value to the
        /// dollar, and a unit of a smaller kind has the smaller share.
        /// </summary>
        public static int[] Freehold(long value, int[] weights)
        {
            long sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }
            var part = new int[weights.Length];
            var rest = new long[weights.Length];
            value = Math.Max(0, value);
            long given = 0;
            for (int i = 0; i < weights.Length && sum > 0; i++)
            {
                long whole = value * weights[i] / sum;
                part[i] = (int)Math.Min(int.MaxValue / 4, whole);
                rest[i] = value * weights[i] % sum;
                given += part[i];
            }
            for (long left = value - given; left > 0 && sum > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < part.Length; i++)
                {
                    if (rest[i] > rest[best])
                    {
                        best = i;
                    }
                }
                part[best]++;
                rest[best] = -1;
            }
            return part;
        }

        /// <summary>A unit's floor, square metres: the building's floor over its units by their weights, at least one.</summary>
        public static int Sqm(int totalSqm, int[] weights, int slot)
        {
            long sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }
            return slot < 0 || slot >= weights.Length || sum <= 0 ? 1 : (int)Math.Max(1, (long)Math.Max(0, totalSqm) * weights[slot] / sum);
        }

        /// <summary>A unit's share of the building's floor, millionths.</summary>
        public static int Share(int[] weights, int slot)
        {
            long sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }
            return slot < 0 || slot >= weights.Length || sum <= 0 ? 0 : (int)((long)weights[slot] * 1000000 / sum);
        }

        /// <summary>What a long lease of a unit costs: its freehold share at the lease's share, named in tens, never under ten.</summary>
        public static int Lease(int freehold)
        {
            if (freehold <= 0)
            {
                return 0;
            }
            long price = (long)freehold * FlatCatalogue.LeaseShare / 1000;
            long round = (price + FlatCatalogue.RoundTo / 2) / FlatCatalogue.RoundTo * FlatCatalogue.RoundTo;
            return (int)Math.Min(int.MaxValue / 4, Math.Max(FlatCatalogue.RoundTo, round));
        }

        /// <summary>The thousandths of a building's units that go on the market in a window: more for a building run down and for a poorer district.</summary>
        public static int ListChance(int cond, int income)
        {
            int chance = FlatCatalogue.ListChance + (EstateRules.RunDown(cond) ? FlatCatalogue.ListRunDown : 0)
                + (EstateCatalogue.IncomeMiddle - EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost)) / FlatCatalogue.ListPerPoint;
            return FlatCatalogue.Clamp(chance, FlatCatalogue.ListLeast, FlatCatalogue.ListMost);
        }

        /// <summary>Whether a unit is on the market in a window: the save's seed, the building, the unit and the window.</summary>
        public static bool Listed(int seed, string place, int slot, int window, int cond, int income)
        {
            return EstateRules.Roll(seed, window, ForListed + slot * 7919, EstateRules.Hash(place)) < ListChance(cond, income);
        }

        /// <summary>
        /// The units of a building on the market on a day, at most <see cref="FlatCatalogue.ListedMost"/>, those the player holds left out
        /// (<paramref name="held"/>). Added to <paramref name="into"/>; how many.
        /// </summary>
        public static int Listings(List<FlatListing> into, FlatBuilding b, int seed, int day, Func<string, int, bool> held)
        {
            if (b.Units < FlatCatalogue.MinUnits || b.Value <= 0)
            {
                return 0;
            }
            int window = Window(day), added = 0;
            int[] weights = null;
            int[] freehold = null;
            for (int slot = 0; slot < b.Units && added < FlatCatalogue.ListedMost; slot++)
            {
                if (!Listed(seed, b.Place, slot, window, b.Cond, b.Income) || (held != null && held(b.Place, slot)))
                {
                    continue;
                }
                if (weights == null)
                {
                    weights = Weights(b.Place, b.Units, b.Income);
                    freehold = Freehold(b.Value, weights);
                }
                into.Add(new FlatListing
                {
                    Place = b.Place, Name = b.Name, District = b.District, Landlord = b.Landlord, Slot = slot, Kind = KindOf(b.Place, slot, b.Income), Units = b.Units,
                    Sqm = Sqm(b.Sqm, weights, slot), Cond = b.Cond, Income = b.Income, Freehold = freehold[slot], Price = Lease(freehold[slot]), Share = Share(weights, slot),
                    TaxYear = b.TaxYear
                });
                added++;
            }
            return added;
        }

        /// <summary>A flat's value now: the building's value by the market, its unit's share, at the lease's share.</summary>
        public static int Value(int buildingValue, string place, int units, int income, int slot)
        {
            int[] w = Weights(place, units, income);
            return slot < 0 || slot >= w.Length ? 0 : Lease(Freehold(buildingValue, w)[slot]);
        }

        /// <summary>The service charge for a month to the building's owner: a share of the rent the flat would let for.</summary>
        public static int ServiceMonth(int marketRentDay, int monthDays)
        {
            long charge = (long)Math.Max(0, marketRentDay) * Math.Max(1, monthDays) * FlatCatalogue.ServiceShare / 1000;
            return (int)Math.Min(int.MaxValue / 4, Math.Max(1, charge));
        }

        /// <summary>The unit's share of the building's property tax for a month: the game's tax for the year over its floor's share.</summary>
        public static int TaxMonth(int buildingTaxYear, int share)
        {
            long tax = (long)Math.Max(0, buildingTaxYear) * Math.Max(0, share) / 1000000 / FlatCatalogue.MonthsPerYear;
            return (int)Math.Max(FlatCatalogue.TaxLeast, Math.Min(int.MaxValue / 4, tax));
        }

        /// <summary>The calendar's month of a day.</summary>
        public static int MonthOf(int day, int monthDays) => Math.Max(0, day) / Math.Max(1, monthDays);

        /// <summary>A month's charge and tax are due when the calendar is in a month after the one last billed: once a month, never twice, and no back-bills for months missed.</summary>
        public static bool BillDue(int billedMonth, int month) => month > billedMonth;

        /// <summary>What a flat is credited when its building is bought whole: the unit's share of the price paid, at the lease's share, so the unit is not paid for twice.</summary>
        public static int FoldCredit(int buildingPrice, string place, int units, int income, int slot)
        {
            int[] w = Weights(place, units, income);
            return slot < 0 || slot >= w.Length ? 0 : Lease(Freehold(buildingPrice, w)[slot]);
        }

        /// <summary>What the player keeps of a flat's purchase against a gain when it folds into a building: the flat's cost over the credit, never under nothing.</summary>
        public static int FoldBasis(int paid, int credit) => Math.Max(0, paid - credit);

        /// <summary>The refusal, if any, of buying a listed flat. Said before any money moves.</summary>
        public static FlatRefusal MayBuy(bool on, bool session, bool listed, int held, int cash, int deposit, int shownPrice, int price)
        {
            if (!on)
            {
                return FlatRefusal.Off;
            }
            if (session)
            {
                return FlatRefusal.Session;
            }
            if (!listed)
            {
                return FlatRefusal.Gone;
            }
            if (held >= FlatCatalogue.HeldMost)
            {
                return FlatRefusal.Most;
            }
            if (shownPrice != price)
            {
                return FlatRefusal.Moved;
            }
            return cash < deposit ? FlatRefusal.Cash : FlatRefusal.None;
        }

        /// <summary>
        /// What the bank puts into a flat: the price less the deposit; 0 for cash (the whole deposit percent). The mortgage's own least and most
        /// are the bank's (<see cref="ProductTerms.FinanceLeast"/> and the offer's limit): <see cref="MortgageFits"/> asks them.
        /// </summary>
        public static int Financed(int price, int depositPercent)
        {
            return depositPercent >= FlatCatalogue.PayInFull ? 0 : ProductTerms.Financed(price, depositPercent, int.MaxValue);
        }

        /// <summary>The deposit at a percent of a price: the home mortgage's own rule; the whole price for cash.</summary>
        public static int Deposit(int price, int depositPercent)
        {
            return depositPercent >= FlatCatalogue.PayInFull ? price : ProductTerms.Deposit(price, depositPercent);
        }

        public static bool MortgageFits(int financed, int least, int most) => financed >= least && financed <= most;
    }
}
