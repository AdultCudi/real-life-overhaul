using System;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.GameUi;

// The mod's marks on the game's destination guider (Scripts/Core/GameUi/GuideMarkRules.cs, GuideMark.cs, GuideMarkWatch.cs).
// A player, 0.28.0: an accident's "Pedestrian down" marker stayed on screen long after the scene was over. A mark takes
// off only what it put on the guider, but takes that off whatever it believed; the watch takes off, once a second, a mark
// whose owner is gone, stopped, past its time or done with its scene; every mark goes when the city unloads; and the
// accident's own scene is ended when its updates stop, its marker first. A course (a driving lesson or test, a run) takes
// the guider from a scene's mark, so that a pedestrian down nearby never leaves the test without its checkpoint marker.
internal static partial class Program
{
    private static void GuideMarks()
    {
        GuideMarkOurs();
        GuideMarkOthers();
        GuideMarkSweeps();
        GuideMarkLoad();
        GuideMarkAccident();
        GuideMarkSources();
        GuideMarkCost();
        GuideMarkWords();
        GuideMarkCourse();
        GuideMarkCourseTicks();
        GuideMarkCourseSources();
        GuideMarkCourseWords();
    }

    // The game's one JobDestination guider, as the marks see it: whether it marks something, through the stand-in object
    // every place that is no building shares, where, and with which words (the very string it was given).
    private sealed class MarkGuider
    {
        public bool Marks;
        public bool StandIn;
        public float Place;
        public object Words;

        public void Set(float place, object words)
        {
            Marks = true;
            StandIn = true;
            Place = place;
            Words = words;
        }

        public void Reset()
        {
            Marks = false;
            StandIn = false;
            Place = 0f;
            Words = null;
        }
    }

    // One of the mod's marks as GuideMark is one: Show takes a free guider (its own, or a lower mark's for a course), Hide resets it only while it is this mark's.
    private sealed class StandInMark : IGuideMarkEntry
    {
        private readonly GuideMarkBook _book;
        private readonly MarkGuider _guider;
        private readonly object _words = new object();
        private float _at;
        private bool _up;
        public bool Gone, Stopped;
        public bool Over;
        public float End;
        public GuideMarkFate DroppedFor = GuideMarkFate.Keep;
        public bool Cleared;

        public StandInMark(GuideMarkBook book, MarkGuider guider, GuideMarkRank rank = GuideMarkRank.Point)
        {
            _book = book;
            _guider = guider;
            Rank = rank;
        }

        public GuideMarkRank Rank { get; }

        public object Words => _words;

        public bool Show(float at)
        {
            if (!StillOurs() && !_book.MayTake(this, _guider.Marks))
            {
                return false;   // another has it (GuideMark.Show asks the book the same)
            }
            _guider.Set(at, _words);
            _at = at;
            _up = true;
            _book.Took(this);
            return true;
        }

        // GuideMark's Showing before 0.28.1: the stand-in where this mark put it (and nothing else asked).
        public bool OldShowing => _up && _guider.Marks && _guider.StandIn && Math.Abs(_guider.Place - _at) < 0.1f;

        public bool StillOurs()
        {
            return _up && GuideMarkRules.Ours(_guider.Marks, _guider.StandIn, _book.Holds(this), true, ReferenceEquals(_guider.Words, _words), Math.Abs(_guider.Place - _at) < 0.1f);
        }

        public void Hide()
        {
            if (StillOurs())
            {
                _guider.Reset();
                Cleared = true;
            }
            _up = false;
            _book.Let(this);
        }

        public bool OwnerGone => Gone;
        public bool OwnerStopped => Stopped;
        public bool Wanted => !Over;
        public float Until => End;

        public void Drop(GuideMarkFate why, bool clear)
        {
            DroppedFor = why;
            if (clear && StillOurs())
            {
                _guider.Reset();
                Cleared = true;
            }
            _up = false;
            _book.Let(this);
        }
    }

