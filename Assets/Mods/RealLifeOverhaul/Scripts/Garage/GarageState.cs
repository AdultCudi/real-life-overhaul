using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The mod's two-wheelers. Each is one of the game's own electric scooters wearing another body.</summary>
    public enum BikeKind
    {
        CityBicycle,
        RoadBicycle,
        StandardMotorbike,
        SportMotorbike
    }

    /// <summary>One two-wheeler of the player's, or one on hire from a dock.</summary>
    public sealed class BikeRecord
    {
        public string Id;          // the id of the scooter that carries it in the save's vehicle list
        public BikeKind Kind;
        public bool Owned;         // false: on hire from a bike-share dock
        public Vector3 Position;   // where it last stood, to put an owned one back if the game removed its scooter
        public float Heading;
        public int Day;            // the day it was hired
        public int Colour = -1;    // 0xRRGGBB of the frame or tank, -1 = the model's own
    }

    /// <summary>
    /// The Garage's part of a save: what each car carries from the custom shop, and which of the save's
    /// scooters are really bicycles and motorbikes. Two strings in the save's modData and nothing else.
    /// The vehicles themselves stay exactly what the base game made: a car keeps its dealer colour name,
    /// a two-wheeler is an ordinary electric scooter. A save loaded without the mod therefore shows stock
    /// cars and scooters, and the game cleans the scooters up by itself as it does any other.
    /// </summary>
    public static class GarageState
    {
        private const string KeyBuilds = "reallife:garage_builds";
        private const string KeyBikes = "reallife:garage_bikes";
        private const string KeyHelmet = "reallife:garage_helmet";

        public static readonly Dictionary<string, GarageBuild> Builds = new Dictionary<string, GarageBuild>();
        public static readonly Dictionary<string, BikeRecord> Bikes = new Dictionary<string, BikeRecord>();

        private static readonly List<string> Gone = new List<string>();

        public static bool Loaded { get; private set; }

        /// <summary>The player owns a helmet (bought with the first motorbike, or at a stand).</summary>
        public static bool Helmet;

        /// <summary>Goes up whenever a build changes, for whatever shows one.</summary>
        public static int Version { get; private set; }

        public static void Reset()
        {
            Loaded = false;
            Builds.Clear();
            Bikes.Clear();
            Helmet = false;
            Version++;
        }

        public static GarageBuild Of(string vehicleId)
        {
            return vehicleId != null && Builds.TryGetValue(vehicleId, out GarageBuild build) ? build : null;
        }

        public static BikeRecord Bike(string vehicleId)
        {
            return vehicleId != null && Bikes.TryGetValue(vehicleId, out BikeRecord bike) ? bike : null;
        }

        /// <summary>Stores a car's build; a build with nothing on it is forgotten.</summary>
        public static void Set(string vehicleId, GarageBuild build)
        {
            if (vehicleId == null)
            {
                return;
            }
            if (build == null || (build.IsStock && build.Spent == 0))
            {
                Builds.Remove(vehicleId);
            }
            else
            {
                Builds[vehicleId] = build;
            }
            Version++;
        }

        public static void Touch()
        {
            Version++;
        }

        public static void Load(Dictionary<string, string> data)
        {
            Builds.Clear();
            Bikes.Clear();
            Helmet = false;
            if (data != null)
            {
                if (data.TryGetValue(KeyBuilds, out string builds) && !string.IsNullOrEmpty(builds))
                {
                    foreach (string line in builds.Split('\n'))
                    {
                        int at = line.IndexOf('|');
                        if (at > 0)
                        {
                            Builds[line.Substring(0, at)] = GarageBuild.Read(line.Substring(at + 1));
                        }
                    }
                }
                if (data.TryGetValue(KeyBikes, out string bikes) && !string.IsNullOrEmpty(bikes))
                {
                    foreach (string line in bikes.Split('\n'))
                    {
                        BikeRecord bike = ReadBike(line);
                        if (bike != null)
                        {
                            Bikes[bike.Id] = bike;
                        }
                    }
                }
                Helmet = data.TryGetValue(KeyHelmet, out string helmet) && helmet == "1";
            }
            Loaded = true;
            Version++;
        }

        /// <summary>
        /// Called while the game saves. A car that is no longer in the save (sold, scrapped) loses its build
        /// here, and a hired two-wheeler whose scooter is gone is forgotten; an owned one is kept, with where
        /// it stood, so that it can be put back.
        /// </summary>
        public static void Save(Dictionary<string, string> data, List<VehicleInstance> vehicles)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            if (vehicles != null)
            {
                Gone.Clear();
                foreach (KeyValuePair<string, GarageBuild> pair in Builds)
                {
                    if (!Has(vehicles, pair.Key))
                    {
                        Gone.Add(pair.Key);
                    }
                }
                for (int i = 0; i < Gone.Count; i++)
                {
                    Builds.Remove(Gone[i]);
                }
                Gone.Clear();
                foreach (KeyValuePair<string, BikeRecord> pair in Bikes)
                {
                    if (!pair.Value.Owned && !Has(vehicles, pair.Key))
                    {
                        Gone.Add(pair.Key);
                    }
                }
                for (int i = 0; i < Gone.Count; i++)
                {
                    Bikes.Remove(Gone[i]);
                }
                Gone.Clear();
            }

            var text = new StringBuilder(256);
            foreach (KeyValuePair<string, GarageBuild> pair in Builds)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append(pair.Key).Append('|').Append(pair.Value.Write());
            }
            data[KeyBuilds] = text.ToString();

            text.Length = 0;
            foreach (KeyValuePair<string, BikeRecord> pair in Bikes)
            {
                BikeRecord bike = pair.Value;
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append(bike.Id).Append('|').Append((int)bike.Kind).Append('|').Append(bike.Owned ? '1' : '0').Append('|')
                    .Append(bike.Position.x.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(bike.Position.y.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(bike.Position.z.ToString("F2", CultureInfo.InvariantCulture)).Append('|')
                    .Append(bike.Heading.ToString("F0", CultureInfo.InvariantCulture)).Append('|')
                    .Append(bike.Day.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(bike.Colour.ToString(CultureInfo.InvariantCulture));
            }
            data[KeyBikes] = text.ToString();
            data[KeyHelmet] = Helmet ? "1" : "0";
        }

        private static bool Has(List<VehicleInstance> vehicles, string id)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && vehicles[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static BikeRecord ReadBike(string line)
        {
            string[] parts = line.Split('|');
            if (parts.Length < 6 || parts[0].Length == 0
                || !int.TryParse(parts[1], out int kind) || kind < 0 || kind > (int)BikeKind.SportMotorbike)
            {
                return null;
            }
            string[] at = parts[3].Split(',');
            var bike = new BikeRecord { Id = parts[0], Kind = (BikeKind)kind, Owned = parts[2] == "1" };
            if (at.Length == 3
                && float.TryParse(at[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(at[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(at[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                bike.Position = new Vector3(x, y, z);
            }
            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out bike.Heading);
            int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out bike.Day);
            if (parts.Length > 6 && int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int colour))
            {
                bike.Colour = colour;
            }
            return bike;
        }
    }

    /// <summary>
    /// Where the Garage meets features it must not depend on. Each is a delegate another feature may set
    /// when it loads; left null, the default written beside it applies. And the other way round: what
    /// those features may want to know about a car's build, by vehicle instance id, safe to call at any time.
    /// </summary>
    public static class GarageBridges
    {
        /// <summary>Licences: may the player ride a motorbike? Null: allowed.</summary>
        public static Func<bool> HasMotorcycleLicence;

        /// <summary>Weather: is the road wet? Null: wet while the game's rain is falling.</summary>
        public static Func<bool> RoadIsWet;

        /// <summary>
        /// Crime: the police have noticed something about the player's vehicle ("exhaust", "helmet"). The
        /// argument is the fine the Garage would charge. Return true if the Crime feature dealt with it (a stop,
        /// a ticket of its own); false or null: the Garage charges the fine itself through the mod's own fine.
        /// </summary>
        public static Func<string, int, bool> PoliceNotice;

        /// <summary>Ragdoll: the rider has come off at this speed (m/s). Return true if a knockdown was played. Null: the bike just stops.</summary>
        public static Func<float, bool> RiderFalls;

        /// <summary>Transport: how much faster this car wears than stock (0 = no faster, 0.25 = a quarter faster).</summary>
        public static float WearOf(string vehicleId)
        {
            GarageBuild build = Settings.GarageTuning ? GarageState.Of(vehicleId) : null;
            return build != null ? build.Modifiers().Wear : 0f;
        }

        /// <summary>Transport: what this car's build adds to an insurance premium (0.10 = a tenth dearer).</summary>
        public static float InsuranceOf(string vehicleId)
        {
            GarageBuild build = GarageState.Of(vehicleId);
            return build != null ? build.Modifiers().Insurance : 0f;
        }

        /// <summary>What a car's build adds to its worth: half of what was spent on it.</summary>
        public static int ValueAdded(string vehicleId)
        {
            GarageBuild build = GarageState.Of(vehicleId);
            return build != null ? Mathf.RoundToInt(build.Spent * GarageCatalogue.Resale) : 0;
        }

        /// <summary>0 stock, 1 sporty, 2 not street legal.</summary>
        public static int LoudnessOf(string vehicleId)
        {
            GarageBuild build = Settings.GarageTuning ? GarageState.Of(vehicleId) : null;
            return build != null ? build.Modifiers().Loudness : 0;
        }

        /// <summary>True if this vehicle of the save is one of the mod's two-wheelers (a scooter in the save).</summary>
        public static bool IsTwoWheeler(string vehicleId)
        {
            return GarageState.Bike(vehicleId) != null;
        }

        internal static bool Wet()
        {
            try
            {
                return RoadIsWet != null ? RoadIsWet() : RainHelper.AreRainDropsFalling;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool Licensed()
        {
            try
            {
                return HasMotorcycleLicence == null || HasMotorcycleLicence();
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
