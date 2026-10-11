using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Civic life: the city's government and its construction programme, and what of them is
    /// seen in the street. This component owns the clock and the save, and nothing else:
    ///   CivicGovernment   mayor, council, elections, polls, petitions (data, by the hour)
    ///   CivicEffects      what the policies do: multipliers the mod owns, labelled transactions
    ///   CivicWorks        the construction programme (data, by the hour)
    ///   CampaignWorld     posters, boards, the canvasser, rallies, the polling place
    ///   SiteDresser       the sites near the player as props, a closed lane, a crew
    ///   CityHallPhone     the City Hall contact, the ballot, the site notice
    ///
    /// COST. Idle, this is two comparisons a frame and one tick a second. The tick reads the
    /// game's clock; only when the hour has changed does anything of the government or the
    /// programme run (arithmetic on a few dozen numbers). The street is looked at once a second
    /// while the player is out of doors: distances to at most six sites and eleven posters.
    /// Physics and navigation queries happen only when something is being put up (one poster
    /// every four seconds during a campaign; a site once when the player first comes near).
    /// Nothing allocates on the tick; texts are built when a page or a prompt is shown.
    ///
    /// SAVE. Two lines of text in the save's modData (reallife:civic_gov, reallife:civic_works).
    /// Nothing else is written: no policy changes a value the game saves. A save made with this
    /// loads without the mod; a save made without it starts with a mayor in office and the
    /// first election two weeks off.
    ///
    /// FAULTS. An error in the street part clears the street (every closed lane opened, every
    /// person given back), is logged once, and leaves the street part off for the session; the
    /// government and the programme go on as data. An error in the data part reverts every
    /// policy effect and switches civic life off for the session.
    /// </summary>
    public sealed class CivicDirector : MonoBehaviour
    {
        private const string KeyGov = "reallife:civic_gov";
        private const string KeyWorks = "reallife:civic_works";

        private enum PromptKind { None, Vote, Leaflet, Notice }

        public static IModLogger Logger;
        public static CivicDirector Instance { get; private set; }

        /// <summary>True while one of civic life's key prompts is on screen: the mod's own "enter the building" on the same key stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._prompt != PromptKind.None || (Instance.Volunteer != null && Instance.Volunteer.Active)
                                 || (Instance._hud != null && Instance._hud.TakesKey));

        /// <summary>True while a campaign rally is gathering or on: no other street scene starts beside it (Scripts/Wiring).</summary>
        public static bool RallyOn => Instance != null && Instance._campaign != null && Instance._campaign.NeedsCast;

        /// <summary>True if a building site that is set out stands within so many metres of a place.</summary>
        public static bool SiteNear(Vector3 at, float metres) => Instance != null && Instance._sites != null && Instance._sites.Near(at, metres);

        public CivicGovernment Government { get; private set; }
        public CivicWorks Works { get; private set; }
        public CivicCity City { get; private set; }
        public VolunteerShift Volunteer { get; private set; }

        private CivicEffects _effects;
        private CivicProps _props;
        private CivicCast _cast;
        private SiteDresser _sites;
        private CampaignWorld _campaign;
        public CampaignWorld Campaign => _campaign;   // read only, for the city map (World/MapLinks)
        private CivicScreen _hud;

        private bool _loaded, _failed, _worldFailed;
        private bool _effectsOn, _worldOut;
        private float _nextSlow, _nextFast;
        private int _lastHour = -1, _lastDay = -1;
        private PromptKind _prompt;
        private SiteState _promptSite;
        private string _voteFirst, _leafletFirst, _noticeFirst, _canvassFirst, _canvassTitle;
        private const string ButtonVote = "reallife_civic_panel_vote";
        private const string ButtonLeaflet = "reallife_civic_panel_leaflet";
        private const string ButtonNotice = "reallife_civic_panel_notice";
        private const string ButtonOffer = "reallife_civic_panel_offer";
        private bool _volunteerKey;
        private int _testPolicy;
        private int _cityTries;
        private float _nextCity;

        /// <summary>True once the save's civic state is loaded and nothing has switched civic life off.</summary>
        public bool Running => _loaded && !_failed && Settings.Civic;

        private bool Elections => Settings.Civic && Settings.CivicElections;

        private void Awake()
        {
            Instance = this;
            City = new CivicCity();
            Government = new CivicGovernment(City);
            Works = new CivicWorks();
            _effects = new CivicEffects();
            _props = new CivicProps(transform);
            _cast = new CivicCast(gameObject);
            _sites = new SiteDresser(_props, Works, _cast);
            _campaign = new CampaignWorld(_props, Government, City, _cast);
            _hud = new CivicScreen(Logger, PromptPressed);
            Volunteer = new VolunteerShift(Government, this);
            Government.LeversChanged = OnLevers;
            _nextSlow = Time.time + 3f;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            // (Nothing is written to the save here: by now the game may already hold another save.)
            ClearWorld();
            try
            {
                _effects.Revert();
                _effectsOn = false;
                CivicPolicy.Publish(null);
                _hud.Dispose();
                _props.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_failed)
            {
                return;
            }
            float now = Time.time;
            try
            {
                if (Volunteer.Active)
                {
                    if (now >= _nextFast)
                    {
                        _nextFast = now + 0.2f;
                        Volunteer.Tick(now, TimeHelper.CurrentHour, _volunteerKey);
                        _volunteerKey = false;
                        if (!Volunteer.Active)
                        {
                            _hud.HideAll();
                        }
                        else
                        {
                            _canvassTitle = _canvassTitle ?? CivicText.T("reallife_civic_card_title");
                            _hud.Status(_canvassTitle, Volunteer.Status);
                            PlayerController player = PlayerHelper.PlayerController;
                            if (Volunteer.Prompt != null && player != null)
                            {
                                _canvassFirst = _canvassFirst ?? CivicText.T("reallife_civic_door_canvass");
                                _hud.Offer(_canvassFirst, ButtonOffer, player.transform.position);
                            }
                            else
                            {
                                _hud.ClearOffer();
                            }
                        }
                    }
                }
                if (now < _nextSlow)
                {
                    return;
                }
                _nextSlow = now + 1f;
                Slow(now);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Civic: switched off for this session after the error above; every policy effect was taken back and the street cleared.");
            try
            {
                ClearWorld();
                _effects.Revert();
                _effectsOn = false;
                CivicPolicy.Publish(null);
            }
            catch (Exception inner)
            {
                Logger?.Error(inner);
            }
        }

        // ---------------------------------------------------------------- once a second

        private void Slow(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return;
            }
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            if (!_loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                Load(day, hour);
            }
            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            if (!Settings.Civic)
            {
                if (_effectsOn || _worldOut)
                {
                    ClearWorld();
                    Effects(false);
                }
                return;
            }
            Effects(Elections);
            if (!City.Ready && _cityTries < 8 && now >= _nextCity)
            {
                // The game's building lists may not be there in the first seconds. After eight looks the city is taken as one district.
                _cityTries++;
                _nextCity = now + 10f;
                City.Build();
            }

            bool newHour = hour != _lastHour;
            bool newDay = day != _lastDay && _lastDay >= 0;
            _lastHour = hour;
            _lastDay = day;

            if (Elections)
            {
                Government.LateSites = Works.Late;
                Government.Tick(day, hour);
                if (newDay)
                {
                    _effects.Daily(Government.State, day);
                }
                if (newHour)
                {
                    _effects.Apply(Government.State.Levers);   // the integer options are re-dithered each hour
                    _effects.Hourly(Government.State, day);
                }
            }
            if (Settings.CivicConstruction)
            {
                Works.Permits = Elections ? Government.State.Levers[(int)Lever.Permits] : 0;
                Works.Tick(day, hour);
                if (newHour)
                {
                    bool outside = !BuildingManager.IsInsideBuilding;
                    if (outside && Works.State.Sites.Count < CivicCatalogue.ActiveSites)
                    {
                        Works.TopUp(day);
                    }
                    if (Works.NoiseNow(day, hour, outside ? PlayerHelper.GetPosition() : Vector3.zero, outside))
                    {
                        CivicEffects.Keep(ModState.CivicNoise, 2);
                    }
                }
            }
            World(now, day, hour);
        }

        // Policy effects on (the levers as they stand) or off (everything as it was found).
        private void Effects(bool on)
        {
            if (on == _effectsOn)
            {
                return;
            }
            _effectsOn = on;
            if (on)
            {
                _effects.Apply(Government.State.Levers);
                CivicPolicy.Publish(Government.State.Levers);
            }
            else
            {
                _effects.Revert();
                CivicPolicy.Publish(null);
            }
        }

        private void OnLevers()
        {
            Works.Permits = Elections ? Government.State.Levers[(int)Lever.Permits] : 0;
            if (_effectsOn)
            {
                _effects.Apply(Government.State.Levers);
                CivicPolicy.Publish(Government.State.Levers);
            }
        }

        // ---------------------------------------------------------------- the street

        private void World(float now, int day, int hour)
        {
            if (_worldFailed)
            {
                return;
            }
            try
            {
                var uis = InstanceBehavior<UIs>.Instance;
                PlayerController player = PlayerHelper.PlayerController;
                bool outdoors = player != null && !BuildingManager.IsInsideBuilding && (uis == null || !uis.timeMachine.isRunning);
                if (!outdoors)
                {
                    if (_worldOut)
                    {
                        ClearWorld();
                    }
                    return;
                }
                _worldOut = true;
                Vector3 at = player.transform.position;
                bool onFoot = !PlayerHelper.IsUsingVehicle;
                if (Elections)
                {
                    _campaign.Tick(now, at, day, hour, onFoot, _sites.CrewOut > 0);
                }
                else if (_campaign.Dressing > 0 || _campaign.PeopleOut > 0)
                {
                    _campaign.Clear();
                }
                if (Settings.CivicConstruction)
                {
                    _sites.Tick(now, at, day, hour, !_campaign.NeedsCast);
                }
                else if (_sites.DressedCount > 0)
                {
                    _sites.Clear();
                }
                _cast.ClearFallen();
                Prompt(at, onFoot, day, hour);
            }
            catch (Exception e)
            {
                _worldFailed = true;
                Logger?.Error(e);
                Logger?.Warn("Civic: the street part (campaign dressing, sites, their people) is off for this session after the error above; elections and sites go on as data, on the phone.");
                try
                {
                    ClearWorld();
                }
                catch (Exception inner)
                {
                    Logger?.Error(inner);
                }
            }
        }

        // Everything civic life has in the street, away at once: every closed lane opened first, then props and people.
        // Each step stands alone: a fault in one (a thing already destroyed with the scene) does not keep the next from running.
        private void ClearWorld()
        {
            _worldOut = false;
            _prompt = PromptKind.None;
            _promptSite = null;
            Step(_clearSites ?? (_clearSites = () => _sites?.Clear()));
            Step(_clearCampaign ?? (_clearCampaign = () => _campaign?.Clear()));
            Step(_clearCast ?? (_clearCast = () => _cast?.ReleaseAll()));
            Step(_clearShift ?? (_clearShift = () => Volunteer?.Stop()));
            Step(_clearHud ?? (_clearHud = () => _hud?.HideAll()));
        }

        private Action _clearSites, _clearCampaign, _clearCast, _clearShift, _clearHud;

        private static void Step(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Prompt(Vector3 player, bool onFoot, int day, int hour)
        {
            PromptKind kind = PromptKind.None;
            SiteState site = null;
            if (onFoot && !Volunteer.Active && !JobDirector.TakesInteractKey && !ThirdPersonCamera.IsUiOpen())
            {
                if (Elections && Government.PollsOpen(day, hour) && Government.State.Voted < 0 && _campaign.PollingInReach(player))
                {
                    kind = PromptKind.Vote;
                }
                else if (Elections && _campaign.CanvasserInReach(player) >= 0)
                {
                    kind = PromptKind.Leaflet;
                }
                else if (Settings.CivicConstruction && (site = _sites.NoticeInReach(player)) != null)
                {
                    kind = PromptKind.Notice;
                }
            }
            _prompt = kind;
            _promptSite = site;
            if (Volunteer.Active)
            {
                return;   // the shift has the two lines
            }
            if (kind == PromptKind.None)
            {
                _hud.ClearOffer();
                return;
            }
            // A place and a verb, in the game's panel; the words are made once.
            if (kind == PromptKind.Vote)
            {
                _voteFirst = _voteFirst ?? CivicText.T("reallife_civic_door_vote");
                _hud.Offer(_voteFirst, ButtonVote, player);
            }
            else if (kind == PromptKind.Leaflet)
            {
                _leafletFirst = _leafletFirst ?? CivicText.T("reallife_civic_door_leaflet");
                _hud.Offer(_leafletFirst, ButtonLeaflet, player);
            }
            else
            {
                _noticeFirst = _noticeFirst ?? CivicText.T("reallife_civic_door_notice");
                _hud.Offer(_noticeFirst, ButtonNotice, player);
            }
        }

        // The panel's button, on the game's key or a click: the canvassing shift's leaflet, or what the prompt offers.
        private void PromptPressed()
        {
            if (_failed || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            try
            {
                if (Volunteer.Active)
                {
                    _volunteerKey = true;
                }
                else if (_prompt != PromptKind.None)
                {
                    Interact();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Interact()
        {
            PromptKind kind = _prompt;
            _prompt = PromptKind.None;
            _hud.ClearOffer();
            switch (kind)
            {
                case PromptKind.Vote:
                    CityHallPhone.OpenBallot();
                    break;
                case PromptKind.Leaflet:
                    _campaign.TakeLeaflet();
                    break;
                case PromptKind.Notice:
                    if (_promptSite != null)
                    {
                        CityHallPhone.OpenNotice(_promptSite);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- the save

        private void Load(int day, int hour)
        {
            Dictionary<string, string> data = SaveGameManager.Current.modData;
            long now = (long)day * 24 + hour;
            if (data == null || !data.TryGetValue(KeyGov, out string gov) || string.IsNullOrEmpty(gov) || !Government.State.Read(gov) || Government.State.Seed == 0)
            {
                Government.NewSave(day, now);
                Logger?.Info("Civic: a new city government for this save; the first election is on day " + Government.State.ElectionDay + ".");
            }
            if (data == null || !data.TryGetValue(KeyWorks, out string works) || string.IsNullOrEmpty(works) || !Works.State.Read(works))
            {
                Works.State.Sites.Clear();
                Works.State.LastHour = now;
            }
            _loaded = true;
            _lastDay = day;
            _lastHour = -1;
            Works.Permits = Elections ? Government.State.Levers[(int)Lever.Permits] : 0;
            CityHallPhone.Setup();
            Logger?.Info("Civic: ready. Election " + Government.State.Number + " " + Government.State.Phase + ", election day " + Government.State.ElectionDay + ", approval "
                         + Mathf.RoundToInt(Government.State.Approval) + ", " + Works.State.Sites.Count + " site(s) in progress.");
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                if (!_loaded || SaveGameManager.Current == null)
                {
                    return;
                }
                if (SaveGameManager.Current.modData == null)
                {
                    SaveGameManager.Current.modData = new Dictionary<string, string>();
                }
                SaveGameManager.Current.modData[KeyGov] = Government.State.Write();
                SaveGameManager.Current.modData[KeyWorks] = Works.State.Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the player's own shopfront

        /// <summary>The shop of the player's that could be refitted; null with the locale key of why not.</summary>
        public BuildingRegistration RefitCandidate(out string reasonKey)
        {
            reasonKey = "reallife_civic_refit_none";
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return null;
            }
            for (int i = 0; i < Works.State.Sites.Count; i++)
            {
                if (Works.State.Sites[i].Mine)
                {
                    reasonKey = "reallife_civic_refit_busy";
                    return null;
                }
            }
            if (Works.State.RefreshedDay > 0 && TimeHelper.CurrentDay - Works.State.RefreshedDay < 28)
            {
                reasonKey = "reallife_civic_refit_recent";
                return null;
            }
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration registration = save.BuildingRegistrations[i];
                if (registration == null || !registration.RentedByPlayer || !registration.HasEstablishedBusiness)
                {
                    continue;
                }
                CityBuildingController controller = CivicCity.Find(registration.Address.streetName, registration.Address.streetNumber);
                if (CivicCity.HasDoor(controller) && controller.building.locatedInsidePrivateFenceIndex < 0)
                {
                    return registration;
                }
            }
            return null;
        }

        /// <summary>Takes a bid for the refit: the price is paid now and the hoarding goes up. Returns the locale key of the answer.</summary>
        public string OrderRefit(BuildingRegistration shop, int bid)
        {
            if (shop == null || bid < 0 || bid >= CivicCatalogue.RefitPrice.Length)
            {
                return "reallife_civic_refit_none";
            }
            CityBuildingController controller = CivicCity.Find(shop.Address.streetName, shop.Address.streetNumber);
            if (!CivicCity.HasDoor(controller))
            {
                return "reallife_civic_refit_none";
            }
            if (!GameManager.ChangeMoneySafe(-CivicCatalogue.RefitPrice[bid], new TransactionInfo("reallife_transaction_civic_refit"), null, null, force: false, showNotification: true))
            {
                return "reallife_civic_refit_broke";
            }
            SiteState site = Works.StartAt(SiteKind.Refit, controller, TimeHelper.CurrentDay, true, CivicCatalogue.RefitHours[bid]);
            if (site == null)
            {
                CivicEffects.Pay(CivicCatalogue.RefitPrice[bid], "reallife_transaction_civic_refit");   // somebody is already at work on that front: the money comes back
                return "reallife_civic_refit_busy";
            }
            return "reallife_civic_refit_ordered";
        }

        // ---------------------------------------------------------------- the test buttons

        private bool TestReady(string test, bool street)
        {
            if (!_loaded || _failed || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Civic)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (street)
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && _worldFailed)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, test, why);
                    return false;
                }
            }
            return true;
        }

        /// <summary>Test: an election campaign starts now (election day in a week); posters and boards go up round the player at once.</summary>
        public void TestCampaign()
        {
            if (!TestReady("Civic campaign", false))
            {
                return;
            }
            if (!Settings.CivicElections)
            {
                TestRefusals.Tell(Logger, "Civic campaign", TestRefusal.FeatureOff);
                return;
            }
            try
            {
                Government.TestCampaign(TimeHelper.CurrentDay);
                if (!BuildingManager.IsInsideBuilding && !_worldFailed)
                {
                    _campaign.Burst(PlayerHelper.GetPosition());
                    _worldOut = true;
                }
                Logger?.Info("Civic test: campaign started; " + _campaign.Dressing + " poster(s) and board(s) put up round the player.");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: today is election day. The first press opens the polls; a second press (or a press outside polling hours) counts the votes.</summary>
        public void TestElectionDay()
        {
            if (!TestReady("Civic election day", false))
            {
                return;
            }
            if (!Settings.CivicElections)
            {
                TestRefusals.Tell(Logger, "Civic election day", TestRefusal.FeatureOff);
                return;
            }
            try
            {
                Government.TestElectionDay(TimeHelper.CurrentDay, TimeHelper.CurrentHour);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the next policy of the catalogue comes into force now (each press, the next one).</summary>
        public void TestPolicy()
        {
            if (!TestReady("Civic policy", false))
            {
                return;
            }
            if (!Settings.CivicElections)
            {
                TestRefusals.Tell(Logger, "Civic policy", TestRefusal.FeatureOff);
                return;
            }
            try
            {
                PolicyDef policy = CivicCatalogue.All[_testPolicy % CivicCatalogue.All.Length];
                _testPolicy++;
                Government.Set(policy.Lever, policy.Dir, true);
                OnLevers();
                Logger?.Info("Civic test: policy '" + policy.Name + "' in force.");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: every policy is taken back at once.</summary>
        public void TestRevert()
        {
            if (!TestReady("Civic revert", false))
            {
                return;
            }
            try
            {
                Government.RevertAll();
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_reverted", 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: a site of a kind starts at the nearest building and is set out now.</summary>
        public void TestSite(SiteKind kind)
        {
            string test = "Civic " + kind;
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.CivicConstruction)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            try
            {
                Vector3 player = PlayerHelper.GetPosition();
                int day = TimeHelper.CurrentDay;
                CityBuildingController place = CivicCity.NearestBuilding(player, 0f, 70f);
                if (place == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no building with a street door within 70 m");
                    return;
                }
                SiteState existing = Works.Nearest(place.entranceDoors[0].doorTransform.position, 2f);
                SiteState site = existing != null && existing.Kind == kind ? existing : null;
                if (site == null && existing != null)
                {
                    Works.State.Sites.Remove(existing);   // another kind of site at this building: the test replaces it
                }
                bool started = false;
                if (site == null)
                {
                    site = Works.StartAt(kind, place, day, false, 0f);
                    started = true;
                }
                if (site == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits);
                    return;
                }
                if (_sites.DressNow(site, day))
                {
                    _worldOut = true;
                    CivicText.Toast(NotificationType.Success, "reallife_civic_test_site", "project", CivicWorks.ProjectName(site), "address", CivicCity.AddressText(site.Street, site.Number), 9f);
                    return;
                }
                if (started)
                {
                    Works.State.Sites.Remove(site);
                }
                Logger?.Info(test + " test: not started (" + _sites.LastRefusal + ").");
                Notifications.Show(NotificationType.Warning, RefusalKey(_sites.LastRefusal), null, 9f, null, null, true, false);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static string RefusalKey(SiteRefusal why)
        {
            switch (why)
            {
                case SiteRefusal.NoLane: return "reallife_civic_site_refused_lane";
                case SiteRefusal.NoKerb: return "reallife_civic_site_refused_kerb";
                case SiteRefusal.Vehicles: return "reallife_civic_site_refused_vehicles";
                case SiteRefusal.NoWall: return "reallife_civic_site_refused_wall";
                case SiteRefusal.NoRoom: return "reallife_civic_site_refused_room";
                case SiteRefusal.CannotClose: return "reallife_civic_site_refused_close";
                default: return "reallife_civic_site_refused_pavement";
            }
        }

        /// <summary>Test: every site does a full working day now, and what stands in the street follows.</summary>
        public void TestAdvance()
        {
            if (!TestReady("Civic advance", false))
            {
                return;
            }
            try
            {
                int before = Works.State.Sites.Count;
                Works.AdvanceDay(TimeHelper.CurrentDay);
                if (!_worldFailed)
                {
                    _sites.RefreshNow(TimeHelper.CurrentDay);
                }
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_advanced", "count", before.ToString(), "left", Works.State.Sites.Count.ToString(), 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: a rally gathers near the player now (a campaign is started first if there is none).</summary>
        public void TestRally()
        {
            if (!TestReady("Civic rally", true))
            {
                return;
            }
            if (!Settings.CivicElections)
            {
                TestRefusals.Tell(Logger, "Civic rally", TestRefusal.FeatureOff);
                return;
            }
            try
            {
                if (CivicBridges.SceneOn())
                {
                    TestRefusals.Tell(Logger, "Civic rally", TestRefusal.SceneActive);
                    return;
                }
                if (CivicBridges.WorkStopped())
                {
                    TestRefusals.Tell(Logger, "Civic rally", TestRefusal.NothingFits, 0, "it is raining: nobody holds a rally in the rain");
                    return;
                }
                if (!Government.Campaigning)
                {
                    Government.TestCampaign(TimeHelper.CurrentDay);
                }
                _campaign.ForceRally();
                _worldOut = true;
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_rally_started", 9f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the street is cleared of everything civic and the construction programme is emptied (it fills again within the hour).</summary>
        public void TestClear()
        {
            if (!_loaded)
            {
                TestRefusals.Tell(Logger, "Civic clear", TestRefusal.NotLoaded);
                return;
            }
            try
            {
                Works.Clear();
                ClearWorld();
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_cleared", 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
