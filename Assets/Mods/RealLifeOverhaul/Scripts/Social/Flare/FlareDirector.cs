using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare, the city's social media app: an account, posts only about what really happened, friends who follow and
    /// answer. Cost: nothing per frame, open or closed: a one-second real-time timer holds it, the work runs on the game's
    /// hour (a step a frame, FlareHourSteps), day and save, on Wiring/FlareLinks and on clicks. A fault switches Flare off
    /// for the session.
    /// </summary>
    public sealed partial class FlareDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeFlare";
        public const int TabFeed = 0, TabPost = 1, TabMe = 2, TabBusiness = 3, TabCity = 4, TabNotices = 5, TabMessages = 6, TabExplore = 7;
        private static readonly string[] AppGlyphs = { "Icon-Heart", "Icon-Chat", "Icon-People" };

        public static IModLogger Logger;
        public static FlareDirector Instance { get; private set; }

        private readonly List<FlarePerson> _people = new List<FlarePerson>(12);
        private readonly FlareHourSteps _hourSteps = new FlareHourSteps();
        private int _dayStep = -1;        // the next of the day's steps (DayStep), or -1 when none is owed
        private int _dayFor;               // the day they are for
        private FlareDayReport _dayReport; // what the first step worked out, for the last
        private bool _dayStepping;         // the day's steps' coroutine is running
        private bool _stepping;   // the hour's steps' coroutine is running
        private FlareFile _file;
        private FlareApp _view;
        private PhoneApp _app;
        private bool _running, _broken, _gaveUp;
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openFeed, _openFeedTab, _openBusinessTab, _openMessages, _openCompose;

        public bool Running => _running && !_broken;

        /// <summary>The save's account; null while Flare is not running.</summary>
        public FlareFile File => Running ? _file : null;

        public IReadOnlyList<FlarePerson> People => _people;

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _openFeed = () => Open(TabMe);
            _openFeedTab = () => Open(TabFeed);
            _openBusinessTab = () => Open(TabBusiness);
            _openMessages = () => Open(TabMessages);
            _openCompose = () => Open(TabPost);
            ShareRegister();
            FlareFacePainter.HoldAtLoad();   // the likeliest faces are drawn while the loading screen is up (Core/LoadHold)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            try
            {
                StandDown(false);
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

        // The first look is a second after the city's objects are made, never in the frame they are made in.
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
                while (StartLine.Holds(StartSeats.Flare))
                {
                    yield return null;   // its first load after a save waits for a frame of its own (Core/StartLine)
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

        private static readonly int HotTick = HotSpots.Slot("Social: FlareDirector.Tick");
        // Once a second: start when the save is ready, stand down when the option goes off, hold the handlers and the app.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.SocialMedia)
            {
                if (_running)
                {
                    StandDown(true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            ShareSecond();
            InvitesTick();   // a yes due is booked; one comparison otherwise
            if (Running)
            {
                PhotosSecond();
                CommentsSecond();   // a comment's words came back: the next is asked; one comparison otherwise
            }
        }

        private void StartUp()
        {
            int day = Save.Day;
            FlareFile file = FlareStore.Load(Save.modData);
            bool fresh = file == null;
            ReadPeople();
            if (fresh)
            {
                int seed;
                unchecked
                {
                    seed = (int)(Environment.TickCount * 31 + day * 7919 + (int)Save.Money);
                }
                file = FlareRules.Open(seed, day, _people.Count, FlareBridges.Standing());
                string employer;
                file.CareerKey = FlareBridges.Career != null ? FlareBridges.Career(out employer) : -1;   // what was there before Flare is no news
                string voted, endorsed;
                file.CivicSeen = FlareBridges.Civic != null ? FlareBridges.Civic(out voted, out endorsed) : 0;
                List<BuildingRegistration> shops = FlareGame.Businesses();
                for (int i = 0; i < shops.Count; i++)
                {
                    FlareRules.ShopDay(file, FlareGame.Place(shops[i]), 0, day);
                }
            }
            Cast(file, day);
            LiveStart(file);
            ForgetAsked();
            _file = file;
            _running = true;
            if (FlareLiveRules.StartDay(file.Net, day, Following))
            {
                LiveDay(day);   // the city's posts of today now (an update to this version, a new account): not an empty feed until tomorrow
            }
            ShareStart();
            PhotosStart();
            Logger?.Info("Flare: " + (fresh ? "an account is opened for this save on day " + day + " with " + file.Followers + " followers (" + _people.Count + " people met, seed "
                                            + file.Seed + ")"
                                            : "the account is read: " + file.Followers + " followers, reputation " + file.Reputation + ", " + file.Posts.Count + " post(s), "
                                              + file.Drafts.Count + " draft(s)") + ".");
            Badge();
        }

        // Flare stops and the app leaves the phone; with 'keep' the account goes into the save's data first.
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && _file != null)
            {
                FinishHours();
                FinishDay();
                FlareStore.Save(Save.modData, _file);
            }
            _dayStep = -1;
            _hourSteps.Clear();
            ShareStop();
            PhotosStop();
            FlareBridges.Watched?.Invoke(false, false);   // no patrols or cleaners of Flare's once it stops (Crime/CityWatch back to 1)
            _running = false;
            _file = null;
            ForgetAsked();
            if (_view != null)
            {
                _view.Dispose();
            }
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
            _gaveUp = false;
        }

        /// <summary>A fault: one line, and nothing more of Flare this session; the save is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown(false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Flare: switched off for this session after an error in " + where
                         + ". The account stands still; what the save holds of it is untouched, and the game and the rest of the mod are not affected.");
        }

        /// <summary>For the options page: everything of Flare's for this save is forgotten, and a new account opens on the next second.</summary>
        public void Forget()
        {
            _running = false;
            _file = null;
            ForgetAsked();
            if (_view != null)
            {
                _view.Changed();
            }
        }

        // What was asked of RLO Texts and the comments: by this file's post numbers.
        private void ForgetAsked()
        {
            _textAsked.Clear();
            _textDms.Clear();
            _textAnswers.Clear();
            CommentsForget();
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new FlareApp(this);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_flare_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(255, 150, 92, 255), new Color32(214, 62, 98, 255), 0.42f, "AppIcon-Flare"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Flare: the game's phone is not built as this version expects, so the Flare app is left off. The account still keeps its drafts and followers.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private static readonly int HotOnHour = HotSpots.Slot("Social: FlareDirector.OnHour");
        // The game's hour: its work queued, done a step a frame from the next frame on (HourSteps).
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                if (!_hourSteps.Add(Save.Day, Save.Hour))
                {
                    FinishOldest();   // a skip faster than the steps: the oldest hour is finished now, in order
                    _hourSteps.Add(Save.Day, Save.Hour);
                }
                if (!_stepping)
                {
                    _stepping = true;
                    StartCoroutine(Guard.Routine(HourSteps()));
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly string[] StepNames =
        {
            "flare: the hour's city posts", "flare: the hour's City Office cases", "flare: the hour's invitations and patrols",
            "flare: the hour's unread count", "flare: the hour's words asked ahead", "flare: the hour's milestones"
        };

        private static readonly int HotHourStep = HotSpots.Slot("Social: FlareDirector.HourSteps");

        // The hour's work a step a frame, from the frame after the game's hour (when every other listener of it runs).
        private IEnumerator HourSteps()
        {
            while (_hourSteps.Busy)
            {
                yield return null;
                if (!Running || Save == null || _file == null)
                {
                    _hourSteps.Clear();
                    break;
                }
                using var hotSpot = HotSpots.Time(HotHourStep);
                try
                {
                    if (_hourSteps.Next(out int day, out int hour, out int step))
                    {
                        using var piece = SessionWatch.Piece(StepNames[step]);
                        HourStep(day, hour, step);
                    }
                }
                catch (Exception e)
                {
                    _hourSteps.Clear();
                    Fault("the hour", e);
                }
            }
            _stepping = false;
        }

        // What is left of the hours waiting, at once: the save, the day and standing down need the hours done first.
        private void FinishHours()
        {
            while (_file != null && _hourSteps.Next(out int day, out int hour, out int step))
            {
                HourStep(day, hour, step);
            }
        }

        private void FinishOldest()
        {
            int waiting = _hourSteps.Waiting;
            while (_file != null && _hourSteps.Waiting == waiting && _hourSteps.Next(out int day, out int hour, out int step))
            {
                HourStep(day, hour, step);
            }
        }

        // One step of an hour (FlareHourSteps): together, in their order, what the hour did in one frame.
        private void HourStep(int day, int hour, int step)
        {
            switch (step)
            {
                case FlareHourSteps.City:
                    CityHour(day, hour);
                    break;
                case FlareHourSteps.Milestones:
                    Milestones(day, hour);
                    break;
                default:
                    LiveHour(day, hour, step);   // the cases, the invitations, the unread count, the words (FlareDirector.Live)
                    break;
            }
        }

        // A promotion, a degree, a vote or an endorsement since the last hour becomes a draft.
        private void Milestones(int day, int hour)
        {
            if (FlareBridges.Career != null)
            {
                string employer;
                int key = FlareBridges.Career(out employer);
                if (FlareRules.Promoted(_file, key))
                {
                    Made(FlareRules.Draft(_file, FlareSource.Promotion, day, hour, -1, null, employer, key % 100));
                }
            }
            if (FlareBridges.Graduated != null)
            {
                string degree = FlareBridges.Graduated();
                if (!string.IsNullOrEmpty(degree))
                {
                    Made(FlareRules.Draft(_file, FlareSource.Graduated, day, hour, -1, null, degree, 0));
                }
            }
            if (FlareBridges.Civic != null)
            {
                string voted, endorsed;
                int fresh = FlareRules.CivicNew(_file, FlareBridges.Civic(out voted, out endorsed));
                if ((fresh & 1) != 0)
                {
                    Made(FlareRules.Draft(_file, FlareSource.Voted, day, hour, -1, null, voted, 0));
                }
                if ((fresh & 2) != 0)
                {
                    Made(FlareRules.Draft(_file, FlareSource.Endorsed, day, hour, -1, null, endorsed, 0));
                }
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Social: FlareDirector.OnDay");
        // After the game's daily run, so yesterday's statement of every business is there. The day's round is queued and played a step a
        // frame from the next frame on (DaySteps): it took 32 to 46 ms in one frame at each midnight in a player's log (28 Oct 2026).
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                FinishHours();   // the day's run follows the hour's, as it did
                FinishDay();     // a day still owed its steps is played now, before this one
                int day = Save.Day;
                if (day != _file.SettledDay)
                {
                    _dayFor = day;
                    _dayStep = 0;
                    if (!_dayStepping)
                    {
                        _dayStepping = true;
                        StartCoroutine(Guard.Routine(DaySteps()));
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private static readonly int HotDayStep = HotSpots.Slot("Social: FlareDirector.DaySteps");

        // The day's round a step a frame, once the hour's steps are done (the day follows the hour's, as it did).
        private IEnumerator DaySteps()
        {
            while (_dayStep >= 0)
            {
                yield return null;
                if (!Running || Save == null || _file == null)
                {
                    _dayStep = -1;
                    break;
                }
                if (_hourSteps.Busy)
                {
                    continue;
                }
                using var hotSpot = HotSpots.Time(HotDayStep);
                try
                {
                    if (_dayStep >= 0)
                    {
                        using var piece = SessionWatch.Piece("flare: a step of the day's round");
                        DayStep();
                    }
                }
                catch (Exception e)
                {
                    _dayStep = -1;
                    Fault("the day", e);
                }
            }
            _dayStepping = false;
        }

        // What is left of the day's steps, at once: the save, the next day and standing down need the day played first.
        private void FinishDay()
        {
            while (_file != null && _dayStep >= 0)
            {
                DayStep();
            }
        }

        // One step of the day's round, in the order it ran in one frame.
        private void DayStep()
        {
            int day = _dayFor;
            int step = _dayStep++;
            switch (step)
            {
                case 0:
                    ReadPeople();
                    Talked(_talked);
                    _dayReport = FlareRules.Day(_file, day, FlareBridges.Standing(), _people);
                    Rewrite(_talked, true);
                    break;
                case 1:
                    CastDay(day);
                    break;
                case 2:
                    CityDay(day);
                    break;
                case 3:
                    LiveDay(day);
                    break;
                case 4:
                    FacesAhead(day);   // the day's likely faces, drawn behind the next black screens
                    break;
                case 5:
                    List<BuildingRegistration> shops = FlareGame.Businesses();
                    for (int i = 0; i < shops.Count; i++)
                    {
                        string place = FlareGame.Place(shops[i]);
                        FlareSource news = FlareRules.ShopDay(_file, place, FlareGame.Takings(shops[i]), day);
                        if (news != FlareSource.None)
                        {
                            Made(FlareRules.Draft(_file, news, day, 8, -1, place, FlareGame.Name(shops[i]), 0));
                        }
                    }
                    break;
                default:
                    _dayStep = -1;
                    FlareDayReport r = _dayReport;
                    if (r.FollowersAfter != r.FollowersBefore || r.ReputationAfter != r.ReputationBefore)
                    {
                        Logger?.Info("Flare: day " + day + ": followers " + r.FollowersBefore + " to " + r.FollowersAfter
                                     + (r.Lost > 0 ? " (" + r.Lost + " drifted away after " + r.Silent + " days without a post)" : string.Empty)
                                     + ", reputation " + r.ReputationBefore + " to " + r.ReputationAfter + " (the city's opinion " + FlareBridges.Standing() + "), "
                                     + r.Following + " friend(s) follow, " + r.FriendPosts + " posted, " + r.DraftsGone + " draft(s) lapsed.");
                    }
                    Changed();
                    break;
            }
        }

        // The whole day's round at once: a test button, and nothing else.
        private void PlayDay(int day)
        {
            _dayFor = day;
            _dayStep = 0;
            FinishDay();
        }

        private void ReadPeople()
        {
            _people.Clear();
            try
            {
                FlareBridges.People?.Invoke(_people);
            }
            catch (Exception e)
            {
                _people.Clear();
                Logger?.Warn("Flare: the people of the social life could not be read, so no friend follows for now. " + e.Message);
            }
        }

        /// <summary>Something happened that the player can post about, told by Wiring/FlareLinks: one draft.</summary>
        public void Happened(FlareSource source, int person, string place, string name, int arg)
        {
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                FlareDraft d = FlareRules.Draft(_file, source, Save.Day, Save.Hour, person, place, name, arg);
                Made(d);
                SelfieBack(d);
                if (source == FlareSource.Arrested)
                {
                    Trended(Save.Day);
                }
            }
            catch (Exception e)
            {
                Fault("a draft", e);
            }
        }

        // A draft was made: one line and the badge, never a notification.
        private void Made(FlareDraft d)
        {
            if (d == null)
            {
                return;
            }
            Logger?.Info("Flare: a draft (" + d.Source + (d.Person >= 0 ? ", with " + PersonName(d.Person) : string.Empty) + (d.Place != null ? ", at " + d.Place : string.Empty)
                         + ") on day " + d.Day + ".");
            Changed();
        }

        /// <summary>The app has been opened: what the badge counted has been seen.</summary>
        public bool AppOpened()
        {
            if (!Running || Save == null)
            {
                return false;
            }
            ReadPeople();
            Names(FlareBridges.Brands, _brands);
            Names(FlareBridges.Candidates, _candidates);
            _file.Unseen = 0;
            Badge();
            return true;
        }

        public void Open(int tab)
        {
            if (_app != null && _view != null && Running)
            {
                _view.Want(tab, null);
                _app.Open();
            }
        }

        /// <summary>The player lost touch with the social life's person in this slot: Flare lets them go (FlareRules.Forget).</summary>
        public void Forget(int index)
        {
            if (_file == null)
            {
                return;
            }
            int posts = FlareRules.Forget(_file, index);
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                if (_people[i].Index == index)
                {
                    _people.RemoveAt(i);   // until the next round reads the social life again
                }
            }
            Logger?.Info("Flare: person " + (index + 1) + " of the social life let go; " + posts + " of their posts and the replies to them taken down.");
        }

        public string PersonName(int index)
        {
            FlarePerson p;
            return index >= 0 && Person(index, out p) ? p.First : null;
        }

        public bool Person(int index, out FlarePerson person)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                if (_people[i].Index == index)
                {
                    person = _people[i];
                    return true;
                }
            }
            person = default(FlarePerson);
            return false;
        }

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Notifications.Show(type, key, pairs != null && pairs.Length > 1 ? FlareText.Data(pairs) : null, 10f, null, Guard.Wrap(click), true, false);
        }

        /// <summary>A notification of Flare's own doing, left out when the Flare notifications option is off; the test entries use <see cref="Tell"/>, which always shows.</summary>
        internal void Notice(NotificationType type, string key, Action click, params string[] pairs)
        {
            if (Settings.SocialMediaNotices)
            {
                Tell(type, key, click, pairs);
            }
        }

        private void Changed()
        {
            Badge();
            if (_view != null)
            {
                _view.Changed();
            }
        }

        private void Badge()
        {
            if (_app != null && _file != null)
            {
                _app.SetBadge(_app.Showing ? 0 : _unread + _file.Drafts.Count);
            }
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
                {
                    FinishHours();   // the save holds every hour that has begun, done
                    FinishDay();     // and the day that has turned
                    FlareStore.Save(Save.modData, _file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
