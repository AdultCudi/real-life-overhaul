using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Rivals seen around the city (docs/features/rivals-seen.md): the rich list's tycoons and the game's own rival owners keep a seeded week that follows how their
    /// business is really doing, are written about where the week has them (the City Ledger's Flare account, the morning paper), and stand at the places near the
    /// player (its part .World). It owns the state and its timing: a second at a time it looks whether a save is ready and the rivals' own feature has made up its
    /// mind (SeenRules.Look), plays a new game day once and a new game hour once, and looks who is near; while a rival stands near the player it glances at the
    /// greeting's panel in between (SeenRules.Glance). It rests in a Going Public session, where the city's people are the host's: nobody appears and nothing is
    /// written. No Update. COST: nothing per frame; a few comparisons a second and five glances a second while a rival stands within
    /// <see cref="SeenCatalogue.GlanceMetres"/>; a pass over the day's rivals and the city's buildings once a game day, the day's rivals and sightings once a game
    /// hour, one placing a look at most.
    /// </summary>
    public sealed partial class SeenDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static SeenDirector Instance { get; private set; }

        private static readonly int HotLook = HotSpots.Slot("Rivals seen: the look");
        private static readonly int HotDay = HotSpots.Slot("Rivals seen: the day's pass");
        private static readonly int HotHour = HotSpots.Slot("Rivals seen: the hour's pass");

        private readonly List<SeenSubject> _subjects = new List<SeenSubject>(32);
        private readonly List<SeenNote> _paper = new List<SeenNote>(SeenCatalogue.PaperMost);
        private readonly SeenPage _page = new SeenPage();
        private readonly WaitForSecondsRealtime _waitLook = new WaitForSecondsRealtime(1f);
        private readonly WaitForSecondsRealtime _waitGlance = new WaitForSecondsRealtime(SeenCatalogue.GlanceTenths / 10f);
        private SeenFile _file;
        private bool _loaded, _broken, _cityWaitSaid;
        private int _readDay = -1;      // the day the rivals and the city's table were last read: stamped only once both are
        private int _readRivals = -1;   // ...and what the rivals' own feature was then
        private int _revision;          // every change to what a page could say: the pages' lines are made again only after one (SeenPage)
        private int _tenths;            // tenths of a second waited since the last look
        private Action _onSave, _onLanguage;
        private Action<SeenNote> _post;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            SeenGame.Logger = logger;
            root.AddComponent<SeenDirector>();
        }

        /// <summary>True while the option and the living economy are on and no Going Public session holds the city's people.</summary>
        public static bool Wanted => SeenRules.Runs(Settings.LivingRivalsSeen, Settings.LivingPopulation, GoingPublic.InSession);

        /// <summary>True while a save is read, the feature is wanted and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Wanted;

        /// <summary>The save's file; null while the feature does not run.</summary>
        public SeenFile File => Running ? _file : null;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _post = Post;
            _onLanguage = Guard.Wrap(() => _revision++);   // the pages' lines were in the language before: made again when next asked
            try
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Combine(LocalizorManager.OnLanguageChanged, _onLanguage);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
                ClearWorld("the city is going");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SeenGame.City.Forget();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // A look a second; while a rival stands near the player, a glance at the greeting's panel every GlanceTenths in between.
        private IEnumerator Tick()
        {
            while (true)
            {
                bool glancing = _glance;
                yield return glancing ? _waitGlance : _waitLook;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    _tenths += glancing ? SeenCatalogue.GlanceTenths : 10;
                    if (_tenths >= 10)
                    {
                        _tenths = 0;
                        Look();
                    }
                    else
                    {
                        Glance();
                    }
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    try
                    {
                        ClearWorld("a fault");
                    }
                    catch (Exception second)
                    {
                        Logger?.Error(second);
                    }
                    Logger?.Warn("Rivals seen: switched off for this session after the error above. Nobody is put in the world and nothing more is written; the save keeps its line as it was.");
                }
            }
        }

        // Once a second: nothing before a save is ready and the rivals' own feature has made up its mind; put away when the feature is not wanted; read the day's
        // rivals and the city when the day turns or the rivals' feature comes on or goes off; play a new day and a new hour once; look who is near.
        private void Look()
        {
            using var hotSpot = HotSpots.Time(HotLook);
            bool ready = SaveGameManager.Current != null && !LoadScene.isLoading && ModState.Loaded;
            int rivals = ready ? Rivals() : SeenRules.RivalsWait;
            SeenStep step = SeenRules.Look(ready, Wanted, _loaded, rivals);
            if (step == SeenStep.PutAway)
            {
                PutAway();
                return;
            }
            if (step != SeenStep.Go)
            {
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            int day = SeenGame.Day, hour = SeenGame.Hour;
            if (SeenRules.ReadAgain(day, _readDay, rivals, _readRivals) && !Pass(day, rivals))
            {
                return;   // the city's buildings are not there yet: the next look reads again
            }
            if (day == _file.Day && hour > _file.Hour)
            {
                HourPass(day, hour);
            }
            World(day, hour, Time.unscaledTime);
        }

        // The rivals' own feature through its bridge; with none, off: nothing waits for tycoons who cannot come.
        private static int Rivals()
        {
            Func<int> rivals = SeenBridges.Rivals;
            return rivals != null ? rivals() : SeenRules.RivalsOff;
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _file = SeenStore.Load(save.modData, out int version) ?? new SeenFile();
            int settled = SeenRules.Settle(_file, SeenGame.Day);
            bool fresh = _file.Seed == 0;
            if (fresh)
            {
                _file.Seed = SeenRules.SeedOf(SeenBridges.Playthrough != null ? SeenBridges.Playthrough() : null);   // once a save, from the playthrough's own id, then kept
            }
            save.modData.Remove(SeenStore.TestKey);   // (an earlier version's copy of the line from before its tests: nothing reads it now)
            _readDay = _readRivals = -1;
            _page.Forget();
            _revision++;
            _loaded = true;
            Logger?.Info("Rivals seen: " + (version > 0 ? "read the save's line (version " + version + "): " + _file.Records.Count + " rival(s) on record, " + _file.Notes.Count
                         + " sighting(s) kept" : "a new file for this save (seed " + _file.Seed + ", from the playthrough's own id)")
                         + (settled > 0 ? "; " + settled + " day(s) of it past today brought back to today" : string.Empty) + ".");
        }

        private void OnSave()
        {
            try
            {
                if (_loaded)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || _file == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[SeenStore.Key] = SeenStore.Write(_file);
        }

        // The option off, the living economy off, or a Going Public session: the line stays as it is, whoever stands in the world goes, and nothing more is written.
        private void PutAway()
        {
            Write();
            ClearWorld(GoingPublic.InSession ? "a Going Public session" : "switched off");
            SeenGame.City.Forget();
            _subjects.Clear();
            _loaded = false;
            _readDay = _readRivals = -1;
            _page.Forget();
            _revision++;
            Logger?.Info("Rivals seen: " + (GoingPublic.InSession ? "a Going Public session, where the city's people are the host's: nobody appears and nothing is written until it ends."
                : "switched off; what the save holds of the rivals is kept and goes on when it is on again."));
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(SeenStore.Key);
                save.modData.Remove(SeenStore.TestKey);
            }
            ClearWorld("the data is reset");
            _file = null;
            _subjects.Clear();
            _loaded = false;
            _readDay = _readRivals = -1;
            _page.Forget();
            _revision++;
        }

        // ---- the day and the hour

        // A new day, the first look after a load, or the rivals' own feature come on or gone off: the day's rivals and the city's table are read (the table needs
        // the rivals' offices), and the day's pass runs if this day has not had it. Nothing is stamped before the city's buildings are there to read, so a table is
        // never left empty for the day. False while they are not.
        private bool Pass(int day, int rivals)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            if (!SeenGame.CityReady())
            {
                if (!_cityWaitSaid)
                {
                    _cityWaitSaid = true;
                    Logger?.Info("Rivals seen: the city's buildings are not there to read yet; the day's pass waits for them.");
                }
                return false;
            }
            SeenGame.City.Subjects(_subjects, day);
            if (!SeenGame.City.Refresh(_subjects))
            {
                return false;
            }
            bool again = day == _readDay;
            _readDay = day;
            _readRivals = rivals;
            _file.Coast = SeenGame.HasCoast();
            bool fresh = day != _file.Day;
            int made = SeenDay.Run(_file, _subjects, day, SeenGame.Level, SeenGame.City.Table);
            _dueHour = -1;   // (the world looks again with the day's rivals)
            _revision++;
            SeenTable table = SeenGame.City.Table;
            if (fresh || again)
            {
                Logger?.Info("Rivals seen: day " + day + (again ? ", read again as the rivals' own feature is now " + (rivals == SeenRules.RivalsOn ? "on" : "off") : string.Empty) + ": "
                             + _subjects.Count + " rival(s) read, " + _file.Records.Count + " on record, " + made + " sighting(s) planned for today; the city has " + table.Eateries
                             + " eating place(s), " + table.OfficesFound + " of " + table.Offices + " office(s) found, " + table.Cars + " car(s) on the ladder.");
                if (Verbose && fresh)
                {
                    bool within = made <= SeenCatalogue.DayMost && _file.Notes.Count <= SeenCatalogue.NotesKept + SeenCatalogue.TestNotesKept;
                    Logger?.Info("Rivals seen check (day " + day + "): " + made + " sighting(s) of " + SeenCatalogue.DayMost + " at most, " + _file.Notes.Count + " kept of "
                                 + SeenCatalogue.NotesKept + " and a test's " + SeenCatalogue.TestNotesKept + ": " + (within ? "PASS" : "PAST THE MOST"));
                }
            }
            return true;
        }

        // A new hour: whether the day's rivals are still there is asked again, then the sightings whose hour has come go out on Flare (nothing of somebody gone since
        // the morning), and the player is seen in turn, a post of the day's like the others.
        private void HourPass(int day, int hour)
        {
            using var hotSpot = HotSpots.Time(HotHour);
            int went = 0;
            try
            {
                went = SeenGame.Still(_subjects);
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: whether the rivals are still there could not be read this hour: " + e.Message);
            }
            if (went > 0)
            {
                _dueHour = -1;   // (who is somewhere is worked out again without them)
                Logger?.Info("Rivals seen: " + went + " rival(s) gone since the morning: nothing more of them is written or shown today.");
            }
            int sent = SeenDay.Hour(_file, _subjects, day, hour, _post);
            SeenNote mine = null;
            try
            {
                string car = SeenGame.PlayerCar();
                bool table = SeenGame.PlayerTable(out string place, out string district);
                if (car != null || table)
                {
                    SeenTable city = SeenGame.City.Table;
                    int rung = SeenRules.RungOf(city.CarPlace(car), city.Cars);
                    mine = SeenDay.Player(_file, day, hour, SeenGame.PlayerKnown(), SeenGame.PlayerName(), rung, car, table ? place : null, district, _post);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the player's car or table could not be read this hour: " + e.Message);
            }
            _revision++;
            if (mine != null)
            {
                SeenGame.Tell(NotificationType.Info, "reallife_seen_note_player", new Dictionary<string, string>(1) { { "line", SeenText.Line(mine) } });
                Logger?.Info("Rivals seen: the player was seen " + (mine.Kind == SeenNoteKind.PlayerCar ? "in a " + mine.Car : "at " + mine.Place) + " on day " + day + " at " + hour
                             + ":00, " + SeenDay.Posted(_file, day) + " of " + SeenCatalogue.PostsMost + " posts today.");
            }
            if (sent > 0)
            {
                Logger?.Info("Rivals seen: " + sent + " sighting(s) went out at " + hour + ":00 on day " + day + ", " + SeenDay.Posted(_file, day) + " of " + SeenCatalogue.PostsMost
                             + " posts today.");
            }
        }

        // A sighting on the City Ledger's Flare account, at its hour and a minute of its own.
        private void Post(SeenNote n)
        {
            string words = SeenText.Line(n);
            if (words.Length == 0 || SeenBridges.Post == null)
            {
                return;
            }
            int minute = 3 + SeenRules.Roll(_file.Seed, SeenRules.Who(n.Key), n.Day * 24 + n.Hour, 77) % 54;
            SeenBridges.Post(words, n.Hour, minute);
        }

        /// <summary>
        /// The society lines of a morning's paper: the sightings of the day before (SeenDay.Paper). Nothing while the feature does not run. Read from the file only,
        /// so the same day gives the same lines however often it is asked.
        /// </summary>
        public void Stories(int day, List<NewsItem> into)
        {
            SeenFile f = File;
            if (f == null || into == null)
            {
                return;
            }
            SeenDay.Paper(f, day, _paper);
            for (int i = 0; i < _paper.Count; i++)
            {
                string line = SeenText.Line(_paper[i]);
                if (line.Length > 0)
                {
                    into.Add(new NewsItem { Key = "reallife_seen_paper", Data = new Dictionary<string, string>(1) { { "line", line } } });
                }
            }
            _paper.Clear();
        }

        // ---- the pages

        /// <summary>
        /// What the Rivals app says of one of the game's rival owners, by the game's own id: the car they drive (null with none on record) and where they are usually
        /// found, in a few words. False, both null, for somebody not on record or gone, and while the feature does not run. Made again only when something it says
        /// has changed (SeenPage).
        /// </summary>
        public bool Owner(string gameId, out string car, out string found)
        {
            SeenFile f = File;
            if (f == null)
            {
                car = found = null;
                return false;
            }
            return _page.Owner(f, _subjects, gameId, _revision, SeenText.Words, out car, out found);
        }

        /// <summary>One line for a tycoon's panel and the rich list: what they drive and where they tend to be found; empty with nothing to say.</summary>
        public string TycoonLine(int index)
        {
            SeenFile f = File;
            return f != null ? _page.Tycoon(f, _subjects, index, SeenGame.Day, SeenGame.Hour, _revision, SeenText.Words) : string.Empty;
        }
    }
}