    // Hide clears this mark's words whatever the mark believed.
    private static void GuideMarkOurs()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var accident = new StandInMark(book, guider);
        Check(accident.Show(40f) && accident.StillOurs() && book.Count == 1 && book.Holds(accident), "guide mark: a mark that takes the free guider is up, in the book and its holder");
        guider.Place = 40.7f;   // the stand-in moved under the mark (the old Showing's place test no longer holds)
        Check(!accident.OldShowing && accident.StillOurs(), "guide mark: the stand-in moved but the words are the mark's own: still the mark's (the old test said it was not)");
        accident.Hide();
        Check(!guider.Marks && accident.Cleared && book.Count == 0 && book.Holder == null, "guide mark: Hide clears the mark's own words even when the old Showing said no, and the book forgets it");
        Check(GuideMarkRules.Ours(true, true, true, false, false, true) && !GuideMarkRules.Ours(true, true, true, false, false, false),
            "guide mark: where no words can be read (a mark given none), the stand-in's place decides");
        Check(GuideMarkRules.Ours(true, true, true, true, true, false), "guide mark: with the words its own, a mark whose place reads otherwise is still the guider's");
    }

    // Hide never clears what another feature or the game put on the guider.
    private static void GuideMarkOthers()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var accident = new StandInMark(book, guider);
        var office = new StandInMark(book, guider);
        accident.Show(40f);
        office.Show(90f);
        object job = new object();
        guider.Set(55f, job);   // a job of the mod's sets the guider itself (Jobs/JobDirector), its own place and words
        Check(!accident.StillOurs(), "guide mark: once a job has set the guider, it is no longer the mark's");
        accident.Hide();
        Check(guider.Marks && guider.Words == job && !accident.Cleared, "guide mark: Hide leaves a job's marker on the guider");
        guider.Reset();
        accident.Show(40f);
        guider.Set(40f, job);   // ...even one put at exactly the mark's place
        Check(!accident.StillOurs() && !accident.Show(40f) && guider.Words == job, "guide mark: a job's marker at the mark's own place is still the job's: the mark does not take it");
        accident.Hide();
        Check(guider.Words == job, "guide mark: Hide leaves a job's marker put at the mark's own place");
        guider.Reset();
        accident.Show(40f);
        guider.Reset();          // the game reset the guider (a delivery van hired)
        office.Show(40f);        // ...and another mark took it, at the same place
        Check(!accident.StillOurs() && office.StillOurs() && book.Holds(office), "guide mark: after another mark took the guider, the first is not its holder, even at the same place");
        accident.Hide();
        Check(guider.Marks && guider.Words == office.Words, "guide mark: Hide of the first mark leaves the second mark's marker");
        guider.Marks = true;
        guider.StandIn = false;   // a building's address on the guider (the game's delivery jobs)
        Check(!office.StillOurs(), "guide mark: a guider on a building is never a mark's");
        Check(!GuideMarkRules.Ours(false, true, true, true, true, true) && !GuideMarkRules.Ours(true, false, true, true, true, true) && !GuideMarkRules.Ours(true, true, false, true, true, true)
              && !GuideMarkRules.Ours(true, true, true, true, false, true),
            "guide mark: no guider, another target, another holder or other words: never the mark's");
    }

    // The watch: an orphaned mark goes by its time, its owner gone or stopped, or its scene over; one that lost the guider is only forgotten.
    private static void GuideMarkSweeps()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var mark = new StandInMark(book, guider) { End = 10f };
        mark.Show(5f);
        Check(book.Sweep(9.9f) == 0 && guider.Marks && book.Count == 1, "guide mark: before its time a mark the owner looks after is kept");
        Check(book.Sweep(10f) == 1 && !guider.Marks && mark.DroppedFor == GuideMarkFate.TimeUp && book.Count == 0, "guide mark: at its time the watch takes it off, the guider reset");
        Check(GuideMarkRules.Fate(true, false, false, true, 0f, 1e9f) == GuideMarkFate.Keep && !GuideMarkRules.TimeUp(0f, 1e9f), "guide mark: a mark with no time given is never timed out");

        mark = new StandInMark(book, guider) { Gone = true };
        mark.Show(5f);
        Check(book.Sweep(1f) == 1 && !guider.Marks && mark.DroppedFor == GuideMarkFate.OwnerGone, "guide mark: a mark whose owner was destroyed without taking it off is taken off");
        mark = new StandInMark(book, guider) { Stopped = true };
        mark.Show(5f);
        Check(book.Sweep(1f) == 1 && !guider.Marks && mark.DroppedFor == GuideMarkFate.OwnerStopped, "guide mark: a mark whose owner the guard stopped is taken off");
        mark = new StandInMark(book, guider) { Over = true };
        mark.Show(5f);
        Check(book.Sweep(1f) == 1 && !guider.Marks && mark.DroppedFor == GuideMarkFate.SceneOver, "guide mark: a mark whose scene is over by its owner's own test (Flare's way to an event) is taken off");

        mark = new StandInMark(book, guider) { Gone = true };
        mark.Show(5f);
        object job = new object();
        guider.Set(70f, job);
        Check(book.Sweep(1f) == 0 && guider.Words == job && mark.DroppedFor == GuideMarkFate.Lost && book.Count == 0,
            "guide mark: a mark that lost the guider is forgotten, and the watch leaves the job's marker alone even with the mark's owner gone");
        guider.Reset();

        var a = new StandInMark(book, guider) { End = 3f };
        var b = new StandInMark(book, guider);
        a.Show(1f);
        guider.Reset();
        b.Show(2f);   // a is still in the book, the guider is b's
        Check(book.Count == 2 && book.Sweep(5f) == 0 && a.DroppedFor == GuideMarkFate.Lost && guider.Words == b.Words && book.Count == 1,
            "guide mark: a mark past its time whose guider another mark has is forgotten, and the other mark stays up");
    }

    // A city that unloads (a save loaded, the menu) takes every mark of the mod's off.
    private static void GuideMarkLoad()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var a = new StandInMark(book, guider);
        var b = new StandInMark(book, guider);
        var c = new StandInMark(book, guider);
        a.Show(1f);
        guider.Reset();
        b.Show(2f);
        guider.Reset();
        c.Show(3f);
        Check(book.Count == 3 && book.DropAll() == 3 && book.Count == 0 && book.Holder == null && !guider.Marks, "guide mark: unloading takes every mark off, the guider reset, none left in the book");
        Check(a.DroppedFor == GuideMarkFate.Unloaded && b.DroppedFor == GuideMarkFate.Unloaded && c.DroppedFor == GuideMarkFate.Unloaded && c.Cleared && !a.Cleared,
            "guide mark: on unloading only the mark the guider shows resets it; the others are forgotten");
        Check(book.DropAll() == 0 && book.Sweep(0f) == 0, "guide mark: an empty book costs nothing and takes nothing off");
    }

    // The player's case: a pedestrian down at 100 s, its director's updates stopping at 150 s. The watch takes the
    // marker off at the latest the scene could last, plus the grace, and not before.
    private static void GuideMarkAccident()
    {
        const float staged = 100f, stops = 150f, hardScene = 300f;   // the accident's HardSceneTime (Traffic/TrafficAccidents)
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var mark = new StandInMark(book, guider);
        float off = -1f;
        for (float now = staged; now < 2000f; now += GuideMarkRules.WatchEvery)
        {
            if (now < stops)
            {
                mark.End = staged + hardScene + GuideMarkRules.SceneGrace;   // UpdateMarker, every tick of the scene
                mark.Show(10f);
            }
            if (book.Sweep(now) > 0)
            {
                off = now;
                break;
            }
        }
        float latest = staged + hardScene + GuideMarkRules.SceneGrace;
        Check(off >= latest && off < latest + GuideMarkRules.WatchEvery + 0.01f && !guider.Marks && mark.DroppedFor == GuideMarkFate.TimeUp,
            "guide mark: an accident's marker with its director not ticking goes at the latest the scene could last plus " + GuideMarkRules.SceneGrace + " s (" + off + " s, wanted " + latest + " s)");
        Check(GuideMarkRules.SceneGrace > 0f && GuideMarkRules.SceneGrace <= 60f && GuideMarkRules.WatchEvery <= 1f, "guide mark: the grace is short and the watch looks every second");
    }

    // The sources: Hide's new rule is GuideMark's, every owner says what keeps its mark, the watch runs with the city,
    // and the accident's scene ends when its updates stop, its marker first.
    private static void GuideMarkSources()
    {
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        string Read(string rel) => System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, rel));
        string mark = Read(@"Core\GameUi\GuideMark.cs");
        string watch = Read(@"Core\GameUi\GuideMarkWatch.cs");
        string start = Read(@"Core\RealLifeOverhaulMod.cs");
        string accidents = Read(@"Traffic\TrafficAccidents.cs");
        int hide = mark.IndexOf("public void Hide()", StringComparison.Ordinal);
        int hideEnd = hide >= 0 ? mark.IndexOf("public static void HideAll", hide, StringComparison.Ordinal) : -1;
        string hideBody = hide >= 0 && hideEnd > hide ? mark.Substring(hide, hideEnd - hide) : string.Empty;
        Check(hideBody.Contains("ResetIfMine()") && !hideBody.Contains("Showing") && mark.Contains("GuideMarkRules.Ours(") && mark.Contains("Book.Holds(this)") && mark.Contains("GameContract.PoiText.Field"),
            "guide mark: Hide resets the guider by the ownership rule (the words, the holder), never by Showing");
        Check(mark.Contains("Took();") && mark.Contains("if (asked && guider != null"), "guide mark: a mark whose call to the game failed after it set the guider is still known, so that it can be taken off");
        Check(watch.Contains("GuideMark.Sweep()") && watch.Contains("GuideMarkRules.WatchEvery") && watch.Contains("Time.unscaledTime") && watch.Contains("HideAll(") && watch.Contains("HotSpots.Time(HotUpdate)"),
            "guide mark: the watch looks once a second, timed, and drops every mark as a city starts and ends");
        Check(start.Contains("Guard.Add<GameUi.GuideMarkWatch>(_root)") && start.Contains("GameUi.GuideMark.HideAll(\"the city is unloading\")") && start.Contains("GameUi.GuideMark.Logger = context.Logger"),
            "guide mark: the watch starts with the city and every mark goes as the city unloads");
        int clear = accidents.IndexOf("private void Clear(bool resumeTraffic", StringComparison.Ordinal);
        int markOff = clear >= 0 ? accidents.IndexOf("_mark.Hide();", clear, StringComparison.Ordinal) : -1;
        int road = clear >= 0 ? accidents.IndexOf("_cordon.Open();", clear, StringComparison.Ordinal) : -1;
        int wrecks = clear >= 0 ? accidents.IndexOf("foreach (Wreck wreck in _wrecks)", clear, StringComparison.Ordinal) : -1;
        Check(markOff > clear && markOff < road && road < wrecks, "guide mark: an accident's Clear takes its marker off first, before anything that could fail");
        Check(accidents.Contains("private void OnDisable()") && accidents.Contains("why: \"its updates stopped\"") && accidents.Contains("_mark.Keep(this);")
              && accidents.Contains("_mark.Until = _hardLimit + GameUi.GuideMarkRules.SceneGrace;"),
            "guide mark: an accident's scene ends when its updates stop, and its marker is kept by the director and timed by the scene's hard limit");
        string[] owners =
        {
            @"Crime\CrimeDirector.cs", @"Events\EventDirector.cs", @"Justice\JusticeDirector.cs", @"Social\SocialDirector.cs", @"Traffic\Reckless\RecklessDrivers.cs",
            @"Transport\Taxi\TaxiDirector.cs", @"Travel\TravelDirector.cs", @"Jobs\CareerDirector.cs", @"Garage\GarageDirector.cs", @"Home\HomeDirector.cs",
            @"Leisure\LeisureDirector.cs", @"Licences\LicenceDirector.cs", @"Traffic\TrafficAccidents.cs"
        };
        var unkept = new System.Collections.Generic.List<string>();
        foreach (string owner in owners)
        {
            string text = Read(owner);
            if (!text.Contains(".Keep(this)") && !text.Contains(".Keep(director)") && !text.Contains(".KeptBy(this)"))
            {
                unkept.Add(owner);
            }
        }
        Check(unkept.Count == 0, "guide mark: every owner of a mark says which component keeps it: " + string.Join(", ", unkept));
        string flare = Read(@"Wiring\FlareLiveLinks.cs");
        Check(flare.Contains(".While(EventGoesOn)") && flare.Contains("EventDirector.StreetEvent(out Vector3 at, out string _)"), "guide mark: Flare's way to a city event goes when that event is over");
        int named = 0;
        var nameless = new System.Collections.Generic.List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            named += System.Text.RegularExpressions.Regex.Matches(text, @"new (RealLifeOverhaul\.)?(GameUi\.)?GuideMark\(""").Count;
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"new (RealLifeOverhaul\.)?(GameUi\.)?GuideMark\(\)"))
            {
                nameless.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(named >= 15 && nameless.Count == 0, "guide mark: every mark of the mod's is made with its owner's name for Player.log (" + named + " named; without: " + string.Join(", ", nameless) + ")");
    }

    // A look of the watch over a full book allocates nothing and costs microseconds.
    private static void GuideMarkCost()
    {
        var books = new GuideMarkBook[16];
        for (int i = 0; i < books.Length; i++)
        {
            books[i] = new GuideMarkBook();
            new StandInMark(books[i], new MarkGuider()).Show(i);   // sixteen marks up, each on a guider of its own, each kept
        }
        GuideMarkLooks(books, 10);
        var clock = new System.Diagnostics.Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        clock.Start();
        GuideMarkLooks(books, 10000);
        double each = clock.Elapsed.TotalMilliseconds / 10000.0;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        int kept = 0;
        foreach (GuideMarkBook book in books)
        {
            kept += book.Count;
        }
        Check(kept == 16 && bytes == 0 && each < 0.05, "guide mark: a look of the watch over 16 marks kept allocates nothing (" + bytes + " bytes) and takes " + each.ToString("F5") + " ms (under 0.05)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void GuideMarkLooks(GuideMarkBook[] books, int times)
    {
        for (int i = 0; i < times; i++)
        {
            for (int b = 0; b < books.Length; b++)
            {
                books[b].Sweep(1f);
            }
        }
    }

    private static void GuideMarkWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("guide mark", langs, codes))
        {
            return;
        }
        string[] wanted =
        {
            "Fixed: the map marker of an accident or another scene could stay on screen after the scene was over.",
            "Corrigido: o marcador no mapa de um acidente ou de outra cena podia continuar na tela depois que a cena terminava.",
            "已修复：事故或其他场景的地图标记可能在场景结束后仍留在屏幕上。"
        };
        bool line = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            bool found = false;
            foreach (var row in langs[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(wanted[l]);
            }
            line &= found;
        }
        Check(line, "guide mark: What's new has the fix's line in English, Portuguese and Chinese, in some reallife_changes_ row");
    }
}

