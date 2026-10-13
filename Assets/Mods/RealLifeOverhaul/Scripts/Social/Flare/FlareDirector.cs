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
    /// Flare, the city's social media app: an account with followers and a reputation, posts made only about what
    /// really happened (a picture or a selfie saved, an outing, a promotion, a shop opened or a record day, a ballot,
    /// an endorsement, a good deed, an arrest), and the player's friends who follow, like and answer. See the Guide's
    /// Flare page.
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for
    /// the save, puts the app on the phone and keeps the three event handlers in place (the game empties its events
    /// on every load). The work is done on the game's own events: the hour (has the player been promoted, voted or
    /// endorsed: two delegate calls and two comparisons), the day (the daily round of FlareRules and a look at each
    /// business's takings), the save, a thing that happened (told by Wiring/FlareLinks, one draft made), and a click in
    /// the app. PER FRAME: NOTHING, open or closed. What a post does to the other features is FlareDirector.Links.cs.
    ///
    /// The rules are game-free (FlareRules, FlareModel, FlareStore); this class reads what they need through
    /// FlareGame and FlareBridges and says what happened. Nothing is written into the game's own records. A fault
    /// anywhere switches Flare off for the session with one line and leaves the game and every other feature alone.
    /// </summary>
    public sealed partial class FlareDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeFlare";
        public const int TabFeed = 0, TabPost = 1, TabMe = 2, TabBusiness = 3, TabCity = 4;
        private static readonly string[] AppGlyphs = { "Icon-Heart", "Icon-Chat", "Icon-People" };

        public static IModLogger Logger;
        public static FlareDirector Instance { get; private set; }

        private readonly List<FlarePerson> _people = new List<FlarePerson>(12);
        private FlareFile _file;
        private FlareApp _view;
        private PhoneApp _app;
        private bool _running, _broken, _gaveUp;
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openFeed, _openFeedTab, _openBusinessTab;

        public bool Running => _running && !_broken;

        /// <summary>The save's account; null while Flare is not running.</summary>
        public FlareFile File => Running ? _file : null;

        public IReadOnlyList<FlarePerson> People => _people;

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _openFeed = () => Open(TabMe);
            _openFeedTab = () => Open(TabFeed);
            _openBusinessTab = () => Open(TabBusiness);
            StartCoroutine(Tick());
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

        // Once a second: start when the save is ready, stand down when the option goes off, hold the handlers, the app on the phone.
        private void Slow()
        {
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
        }

        // ---------------------------------------------------------------- starting and stopping

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
            _file = file;
            _running = true;
            Logger?.Info("Flare: " + (fresh ? "an account is opened for this save on day " + day + " with " + file.Followers + " followers (" + _people.Count + " people met, seed " + file.Seed + ")"
                                            : "the account is read: " + file.Followers + " followers, reputation " + file.Reputation + ", " + file.Posts.Count + " post(s), " + file.Drafts.Count + " draft(s)") + ".");
            Badge();
        }

        // Flare stops: the app leaves the phone. With 'keep' the account goes into the save's data first (a stop in the middle of play).
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && _file != null)
            {
                FlareStore.Save(Save.modData, _file);
            }
            _running = false;
            _file = null;
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

        /// <summary>A fault: one line, and nothing more of Flare this session. What the save holds of it is left as it is.</summary>
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
            Logger?.Warn("Flare: switched off for this session after an error in " + where + ". The account stands still; what the save holds of it is untouched, and the game and the rest of the mod are not affected.");
        }

        /// <summary>For the options page: everything of Flare's for this save is forgotten, and a new account opens on the next second.</summary>
        public void Forget()
        {
            _running = false;
            _file = null;
            if (_view != null)
            {
                _view.Changed();
            }
        }

        private void Install()
        {
            var view = new FlareApp(this);
            GameSprites.Want(AppGlyphs);
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

        // ---------------------------------------------------------------- the hour and the day

        // GlobalEvents.onNewHour: a promotion, a ballot or an endorsement since the last hour becomes a draft.
        private void OnHour()
        {
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                Look(Save.Day, Save.Hour);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private void Look(int day, int hour)
        {
            CityHour(day, hour);
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

        // GlobalEvents.onNewDay: after the game's daily run, so yesterday's statement of every business is there.
        private void OnDay()
        {
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day != _file.SettledDay)
                {
                    PlayDay(day);
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private void PlayDay(int day)
        {
            ReadPeople();
            Talked(_talked);
            FlareDayReport r = FlareRules.Day(_file, day, FlareBridges.Standing(), _people);
            Rewrite(_talked, true);
            CastDay(day);
            CityDay(day);
            List<BuildingRegistration> shops = FlareGame.Businesses();
            for (int i = 0; i < shops.Count; i++)
            {
                FlareSource news = FlareRules.ShopDay(_file, FlareGame.Place(shops[i]), FlareGame.Takings(shops[i]), day);
                if (news != FlareSource.None)
                {
                    Made(FlareRules.Draft(_file, news, day, 8, -1, FlareGame.Place(shops[i]), FlareGame.Name(shops[i]), 0));
                }
            }
            if (r.FollowersAfter != r.FollowersBefore || r.ReputationAfter != r.ReputationBefore)
            {
                Logger?.Info("Flare: day " + day + ": followers " + r.FollowersBefore + " to " + r.FollowersAfter + (r.Lost > 0 ? " (" + r.Lost + " drifted away after " + r.Silent + " days without a post)" : string.Empty)
                             + ", reputation " + r.ReputationBefore + " to " + r.ReputationAfter + " (the city's opinion " + FlareBridges.Standing() + "), " + r.Following + " friend(s) follow, "
                             + r.FriendPosts + " posted, " + r.DraftsGone + " draft(s) lapsed.");
            }
            Changed();
        }

        // The social life's people as they stand now, through the bridge; nobody without it.
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

        // ---------------------------------------------------------------- what happened

        /// <summary>
        /// Something happened that the player can post about (told by Wiring/FlareLinks): a picture saved, an outing,
        /// a good deed, an arrest. One draft; nothing while Flare is not running.
        /// </summary>
        public void Happened(FlareSource source, int person, string place, string name, int arg)
        {
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                Made(FlareRules.Draft(_file, source, Save.Day, Save.Hour, person, place, name, arg));
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

        // A draft was made: one line, and the app's badge (never a notification: the player looks when they like).
        private void Made(FlareDraft d)
        {
            if (d == null)
            {
                return;
            }
            Logger?.Info("Flare: a draft (" + d.Source + (d.Person >= 0 ? ", with " + PersonName(d.Person) : string.Empty) + (d.Place != null ? ", at " + d.Place : string.Empty) + ") on day " + d.Day + ".");
            Changed();
        }

        // ---------------------------------------------------------------- what the app asks

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

        /// <summary>
        /// The player posts a draft. False, with the reason said, if it cannot be posted now (two hours apart, three a
        /// day). What it brought is said by a notification and a line.
        /// </summary>
        public bool PostDraft(int draftId, FlareTone tone, bool tag)
        {
            FlareDraft d = Running ? _file.Draft(draftId) : null;
            if (d == null || Save == null)
            {
                return false;
            }
            try
            {
                int day = Save.Day, minute = FlareGame.MinuteOfDay;
                if (FlareRules.CanPost(_file, day, minute) != FlareRules.Wait.None)
                {
                    Changed();   // the page says when (its Post button is grey until then)
                    return false;
                }
                int before = _file.Followers, reputation = _file.Reputation;
                ReadPeople();
                FlarePost p = FlareRules.Post(_file, d, tone, tag ? d.Person : -1, _people, day, Save.Hour, minute, _testViral);
                _testViral = false;
                Logger?.Info("Flare: posted (" + p.Source + ", " + p.Tone + (p.Tag >= 0 ? ", " + PersonName(p.Tag) + " tagged" : string.Empty) + "): quality " + p.Quality + ", tone fit "
                             + FlareRules.ToneFit(p.Source, p.Tone) + ", seen by " + p.Reach + ", " + p.Likes + " likes, " + p.Replies + " replies, followers " + before + " to " + _file.Followers
                             + ", reputation " + reputation + " to " + _file.Reputation + (p.Viral ? ", viral" : string.Empty) + (p.Bad ? ", went badly" : string.Empty) + ".");
                if (p.Viral && FlareRules.MayNotify(_file, day, Settings.SocialMediaNotices))
                {
                    Tell(NotificationType.Success, "reallife_flare_note_viral", _openFeed, "followers", FlareText.N(p.Gained));
                }
                Influence(p, d);
                CityInfluence(p, d);
                Changed();
                return true;
            }
            catch (Exception e)
            {
                Fault("a post", e);
                return false;
            }
        }

        /// <summary>A friend's first name by the social life's number, or null.</summary>
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

        // ---------------------------------------------------------------- saying things

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Notifications.Show(type, key, pairs != null && pairs.Length > 1 ? FlareText.Data(pairs) : null, 10f, null, click, true, false);
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
                _app.SetBadge(_app.Showing ? 0 : _file.Unseen + _file.Drafts.Count);
            }
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
                {
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
