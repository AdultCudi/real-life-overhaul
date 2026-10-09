using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the mod remembers about one car the player owns. Everything the game itself keeps (fuel, damage, dirt, tickets) stays the game's.</summary>
    public sealed class CarRecord
    {
        public string Id;             // the game's vehicle instance id
        public float Km;              // driven since the mod first saw it
        public float ServicedKm;      // the reading at its last service
        public float Tyres = 1f;      // 1 new, 0 bald
        public float Brakes = 1f;
        public int InsuredUntil;      // the last day of the paid period; 0 for no policy
        public int Claims;            // claims that still count against the premium
        public int LastClaimDay;
        public int RegisteredDay;     // the day its registration and inspection were last done
        public int Breakdowns;
        public bool Broken;           // it will not go until somebody has seen to it
        public float GraceKm;         // a roadside repair holds until this reading
        public int RemindedDay;       // the last day the player was reminded about its registration
        public float LastDamage = -1f;   // the game's damage figure when last looked at; not saved
    }

    /// <summary>
    /// The rules of car ownership: what things cost, how long they last, what the odds are.
    /// Game-free apart from Mathf, and every figure is here.
    ///
    /// The game already has fuel (a tank per vehicle type, 1.30 a litre at a station, a 10-litre
    /// can for 30), bodywork damage (repaired at a station's garage for damage x half the car's
    /// price), dirt (washed there for 50), parking tickets and towing. Those are left alone.
    /// What is added, per car: distance driven, a service interval, tyres and brakes that wear,
    /// breakdowns when the service is neglected, insurance that pays most of a repair bill, and
    /// a yearly registration and inspection. Prices are shares of the car's own list price
    /// (VehicleType.price) between a floor and a ceiling, so a hatchback is cheap to run and a
    /// supercar is not.
    /// </summary>
    public static class CarRules
    {
        public const float ServiceShare = 0.012f, ServiceLeast = 120f, ServiceMost = 900f;
        public const float TyresShare = 0.016f, TyresLeast = 180f, TyresMost = 1400f;
        public const float BrakesShare = 0.008f, BrakesLeast = 90f, BrakesMost = 700f;
        public const float RegistrationBase = 60f, RegistrationShare = 0.002f, RegistrationMost = 300f;
        public const float RegistrationLate = 75f;
        public const int RegistrationEarly = 30;      // days before it runs out that it can be renewed
        public const float PremiumShare = 0.006f, PremiumLeast = 40f, PremiumMost = 2500f;
        public const float PremiumPerClaim = 0.25f;   // each claim that still counts adds a quarter
        public const int PolicyDays = 28;
        public const int ClaimForgottenAfter = 56;    // days without a claim after which one stops counting
        public const float Excess = 100f;             // of a repair bill the owner always pays
        public const float Cover = 0.8f;              // of the rest that the insurer pays
        public const float RoadsideCallOut = 95f;     // a mechanic to a breakdown (half for an insured car)
        public const float FuelCallOut = 34f;         // a can brought to the roadside...
        public const float FuelLitres = 10f;
        public const float FuelPerLitre = 2.6f;       // ...at twice the pump price
        public const float TowFee = 180f;             // the same as the mod's other tow
        public const float ReplaceBelow = 0.35f;      // tyres and brakes are changed at a service when this worn
        public const float WarnBelow = 0.2f;
        public const float WearFrom = 0.3f;           // below this they begin to tell
        public const float GripAtBald = 0.88f;        // of the tyre's grip left at 0
        public const float BrakeAtWorn = 0.80f;       // of the brake torque left at 0
        public const float GraceKm = 8f;              // a roadside repair holds this long

        public static float Clamp(float price, float share, float least, float most)
        {
            return Mathf.Round(Mathf.Clamp(price * share, least, most) / 5f) * 5f;
        }

        public static float Service(float price) => Clamp(price, ServiceShare, ServiceLeast, ServiceMost);
        public static float TyreSet(float price) => Clamp(price, TyresShare, TyresLeast, TyresMost);
        public static float BrakeSet(float price) => Clamp(price, BrakesShare, BrakesLeast, BrakesMost);
        public static float Registration(float price) => Mathf.Round(Mathf.Min(RegistrationMost, RegistrationBase + price * RegistrationShare) / 5f) * 5f;

        public static float Premium(float price, int claims)
        {
            return Mathf.Round(Mathf.Clamp(price * PremiumShare, PremiumLeast, PremiumMost) * (1f + PremiumPerClaim * Mathf.Clamp(claims, 0, 6)) / 5f) * 5f;
        }

        /// <summary>What the insurer pays towards a repair bill.</summary>
        public static float Payout(float bill)
        {
            return Mathf.Max(0f, Mathf.Round((bill - Excess) * Cover));
        }

        /// <summary>Kilometres between services.</summary>
        public static float ServiceEvery(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Economy: return 70f;
                case CarKind.Sports: return 40f;
                case CarKind.Luxury:
                case CarKind.Truck: return 50f;
                default: return 60f;
            }
        }

        /// <summary>Kilometres a set of tyres lasts.</summary>
        public static float TyreLife(CarKind kind)
        {
            return kind == CarKind.Sports ? 120f : kind == CarKind.Truck || kind == CarKind.Van ? 200f : 180f;
        }

        public static float BrakeLife(CarKind kind)
        {
            return kind == CarKind.Sports ? 110f : kind == CarKind.Truck ? 120f : 150f;
        }

        /// <summary>How thirsty a kind of car is beside the game's one figure for all of them.</summary>
        public static float Thirst(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Economy: return 0.8f;
                case CarKind.Luxury: return 1.25f;
                case CarKind.Sports: return 1.4f;
                case CarKind.Utility: return 1.3f;
                case CarKind.Van: return 1.2f;
                case CarKind.Truck: return 1.6f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The chance of breaking down in the next kilometre: none until the service is overdue,
        /// 0.4 % just after, 3 % a whole interval late; one percent more with bodywork damage of 70 % or worse.
        /// </summary>
        public static float BreakdownPerKm(float sinceService, float interval, float damage)
        {
            float odds = damage >= 0.7f ? 0.01f : 0f;
            if (sinceService > interval)
            {
                odds += 0.004f + 0.026f * Mathf.Clamp01((sinceService - interval) / interval);
            }
            return odds;
        }

        public static float Grip(float tyres)
        {
            return tyres >= WearFrom ? 1f : Mathf.Lerp(GripAtBald, 1f, Mathf.Clamp01(tyres / WearFrom));
        }

        public static float BrakeForce(float brakes)
        {
            return brakes >= WearFrom ? 1f : Mathf.Lerp(BrakeAtWorn, 1f, Mathf.Clamp01(brakes / WearFrom));
        }
    }

    /// <summary>
    /// The records of the player's cars, kept in the save's modData under one key and keyed by
    /// the game's vehicle instance id. A record whose car is no longer in the save (sold, taken
    /// away) is dropped when the save is read and again whenever it is written.
    /// </summary>
    public static class CarGarage
    {
        private const string Key = "reallife:garage";

        private static readonly Dictionary<string, CarRecord> Records = new Dictionary<string, CarRecord>(8);
        private static readonly List<string> Scratch = new List<string>(8);

        public static bool Loaded { get; private set; }

        public static void Reset()
        {
            Records.Clear();
            Loaded = false;
        }

        /// <summary>The record of a car, made the first time it is asked for.</summary>
        public static CarRecord For(string id, int today)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            if (!Records.TryGetValue(id, out CarRecord record))
            {
                record = new CarRecord { Id = id, RegisteredDay = today };   // a car comes registered
                Records[id] = record;
            }
            return record;
        }

        public static bool Has(string id)
        {
            return id != null && Records.ContainsKey(id);
        }

        public static void Load(Dictionary<string, string> data)
        {
            Records.Clear();
            if (data != null && data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                foreach (string row in text.Split(';'))
                {
                    string[] f = row.Split('~');
                    if (f.Length < 12 || string.IsNullOrEmpty(f[0]))
                    {
                        continue;
                    }
                    var record = new CarRecord
                    {
                        Id = f[0], Km = F(f[1]), ServicedKm = F(f[2]), Tyres = Mathf.Clamp01(F(f[3])), Brakes = Mathf.Clamp01(F(f[4])),
                        InsuredUntil = I(f[5]), Claims = I(f[6]), LastClaimDay = I(f[7]), RegisteredDay = I(f[8]), Breakdowns = I(f[9]),
                        Broken = f[10] == "1", GraceKm = F(f[11])
                    };
                    Records[record.Id] = record;
                }
            }
            Loaded = true;
        }

        public static void Save(Dictionary<string, string> data, List<VehicleInstance> owned, List<VehicleInstance> lent)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            Prune(owned, lent);
            var text = new StringBuilder(Records.Count * 64);
            foreach (CarRecord r in Records.Values)
            {
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.Append(r.Id).Append('~').Append(S(r.Km)).Append('~').Append(S(r.ServicedKm)).Append('~').Append(S(r.Tyres)).Append('~').Append(S(r.Brakes)).Append('~')
                    .Append(r.InsuredUntil).Append('~').Append(r.Claims).Append('~').Append(r.LastClaimDay).Append('~').Append(r.RegisteredDay).Append('~')
                    .Append(r.Breakdowns).Append('~').Append(r.Broken ? '1' : '0').Append('~').Append(S(r.GraceKm));
            }
            data[Key] = text.ToString();
        }

        /// <summary>Drops the records of cars the save no longer has. Returns how many went.</summary>
        public static int Prune(List<VehicleInstance> owned, List<VehicleInstance> lent)
        {
            Scratch.Clear();
            foreach (string id in Records.Keys)
            {
                if (!Contains(owned, id) && !Contains(lent, id))
                {
                    Scratch.Add(id);
                }
            }
            for (int i = 0; i < Scratch.Count; i++)
            {
                Records.Remove(Scratch[i]);
            }
            int gone = Scratch.Count;
            Scratch.Clear();
            return gone;
        }

        public static bool Contains(List<VehicleInstance> cars, string id)
        {
            for (int i = 0; cars != null && i < cars.Count; i++)
            {
                if (cars[i] != null && cars[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static float F(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        private static int I(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static string S(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