internal static partial class Program
{
    // The player's case: a pedestrian down near a driving test, in either order, and with no test.
    private static void GuideMarkCourse()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var test = new StandInMark(book, guider, GuideMarkRank.Course);
        var accident = new StandInMark(book, guider);
        Check(test.Show(10f) && !accident.Show(40f) && test.StillOurs() && guider.Words == test.Words,
            "guide mark course: a scene that starts during a test does not take the test's checkpoint marker");
        accident.Hide();
        Check(test.StillOurs() && guider.Words == test.Words && !test.Cleared, "guide mark course: the scene's end leaves the test's marker on the guider");
        test.Hide();

        book = new GuideMarkBook();
        guider = new MarkGuider();
        test = new StandInMark(book, guider, GuideMarkRank.Course);
        accident = new StandInMark(book, guider);
        accident.Show(40f);
        Check(test.Show(10f) && test.StillOurs() && !accident.StillOurs() && guider.Words == test.Words && book.Holds(test),
            "guide mark course: a test that starts while a scene has the guider takes it: its checkpoint is marked at once");
        Check(!accident.Show(40f) && test.StillOurs(), "guide mark course: the scene asking again (every tick) does not take the guider back during the test");
        Check(test.Show(25f) && test.StillOurs() && Math.Abs(guider.Place - 25f) < 0.01f, "guide mark course: the test moves its marker on to the next checkpoint with the scene still running");
        Check(book.Sweep(1f) == 0 && accident.DroppedFor == GuideMarkFate.Lost && test.StillOurs(), "guide mark course: the watch only forgets the scene's mark that gave way; the test's stays");
        accident.Hide();
        Check(test.StillOurs() && guider.Words == test.Words && !accident.Cleared, "guide mark course: when the scene ends during the test the test's marker is still there");
        accident.Show(40f);
        test.Hide();
        Check(!guider.Marks && test.Cleared, "guide mark course: the test's end takes its own marker off");
        Check(accident.Show(40f) && accident.StillOurs() && guider.Words == accident.Words, "guide mark course: a scene still running has the guider again once the test is over");
        accident.Hide();

