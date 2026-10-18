using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Civic life: owns the clock, the save, the street prompts and the test buttons of the city's government
    /// (<see cref="CivicGovernment"/>) and construction programme (<see cref="CivicWorks"/>), shared in a Going Public session.
    /// A street fault clears the street for the session; a data fault reverts every policy and switches civic life off.
    /// Cost: two comparisons a frame and a one-second tick that works only when the hour has changed.
    /// </summary>
    public sealed partial class CivicDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("civic", "CivicDirector", typeof(CivicDirector));

        private const string KeyGov = "reallife:civic_gov";
        private const string KeyWorks = "reallife:civic_works";
        public const string KeyOffice = "reallife:civic_office";

        private enum PromptKind { None, Vote, Leaflet, Notice, Knock, Poster }

        public static IModLogger Logger;
        public static CivicDirector Instance { get; private set; }

        /// <summary>True while a civic key prompt is on screen: the mod's own "enter the building" on that key stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._prompt != PromptKind.None || (Instance.Volunteer != null && Instance.Volunteer.Active)
                                 || (Instance._hud != null && Instance._hud.TakesKey));

        /// <summary>True while a campaign rally gathers or runs: no other street scene starts beside it.</summary>
        public static bool RallyOn => Instance != null && Instance._campaign != null && Instance._campaign.NeedsCast;

        public static bool SiteNear(Vector3 at, float metres) => Instance != null && Instance._sites != null && Instance._sites.Near(at, metres);

        public CivicGovernment Government { get; private set; }
        public CivicWorks Works { get; private set; }
        public CivicCity City { get; private set; }
        public VolunteerShift Volunteer { get; private set; }
        public CivicOffice Office { get; private set; }
        private OfficeScenes _scenes;

        private CivicEffects _effects;
        private CivicProps _props;
        private CivicCast _cast;
        private SiteDresser _sites;
        private CampaignWorld _campaign;
        public CampaignWorld Campaign => _campaign;   // for the city map
        private CivicScreen _hud;

        private bool _loaded, _failed, _worldFailed;
        private bool _effectsOn, _worldOut;
        private float _nextSlow, _nextFast;
        private int _lastHour = -1, _lastDay = -1;
        private bool _volunteerKey;
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
            Office = new CivicOffice(Government, City);
            _scenes = new OfficeScenes(Office, Government, City);
            _campaign.OwnShare = () => Office.Campaigning && Office.State.Race == OfficeRace.Council ? Mathf.Min(0.5f, 0.15f + Office.State.PostersUp * 0.02f) : 0f;
            _campaign.OwnColour = () => Office.State.Race == OfficeRace.Mayor && Government.State.PlayerSlot >= 0 ? Government.State.PlayerSlot : 3;
            _campaign.OwnCandidate = () => Office.State.Me;
            _campaign.OwnArrived = (listeners, over) => _scenes.Rally(listeners, over);
            Government.LeversChanged = OnLevers;
            _nextSlow = Time.time + 3f;
            _heard = Heard;
            _reported = Reported;
            SharedCity.Register(new SharedCityPart
            {
                Id = 'G', Name = "government",
                Ready = () => _loaded && !_failed && Elections,
                Write = () => CivicShare.WriteGov(Government.State),
                Apply = ApplyGov,
                Request = (text, who) => HostRequests(text, who, false),
                Left = () => Logger?.Info("Civic: the host's government is no longer followed; its hours run here again from hour " + Government.State.LastHour + ", the host's last.")
            });
            SharedCity.Register(new SharedCityPart
            {
                Id = 'W', Name = "building sites",
                Ready = () => _loaded && !_failed && Settings.Civic && Settings.CivicConstruction,
                Write = () => CivicShare.WriteWorks(Works.State),
                Apply = ApplyWorks,
                Request = (text, who) => HostRequests(text, who, true),
                Left = () => Works.Shared = false
            });
            SharedCity.Register(new SharedCityPart
            {
                Id = 'N', Name = "the city's notices",
                Ready = () => _loaded && !_failed && Elections,
                Write = () => "-",   // nothing to start from: the notices are only what has just happened
                Changes = TakeNotices,
                Apply = ShowNotices
            });
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            // Nothing is saved here: by now the game may hold another save.
            ClearWorld();
            try
            {
                _effects.Revert();
                _effectsOn = false;
                CivicPolicy.Publish(null);
                _hud.Dispose();
                _props.Dispose();
                _scenes.Destroy();
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

        private static readonly int HotUpdate = HotSpots.Slot("Civic: CivicDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
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
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));

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
                // The game's building lists may be missing in the first seconds; after eight tries the city is one district.
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
                if (SharedCity.Follows('G'))
                {
                    Government.FollowTick(day, hour);   // a guest: the city's hours are the host's
                }
                else
                {
                    bool leading = SharedCity.Leading;
                    int ran;
                    Listen(leading);
                    try
                    {
                        ran = Government.Tick(day, hour);
                    }
                    finally
                    {
                        Listen(false);
                    }
                    if (ran > 0 && leading)
                    {
                        SharedCity.Changed('G');
                    }
                }
                if (newDay)
                {
                    _effects.Daily(Government.State, day);
                }
                if (!SharedCity.Follows('G'))
                {
                    _scenes.Tick(day, hour);   // the player's debate, result night and council meetings
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
                Works.Shared = SharedCity.Follows('W');   // a guest works only its own refit; the city's sites are the host's
                Works.Tick(day, hour);
                if (newHour && !Works.Shared)
                {
                    SharedCity.Changed('W');
                }
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

        // On: the levers as they stand; off: everything as it was found.
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

        // A line that is there but cannot be read is kept for a version that can (Core/SaveLines).
        private static bool Readable(Dictionary<string, string> data, string key, bool read)
        {
            if (!read)
            {
                SaveLines.Unreadable(data, key, "it could not be read");
            }
            return read;
        }

        private void Load(int day, int hour)
        {
            Dictionary<string, string> data = SaveGameManager.Current.modData;
            long now = (long)day * 24 + hour;
            if (data == null || !data.TryGetValue(KeyGov, out string gov) || string.IsNullOrEmpty(gov) || !Readable(data, KeyGov, Government.State.Read(gov)) || Government.State.Seed == 0)
            {
                Government.NewSave(day, now);
                Logger?.Info("Civic: a new city government for this save; the first election is on day " + Government.State.ElectionDay + ".");
            }
            if (data == null || !data.TryGetValue(KeyWorks, out string works) || string.IsNullOrEmpty(works) || !Readable(data, KeyWorks, Works.State.Read(works)))
            {
                Works.State.Sites.Clear();
                Works.State.LastHour = now;
            }
            if (data == null || !data.TryGetValue(KeyOffice, out string office) || string.IsNullOrEmpty(office) || !Readable(data, KeyOffice, Office.State.Read(office)))
            {
                Office.State.Reset();
            }
            _loaded = true;
            _lastDay = day;
            _lastHour = -1;
            Works.Permits = Elections ? Government.State.Levers[(int)Lever.Permits] : 0;
            CityHallPhone.Setup();
            Logger?.Info("Civic: ready. Election " + Government.State.Number + " " + Government.State.Phase + ", election day " + Government.State.ElectionDay + ", approval "
                         + Mathf.RoundToInt(Government.State.Approval) + ", " + Works.State.Sites.Count + " site(s) in progress.");
        }

        private Action _onSave;   // made once: SaveHook compares it

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
                SaveGameManager.Current.modData[KeyOffice] = Office.State.Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

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
                if (registration == null || !GoingPublic.Mine(registration) || !registration.HasEstablishedBusiness)
                {
                    continue;
                }
                CityBuildingController controller = CivicCity.Find(registration.Address.streetName, registration.Address.streetNumber);
                if (CivicCity.HasDoor(controller) && !GameMembers.Fenced(controller.building))
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
            int price = CivicCatalogue.RefitPriceNow(bid);
            if (!GameManager.ChangeMoneySafe(-price, new TransactionInfo("reallife_transaction_civic_refit"), null, null, force: false, showNotification: true))
            {
                return "reallife_civic_refit_broke";
            }
            SiteState site = Works.StartAt(SiteKind.Refit, controller, TimeHelper.CurrentDay, true, CivicCatalogue.RefitHours[bid]);
            if (site == null)
            {
                CivicEffects.Pay(price, "reallife_transaction_civic_refit");   // somebody is already at work on that front: the money comes back
                return "reallife_civic_refit_busy";
            }
            return "reallife_civic_refit_ordered";
        }
    }
}
