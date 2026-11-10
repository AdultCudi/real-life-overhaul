using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Rivals seen around the city (docs/features/rivals-seen.md): the rich list's tycoons and the game's own rival owners keep a seeded week that follows how their
    /// business is really doing, are written about where the week has them (the City Ledger's Flare account, the morning paper), and stand at the places near the
    /// player (its part .World). It owns the state and its timing: a second at a time it looks whether a save is ready, plays a new game day once and a new game hour
    /// once, and looks who is near. It rests in a Going Public session, where the city's people are the host's: nobody appears and nothing is written. No Update.
    /// COST: nothing per frame; a few comparisons a second, a pass over the day's rivals once a game day and once a game hour.
    /// </summary>
    public sealed partial class SeenDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static SeenDirector Instance { get; private set; }

        private static readonly int HotDay = HotSpots.Slot("Rivals seen: the day's pass");
        private static readonly int HotHour = HotSpots.Slot("Rivals seen: the hour's pass");

        private readonly List<SeenSubject> _subjects = new List<SeenSubject>(32);
        private readonly List<SeenNote> _paper = new List<SeenNote>(SeenCatalogue.PaperMost);
        private SeenFile _file;
        private bool _loaded, _broken;
        private int _subjectsDay = -1;
        private Action _onSave;
        private Action<SeenNote> _post;
        private string _beforeTests;

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
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
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
                    Look();
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

        // Once a second: start when the save is ready, put away when the feature is not wanted, play a new day and a new hour once, look who is near.
        private void Look()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Wanted)
            {
                if (_loaded)
                {
                    PutAway();
                }
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            int day = SeenGame.Day, hour = SeenGame.Hour;
            if (day != _subjectsDay)
            {
                Pass(day);
            }
            if (day == _file.Day && hour > _file.Hour)
            {
                HourPass(day, hour);
            }
            World(day, hour, Time.unscaledTime);
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _file = SeenStore.Load(save.modData, out int version) ?? new SeenFile();
            if (_file.Seed == 0)
            {
                int seed = SeenBridges.Seed != null ? SeenBridges.Seed() : 0;
                _file.Seed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every rival's week comes from it
            }
            _beforeTests = save.modData.TryGetValue(SeenStore.TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _subjectsDay = -1;
            _loaded = true;
            Logger?.Info("Rivals seen: " + (version > 0 ? "read the save's line (version " + version + "): " + _file.Records.Count + " rival(s) on record, " + _file.Notes.Count
                         + " sighting(s) kept" : "a new file for this save (seed " + _file.Seed + ")") + ".");
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
            if (_beforeTests != null)
            {
                save.modData[SeenStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(SeenStore.TestKey);
            }
        }

        // The option off, the living economy off, or a Going Public session: the line stays as it is, whoever stands in the world goes, and nothing more is written.
        private void PutAway()
        {
            Write();
            ClearWorld(GoingPublic.InSession ? "a Going Public session" : "switched off");
            SeenGame.City.Forget();
            _subjects.Clear();
            _loaded = false;
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
            _beforeTests = null;
            _subjects.Clear();
            _loaded = false;
        }

        // ---- the day and the hour

        // A new day (or the first look after a load): the city's table and the day's rivals are read, and the day's pass runs if this day has not had it.
        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            _subjectsDay = day;
            SeenGame.City.Refresh(day);
            SeenGame.City.Subjects(_subjects, day);
            _file.Coast = SeenGame.HasCoast();
            bool fresh = day != _file.Day;
            int made = SeenDay.Run(_file, _subjects, day, SeenGame.Level, SeenGame.City);
            _dueHour = -1;   // (the world looks again with the day's rivals)
            if (fresh)
            {
                Logger?.Info("Rivals seen: day " + day + ": " + _subjects.Count + " rival(s) read, " + _file.Records.Count + " on record, " + made + " sighting(s) planned for today.");
                if (Verbose)
                {
                    Logger?.Info("Rivals seen check (day " + day + "): " + made + " sighting(s) of " + SeenCatalogue.DayMost + " at most, " + _file.Notes.Count + " kept of "
                                 + SeenCatalogue.NotesKept + ": " + (made <= SeenCatalogue.DayMost && _file.Notes.Count <= SeenCatalogue.NotesKept ? "PASS" : "PAST THE MOST"));
                }
            }
        }

        // A new hour: the sightings whose hour has come go out on Flare, and the player is seen in turn.
        private void HourPass(int day, int hour)
        {
            using var hotSpot = HotSpots.Time(HotHour);
            int sent = SeenDay.Hour(_file, day, hour, _post);
            SeenNote mine = null;
            try
            {
                string car = SeenGame.PlayerCar();
                bool table = SeenGame.PlayerTable(out string place, out string district);
                if (car != null || table)
                {
                    int rung = SeenRules.RungOf(SeenGame.City.CarPlace(car), SeenGame.City.Cars);
                    mine = SeenDay.Player(_file, day, hour, SeenGame.PlayerKnown(), SeenGame.PlayerName(), rung, car, table ? place : null, district);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the player's car or table could not be read this hour: " + e.Message);
            }
            if (mine != null)
            {
                mine.Posted = true;
                Post(mine);
                SeenGame.Tell(NotificationType.Info, "reallife_seen_note_player", new Dictionary<string, string>(1) { { "line", SeenText.Line(mine) } });
                Logger?.Info("Rivals seen: the player was seen " + (mine.Kind == SeenNoteKind.PlayerCar ? "in a " + mine.Car : "at " + mine.Place) + " on day " + day + " at " + hour + ":00.");
            }
            if (sent > 0)
            {
                Logger?.Info("Rivals seen: " + sent + " sighting(s) went out at " + hour + ":00 on day " + day + ".");
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
        /// What a rival's page says of them: the car they drive, where they tend to be found (and, for somebody who knows the player well enough, where they will be
        /// next today), and the last sighting. False for somebody not on record or gone.
        /// </summary>
        public bool PageLines(string key, out string car, out string found, out string seen)
        {
            car = found = seen = null;
            SeenFile f = File;
            SeenRecord r = f != null && !string.IsNullOrEmpty(key) ? f.Of(key) : null;
            SeenSubject s = r != null ? Subject(key) : null;
            if (s == null || s.Gone || r.MarkDay < 0)
            {
                return false;
            }
            car = r.Car.Length > 0 ? SeenGame.CarName(r.Car) : null;
            found = SeenText.Habit(r.Form);
            if (r.Known >= SeenRules.KnowsFrom)
            {
                SeenSpot next = SeenRules.Next(f, r, s, SeenGame.Day, SeenGame.Hour, out int at);
                if (next != SeenSpot.Nowhere)
                {
                    found += " " + SeenText.Fill("reallife_seen_today", "spot", SeenText.Spot(next), "hour", at.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            SeenNote last = SeenDay.Last(f, key);
            seen = last != null ? SeenText.Line(last) : null;
            if (string.IsNullOrEmpty(seen))
            {
                seen = null;
            }
            return true;
        }

        /// <summary>One line for a tycoon's panel and the rich list: what they drive and where they tend to be found; empty with nothing to say.</summary>
        public string TycoonLine(int index)
        {
            return PageLines("t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), out string car, out string found, out string _)
                ? (car != null ? SeenText.Fill("reallife_seen_page_car", "car", car) + " " : string.Empty) + found : string.Empty;
        }

        /// <summary>Where a rival tends to be found, in a few words for a narrow place; null for somebody not on record.</summary>
        public string FoundShort(string key)
        {
            SeenFile f = File;
            SeenRecord r = f != null && !string.IsNullOrEmpty(key) ? f.Of(key) : null;
            return r != null && r.MarkDay >= 0 ? SeenText.T(SeenCatalogue.FoundKeys[SeenCatalogue.Clamp((int)r.Form, 0, SeenCatalogue.FoundKeys.Length - 1)]) : null;
        }

        private SeenSubject Subject(string key)
        {
            for (int i = 0; i < _subjects.Count; i++)
            {
                if (_subjects[i].Key == key)
                {
                    return _subjects[i];
                }
            }
            return null;
        }
    }
}
