using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What the loaded city is to the mod.</summary>
    public enum CityKind
    {
        Unknown,        // nothing could be read: the mod runs as it always has, as in New York
        NewYork,        // the New York the mod knows
        NewYorkGrown,   // New York with districts a free update added: still New York, the new districts get the mod's defaults
        Other           // another city (a DLC's): every feature that stands on New York's data stands down
    }

    /// <summary>Where a verdict comes from, strongest first.</summary>
    public enum CitySource
    {
        Buildings,   // the districts of the city's own buildings (CityManager's list): the game's data
        Scenes,      // the scenes loaded, before the city's buildings are listed
        Nothing      // neither could be read
    }

    /// <summary>What the city check found: the loaded city's districts by their buildings, its scenes, and the game's district list.</summary>
    public sealed class CityCensus
    {
        private readonly Dictionary<string, int> _byDistrict = new Dictionary<string, int>(16, StringComparer.Ordinal);

        public readonly List<string> Scenes = new List<string>(16);     // the names of the scenes loaded, as the engine gives them
        public readonly List<string> GameDistricts = new List<string>(16);   // every district the game's data knows (NeighborhoodHelper), in any city

        public int Buildings { get; private set; }

        public IReadOnlyDictionary<string, int> ByDistrict => _byDistrict;

        /// <summary>One building of the loaded city, by its district; an empty one counts as the global district.</summary>
        public void Add(string district)
        {
            string id = string.IsNullOrEmpty(district) ? CityDistricts.Global : district;
            _byDistrict.TryGetValue(id, out int count);
            _byDistrict[id] = count + 1;
            Buildings++;
        }

        public void Clear()
        {
            _byDistrict.Clear();
            Scenes.Clear();
            GameDistricts.Clear();
            Buildings = 0;
        }

        /// <summary>True when the city's buildings were read: the districts then decide.</summary>
        public bool HasBuildings => Buildings > 0;

        /// <summary>True for a district with a building in the loaded city; for every id while the buildings are not read.</summary>
        public bool Here(string district)
        {
            return !HasBuildings || (!string.IsNullOrEmpty(district) && _byDistrict.ContainsKey(district));
        }
    }

    /// <summary>The city check's answer, and its one log line.</summary>
    public sealed class CityVerdict
    {
        public CityKind Kind;
        public CitySource Source;
        public int KnownDistricts;                           // of the seven, how many the city has
        public readonly List<string> NewDistricts = new List<string>(4);    // districts of the city's buildings the mod does not know, ordinal order
        public readonly List<string> NewYorkScenes = new List<string>(4);   // the scenes of New York's own that are loaded
        public readonly List<string> OtherScenes = new List<string>(4);     // the scenes loaded that are no city's the mod knows
        public readonly List<string> Elsewhere = new List<string>(4);       // districts of the game's data with no building in this city

        /// <summary>True while the features that stand on New York's data run here.</summary>
        public bool NewYorkFeatures => Kind != CityKind.Other;
    }

    /// <summary>
    /// THE CITY CHECK's decision table, game-free (checked by EconomyCheck). At every city load (Core/CityCheck) the mod
    /// asks whether the loaded city is the New York it knows, from the game's own data:
    ///   1. the districts of the city's buildings, when CityManager has listed them: one of the seven districts of
    ///      <see cref="CityDistricts.NewYork"/> or more is New York, with any other district a free update's addition
    ///      (<see cref="CityKind.NewYorkGrown"/>); buildings and none of the seven is another city;
    ///   2. else the scenes loaded: one of New York's own (LowerManhattan, IndustryCity, TheHamptons) is New York; none of
    ///      them but a scene no city of the mod's has is another city;
    ///   3. else nothing is known, and the mod runs as it always has.
    /// The game's menus, the transition to a save and the blueprint creator are no city's scenes; the city's shared scenes
    /// (MainScene, Buildings, Streets and the rest, a season's decorations) say nothing alone. A season's scene the game
    /// adds later is a scene no city of the mod's has, so the scenes alone never decide against New York while the
    /// buildings can be read; they decide only behind the loading screen, before the buildings are listed, and the
    /// buildings decide again at once (Core/CityCheck).
    /// </summary>
    public static class CityRules
    {
        /// <summary>The scenes of New York's own (the game's GameScenes): one of them loaded is New York.</summary>
        public static readonly string[] NewYorkScenes = { "LowerManhattan", "IndustryCity", "TheHamptons" };

        /// <summary>The scenes every city of the game's loads, and the game's other scenes: they say nothing of the city.</summary>
        public static readonly string[] SharedScenes =
        {
            "MainScene", "UI", "Buildings", "ExteriorProps", "LightsAndPostprocessing", "Streets", "Indoor", "ChristmasStreetDecorations",
            "Intro", "MainMenu", "TransitionToSave", "BlueprintCreator", "DontDestroyOnLoad"
        };

        /// <summary>The verdict for what the census found (<paramref name="into"/> is filled and returned).</summary>
        public static CityVerdict Decide(CityCensus census, CityVerdict into)
        {
            into.Kind = CityKind.Unknown;
            into.Source = CitySource.Nothing;
            into.KnownDistricts = 0;
            into.NewDistricts.Clear();
            into.NewYorkScenes.Clear();
            into.OtherScenes.Clear();
            into.Elsewhere.Clear();
            if (census == null)
            {
                return into;
            }
            for (int i = 0; i < census.Scenes.Count; i++)
            {
                string scene = census.Scenes[i];
                if (string.IsNullOrEmpty(scene))
                {
                    continue;
                }
                if (Array.IndexOf(NewYorkScenes, scene) >= 0)
                {
                    AddOnce(into.NewYorkScenes, scene);
                }
                else if (Array.IndexOf(SharedScenes, scene) < 0)
                {
                    AddOnce(into.OtherScenes, scene);
                }
            }
            into.OtherScenes.Sort(StringComparer.Ordinal);
            for (int i = 0; i < census.GameDistricts.Count; i++)
            {
                string id = census.GameDistricts[i];
                if (census.HasBuildings && !string.IsNullOrEmpty(id) && id != CityDistricts.Global && !census.ByDistrict.ContainsKey(id))
                {
                    AddOnce(into.Elsewhere, id);
                }
            }
            into.Elsewhere.Sort(StringComparer.Ordinal);
            if (census.HasBuildings)
            {
                into.Source = CitySource.Buildings;
                foreach (KeyValuePair<string, int> district in census.ByDistrict)
                {
                    if (CityDistricts.Known(district.Key))
                    {
                        into.KnownDistricts++;
                    }
                    else if (district.Key != CityDistricts.Global)
                    {
                        into.NewDistricts.Add(district.Key);
                    }
                }
                into.NewDistricts.Sort(StringComparer.Ordinal);
                into.Kind = into.KnownDistricts == 0 ? CityKind.Other : into.NewDistricts.Count > 0 ? CityKind.NewYorkGrown : CityKind.NewYork;
                return into;
            }
            if (into.NewYorkScenes.Count > 0)
            {
                into.Source = CitySource.Scenes;
                into.Kind = CityKind.NewYork;
            }
            else if (into.OtherScenes.Count > 0)
            {
                into.Source = CitySource.Scenes;
                into.Kind = CityKind.Other;
            }
            return into;
        }

        /// <summary>
        /// The one log line for a verdict: "City check: what was found: New York features on" (or "off in this city"),
        /// in English for Player.log, numbers in the invariant culture. On a city load only.
        /// </summary>
        public static string Line(CityVerdict verdict, CityCensus census)
        {
            var text = new StringBuilder(256);
            text.Append("City check: ");
            switch (verdict.Kind)
            {
                case CityKind.NewYork:
                    text.Append("New York");
                    break;
                case CityKind.NewYorkGrown:
                    text.Append("New York with districts the mod does not know");
                    break;
                case CityKind.Other:
                    text.Append("another city");
                    break;
                default:
                    text.Append("the city could not be read");
                    break;
            }
            text.Append(" (");
            if (verdict.Source == CitySource.Buildings && census != null)
            {
                text.Append(verdict.KnownDistricts.ToString(CultureInfo.InvariantCulture)).Append(" of ")
                    .Append(CityDistricts.NewYork.Length.ToString(CultureInfo.InvariantCulture)).Append(" districts of New York among ")
                    .Append(census.Buildings.ToString(CultureInfo.InvariantCulture)).Append(" buildings");
                if (verdict.NewDistricts.Count > 0)
                {
                    text.Append("; districts it does not know: ");
                    for (int i = 0; i < verdict.NewDistricts.Count; i++)
                    {
                        census.ByDistrict.TryGetValue(verdict.NewDistricts[i], out int count);
                        text.Append(i > 0 ? ", " : string.Empty).Append(verdict.NewDistricts[i]).Append(" (")
                            .Append(count.ToString(CultureInfo.InvariantCulture)).Append(')');
                    }
                }
            }
            else if (verdict.Source == CitySource.Scenes)
            {
                text.Append("from the scenes alone, before the city's buildings are listed");
            }
            else
            {
                text.Append("no building listed and no scene of a city known");
            }
            Scenes(text, "; New York's scenes: ", verdict.NewYorkScenes);
            Scenes(text, "; other scenes: ", verdict.OtherScenes);
            Scenes(text, "; districts of the game's data not in this city: ", verdict.Elsewhere);
            text.Append("): New York features ").Append(verdict.NewYorkFeatures ? "on" : "off in this city");
            if (verdict.Kind == CityKind.NewYorkGrown)
            {
                text.Append("; the districts it does not know get the mod's defaults");
            }
            else if (verdict.Kind == CityKind.Unknown)
            {
                text.Append(", as the mod has always run");
            }
            return text.Append('.').ToString();
        }

        private static void Scenes(StringBuilder text, string head, List<string> names)
        {
            if (names.Count == 0)
            {
                return;
            }
            text.Append(head);
            for (int i = 0; i < names.Count; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(names[i]);
            }
        }

        private static void AddOnce(List<string> list, string item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }
    }
}
