using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    ///   CivicOffice       the player standing for office, the campaign, an office held (OfficeRules, OfficeState: game-free;
    ///                     OfficePhone its City Hall pages, OfficeScenes its scenes in the game's list window)
    ///
    /// COST. Idle, this is two comparisons a frame and one tick a second. The tick reads the
    /// game's clock; only when the hour has changed does anything of the government or the
    /// programme run (arithmetic on a few dozen numbers). The street is looked at once a second
    /// while the player is out of doors: distances to at most six sites and eleven posters.
    /// Physics and navigation queries happen only when something is being put up (one poster
    /// every four seconds during a campaign; a site once when the player first comes near).
    /// Nothing allocates on the tick; texts are built when a page or a prompt is shown.
    ///
    /// SAVE. Three lines of text in the save's modData (reallife:civic_gov, reallife:civic_works, reallife:civic_office).
    /// Nothing else is written: no policy changes a value the game saves. A save made with this
    /// loads without the mod; a save made without it starts with a mayor in office and the
    /// first election two weeks off.
    ///
    /// A GOING PUBLIC SESSION. The government and the building sites are the city's, one for every player (Core/SharedCity):
    /// the host runs their hours as alone and sends them (G the government, W the sites, N what the city's toasts and
    /// stories said in those hours, so that every player hears of a campaign, a policy or a result as the host's does);
    /// a guest runs none of the city's hours (CivicGovernment.FollowTick, CivicWorks.Shared), applies the host's state
    /// here (the policies' effects on that player's own game, the posters, the sites in the street) and sends what its
    /// player does that changes the city. Its own part of an election (its ballot, what it gave, the favours owed for
    /// it) is kept by the guest and settled here when the host's count comes.
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
        public const string KeyOffice = "reallife:civic_office";

        private enum PromptKind { None, Vote, Leaflet, Notice, Knock, Poster }

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
        public CivicOffice Office { get; private set; }
        private OfficeScenes _scenes;
        private int _knockBuilding;
        private string _knockDistrict, _knockAddress;
        private Vector3 _posterAt, _posterNormal;
        private string _knockFirst, _posterFirst;
        private const string ButtonKnock = "reallife_civic_office_panel_knock";
        private const string ButtonPoster = "reallife_civic_office_panel_poster";
        private static readonly Collider[] Nearby = new Collider[24];
        private static int _buildingMask;

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
        private readonly StringBuilder _notices = new StringBuilder(256);   // the host: the city's toasts and stories since the last update
        private Action<NotificationType, string, string[], float> _heard;
        private Action<string, string> _reported;
        private const char Gap = '\u001f';   // between the fields of one notice (never in a text)
        private const string Mod = "reallife";                       // (apart, so that the locale check does not take a prefix for a key)
        private const string CivicKeys = Mod + "_civic_";             // the only keys a host's notice may name
        private const string PollsKeys = Mod + "_civic_polls_open";   // the polls opening: each guest says its own

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
                Write = () => "-",   // (nothing to start from: the notices are only ever what has just happened)
                Changes = TakeNotices,
                Apply = ShowNotices
            });
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
                if (SharedCity.Follows('G'))
                {
                    Government.FollowTick(day, hour);   // a guest of a Going Public session: the city's hours are the host's
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
                    _scenes.Tick(day, hour);   // the player's own debate, result night and council meetings (a guest's city is the host's)
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

        // ---------------------------------------------------------------- a Going Public session

        // The host: the city's toasts and stories are heard while its hours run (and while a guest's request is applied).
        private void Listen(bool on)
        {
            CivicText.Heard = on ? _heard : null;
            CivicBridges.Reported = on ? _reported : null;
        }

        private void Heard(NotificationType type, string key, string[] pairs, float seconds)
        {
            if (key.StartsWith(PollsKeys, StringComparison.Ordinal) || _notices.Length > 8192)
            {
                return;   // each guest says its own polling place
            }
            _notices.Append('T').Append(Gap).Append((int)type).Append(Gap).Append(seconds.ToString("0.#", CultureInfo.InvariantCulture)).Append(Gap).Append(key);
            for (int i = 0; pairs != null && i < pairs.Length; i++)
            {
                _notices.Append(Gap).Append(Clean(pairs[i]));
            }
            _notices.Append('\n');
        }

        private void Reported(string headline, string body)
        {
            if (_notices.Length <= 8192)
            {
                _notices.Append('R').Append(Gap).Append(headline).Append(Gap).Append(body).Append('\n');
            }
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace(Gap, ' ').Replace('\n', ' ');
        }

        private string TakeNotices()
        {
            if (_notices.Length == 0)
            {
                return null;
            }
            string text = _notices.ToString();
            _notices.Length = 0;
            return text;
        }

        // A guest: what the host's player was told of the city, told here too.
        private bool ShowNotices(string text, bool whole)
        {
            if (whole)
            {
                return true;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(Gap);
                if (f.Length >= 4 && f[0] == "T" && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                    && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && f[3].StartsWith(CivicKeys, StringComparison.Ordinal))
                {
                    string[] pairs = null;
                    if (f.Length > 4)
                    {
                        pairs = new string[f.Length - 4];
                        Array.Copy(f, 4, pairs, 0, pairs.Length);
                    }
                    CivicText.Show((NotificationType)type, f[3], pairs, Mathf.Clamp(seconds, 4f, 20f));
                }
                else if (f.Length >= 3 && f[0] == "R" && f[1].StartsWith(CivicKeys, StringComparison.Ordinal) && f[2].StartsWith(CivicKeys, StringComparison.Ordinal))
                {
                    CivicBridges.Report(f[1], f[2]);
                }
            }
            return true;
        }

        // A guest: the host's government. The policies' effects follow it here; the player's own part of an election that has
        // been counted is settled as the host's own count would have settled it.
        private bool ApplyGov(string text, bool whole)
        {
            GovState s = Government.State;
            GovChange change;
            if (!CivicShare.ApplyGov(s, text, out change))
            {
                return false;
            }
            if (change.LeversMoved != 0)
            {
                OnLevers();
            }
            if (change.Counted && s.Winner >= 0 && s.Winner < s.CandidateCount)
            {
                int per = Mathf.Max(1, CivicCatalogue.FavourPerNow);
                if (s.Donated[s.Winner] >= per)
                {
                    s.Favours = Mathf.Min(2, s.Favours + s.Donated[s.Winner] / per);
                }
                if (s.Voted == s.Winner)
                {
                    ModState.AddModifier(ModState.MoodUp);
                }
            }
            if (change.Phase)
            {
                Government.CampaignChanged?.Invoke();
            }
            return true;
        }

        // A guest: the host's building sites; its own refit stays its own.
        private bool ApplyWorks(string text, bool whole)
        {
            if (!CivicShare.ApplyWorks(Works.State, text))
            {
                return false;
            }
            Works.Shared = true;
            Works.Earshots();
            Works.SitesChanged?.Invoke();
            return true;
        }

        // The host: a guest's requests, one a line; what they say of the city is heard by every guest with the next update.
        private void HostRequests(string text, string who, bool works)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            string[] lines = text.Split('\n');
            int done = 0;
            Listen(true);
            try
            {
                for (int i = 0; i < lines.Length && i < 64; i++)
                {
                    if (lines[i].Length > 0 && (works ? Works.HostRequest(lines[i], day) : Government.HostRequest(lines[i], who, day, hour)))
                    {
                        done++;
                    }
                }
            }
            finally
            {
                Listen(false);
            }
            if (done > 0)
            {
                SharedCity.Changed(works ? 'W' : 'G');
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Civic check (the host): " + done + " of " + lines.Length + " request line(s) of " + who + " applied to the city's " + (works ? "sites" : "government") + ": PASS");
            }
        }

        // A test that would change the city's government or sites: not while they are the host's.
        private bool HostOwns(string test, char part)
        {
            if (!SharedCity.Follows(part))
            {
                return false;
            }
            TestRefusals.TellKey(Logger, test, "reallife_gp_city_test_host");
            return true;
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

        /// <summary>The player's own rally is set up near them (paid for from their fund). False if there is no room for one now.</summary>
        public bool OwnRally()
        {
            if (_worldFailed || BuildingManager.IsInsideBuilding || !_campaign.OwnRally())
            {
                return false;
            }
            _worldOut = true;
            return true;
        }

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
                else if (Elections && CivicOffice.Enabled && !SharedCity.Follows('G') && !_scenes.Active)
                {
                    kind = OfficePrompt(player, day, hour);
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
            else if (kind == PromptKind.Knock)
            {
                _knockFirst = _knockFirst ?? CivicText.T("reallife_civic_office_door_prompt");
                _hud.Offer(_knockFirst, ButtonKnock, player);
            }
            else if (kind == PromptKind.Poster)
            {
                _posterFirst = _posterFirst ?? CivicText.T("reallife_civic_office_poster_prompt");
                _hud.Offer(_posterFirst, ButtonPoster, player);
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
                case PromptKind.Knock:
                    _scenes.Door(_knockBuilding, _knockDistrict, _knockAddress);
                    break;
                case PromptKind.Poster:
                    PutUpPoster();
                    break;
            }
        }

        // The player's own politics in the street: a poster to put up on the wall in front of them, a door to knock on. Looked at once a second, and only during a run.
        private PromptKind OfficePrompt(Vector3 player, int day, int hour)
        {
            OfficeState s = Office.State;
            if (!s.Running)
            {
                return PromptKind.None;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            // (never beside a door: there the key is the door's)
            if (s.PostersHeld > 0 && Office.Campaigning && controller != null && NearestDoor(player, 2.5f) == null
                && _campaign.WallAhead(player, controller.transform.forward, out _posterAt, out _posterNormal))
            {
                return PromptKind.Poster;
            }
            if (hour < OfficeRules.DoorsFrom || hour >= OfficeRules.DoorsTo)
            {
                return PromptKind.None;
            }
            // Somebody's home: a residential building's street door, not the player's own (the key opens every other door as before).
            CityBuildingController building = NearestDoor(player, 3f);
            if (building == null || building.building == null || building.building.BuildingType != "ba:buildingtype_residential"
                || !Office.MayKnock(day, hour, building.building.Neighbourhood, building.GetInstanceID()) || CivicOffice.IsMine(building))
            {
                return PromptKind.None;
            }
            _knockBuilding = building.GetInstanceID();
            _knockDistrict = building.building.Neighbourhood;
            _knockAddress = building.building.Address != null ? building.building.Address.ToFormattedStringSafe() : null;
            return PromptKind.Knock;
        }

        // The building whose street door is nearest the player, within reach: found through the colliders round the player, as the mod's own way into a building is.
        private static CityBuildingController NearestDoor(Vector3 player, float reach)
        {
            if (_buildingMask == 0)
            {
                _buildingMask = LayerMask.GetMask("Buildings", "BuildingsOutlined");
                if (_buildingMask == 0)
                {
                    _buildingMask = Physics.AllLayers;
                }
            }
            CityBuildingController best = null;
            float bestSqr = reach * reach;
            int count = Physics.OverlapSphereNonAlloc(player, 12f, Nearby, _buildingMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                CityBuildingController controller = Nearby[i] != null ? Nearby[i].GetComponentInParent<CityBuildingController>() : null;
                if (!CivicCity.HasDoor(controller) || controller.building.locatedInsidePrivateFenceIndex >= 0)
                {
                    continue;
                }
                float sqr = (controller.entranceDoors[0].doorTransform.position - player).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = controller;
                }
            }
            return best;
        }

        private void PutUpPoster()
        {
            Vector3 player = PlayerHelper.GetPosition();
            if (Office.State.PostersHeld <= 0 || !_campaign.PutUp(_posterAt, _posterNormal, player))
            {
                return;
            }
            Office.PosterUp(NeighborhoodHelper.CurrentNeighborhood);
            _worldOut = true;
            CivicText.Toast(UI.Notification.NotificationType.Info, "reallife_civic_office_poster_up", "count", Office.State.PostersHeld.ToString(), 6f);
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
            if (data == null || !data.TryGetValue(KeyOffice, out string office) || string.IsNullOrEmpty(office) || !Office.State.Read(office))
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
                SaveGameManager.Current.modData[KeyOffice] = Office.State.Write();
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
                if (registration == null || !GoingPublic.Mine(registration) || !registration.HasEstablishedBusiness)
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
            if (!TestReady("Civic campaign", false) || HostOwns("Civic campaign", 'G'))
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
            if (!TestReady("Civic election day", false) || HostOwns("Civic election day", 'G'))
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
            if (!TestReady("Civic policy", false) || HostOwns("Civic policy", 'G'))
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
            if (!TestReady("Civic revert", false) || HostOwns("Civic revert", 'G'))
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
            if (!TestReady(test, true) || HostOwns(test, 'W'))
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
            if (!TestReady("Civic advance", false) || HostOwns("Civic advance", 'W'))
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
            if (!Government.Campaigning && HostOwns("Civic rally", 'G'))
            {
                return;   // (a rally of a campaign that is on is this player's to see; a campaign is the host's to start)
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

        // ---------------------------------------------------------------- the player's own politics: test buttons

        private bool OfficeTestReady(string test)
        {
            if (!TestReady(test, false) || HostOwns(test, 'G'))
            {
                return false;
            }
            if (!Settings.CivicElections || !Settings.CivicOffice)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        /// <summary>Test: the player is a candidate for a race now (a campaign is started if none is on), papers filed, a platform drawn for them.</summary>
        public void TestStand(OfficeRace race)
        {
            string test = "Civic stand for " + race;
            if (!OfficeTestReady(test))
            {
                return;
            }
            try
            {
                CivicText.Toast(UI.Notification.NotificationType.Info, Office.TestFile(race, TimeHelper.CurrentDay), 9f);
                Logger?.Info("Civic test: the player is a candidate for " + race + " in the election of day " + Government.State.ElectionDay + ".");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the player holds an office now, as if just elected; the council meets today.</summary>
        public void TestOffice(OfficeRace race)
        {
            string test = "Civic office " + race;
            if (!OfficeTestReady(test))
            {
                return;
            }
            try
            {
                Office.TestOffice(race, TimeHelper.CurrentDay);
                OnLevers();
                CivicText.Toast(UI.Notification.NotificationType.Info, race == OfficeRace.Mayor ? "reallife_civic_office_test_mayor" : "reallife_civic_office_test_council", 9f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: today's scene of the player's run or office now (the debate while a candidate, else a council meeting while in office).</summary>
        public void TestScene()
        {
            if (!OfficeTestReady("Civic office scene"))
            {
                return;
            }
            try
            {
                if (_scenes.Active || !OfficeScenes.Free())
                {
                    TestRefusals.Tell(Logger, "Civic office scene", TestRefusal.ScreenBusy);
                    return;
                }
                int day = TimeHelper.CurrentDay;
                if (Office.Campaigning)
                {
                    Government.State.ElectionDay = day + CivicCatalogue.DebateBefore;
                    Office.State.ForDay = Government.State.ElectionDay;
                    Office.State.Debate = 0;
                    _scenes.Tick(day, 19);
                }
                else if (Office.State.InOffice)
                {
                    Office.State.NextMeeting = day;
                    _scenes.Tick(day, OfficeRules.MeetingHour);
                }
                else
                {
                    TestRefusals.Tell(Logger, "Civic office scene", TestRefusal.NothingFits, 0, "the player is neither a candidate nor in office");
                }
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
            if (HostOwns("Civic clear", 'W'))
            {
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
