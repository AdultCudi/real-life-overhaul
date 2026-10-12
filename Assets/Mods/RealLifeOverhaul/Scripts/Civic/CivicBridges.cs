using System;
using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a city policy currently sets, for any other feature to read. Every value is safe to
    /// read at any time, costs nothing, and is the neutral value (1, or the stock hour) whenever
    /// civic life is off, not loaded, or the lever stands where the city came.
    ///
    /// THE LINES OTHER FEATURES CALL
    ///   Law / Crime     fine = Mathf.RoundToInt(fine * CivicPolicy.FineScale);          and once at start-up: CivicBridges.FinesScaledAtSource = true;
    ///                   chanceSeen *= CivicPolicy.EnforcementScale;
    ///   Police presence nothing: Settings.PolicePresence and Settings.PatrolFrequency are already scaled (and put back).
    ///   Transport       headway /= CivicPolicy.ServiceScale;   (bus fares are not touched: the policy changes the subway fare only, which civic life charges itself;
    ///                   a feature that applied the subway fare change itself would set CivicBridges.FaresScaledAtSource = true)
    ///   Events          chance *= CivicPolicy.EventScale;
    ///   Night life      if (hour >= CivicPolicy.ClosingHour &amp;&amp; hour &lt; 6) { closed }
    ///   Business        nothing to call; read CivicPolicy.Position(Lever.Grants) if it wants to mention it.
    /// </summary>
    public static class CivicPolicy
    {
        private static readonly int[] None = new int[CivicCatalogue.Levers];
        private static int[] _levers = None;

        /// <summary>Raised (on the main thread, from an hourly tick or a button) when any lever has moved.</summary>
        public static event Action Changed;

        /// <summary>Where a lever stands now: -1, 0 or 1.</summary>
        public static int Position(Lever lever)
        {
            return _levers[(int)lever];
        }

        public static float PoliceScale => CivicCatalogue.PoliceScale(Position(Lever.Policing));
        public static float CrimeScale => CivicCatalogue.CrimeScale(Position(Lever.Policing), Position(Lever.NightLife));
        public static float FineScale => CivicCatalogue.FineScale(Position(Lever.Traffic));
        /// <summary>How much more (or less) likely a traffic offence is to be noticed.</summary>
        public static float EnforcementScale => Position(Lever.Traffic) > 0 ? 1.3f : Position(Lever.Traffic) < 0 ? 0.75f : 1f;
        public static float AccidentScale => CivicCatalogue.AccidentScale(Position(Lever.Traffic));
        public static float ServiceScale => CivicCatalogue.ServiceScale(Position(Lever.Transit));
        public static float EventScale => CivicCatalogue.EventScale(Position(Lever.Events));
        public static int ClosingHour => CivicCatalogue.ClosingHour(Position(Lever.NightLife));
        public static float SiteSpeed => CivicCatalogue.SiteSpeed(Position(Lever.Permits));

        /// <summary>Civic life's own: the levers as the effects now apply them (the state's array), or null for none.</summary>
        internal static void Publish(int[] levers)
        {
            _levers = levers ?? None;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }
    }

    /// <summary>
    /// What civic life asks of features that are not in this tree, each with a default that
    /// works without them. A feature that exists sets the delegate once when it starts:
    ///
    ///   Law     CivicBridges.CriminalRecord = () => LawRecord.Convictions > 0;
    ///           CivicBridges.Reputation = () => Reputation.Score;                  // -100 to 100
    ///           CivicBridges.NoteCrime();                                          // when a crime happens in the player's city
    ///   Events  CivicBridges.LargeScene = () => EventDirector.IsActive;            // a street event is on: no rally, no site crew beside it
    ///           CivicBridges.WeatherStopsWork = () => Weather.IsStormOrRain;
    ///   Social  CivicBridges.News = (headlineKey, bodyKey) => Social.News.Report(headlineKey, bodyKey);
    ///   Jobs    CivicBridges.Labouring = site => JobDirector.Instance.TakeLabouring(site);   // starts a labouring shift at a place; false if it cannot
    ///   City    CivicBridges.CityPeopleOut = () => CityLifeDirector.Instance.PeopleOut;      // so that civic life's own cast yields to the cap of eight
    ///   Traffic CivicBridges.NoteAccident();                                       // when a traffic accident happens
    /// </summary>
    public static class CivicBridges
    {
        /// <summary>True if the player has a criminal record. Default: nobody has.</summary>
        public static Func<bool> CriminalRecord;

        /// <summary>The player's standing in the city, -100 to 100. Default: 0.</summary>
        public static Func<int> Reputation;

        /// <summary>True while something large is on in the street near the player. Default: an accident scene or a crime scene of this mod.</summary>
        public static Func<bool> LargeScene;

        /// <summary>True while the weather stops outdoor work. Default: the game's rain.</summary>
        public static Func<bool> WeatherStopsWork;

        /// <summary>Prints a story (two locale keys: headline, body). Default: the headline as a notification.</summary>
        public static Action<string, string> News;

        /// <summary>Starts a labouring shift at a site. Default: none, and the offer is not made.</summary>
        public static Func<Vector3, bool> Labouring;

        /// <summary>How many of city life's people are out. Default: unknown (0).</summary>
        public static Func<int> CityPeopleOut;

        /// <summary>Set by a feature that multiplies its own fines by CivicPolicy.FineScale: civic life then leaves fines alone.</summary>
        public static bool FinesScaledAtSource;

        /// <summary>Set by a feature that applies the subway fare change itself: civic life then leaves subway fares alone.</summary>
        public static bool FaresScaledAtSource;

        /// <summary>
        /// The bank entry a feature gives the game's subway fare back under (a ride on a day pass: Transport/SubwayJourney).
        /// Civic life does not count such a ride as a fare paid, so the policy's change to the fare is not charged on it.
        /// </summary>
        public const string FareGivenBack = "reallife_transaction_subway_pass";

        /// <summary>Crimes and accidents other features have told civic life about since it last looked.</summary>
        internal static int Crimes, Accidents;

        public static void NoteCrime()
        {
            Crimes++;
        }

        public static void NoteAccident()
        {
            Accidents++;
        }

        internal static bool HasRecord()
        {
            try
            {
                return CriminalRecord != null && CriminalRecord();
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static int Standing()
        {
            try
            {
                return Reputation != null ? Mathf.Clamp(Reputation(), -100, 100) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        internal static bool SceneOn()
        {
            try
            {
                if (LargeScene != null && LargeScene())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // a bridge that faults is as good as none
            }
            return TrafficAccidents.IsActive || CrimeDirector.IsActive;
        }

        internal static bool WorkStopped()
        {
            try
            {
                if (WeatherStopsWork != null)
                {
                    return WeatherStopsWork();
                }
            }
            catch (Exception)
            {
                // fall through to the game's rain
            }
            return RainHelper.isRaining;
        }

        internal static int PeopleOut()
        {
            try
            {
                return CityPeopleOut != null ? CityPeopleOut() : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>A story for the paper if there is one, else the headline as a notification.</summary>
        internal static void Report(string headlineKey, string bodyKey)
        {
            try
            {
                if (News != null)
                {
                    News(headlineKey, bodyKey);
                    return;
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            CivicText.Toast(UI.Notification.NotificationType.Info, headlineKey, 10f);
        }
    }
}
