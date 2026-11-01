using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Population
{
    /// <summary>The population's tests on the options page: the city generated now, a month lived at once, and a household picked at random and shown.</summary>
    public sealed partial class PopulationDirector
    {
        private bool _fresh;   // the next start generates the city again, whatever the save's line holds

        /// <summary>Options, Tests: the city's people generated again from its buildings now (the save's line is replaced at the next save).</summary>
        public void TestGenerate()
        {
            if (!Ready("generate now"))
            {
                return;
            }
            StandDown("the test generates the city again", false);
            _fresh = true;
            _starting = false;
            PopulationText.Toast(NotificationType.Info, "reallife_pop_note_generating", null);
            Logger?.Info("Living population test: the city's people are generated again from its buildings, in slices.");
        }

        /// <summary>
        /// Options, Tests: a month of the population's days lived at once (births, deaths, moves, the month's rents and pay). The population lives ahead of the
        /// clock and waits for the game's days to catch up.
        /// </summary>
        public void TestMonth()
        {
            if (!Ready("a month passes") || _state == null)
            {
                return;
            }
            DaySpread.Shared.Flush(this);
            PopulationState s = _state;
            int days = GenerateRules.MonthDays(s.YearDays);
            int people = s.People.Live, births = 0, deaths = 0, moved = 0;
            float started = UnityEngine.Time.realtimeSinceStartup;
            for (int i = 0; i < days; i++)
            {
                int month = s.MonthNumber;
                _sim.BeginDay(s.Day + 1, Inputs(s), new List<BusinessSeed>(_seeds));
                _sim.RunAll();
                births += _sim.Life.Births;
                deaths += _sim.Life.Deaths;
                if (s.MonthNumber != month)
                {
                    moved += s.Region.MovedInLast + s.Region.MovedOutLast;
                }
            }
            DemandWhole();
            UseFigures();
            PopulationGame.RedrawMine();
            SharedCity.Changed(PopulationShare.Part);
            PopulationText.Toast(NotificationType.Info, "reallife_pop_note_month", new Dictionary<string, string>
            {
                { "days", days.ToString(CultureInfo.InvariantCulture) }, { "births", births.ToString(CultureInfo.InvariantCulture) },
                { "deaths", deaths.ToString(CultureInfo.InvariantCulture) }, { "people", s.People.Live.ToString(CultureInfo.InvariantCulture) }
            });
            Logger?.Info("Living population test: " + days + " day(s) lived at once in " + ((UnityEngine.Time.realtimeSinceStartup - started) * 1000f).ToString("0", CultureInfo.InvariantCulture)
                         + " ms: people " + people + " to " + s.People.Live + ", " + births + " births, " + deaths + " deaths, " + moved + " households came or left; the population is on day "
                         + s.Day + ", ahead of the game's " + Save.Day + ".");
        }

        /// <summary>Options, Tests: a household picked at random, and what it earned, paid, spent and saved this month in a notice.</summary>
        public void TestHousehold()
        {
            if (!Ready("show a random household"))
            {
                return;
            }
            PopulationState s = CityPopulation.Live;
            if (s == null || s.Households.Live == 0)
            {
                PopulationText.Toast(NotificationType.Warning, CityPopulation.Following ? "reallife_pop_note_guest" : "reallife_pop_note_not_ready", null);
                return;
            }
            int h = -1;
            for (int tries = 0; tries < 64 && h < 0; tries++)
            {
                int pick = UnityEngine.Random.Range(0, s.Households.Count);   // a test's own pick, never shared
                h = s.Households.Alive(pick) && (s.Households.Flags[pick] & HouseholdFlags.Away) == 0 ? pick : -1;
            }
            if (h < 0)
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_not_ready", null);
                return;
            }
            PopulationView v = PopulationViews.Household(s, CityPopulation.Pools, h);
            int b = s.Households.Building[h];
            PopulationText.Toast(NotificationType.Info, "reallife_pop_note_household", new Dictionary<string, string>
            {
                { "name", PopulationGame.NameOf(v.NameSeed, v.Female) }, { "people", s.Households.Size[h].ToString(CultureInfo.InvariantCulture) },
                { "address", PopulationText.Address(s, b) }, { "earned", PopulationText.Money(v.Hero[0].Value) }, { "tax", PopulationText.Money(v.Hero[1].Value) },
                { "spent", PopulationText.Money(v.Hero[2].Value) }, { "saved", PopulationText.Money(v.Hero[3].Value) }
            });
            Logger?.Info("Living population test: household " + s.Households.Id[h] + " at " + PopulationText.Address(s, b) + ": " + PopulationText.Describe(v));
        }

        // True when a test can run now; else why not in a notice, one sentence for each cause.
        private bool Ready(string test)
        {
            if (!Settings.LivingPopulation)
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_off", null);
                return false;
            }
            if (Save == null || _broken)
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_not_ready", null);
                Logger?.Info("Living population test '" + test + "': " + (_broken ? "switched off after an error this session" : "no save loaded") + ".");
                return false;
            }
            if (SharedCity.Follows(PopulationShare.Part))
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_guest", null);
                return false;
            }
            return true;
        }
    }
}