        book = new GuideMarkBook();
        guider = new MarkGuider();
        var scene = new StandInMark(book, guider);
        var taxi = new StandInMark(book, guider);
        Check(scene.Show(40f) && !taxi.Show(70f) && scene.StillOurs(), "guide mark course: with no test, the first mark keeps the guider as before");
        scene.Hide();
        Check(!guider.Marks && taxi.Show(70f) && taxi.StillOurs(), "guide mark course: with no test, the next mark takes the guider once the first lets it go, as before");
        taxi.Hide();

        object job = new object();
        guider.Set(55f, job);   // a job of the mod's or the game's own delivery has set the guider itself
        test = new StandInMark(book, guider, GuideMarkRank.Course);
        Check(!test.Show(10f) && guider.Words == job, "guide mark course: a course never takes a job's or the game's own marker");
        guider.Reset();
        var run = new StandInMark(book, guider, GuideMarkRank.Course);
        test.Show(10f);
        Check(!run.Show(20f) && test.StillOurs(), "guide mark course: of two courses the first keeps the guider");
        test.Hide();
        run.Hide();

        Check(GuideMarkRules.MayTake(false, false, GuideMarkRank.Course, GuideMarkRank.Point) && GuideMarkRules.MayTake(true, true, GuideMarkRank.Point, GuideMarkRank.Course)
              && !GuideMarkRules.MayTake(true, true, GuideMarkRank.Point, GuideMarkRank.Point) && !GuideMarkRules.MayTake(true, true, GuideMarkRank.Course, GuideMarkRank.Point)
              && !GuideMarkRules.MayTake(true, false, GuideMarkRank.Point, GuideMarkRank.Course) && !GuideMarkRules.MayTake(true, true, GuideMarkRank.Course, GuideMarkRank.Course),
            "guide mark course: the rule: a free guider for anyone; a mark's for a course over a point; never a job's, a course's, or a point's for a point");
    }

    // A whole lesson as the director runs it: the test asks five times a second, the scenes (an accident, a crime, a street
    // event) every tick of theirs, and the watch looks every second. After the test's first ask, every look finds its marker.
    private static void GuideMarkCourseTicks()
    {
        var book = new GuideMarkBook();
        var guider = new MarkGuider();
        var test = new StandInMark(book, guider, GuideMarkRank.Course);
        var scenes = new[] { new StandInMark(book, guider), new StandInMark(book, guider), new StandInMark(book, guider) };
        float[] from = { 15f, 40f, 75f }, to = { 90f, 60f, 200f };   // seconds: the accident starts before the test, the street event outlasts it
        int looks = 0, missing = 0;
        float checkpoint = 1f;
        for (int tick = 0; tick < 1500; tick++)
        {
            float now = tick * 0.2f;
            for (int s = 0; s < scenes.Length; s++)
            {
                if (now >= from[s] && now < to[s])
                {
                    scenes[s].Show(100f + s);
                }
                else if (now >= to[s] && now < to[s] + 0.2f)
                {
                    scenes[s].Hide();
                }
            }
            if (now >= 20f && now < 180f)
            {
                if (tick % 50 == 0)
                {
                    checkpoint += 1f;   // the next checkpoint, every ten seconds
                }
                test.Show(checkpoint);
                looks++;
                missing += test.StillOurs() && Math.Abs(guider.Place - checkpoint) < 0.01f ? 0 : 1;
            }
            else if (now >= 180f && now < 180.2f)
            {
                test.Hide();
            }
            if (tick % 5 == 0)
            {
                book.Sweep(now);
            }
        }
        Check(looks > 700 && missing == 0, "guide mark course: through a lesson with three scenes coming and going, the test's checkpoint was marked at every look ("
                                            + missing + " of " + looks + " without)");
        Check(!guider.Marks && book.Count == 0, "guide mark course: once the test and every scene are over, nothing of theirs is left on the guider");
    }

    // The sources: GuideMark asks the book's rule, the driving test and the run are courses, nothing else is, and a
    // driving test's checkpoint counts when driven through whether or not its marker is up.
    private static void GuideMarkCourseSources()
    {
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        string Read(string rel) => System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, rel));
        string mark = Read(@"Core\GameUi\GuideMark.cs");
        Check(mark.Contains("Book.MayTake(this, marks)") && mark.Contains("GuideMarkRank IGuideMarkEntry.Rank => _rank;") && !mark.Contains("if (!mine && guider.target != null)"),
            "guide mark course: GuideMark.Show takes the guider by the book's rule (a course over a point), and says its rank to the book");
        string test = Read(@"Licences\Driving\PracticalTest.cs");
        string run = Read(@"Home\JogRun.cs");
        Check(test.Contains("new GuideMark(\"the driving test\").Ranked(GuideMarkRank.Course)") && run.Contains("new GameUi.GuideMark(\"the run\").Ranked(GameUi.GuideMarkRank.Course)"),
            "guide mark course: the driving lesson and test, and the run, mark their checkpoints as courses");
        var ranked = new System.Collections.Generic.List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string name = System.IO.Path.GetFileName(file);
            if (name != "PracticalTest.cs" && name != "JogRun.cs" && System.IO.File.ReadAllText(file).Contains("GuideMarkRank.Course)"))
            {
                ranked.Add(name);
            }
        }
        Check(ranked.Count == 0, "guide mark course: no scene or errand is a course, so with no course running every mark keeps the guider as before (" + string.Join(", ", ranked) + ")");
        string flat = test.Replace("\r\n", "\n");
        int drive = flat.IndexOf("private void Drive(float now)", StringComparison.Ordinal);
        int driveEnd = drive >= 0 ? flat.IndexOf("\n        private ", drive + 10, StringComparison.Ordinal) : -1;
        string body = drive >= 0 && driveEnd > drive ? flat.Substring(drive, driveEnd - drive) : string.Empty;
        Check(body.Contains("_route.Checkpoints[_next] - position") && body.Contains("sqr < Reach * Reach") && !body.Contains("_mark"),
            "guide mark course: a test's checkpoint counts when the car is within reach of it, whether or not its marker is up");
    }

    private static void GuideMarkCourseWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("guide mark course", langs, codes))
        {
            return;
        }
        string[] wanted =
        {
            "Fixed: a street scene during a driving lesson or test, such as a pedestrian hit by traffic, no longer stops the checkpoints from showing.",
            "Corrigido: uma cena de rua durante uma aula ou prova de direção, como um pedestre atropelado, não impede mais que os pontos de controle apareçam.",
            "已修复：驾驶课或驾驶考试期间发生的街头场景（例如行人被车撞倒）不再导致检查点无法显示。"
        };
        bool line = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            bool found = false;
            foreach (var row in langs[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(wanted[l]);
            }
            line &= found;
        }
        Check(line, "guide mark course: What's new has the fix's line in English, Portuguese and Chinese, in some reallife_changes_ row");
    }
}
