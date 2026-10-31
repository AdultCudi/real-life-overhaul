using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's demand for the mod's own businesses (Population/Demand): measured in the day's turn right after the population's own day, in the same
    /// slices, so every factor is in place before the day's figures are used and told to the guests; read back from the save at a load, so the factors stand
    /// the moment the city does; written beside the population's line; taken from the host's part on a Going Public guest; and its test on the options page.
    /// COST: nothing per frame; the measure about a fifth of the population's own day, in its slices.
    /// </summary>
    public sealed partial class PopulationDirector
    {
        private readonly PopDemandJob _demandJob = new PopDemandJob();
        private PopDemandState _demand;            // this player's own; null while not running
        private PopDemandState _guestDemand;       // a guest's copy of the host's
        private bool _demandBegun;                 // the day's measure is under way

        // A step of the day's measure, after the population's own day; true when done (the factors in use from then).
        private bool DemandStep(ref WorkSlice slice)
        {
            PopulationState s = _sim.State;
            if (s == null)
            {
                return true;
            }
            if (!_demandBegun)
            {
                if (_demand == null)
                {
                    _demand = new PopDemandState();
                }
                _demandJob.Begin(s, _sim.Demand, _sim.Pulls, _demand);
                _demandBegun = true;
            }
            if (!_demandJob.Step(ref slice))
            {
                return false;
            }
            _demandBegun = false;
            CityDemand.Live = _demand;
            if (Verbose)
            {
                Logger?.Info("Population check (the businesses' demand, day " + _demand.Day + "): " + (_demand.AnchorDay >= 0 ? "anchored on day " + _demand.AnchorDay : "not anchored yet, every business keeps its own figures")
                             + "; hotels " + _demand.Factors[(int)PopDemand.HotelGuests] + ", savers " + _demand.Factors[(int)PopDemand.SaversPeople] + ", tenants "
                             + _demand.Factors[(int)PopDemand.Tenants] + ", council " + _demand.Factors[(int)PopDemand.CouncilRevenue] + " per mille of their first day: "
                             + (Within(_demand) ? "PASS" : "FAIL, a factor past its bounds"));
            }
            return true;
        }

        private static bool Within(PopDemandState m)
        {
            for (int i = 0; i < m.Factors.Length; i++)
            {
                if (m.Factors[i] < 0 || m.Factors[i] > PopDemandCatalogue.FactorMost)
                {
                    return false;
                }
            }
            return true;
        }

        // The population in use: a load's demand read from its line (the factors stand at once), unless the line is of another population; a city just generated
        // keeps the demand its first day measured.
        private void BindDemand(PopulationState s, bool generated)
        {
            if (!generated || _demand == null)
            {
                Dictionary<string, string> data = PopulationGame.Data();
                PopDemandState read = data != null ? PopDemandStore.Load(data, out _) : null;
                _demand = read != null && read.Seed == s.Seed && read.Generated == s.Generated ? read : _demand != null && _demand.Seed == s.Seed && _demand.Generated == s.Generated ? _demand : new PopDemandState();
            }
            CityDemand.Live = _demand;
        }

        // The demand put away with the population (its line stays in the save as it was written).
        private void DropDemand()
        {
            _demandBegun = false;
            _demand = null;
            _guestDemand = null;
            CityDemand.Live = null;
        }

        // The demand's line beside the population's, at a save.
        private void SaveDemand(Dictionary<string, string> data)
        {
            if (_demand != null && !_demand.Guest)
            {
                data[PopDemandStore.Key] = PopDemandStore.Write(_demand);
            }
        }

        // A guest's copy of the host's demand, to be filled from the host's part.
        private PopDemandState GuestDemand()
        {
            if (_guestDemand == null)
            {
                _guestDemand = new PopDemandState();
            }
            return _guestDemand;
        }

        // A test's days lived at once: the last one measured whole, so the factors follow.
        private void DemandWhole()
        {
            if (_sim.State == null)
            {
                return;
            }
            if (_demand == null)
            {
                _demand = new PopDemandState();
            }
            _demandBegun = false;
            _demandJob.Begin(_sim.State, _sim.Demand, _sim.Pulls, _demand);
            _demandJob.RunAll();
            CityDemand.Live = _demand;
        }

        /// <summary>Options, Tests: what the city's people bring each of the mod's own businesses now, against its first day, in a notice and the log.</summary>
        public void TestDemand()
        {
            if (!Settings.LivingPopulation)
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_off", null);
                return;
            }
            PopDemandState m = CityDemand.Live;
            if (m == null || m.Day < 0)
            {
                PopulationText.Toast(NotificationType.Warning, "reallife_pop_note_not_ready", null);
                return;
            }
            if (m.AnchorDay < 0)
            {
                PopulationText.Toast(NotificationType.Info, "reallife_pop_dem_note_settling", null);
                return;
            }
            PopulationView v = PopDemandViews.Demands(m);
            var data = new Dictionary<string, string>
            {
                { "first", v.Rows.Count > 0 ? PopulationText.T(v.Rows[0].Title) : string.Empty }, { "first_change", v.Rows.Count > 0 ? PopulationText.Permille(v.Rows[0].Value) : string.Empty },
                { "second", v.Rows.Count > 1 ? PopulationText.T(v.Rows[1].Title) : string.Empty }, { "second_change", v.Rows.Count > 1 ? PopulationText.Permille(v.Rows[1].Value) : string.Empty },
                { "day", m.AnchorDay.ToString(CultureInfo.InvariantCulture) }
            };
            PopulationText.Toast(NotificationType.Info, "reallife_pop_dem_note_shown", data);
            var log = new StringBuilder(512);
            for (int g = 0; g < PopDemandViews.Groups.Length; g++)
            {
                PopulationView one = PopDemandViews.Demand(m, g);
                log.Append(g > 0 ? "; " : string.Empty).Append(one.Title).Append(' ');
                for (int i = 0; i < one.Hero.Count; i++)
                {
                    log.Append(i > 0 ? "/" : string.Empty).Append(one.Hero[i].Value.ToString(CultureInfo.InvariantCulture));
                }
            }
            Logger?.Info("Living population test: the businesses' demand on day " + m.Day + " (anchored on day " + m.AnchorDay + "), per mille of the first day: " + log + ".");
        }
    }
}
