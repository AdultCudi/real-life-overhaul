using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The subway stations the mod stands itself, among the game's own in <c>CityManager.subwayStations</c>. The game names a
    /// station from its <c>stationName</c>, a value of a fixed list, so a station of the mod's own carries the value of the
    /// entrance it was copied from and is never named by it: every reader of the mod asks here first (GameMembers.NameKey),
    /// and what is keyed to the game's twenty (the bus network's ends, the leisure landmarks) leaves these out (<see cref="Is"/>).
    /// Filled and emptied by the stations' own feature (Scripts/Subway). COST: a list of three; a reference comparison each.
    /// </summary>
    public static class OwnStations
    {
        private static readonly List<SubwayStation> Stations = new List<SubwayStation>(4);
        private static readonly List<string> Keys = new List<string>(4);
        private static readonly List<string> Names = new List<string>(4);

        /// <summary>How many stand now.</summary>
        public static int Count => Stations.Count;

        /// <summary>Changes whenever one is added or taken away: a reader that keeps a list compares it.</summary>
        public static int Stamp { get; private set; }

        /// <summary>A station of the mod's own joins: its name's locale key and its plain name for the log.</summary>
        public static void Add(SubwayStation station, string nameKey, string logName)
        {
            if (ReferenceEquals(station, null) || Stations.Contains(station))
            {
                return;
            }
            Stations.Add(station);
            Keys.Add(nameKey);
            Names.Add(logName);
            Stamp++;
        }

        /// <summary>It leaves. Safe with one that never joined.</summary>
        public static void Remove(SubwayStation station)
        {
            int at = IndexOf(station);
            if (at < 0)
            {
                return;
            }
            Stations.RemoveAt(at);
            Keys.RemoveAt(at);
            Names.RemoveAt(at);
            Stamp++;
        }

        /// <summary>None is left (the city went).</summary>
        public static void Clear()
        {
            if (Stations.Count > 0)
            {
                Stamp++;
            }
            Stations.Clear();
            Keys.Clear();
            Names.Clear();
        }

        private static int IndexOf(SubwayStation station)
        {
            for (int i = 0; i < Stations.Count; i++)
            {
                if (ReferenceEquals(Stations[i], station))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True for a station the mod stands: not one of the game's twenty.</summary>
        public static bool Is(SubwayStation station)
        {
            return !ReferenceEquals(station, null) && IndexOf(station) >= 0;
        }

        /// <summary>Its name's locale key, or null for a station of the game's.</summary>
        public static string KeyOf(SubwayStation station)
        {
            int at = ReferenceEquals(station, null) ? -1 : IndexOf(station);
            return at >= 0 ? Keys[at] : null;
        }

        /// <summary>A station's name for a log line: the mod's own by its plain name, the game's by its name's value.</summary>
        public static string LogName(SubwayStation station)
        {
            if (ReferenceEquals(station, null))
            {
                return "?";
            }
            int at = IndexOf(station);
            if (at >= 0)
            {
                return Names[at];
            }
            try
            {
                return StationValue(station);
            }
            catch (System.Exception e) when (Guard.Missing(e))
            {
                return station.name;   // a game without the name field: the station's own object name stands in
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string StationValue(SubwayStation station)
        {
            return station.stationName.ToString();
        }
    }
}
