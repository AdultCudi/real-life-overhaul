using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BAModAPI;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Economy;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's population (plans/tech-overhaul/POPULATION.md; Living population, the overhaul's master option): owns its state and its timing. At the
    /// first second after a load (its seat on the start line) it reads its line from the save on a worker and binds it to the city as it stands, or, for a
    /// save without one, generates the city's people from its buildings (in slices, the first day lived at once so every business is anchored at the
    /// game's own count). Each game day it lives the city's day in slices of about a millisecond (Core/DayPump), after asking what people hear of each
    /// business and linking the player's staff to residents; then it moves the game's customers (Population/PopulationGame.Calculators), feeds the city's
    /// economy its districts' incomes, and tells the guests of a Going Public session. At a save it writes its line in pieces on the save's workers. A
    /// guest follows the host's population and lives none of its own. With the option off, or in a city it does not know, nothing of it is in the game.
    /// No Update. COST: nothing per frame; a few comparisons a second, the day's work spread over its frames.
    /// </summary>
    public sealed partial class PopulationDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static PopulationDirector Instance { get; private set; }

        private const double StepMs = 1.0;   // milliseconds of the population's work a step (of DayPump's two a frame)

        private readonly PopulationSim _sim = new PopulationSim();
        private readonly List<CityBuilding> _buildings = new List<CityBuilding>(2048);
        private readonly List<PopDistrict> _districts = new List<PopDistrict>(12);
        private readonly List<BusinessSeed> _seeds = new List<BusinessSeed>(2048);
        private readonly List<StaffRef> _staff = new List<StaffRef>(64);
        private readonly Dictionary<string, int> _staffAt = new Dictionary<string, int>(32, StringComparer.Ordinal);
        private readonly List<LinkAsk> _asks = new List<LinkAsk>(32);
        private PopulationState _state;            // the population in use; null while not running
        private PopulationState _guest;            // a guest's copy of the host's figures
        private Action _onDay, _onSave;
        private Func<bool> _dayStep;
        private bool _broken;
        private bool _starting;                    // the save's line is being read on a worker
        private volatile PopulationState _read;    // what the worker read
        private volatile bool _readDone;
        private string _readWhy;
        private int _attentionCursor = -1;         // the day's look at what people hear of each business, before the sim's turn (-2: the businesses read first; -1: none)
        private int _jobDay = -1;                  // the day the job under way lives
        private bool _generating;
        private DistrictSaveZip _zip;
        private bool _checkedCalculators;

        public bool Running => _state != null && !_broken;
        internal PopulationState State => _state;
        internal PopulationSim Sim => _sim;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;
        private static readonly int HotDay = HotSpots.Slot("Population: the day's turn");

        private void Awake()
        {
            Instance = this;
            _onDay = OnDay;
            _onSave = OnSave;
            _dayStep = DayStep;
            StartCoroutine(Guard.Routine(Tick()));   // its first second after a load waits for its seat (Core/StartLine)
            EconomyDirector.AfterDay += _onDay;
            SharedCity.Register(new SharedCityPart
            {
                Id = PopulationShare.Part, Name = "population",
                Ready = () => Running || _guest != null,
                Write = () => _state != null && !SharedCity.Follows(PopulationShare.Part) ? PopulationShare.Write(_state, _demand) : null,
                Apply = ApplyHost,
                Left = () =>
                {
                    _guest = null;
                    _guestDemand = null;
                    CityDemand.Live = null;
                    CityPopulation.Following = false;
                    Logger?.Info("Living population: the host's population is no longer followed; this player's own city lives again from its next day.");
                }
            });
        }

        private void OnDestroy()
        {
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            DaySpread.Shared.Drop(this);
            if (Instance != this)
            {
                return;
            }
            StandDown("the city is unloading", false);
            PopulationAttention.Clear();
            Instance = null;
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                while (StartLine.Holds(StartSeats.Population))
                {
                    yield return null;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Fault("the timer", e);
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Population: PopulationDirector.Tick");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.LivingPopulation || !CityCheck.NewYork)
            {
                if (Running || _guest != null)
                {
                    StandDown(!Settings.LivingPopulation ? "the option is off" : "a city it does not know", true);
                }
                return;
            }
            if (SharedCity.Follows(PopulationShare.Part))
            {
                return;   // a guest: the host's figures come with its updates
            }
            SaveHook.Keep(_onSave);
            if (_state == null)
            {
                StartUp();
                return;
            }
            if (!_sim.Busy && _attentionCursor == -1 && Save.Day > _state.Day)
            {
                BeginDay(Save.Day);   // a day the game's event did not bring (a load at midnight, a skip)
            }
        }

        // The first second after a load: the save's line read on a worker, then bound to the city; or the city generated.
        private void StartUp()
        {
            if (_sim.Busy)
            {
                return;
            }
            if (!_starting)
            {
                if (!PopulationGame.ReadCity(_buildings, _districts))
                {
                    return;   // the city's buildings are not there yet: the next second
                }
                Dictionary<string, string> data = PopulationGame.Data();
                string line = !_fresh && data != null && data.TryGetValue(PopulationStore.Key, out string text) ? text : null;
                _fresh = false;
                _starting = true;
                _readDone = false;
                _read = null;
                if (string.IsNullOrEmpty(line))
                {
                    _readDone = true;
                    _readWhy = "no line in this save";
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            _read = PopulationStore.Decode(line, out int version);
                            _readWhy = _read != null ? "version " + version : "a line it cannot read (kept as it was)";
                        }
                        catch (Exception e)
                        {
                            _readWhy = e.GetType().Name;
                        }
                        _readDone = true;
                    });
                }
                return;
            }
            if (!_readDone)
            {
                return;   // the worker is still reading
            }
            _starting = false;
            PopulationState read = _read;
            _read = null;
            PopulationGame.ReadSeeds(_seeds, Staff());
            int seed = CitySeed();
            if (GenerateRules.Outdated(read))
            {
                _readWhy += ", a city of " + read.Households.Live + " households counted by measure " + read.Measure + " (now " + PopulationCatalogue.MeasureVersion + ")";
                seed = read.Seed != 0 ? read.Seed : seed;
                read = null;
            }
            if (read != null)
            {
                int away = CityTable.Rebind(read, new List<CityBuilding>(_buildings), new List<PopDistrict>(_districts));
                LabourRules.Setup(read, _seeds);
                _sim.State = read;
                _sim.Measure = MeasureRules.Measure(read.Buildings, read.Districts, null);
                CityTable.Middles(read);
                Begin(read, false);
                Dictionary<string, string> saved = PopulationGame.Data();
                if (saved != null && saved.TryGetValue(HoldRules.Key, out string heldLine))
                {
                    HoldRules.Read(read, heldLine);
                }
                Logger?.Info("Living population: loaded (" + _readWhy + ") on day " + Save.Day + ": " + read.Households.Live + " households, " + read.People.Live + " people in "
                             + read.Buildings.Count + " buildings" + (away > 0 ? "; " + away + " household(s) away, their building not standing at this load" : string.Empty) + ".");
                return;
            }
            var fresh = new PopulationState();
            fresh.Districts.AddRange(_districts);
            fresh.Buildings.AddRange(_buildings);
            CityTable.Sort(fresh.Buildings);
            fresh.IndexBuildings();
            CityTable.Middles(fresh);
            _generating = true;
            _sim.BeginGenerate(fresh, seed, Save.Day, PopulationGame.YearDays, new List<BusinessSeed>(_seeds));
            _jobDay = Save.Day;
            DaySpread.Shared.Add(this, "Population: the city generated", _dayStep, Time.frameCount, HotDay);
            Logger?.Info("Living population: " + _readWhy + "; the city's people are generated from its " + fresh.Buildings.Count + " buildings with seed " + seed + ", in slices.");
        }

        // The city's seed: the city economy's own (a Going Public guest has the host's), or a seed of this save's own when that is off.
        private int CitySeed()
        {
            CityState city = CityEconomy.Live;
            if (city != null && city.Seed != 0)
            {
                return city.Seed;
            }
            return new System.Random().Next(1, int.MaxValue);   // kept in the population's line from now on
        }

        private Dictionary<string, int> Staff()
        {
            PopulationGame.ReadStaff(_staff, _staffAt);
            return _staffAt;
        }

        // The population in use from now: the readers' interface, the customers' figures, the save's line kept.
        private void Begin(PopulationState s, bool generated)
        {
            _state = s;
            CityPopulation.Live = s;
            CityPopulation.Pools = _sim.Demand;
            CityPopulation.Following = false;
            BindDemand(s, generated);
            UseFigures();   // a load's day is lived already: its takings are in the line, its pools (a household's part) come with the next day's turn
        }

        // After a job: the customers' figures, the city economy's districts, the guests, and the readers.
        private void UseFigures()
        {
            PopulationState s = _state;
            if (s == null)
            {
                return;
            }
            PopulationCustomers.Use(s, true);
            CityPopulation.PullTable = _sim.Pulls;
            if (!_checkedCalculators && PopulationCustomers.On)
            {
                _checkedCalculators = true;
                CheckCalculators();
            }
        }

        // The self-check of the customers' count: the factory hands out the mod's copies.
        private void CheckCalculators()
        {
            BuildingRegistration any = null;
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count && any == null; i++)
            {
                any = all[i] != null && !string.IsNullOrEmpty(all[i].businessTypeName) && PopulationCatalogue.CategoryOfType(all[i].businessTypeName) >= 0 ? all[i] : null;
            }
            bool serves = any == null || PopulationGame.CalculatorsServe(any);
            if (!serves)
            {
                PopulationCustomers.Stop();
                Logger?.Warn("Living population: the game chose its customer calculators before the mod's start could give it its own; the game's own customers stand this session.");
            }
            if (Verbose)
            {
                Logger?.Info("Population check (the customers' count at the load): the factory hands out the mod's calculators: " + (serves ? "PASS" : "FAIL, the game's own stand"));
            }
        }

        private static DemandInputs Inputs(PopulationState s)
        {
            int districts = s.Districts.Count;
            var bears = new int[districts];
            for (int d = 0; d < districts; d++)
            {
                bears[d] = CityEconomy.Bears(s.Districts[d].Id);
            }
            CityState city = CityEconomy.Live;
            return new DemandInputs
            {
                BaseBp = (int)Math.Round(CityEconomy.BasePoints * 100f), Mood = city != null ? CityModel.Spend(city.Cycle, CityEconomy.Level) : 1000,
                Visitors = city != null ? CityModel.Spend(city.Cycle, CityEconomy.Level) : 1000, Bears = bears
            };
        }

        // After the game's daily run: the day's turn queued.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotDay);
            if (!Running || Save == null || SharedCity.Follows(PopulationShare.Part))
            {
                return;
            }
            try
            {
                BeginDay(Save.Day);
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private void BeginDay(int day)
        {
            DaySpread.Shared.Flush(this);   // a turn still owed to an earlier day is finished first
            if (_state == null || day <= _state.Day)   // a day is never lived twice
            {
                return;
            }
            HoldRules.Expire(_state, day);
            _jobDay = day;
            _attentionCursor = -2;   // the frame the day turns in only queues the turn: the businesses are read in its first step
            DaySpread.Shared.Add(this, "Population: the day's turn", _dayStep, Time.frameCount, HotDay);
        }

        // A step of the job under way: the businesses read, what people hear of each, then the staff linked, then the sim's slices; true when done.
        private bool DayStep()
        {
            if (_broken || Save == null)
            {
                return true;
            }
            try
            {
                if (_attentionCursor == -2)
                {
                    PopulationGame.ReadSeeds(_seeds, Staff());   // the businesses as they stand: one pass over the game's registrations
                    _attentionCursor = GenerateRules.MonthOf(_jobDay, _state.YearDays) != _state.MonthNumber ? -3 : 0;
                    return false;
                }
                if (_attentionCursor == -3)
                {
                    Rescan();   // a month turns: the buildings finished since, and the mod's land left
                    _attentionCursor = 0;
                    return false;
                }
                if (_attentionCursor >= 0)
                {
                    Attention();
                    return false;
                }
                WorkSlice slice = WorkSlice.Of(StepMs, 1);
                if (!_sim.Step(ref slice) || !DemandStep(ref slice))
                {
                    return false;   // the population's own day first, then what it brings the mod's own businesses
                }
                JobDone();
                return true;
            }
            catch (Exception e)
            {
                Fault("the day's turn", e);
                return true;
            }
        }

        private readonly List<CityBuilding> _scan = new List<CityBuilding>(2048);

        // The month's look at the city as it stands (POPULATION.md section 14): a building finished, demolished, extended or converted since joins the
        // table as a load binds it (the households of a flat gone look for another at the month's moves), and each district's own land left for homes
        // is measured from the construction game. One pass over the game's buildings and a lookup each; binding again only when one differs.
        private void Rescan()
        {
            PopulationState s = _state;
            if (s == null || _sim.Busy)
            {
                return;
            }
            if (PopulationGame.ReadCity(_buildings, _districts))
            {
                _scan.Clear();
                for (int i = 0; i < _buildings.Count; i++)
                {
                    CityBuilding b = _buildings[i];
                    int d = b.District >= 0 && b.District < _districts.Count ? s.DistrictOf(_districts[b.District].Id) : -1;
                    if (d < 0)
                    {
                        continue;   // a district the table does not know joins at the next load
                    }
                    b.District = d;
                    _scan.Add(b);
                }
                if (GrowthRules.Differs(s, _scan))
                {
                    int homes = GrowthRules.Homes(s);
                    int away = _sim.Rebuild(new List<CityBuilding>(_scan));
                    Logger?.Info("Living population: the city's buildings changed since the last look; " + s.Buildings.Count + " buildings, " + (GrowthRules.Homes(s) - homes) + " flats more"
                                 + (away > 0 ? ", " + away + " household(s) to look for another flat" : string.Empty) + ".");
                }
            }
            for (int d = 0; d < s.Districts.Count; d++)
            {
                int sqm = PopulationBridges.LandHomes != null ? PopulationBridges.LandHomes(s.Districts[d].Id) : -1;
                GrowthRules.SetLand(s, d, sqm < 0 ? -1 : GrowthRules.FlatsOf(s, d, sqm));
            }
        }

        /// <summary>
        /// The city's developers started a building of a use (homes 0, offices 2) and so many m2 in a district of the mod's: the year's homes or offices
        /// started, the pull closed once enough are.
        /// </summary>
        internal void GrowthStarted(string district, int use, int sqm)
        {
            PopulationState s = _state;
            int d = s != null ? s.DistrictOf(district) : -1;
            if (d < 0 || _broken)
            {
                return;
            }
            if (use == 0)
            {
                GrowthRules.Start(s, GrowthRules.FlatsOf(s, d, sqm), _sim.Pulls);
            }
            else if (use == 2)
            {
                GrowthRules.StartOffices(s, GrowthRules.JobsOf(sqm), _sim.Pulls);
            }
        }

        // A slice of the look at what people hear of each business; then the staff linked and the sim's turn begun.
        private void Attention()
        {
            PopulationState s = _state;
            BusinessTable bt = s.Businesses;
            WorkSlice slice = WorkSlice.Of(StepMs, 16);
            while (_attentionCursor < bt.Count && slice.More())
            {
                int b = _attentionCursor++;
                BuildingRegistration reg = bt.Seen[b] ? PopulationGame.Registration(bt.Key[b]) : null;
                bt.Attention[b] = reg != null ? PopulationAttention.Of(reg, (name, e) =>
                {
                    Logger?.Error(e);
                    Logger?.Warn("Living population: the attention source '" + name + "' failed and is left out.");
                }) : 0;
            }
            if (_attentionCursor < bt.Count)
            {
                return;
            }
            _attentionCursor = -1;
            LinkStaff(s, _jobDay);
            if (PopulationBridges.People != null)
            {
                _asks.Clear();
                PopulationBridges.People(_asks);
                PopulationLinks.People(s, _asks, _jobDay);
            }
            _sim.BeginDay(_jobDay, Inputs(s), new List<BusinessSeed>(_seeds));
        }

        // The job is done: its figures in use, the city economy fed, the guests told, the player's customers drawn again.
        private void JobDone()
        {
            if (_generating)
            {
                _generating = false;
                Begin(_sim.State, true);
                Logger?.Info("Living population: generated on day " + _sim.State.Day + ": " + _state.Households.Live + " households, " + _state.People.Live + " people, "
                             + _state.Businesses.Count + " businesses anchored at the game's own customers.");
                if (Verbose)
                {
                    int households = _state.Households.Live, people = _state.People.Live;
                    bool fits = households >= PopulationCatalogue.HouseholdsLeast && households <= PopulationCatalogue.HouseholdsMost
                                && people >= PopulationCatalogue.PeopleLeast && people <= PopulationCatalogue.PeopleMost;
                    // the owner's size is the city with Uptown and Cedar Heights; with Uptown off the city is the game's alone, a third smaller
                    Logger?.Info("Population check (the city's size): " + households + " households and " + people + " people from " + _state.Buildings.Count
                                 + " buildings, wanted " + PopulationCatalogue.HouseholdsLeast + " to " + PopulationCatalogue.HouseholdsMost + " and " + PopulationCatalogue.PeopleLeast
                                 + " to " + PopulationCatalogue.PeopleMost + (Settings.Uptown ? ": " : " with Uptown, which is off: ")
                                 + (fits || !Settings.Uptown ? "PASS" : "FAIL, the city's homes are measured off the owner's size"));
                    GenerateRules.StartCheck(_state, out int unprovided, out int deeper);
                    Logger?.Info("Population check (the households' money): " + unprovided + " household(s) with somebody at home and nobody bringing money in, " + deeper
                                 + " in more debt after the first day's rent, wanted none of either: " + (unprovided == 0 && deeper == 0 ? "PASS" : "FAIL"));
                }
            }
            UseFigures();
            int asked = PopulationGame.RedrawMine();
            CityPopulation.RaiseDayLived(e => Logger?.Error(e));
            SharedCity.Changed(PopulationShare.Part);
            if (Verbose)
            {
                long spent = _sim.Demand.ResidentSpend + _sim.Demand.OutsiderSpend, taken = _sim.Demand.Takings + _sim.Demand.Outside;
                Logger?.Info("Population check (the day " + _state.Day + "): " + _state.Households.Live + " households, " + _state.People.Live + " people, unemployment "
                             + CityPopulation.Unemployment / 10.0f + "%; spent into the pools " + spent / 100 + ", taken and beyond " + taken / 100 + "; " + asked
                             + " of the player's businesses drawn again: "
                             + (spent == taken ? "PASS" : "FAIL, the pools do not balance"));
            }
        }

        /// <summary>True while the population runs and so can hold a kept business's staff to their jobs; what the sale's words say depends on it.</summary>
        public bool CanHold => Running && !SharedCity.Follows(PopulationShare.Part);

        /// <summary>A sale kept the player's staff (by the game's employee ids, before the game dropped them): the people they are stay in their jobs for 30 days. Returns how many.</summary>
        public int HoldStaff(IList<string> employeeIds)
        {
            if (!CanHold || Save == null || employeeIds == null)
            {
                return 0;
            }
            int held = 0;
            for (int i = 0; i < employeeIds.Count; i++)
            {
                held += HoldRules.Start(_state, PopulationLinks.Linked(_state, LinkKind.Employee, employeeIds[i]), Save.Day) ? 1 : 0;
            }
            if (Verbose)
            {
                Logger?.Info("Population check (a sale's staff): " + held + " of " + employeeIds.Count + " member(s) of staff held to their jobs for " + HoldRules.Days + " days: PASS");
            }
            return held;
        }

        /// <summary>A rival's business passes to another owner: the residents who work there stay in their jobs for 30 days. Returns how many.</summary>
        public int HoldBusiness(string street, int number)
        {
            if (!CanHold || Save == null)
            {
                return 0;
            }
            return HoldRules.StartAt(_state, _state.EmployerOfKey(PopulationState.KeyOf(street, number)), Save.Day);
        }

        // The player's staff linked to residents: each member of the game's staff a person of the city, in the job the game gives them.
        private void LinkStaff(PopulationState s, int day)
        {
            int linked = PopulationLinks.Staff(s, _staff, day);
            if (Verbose && linked > 0)
            {
                Logger?.Info("Population check (the player's staff): " + linked + " member(s) of staff linked to residents today: PASS");
            }
        }

        // A guest takes the host's part.
        private bool ApplyHost(string text, bool whole)
        {
            try
            {
                if (_guest == null)
                {
                    _guest = new PopulationState();
                }
                if (!PopulationShare.Apply(_guest, text, GuestDemand()))
                {
                    return false;
                }
                if (_state != null)
                {
                    DaySpread.Shared.Drop(this);
                    _state = null;   // this player's own population is the host's from now (its line stays in the save as it was)
                }
                CityPopulation.Live = _guest;
                CityPopulation.Pools = null;
                CityPopulation.Following = true;
                CityDemand.Live = _guestDemand;
                PopulationCustomers.Use(_guest, Settings.LivingPopulation);
                PopulationGame.RedrawMine();
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // The game is saving: the job under way finished, and the line written in pieces on the save's workers.
        private void OnSave()
        {
            try
            {
                if (_state == null || SharedCity.Follows(PopulationShare.Part))
                {
                    return;
                }
                DaySpread.Shared.Flush(this);
                Dictionary<string, string> data = PopulationGame.Data();
                if (data == null)
                {
                    return;
                }
                if (_zip == null)
                {
                    _zip = new DistrictSaveZip(DistrictSaveZip.WorkersHere);
                }
                float started = Time.realtimeSinceStartup;
                string line = PopulationStore.Line(_state, _zip) ?? PopulationStore.Encode(_state);
                data[PopulationStore.Key] = line;
                string holds = HoldRules.Write(_state);
                if (holds != null)
                {
                    data[HoldRules.Key] = holds;
                }
                else
                {
                    data.Remove(HoldRules.Key);
                }
                SaveDemand(data);
                if (Verbose)
                {
                    Logger?.Info("Population check (the save): " + line.Length / 1024 + " KB in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture)
                                 + " ms on " + _zip.Workers + " worker(s): PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the save goes on without the population's line: the last one stays
            }
        }

        /// <summary>Nothing of the population is in the game any more: the customers' count is the game's own, the readers see no population.</summary>
        private void StandDown(string why, bool tell)
        {
            bool was = _state != null || _guest != null;
            if (_state != null && tell && Save != null && !_broken)
            {
                DaySpread.Shared.Flush(this);   // the day under way played to its end: the line holds whole days
                OnSave();   // the line kept as it stands, so switching the option back on goes on from here
            }
            DaySpread.Shared.Drop(this);
            _state = null;
            _guest = null;
            _starting = false;
            _attentionCursor = -1;
            _checkedCalculators = false;
            CityPopulation.Live = null;
            CityPopulation.Pools = null;
            CityPopulation.PullTable = null;
            CityPopulation.Following = false;
            DropDemand();
            PopulationCustomers.Stop();
            if (was && Settings.LivingPopulation)
            {
                PopulationGame.RedrawMine();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Living population: stood down (" + why + "); the game's own customers and the city economy's own figures stand.");
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                PopulationCustomers.Stop();
                CityPopulation.Live = null;
                CityDemand.Live = null;
            }
            Logger?.Warn("Living population: switched off for this session after an error in " + where + ". The game's own customers and the city economy's own figures stand.");
        }

        /// <summary>For the save tools: the population stands down and forgets this save (its line was removed); the timer starts it again if the option is on.</summary>
        public void Forget()
        {
            StandDown("the save's line was removed", false);
        }
    }
}
