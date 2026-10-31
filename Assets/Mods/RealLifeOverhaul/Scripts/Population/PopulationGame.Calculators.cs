using System;
using System.Collections.Generic;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BigAmbitions.DayNightCycle;
using Helpers;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The one narrow change to the game's customers (POPULATION.md section 7; the owner, 1 Nov 2026: yes). The game works out an hour's customers for a
    /// business, the player's and the city's alike, in its customer entries calculators (CustomerEntriesCalculatorFactory hands out one of five, chosen by
    /// the business's type): the hour's count is the business's "initial customers" (its room) times its promotion and its type's day and hour factors,
    /// capped by its staffed furniture (a shop) or its workers on shift (an office). A Harmony patch was the design's way in, but the game ships no Harmony
    /// (only Going Public brings one) and the mod takes no new dependency; the factory's calculators are plain objects the game keeps in five static
    /// fields, and each calculator's hour is a virtual method. So at start each field is given the mod's own subclass of the game's own calculator, which
    /// asks the game's own method for the hour with the initial customers scaled by the population's factor (CustomerRules): every cap, the day and hour
    /// factors, an office's demand, the AI's draw after it stay the game's. The subclass is the game's calculator in all else, so it stands down to the
    /// game's own result exactly: with Living population off, for a business the population has no figure for yet, and wherever a fault comes (logged once).
    /// A field missing or of another type after a game update leaves that calculator the game's, with one log line, and the population's other figures go on.
    /// COST: an hour's count of a business asks one dictionary look-up and a few multiplications more; nothing per frame.
    /// </summary>
    internal static partial class PopulationGame
    {
        /// <summary>True when every one of the game's calculators is the mod's copy (the population's factor reaches the game's own count).</summary>
        public static bool CalculatorsIn { get; private set; }

        /// <summary>The factory's five calculators given the mod's copies, once at start, before any save loads.</summary>
        public static void InstallCalculators()
        {
            int replaced = 0;
            replaced += Replace(GameContract.CalculatorPlain, new PlainCalculator()) ? 1 : 0;
            replaced += Replace(GameContract.CalculatorRetail, new RetailCalculator()) ? 1 : 0;
            replaced += Replace(GameContract.CalculatorOffice, new OfficeCalculator()) ? 1 : 0;
            replaced += Replace(GameContract.CalculatorCasino, new CasinoCalculator()) ? 1 : 0;
            replaced += Replace(GameContract.CalculatorCinema, new CinemaCalculator()) ? 1 : 0;
            CalculatorsIn = replaced == 5;
            if (CalculatorsIn)
            {
                Logger?.Info("Living population: the game's five customer calculators carry the population's count (each stands down to the game's own result while it has no figure).");
            }
            else
            {
                Logger?.Warn("Living population: " + replaced + " of the game's five customer calculators could be given the population's count (a newer game?); the game's own customers stand, and the population's other figures go on.");
            }
        }

        // A factory field given the mod's copy; false (the game's own kept) when the field is missing or will not take it.
        private static bool Replace(GameMember member, CustomerEntriesCalculator ours)
        {
            try
            {
                FieldInfo field = member != null ? member.Field : null;
                if (field == null || !field.FieldType.IsInstanceOfType(ours))
                {
                    return false;
                }
                field.SetValue(null, ours);
                return ReferenceEquals(field.GetValue(null), ours);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>
        /// The self-check at a load: the factory really hands out the mod's copy for a registration (true), or the game made its own choice before the mod's
        /// start could set it (false: the game's own customers stand; one log line).
        /// </summary>
        public static bool CalculatorsServe(BuildingRegistration reg)
        {
            if (!CalculatorsIn || reg == null)
            {
                return false;
            }
            try
            {
                CustomerEntriesCalculator c = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
                return c is PlainCalculator || c is RetailCalculator || c is OfficeCalculator || c is CasinoCalculator || c is CinemaCalculator;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The initial customers of an hour as the population moves them: the game's own while it has no figure.
        private static float Scaled(BuildingRegistration reg, float initial, float promotionNow, bool usesPromotion)
        {
            if (!PopulationCustomers.On || reg == null)
            {
                return initial;
            }
            try
            {
                float scale = PopulationCustomers.Scale(reg, (int)Math.Round(promotionNow * 1000f), usesPromotion);
                return scale == 1f ? initial : initial * scale;
            }
            catch (Exception e)
            {
                PopulationCustomers.Fault(e);
                return initial;
            }
        }

        private sealed class PlainCalculator : CustomerEntriesCalculator
        {
            public override int GetCustomersByHour(int hour, DayOfWeekOrdered day, float initialCustomers)
            {
                return base.GetCustomersByHour(hour, day, Scaled(registration, initialCustomers, PromotionOrNone(this), true));
            }

            public float Promotion() => GetPromotionMultiplier();
        }

        private sealed class RetailCalculator : CustomerEntriesCalculatorRetail
        {
            public override int GetCustomersByHour(int hour, DayOfWeekOrdered day, float initialCustomers)
            {
                return base.GetCustomersByHour(hour, day, Scaled(registration, initialCustomers, PromotionOrNone(this), true));
            }

            public float Promotion() => GetPromotionMultiplier();
        }

        private sealed class OfficeCalculator : CustomerEntriesCalculatorOffice
        {
            public override int GetCustomersByHour(int hour, DayOfWeekOrdered day, float initialCustomers)
            {
                return base.GetCustomersByHour(hour, day, Scaled(registration, initialCustomers, PromotionOrNone(this), true));
            }

            public float Promotion() => GetPromotionMultiplier();
        }

        private sealed class CasinoCalculator : CustomerEntriesCalculatorCasino
        {
            public override int GetCustomersByHour(int hour, DayOfWeekOrdered day, float initialCustomers)
            {
                return base.GetCustomersByHour(hour, day, Scaled(registration, initialCustomers, 0f, false));   // the casino's count takes no promotion
            }
        }

        private sealed class CinemaCalculator : CustomerEntriesCalculatorCinemaTheater
        {
            public override int GetCustomersByHour(int hour, DayOfWeekOrdered day, float initialCustomers)
            {
                return base.GetCustomersByHour(hour, day, Scaled(registration, initialCustomers, PromotionOrNone(this), true));
            }

            public float Promotion() => GetPromotionMultiplier();
        }

        // The calculator's promotion multiplier for its registration now (the game's own arithmetic), 0 when it cannot be read: then no promotion is taken back.
        private static float PromotionOrNone(CustomerEntriesCalculator c)
        {
            try
            {
                switch (c)
                {
                    case PlainCalculator p: return p.Promotion();
                    case RetailCalculator r: return r.Promotion();
                    case OfficeCalculator o: return o.Promotion();
                    case CinemaCalculator m: return m.Promotion();
                    default: return 0f;
                }
            }
            catch (Exception)
            {
                return 0f;
            }
        }
    }

    /// <summary>
    /// The population's figure for each business as the game's calculators and the mod's copies of their arithmetic ask it: a registration found by
    /// its object (made again from the business table after each day the population lives, and at a load), its factor and its anchor's promotion.
    /// Main thread only. COST: a dictionary look-up an asking.
    /// </summary>
    internal static class PopulationCustomers
    {
        private static readonly Dictionary<BuildingRegistration, int> ByRegistration = new Dictionary<BuildingRegistration, int>(2048, ReferenceComparer<BuildingRegistration>.Instance);
        private static PopulationState _state;
        private static bool _faulted;

        /// <summary>True while the population's figures move the game's customers.</summary>
        public static bool On { get; private set; }

        /// <summary>The figures in use from now: every registration of the game's matched to its business by its address.</summary>
        public static void Use(PopulationState s, bool on)
        {
            _state = s;
            ByRegistration.Clear();
            On = on && s != null && PopulationGame.CalculatorsIn && !_faulted;
            if (!On)
            {
                return;
            }
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || reg.Address == null)
                {
                    continue;
                }
                int b = s.BusinessOfKey(PopulationState.KeyOf(reg.Address.streetName, reg.Address.streetNumber));
                if (b >= 0 && !ByRegistration.ContainsKey(reg))
                {
                    ByRegistration.Add(reg, b);
                }
            }
        }

        /// <summary>Nothing moves the game's customers any more (the option off, the city unloads, the population stands down).</summary>
        public static void Stop()
        {
            On = false;
            _state = null;
            ByRegistration.Clear();
        }

        /// <summary>A registration's scale of its initial customers: its factor, the promotion since its anchor taken back where the formula uses it; 1 with no figure.</summary>
        public static float Scale(BuildingRegistration reg, int promotionNow, bool usesPromotion)
        {
            PopulationState s = _state;
            if (!On || s == null || !ByRegistration.TryGetValue(reg, out int b) || b >= s.Businesses.Count || !s.Businesses.Seen[b])
            {
                return 1f;
            }
            BusinessTable bt = s.Businesses;
            if (bt.AnchorDay[b] < 0)
            {
                return 1f;
            }
            int factor = CustomerRules.Factor(bt.Takings[b], bt.Anchor[b], bt.AnchorDay[b]);
            return CustomerRules.InitialScale(factor, bt.AnchorPromotion[b], promotionNow, usesPromotion);
        }

        /// <summary>The scale the mod's own copies of the arithmetic read (Core/CustomerScale): with the registration's promotion now from the game's own figures.</summary>
        public static float ScaleFor(BuildingRegistration reg)
        {
            return On && reg != null ? Scale(reg, PopulationGame.PromotionNow(reg), true) : 1f;
        }

        /// <summary>
        /// A site's scale for a business type in a district (a franchise weighs a site before it opens): how that kind of business does there now against
        /// its anchors, on the average of those open; 1 with none.
        /// </summary>
        public static float SiteScale(string district, string type)
        {
            PopulationState s = _state;
            int d = s != null ? s.DistrictOf(district) : -1;
            if (!On || d < 0)
            {
                return 1f;
            }
            long sum = 0;
            int count = 0;
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                if (bt.Seen[b] && bt.District[b] == d && bt.AnchorDay[b] >= 0 && string.Equals(bt.Type[b], type, StringComparison.Ordinal))
                {
                    sum += CustomerRules.Factor(bt.Takings[b], bt.Anchor[b], bt.AnchorDay[b]);
                    count++;
                }
            }
            return count > 0 ? sum / (float)count / 1000f : 1f;
        }

        /// <summary>A fault in the game's own call: one log line, and the game's own customers for the rest of the session.</summary>
        public static void Fault(Exception e)
        {
            if (_faulted)
            {
                return;
            }
            _faulted = true;
            On = false;
            PopulationGame.Logger?.Error(e);
            PopulationGame.Logger?.Warn("Living population: a fault in the customers' count; the game's own customers stand for this session.");
        }
    }
}
