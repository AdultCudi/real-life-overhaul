using System;
using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a city policy sets now, for any other feature to read: free to read, and neutral while civic life is off or the lever stands where the city came.</summary>
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

        /// <summary>What city policy adds to (or takes off) each parking ticket now, at today's prices; 0 with civic life off.</summary>
        public static int TicketExtra => CivicCatalogue.TicketExtraNow(Position(Lever.Parking));
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

    /// <summary>What civic life asks of features outside this folder: delegates with defaults that work without them, set by Wiring/CivicLinks.</summary>
    public static class CivicBridges
    {
        /// <summary>True if the player has a criminal record. Default: nobody has.</summary>
        public static Func<bool> CriminalRecord;

        /// <summary>The player's standing in the city, -100 to 100. Default: 0.</summary>
        public static Func<int> Reputation;

        /// <summary>What the player's following on social media adds to their endorsement, before the cap. Default: nothing.</summary>
        public static Func<float> Following;

        /// <summary>Social media's mood about the city's running, points of the mayor's approval a day (one at most either way). Default: none.</summary>
        public static Func<float> Mood;

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

        /// <summary>The raise agreed for an employee (by the game's id) on top of the game's wage, money an hour. Default: none (Economy/Company sets it).</summary>
        public static Func<string, float> Raise;

        /// <summary>The bank entry a day pass gives the game's subway fare back under: such a ride is not a fare paid, so the policy's change is not charged on it.</summary>
        public const string FareGivenBack = "reallife_transaction_subway_pass";

        // What running for office asks.

        /// <summary>The city's economy, as the cycle stands: 0 a boom, 1 steady, 2 a slowdown, 3 a recession. Default: steady.</summary>
        public static Func<int> Economy;

        /// <summary>The player's weight with the council, 0 to 100 (Standing's influence). Default: none.</summary>
        public static Func<int> Influence;

        /// <summary>How well the city knows the player, 0 to 100 (Standing's recognition). Default: nobody knows them.</summary>
        public static Func<int> Recognition;

        /// <summary>What the city makes of the player's integrity, 0 to 100 (Standing). Default: 50.</summary>
        public static Func<int> Integrity;

        /// <summary>A conviction that still counts. Default: none.</summary>
        public static Func<bool> Convicted;

        /// <summary>A case before the court. Default: none.</summary>
        public static Func<bool> CaseOpen;

        /// <summary>The player lets property to tenants (a vote on rent is then a conflict of interest). Default: no.</summary>
        public static Func<bool> Landlord;

        /// <summary>Something the player did in politics moves Standing: integrity points, and tenths of a point of recognition from the press. Default: nothing.</summary>
        public static Action<int, int> StandingMoved;

        /// <summary>Opens a case in court on a summons: 0 campaign finance, 1 misconduct in public office; the amount involved. Its number, or 0 with no court.</summary>
        public static Func<int, int, int> FileCase;

        /// <summary>A moment of the campaign or the office the player may post about on social media: what (CivicOffice.Post*) and a name. Default: none.</summary>
        public static Action<int, string> CampaignPost;

        internal static int Cycle()
        {
            return Ask(Economy, 1, 0, 3);
        }

        internal static int Ask(Func<int> question, int fallback, int min, int max)
        {
            try
            {
                if (question != null)
                {
                    int answer = question();
                    return answer < min ? min : answer > max ? max : answer;
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            return fallback;
        }

        internal static bool Ask(Func<bool> question)
        {
            try
            {
                return question != null && question();
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

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

        /// <summary>Set by the director while the host of a Going Public session runs the city's hours: the city's stories are heard as well, for the guests' papers.</summary>
        internal static Action<string, string> Reported;

        /// <summary>A story for the paper if there is one, else the headline as a notification.</summary>
        internal static void Report(string headlineKey, string bodyKey)
        {
            Reported?.Invoke(headlineKey, bodyKey);
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
