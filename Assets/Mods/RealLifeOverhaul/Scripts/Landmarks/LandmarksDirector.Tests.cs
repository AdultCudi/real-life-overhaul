using System;
using System.Collections.Generic;
using RealLifeOverhaul.Uptown;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// The landmarks' test entries (Options > Tests): each works from wherever the player is and brings forward what it
    /// needs (announced, awarded, finished, opened, a match day) so nothing is waited for or looked for, writes one
    /// "Landmarks check (...)" line, and says in a notice what it did or why not. Each is a public static method a test
    /// runner calls with no input. The state before the first test is kept for "put the landmarks back".
    /// </summary>
    public sealed partial class LandmarksDirector
    {
        private const string TestName = "Landmarks";
        private const float TakeWait = 20f;   // s of real time the game is given to take the player out of a building or a car

        private static int _testOpening = -1;   // the landmark whose opening a test holds today, its ceremony at any hour
        private static int _testMatch = -1;     // the day a test made a match day
        private static readonly List<string> TestBefore = new List<string>(16);   // the district lots the tests finished, to take back

        // Why a test cannot run now, said in a notice; false when it can.
        private static bool Refused(string test, bool needsDistrict)
        {
            if (LandmarksGame.Loading || Instance == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return true;
            }
            if (!Settings.Landmarks)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return true;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                Load();
            }
            if (_unread || _broken)
            {
                TestRefusals.TellKey(Logger, test, LandmarksText.TestUnread, _unread ? "its line in this save cannot be read" : "it was switched off after a fault");
                return true;
            }
            if (needsDistrict && !DistrictBuildings.Lives)
            {
                TestRefusals.TellKey(Logger, test, LandmarksText.TestNoDistrict, "Uptown is not a district of the game at this load");
                return true;
            }
            if (Instance != null && !Instance._running)
            {
                Instance._running = true;
                Instance._lastHour = -1;
            }
            return false;
        }

        // The state as it is before a test first changes it, kept once to be put back.
        private static void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = LandmarkStore.Write(_state);
                TestBefore.Clear();
            }
        }

        private static void Done(string line, bool pass, string notice, string name)
        {
            line += ": " + (pass ? "PASS" : "FAIL");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
            Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, notice, name != null ? new Dictionary<string, string>(1) { { "name", name } } : null, 10f);
        }

        // ---------------------------------------------------------------- the census

        /// <summary>"Landmarks: census": each landmark's stage, site and held lots, what stands of it, and the lift of the lots round it, against what the rules say.</summary>
        public static void TestCensus()
        {
            const string test = "Landmarks census";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                RefreshSites();
                bool pass = true;
                var t = new System.Text.StringBuilder(400);
                int held = 0;
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    LandmarkFile f = _state.Files[k];
                    LandmarkSite site = Sites[k];
                    t.Append(k > 0 ? "; " : string.Empty).Append(LandmarkRules.Name(f.Kind)).Append(' ').Append(f.Stage);
                    if (!SiteKnown[k])
                    {
                        t.Append(" with no site (").Append(DistrictBuildings.Lives ? "no block of its own is free" : "the district does not stand").Append(')');
                        pass &= f.Block == 0;
                        continue;
                    }
                    held += site.Lots.Count;
                    t.Append(" on block ").Append(site.Block).Append(", ").Append(site.Lots.Count).Append(" lot(s), ").Append(Mathf.RoundToInt(site.Ground.Width)).Append(" by ")
                     .Append(Mathf.RoundToInt(site.Ground.Depth)).Append(" m");
                    for (int i = 0; i < site.Lots.Count; i++)
                    {
                        pass &= !Settings.Landmarks || HeldLots.Contains(site.Lots[i]);
                    }
                    if (f.Kind != LandmarkKind.Stadium && f.Built)
                    {
                        bool stands = DistrictBuildings.Finished(site.Address), waits = DistrictBuildings.Taken(site.Address);
                        t.Append(", its building ").Append(stands ? "standing" : waits ? "the district's from the next load" : "not the district's yet").Append(" (").Append(f.Design).Append(')');
                        pass &= waits;
                    }
                    if (f.Kind == LandmarkKind.Stadium && f.Built)
                    {
                        bool shell = Instance._pieces[k] != null;
                        t.Append(", its stands ").Append(shell ? "standing" : "not stood yet");
                        pass &= shell || !PropLibrary.Settled;
                    }
                    if (f.Built)
                    {
                        LandmarkRules.Front(site.Ground, out float x, out float z);
                        Lot near = Nearest(x, z, site);
                        if (near != null)
                        {
                            t.Append(", the lot ").Append(near.Id).Append(" beside it lifted ").Append(LiftOf(near.Id));
                        }
                    }
                }
                pass &= held == HeldLots.Count || !Settings.Landmarks;
                Done("Landmarks check (census): " + t + "; " + HeldLots.Count + " lot(s) held", pass, pass ? LandmarksText.TestCensusDone : LandmarksText.TestCensusOdd, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The district's lot nearest a point that is not part of the landmark's own site.
        private static Lot Nearest(float x, float z, LandmarkSite site)
        {
            Lot best = null;
            float least = float.MaxValue;
            IReadOnlyList<Lot> first = UptownLots.FirstBlock;
            for (int i = 0; first != null && i < first.Count; i++)
            {
                if (site.Lots.Contains(first[i].Id))
                {
                    continue;
                }
                float d = (first[i].X - x) * (first[i].X - x) + (first[i].Z - z) * (first[i].Z - z);
                if (d < least)
                {
                    least = d;
                    best = first[i];
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- announced and awarded

        /// <summary>"Landmarks: the next landmark announced now": the first not yet announced is, today, its tender out or the city's works planned.</summary>
        public static void TestAnnounce()
        {
            const string test = "Landmarks announce";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                RefreshSites();
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    LandmarkFile f = _state.Files[k];
                    if (f.Stage != LandmarkStage.Planned)
                    {
                        continue;
                    }
                    if (!SiteKnown[k])
                    {
                        TestRefusals.TellKey(Logger, test, LandmarksText.TestNoSite, LandmarkRules.Name(f.Kind) + " has no free block");
                        return;
                    }
                    BeforeTest();
                    int day = LandmarksGame.Day;
                    if (_state.Anchor <= 0)
                    {
                        _state.Anchor = day;
                    }
                    Instance.Announce(f, Sites[k], day);
                    Written();
                    bool pass = f.Stage == LandmarkStage.Tendered || f.Stage == LandmarkStage.Building;
                    Done("Landmarks check (announce): " + LandmarkRules.Name(f.Kind) + " announced on day " + day + " on block " + f.Block + ", "
                         + (f.Stage == LandmarkStage.Tendered ? "out to tender as tender " + f.Tender : "built by the city's own works to day " + f.Due), pass, LandmarksText.TestAnnounced, NameOf(f.Kind));
                    return;
                }
                TestRefusals.TellKey(Logger, test, LandmarksText.TestNothing, "every landmark has been announced");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Landmarks: its tender awarded now": the first landmark out to tender has its bids opened now, the player's among them if they bid.</summary>
        public static void TestAward()
        {
            const string test = "Landmarks award";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    LandmarkFile f = _state.Files[k];
                    if (f.Stage != LandmarkStage.Tendered)
                    {
                        continue;
                    }
                    BeforeTest();
                    bool awarded = LandmarkBridges.AwardTender != null && LandmarkBridges.AwardTender(f.Tender);
                    if (!awarded)
                    {
                        Instance.CityWorks(f, LandmarksGame.Day, "a test found its tender gone");
                    }
                    Written();
                    bool pass = f.Stage == LandmarkStage.Building;
                    Done("Landmarks check (award): " + LandmarkRules.Name(f.Kind) + "'s tender " + (awarded ? "awarded" : "gone, the city's own works") + ": built by " + f.Builder
                         + (f.Due > 0 ? " to day " + f.Due : " (the player's project)"), pass, LandmarksText.TestAwarded, NameOf(f.Kind));
                    return;
                }
                TestRefusals.TellKey(Logger, test, LandmarksText.TestNothing, "no landmark is out to tender (press 'the next landmark announced now' first)");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- finished now

        /// <summary>"Landmarks: Summit Tower finished now".</summary>
        public static void TestTowerNow() => FinishNow(LandmarkKind.Tower, "tower now");

        /// <summary>"Landmarks: Uptown Galleria finished now".</summary>
        public static void TestMallNow() => FinishNow(LandmarkKind.Mall, "mall now");

        /// <summary>"Landmarks: Crest Field finished now".</summary>
        public static void TestStadiumNow() => FinishNow(LandmarkKind.Stadium, "stadium now");

        // A landmark handed over today from wherever it stands: announced if it was not, its tender or works brought to an end.
        private static void FinishNow(LandmarkKind kind, string what)
        {
            string test = "Landmarks " + what;
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                if (!Finished(kind, test, out LandmarkFile f, out LandmarkSite site))
                {
                    return;
                }
                bool district = f.Kind == LandmarkKind.Stadium || DistrictBuildings.Taken(site.Address);
                bool pass = f.Built && district;
                Done("Landmarks check (" + what + "): " + LandmarkRules.Name(kind) + " handed over on day " + f.Finished + " on block " + f.Block + ", "
                     + (f.Kind == LandmarkKind.Stadium ? "its stands " + (Instance._pieces[(int)kind] != null ? "standing now" : "standing once the props are open")
                        : "its building " + (district ? "the district's from the next load (" + f.Design + ", " + site.Anchor.Count + " lot(s)" + (site.Units.Count > 0 ? ", " + site.Units.Count + " shops" : string.Empty) + ")"
                            : "not the district's yet (the city's designs are being measured: it is asked again each second)")) + "; it opens on day " + f.Opening,
                    pass, LandmarksText.TestFinished, NameOf(kind));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The landmark brought to its handover now, however far it had come; false with a notice when it cannot be.
        private static bool Finished(LandmarkKind kind, string test, out LandmarkFile f, out LandmarkSite site)
        {
            RefreshSites();
            int k = (int)kind;
            f = _state.Files[k];
            site = Sites[k];
            if (!SiteKnown[k])
            {
                TestRefusals.TellKey(Logger, test, LandmarksText.TestNoSite, LandmarkRules.Name(kind) + " has no free block");
                return false;
            }
            if (f.Built)
            {
                return true;
            }
            BeforeTest();
            int day = LandmarksGame.Day;
            if (_state.Anchor <= 0)
            {
                _state.Anchor = day;
            }
            if (f.Stage == LandmarkStage.Planned)
            {
                f.Stage = LandmarkStage.Announced;
                f.Block = site.Block;
                f.Announced = day;
                PickDesign(f, site);
            }
            if (f.Stage == LandmarkStage.Tendered && LandmarkBridges.DropTender != null)
            {
                LandmarkBridges.DropTender(f.Tender);
            }
            if (f.Stage < LandmarkStage.Building)
            {
                f.Builder = LandmarkBuilder.City;
                f.Tender = 0;
                f.Started = day;
                f.Due = day;
                f.Stage = LandmarkStage.Building;
            }
            if (f.SiteId > 0 && LandmarkBridges.FinishSite != null)
            {
                LandmarkBridges.FinishSite(f.SiteId);
            }
            f.SiteId = 0;
            Instance.Finish(f, site, day);
            TestBefore.Add(site.Address);
            TestBefore.AddRange(site.Units);
            Instance.Looks(true);
            Written();
            return true;
        }

        // ---------------------------------------------------------------- an opening and a match

        /// <summary>"Landmarks: an opening day today, and you at it": the latest landmark handed over opens today (Crest Field finished first if none is), its ceremony at any hour, the player at its front.</summary>
        public static void TestOpening()
        {
            const string test = "Landmarks opening";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                RefreshSites();
                LandmarkFile pick = null;
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    LandmarkFile f = _state.Files[k];
                    if (f.Built && f.Builder != LandmarkBuilder.Player && (pick == null || f.Finished > pick.Finished))
                    {
                        pick = f;
                    }
                }
                if (pick == null && !Finished(LandmarkKind.Stadium, test, out pick, out _))
                {
                    return;
                }
                BeforeTest();
                int day = LandmarksGame.Day;
                pick.Stage = LandmarkStage.Finished;
                pick.Opening = day;
                pick.Joined = false;
                if (pick.Builder == LandmarkBuilder.Player)
                {
                    pick.Builder = LandmarkBuilder.City;   // the construction game's ribbon is the player's own company's: the test shows the city's ceremony
                }
                _testOpening = (int)pick.Kind;
                Instance._crowdKey = -2;
                Instance._panelLines = -1;
                Written();
                bool there = TakeToFront(pick.Kind);
                Done("Landmarks check (opening): " + LandmarkRules.Name(pick.Kind) + " opens today, day " + day + ", its ceremony at any hour of it, the player "
                     + (there ? "at its front" : "on the way to its front"), true, LandmarksText.TestOpening, NameOf(pick.Kind));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Landmarks: a match day now at Crest Field, and you at it": the stadium finished and opened if it is not, a home match today with its crowds now and the traffic round it, the player at its front.</summary>
        public static void TestMatch()
        {
            const string test = "Landmarks match";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                if (!Finished(LandmarkKind.Stadium, test, out LandmarkFile f, out LandmarkSite site))
                {
                    return;
                }
                BeforeTest();
                int day = LandmarksGame.Day;
                if (f.Stage == LandmarkStage.Finished)
                {
                    f.Opening = Math.Min(f.Opening, day - 1);
                    Instance.Open(f, true);
                }
                _testMatch = day;
                Instance._crowdKey = -2;
                Instance.Crowds(1L << 8 | (long)day << 16);
                Instance.Traffic(true);
                Instance._panelLines = -1;
                Written();
                bool there = TakeToFront(LandmarkKind.Stadium);
                int crowds = Instance._crowds.Count;
                Done("Landmarks check (match): a home match today at " + LandmarkRules.Name(LandmarkKind.Stadium) + " against club " + LandmarkRules.Opponent(day) + ", "
                     + crowds + " crowd square(s) stood, the traffic of the buildings within " + Mathf.RoundToInt(LandmarkRules.MatchReach) + " m lifted by "
                     + LandmarkRules.MatchLift + ", the player " + (there ? "at its front" : "on the way to its front"), crowds > 0, LandmarksText.TestMatch, NameOf(LandmarkKind.Stadium));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- take me there

        /// <summary>"Landmarks: take me to Crest Field".</summary>
        public static void TestTakeStadium() => TakeMe(LandmarkKind.Stadium);

        /// <summary>"Landmarks: take me to Summit Tower".</summary>
        public static void TestTakeTower() => TakeMe(LandmarkKind.Tower);

        /// <summary>"Landmarks: take me to Uptown Galleria".</summary>
        public static void TestTakeMall() => TakeMe(LandmarkKind.Mall);

        private static void TakeMe(LandmarkKind kind)
        {
            string test = "Landmarks take me to the " + LandmarkRules.Name(kind);
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                RefreshSites();
                if (!SiteKnown[(int)kind])
                {
                    TestRefusals.TellKey(Logger, test, LandmarksText.TestNoSite, LandmarkRules.Name(kind) + " has no free block");
                    return;
                }
                bool there = TakeToFront(kind);
                LandmarkRules.Front(Sites[(int)kind].Ground, out float x, out float z);
                Done("Landmarks check (take me, " + LandmarkRules.Name(kind) + "): to (" + Mathf.RoundToInt(x) + ", " + Mathf.RoundToInt(z) + ") in front of its site on block "
                     + Sites[(int)kind].Block + ", " + _state.Files[(int)kind].Stage + (there ? ", there now" : ", once the game has taken the player out"), true, LandmarksText.TestTaken, NameOf(kind));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static Vector3 _takeTo;
        private static float _takeUntil = -1f;

        // The player at a landmark's front, looking at it: at once on foot, else once the game has taken them out of a building or a car.
        private static bool TakeToFront(LandmarkKind kind)
        {
            LandmarkRules.Front(Sites[(int)kind].Ground, out float x, out float z);
            _takeTo = new Vector3(x, UptownLots.GroundY, z + 1.2f);
            if (LandmarksGame.TakeTo(_takeTo, 180f))
            {
                _takeUntil = -1f;
                return true;
            }
            _takeUntil = Time.unscaledTime + TakeWait;
            return false;
        }

        // Once a second while a test is taking the player somewhere: there once the game has let them out.
        private static void Taking()
        {
            if (_takeUntil < 0f)
            {
                return;
            }
            if (Time.unscaledTime > _takeUntil)
            {
                _takeUntil = -1f;
                Logger?.Info("Landmarks test: the game did not let the player out of the building or the car in time; they stay where they are.");
                return;
            }
            if (LandmarksGame.TakeTo(_takeTo, 180f))
            {
                _takeUntil = -1f;
            }
        }

        // ---------------------------------------------------------------- put back

        /// <summary>"Landmarks: put everything back as before the tests": the state from before the first test, the district's buildings the tests finished taken back, their tenders and sites taken away.</summary>
        public static void TestBack()
        {
            const string test = "Landmarks back";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                if (_beforeTests == null)
                {
                    TestRefusals.TellKey(Logger, test, LandmarksText.TestNothing, "no test has changed anything");
                    return;
                }
                LandmarkState before = LandmarkStore.Read(_beforeTests, out _) ?? new LandmarkState();
                int kept = 0, taken = 0;
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    LandmarkFile now = _state.Files[k], was = before.Files[k];
                    if (now.Tender > 0 && now.Tender != was.Tender && LandmarkBridges.DropTender != null)
                    {
                        LandmarkBridges.DropTender(now.Tender);
                    }
                    if (now.SiteId > 0 && now.SiteId != was.SiteId && LandmarkBridges.DropSite != null)
                    {
                        LandmarkBridges.DropSite(now.SiteId);
                    }
                }
                for (int i = 0; i < TestBefore.Count; i++)
                {
                    if (DistrictBuildings.Withdraw(TestBefore[i], out string why))
                    {
                        taken++;
                    }
                    else if (DistrictBuildings.Taken(TestBefore[i]))
                    {
                        kept++;
                        Logger?.Info("Landmarks test: the district's building on lot " + TestBefore[i] + " stays (" + why + ").");
                    }
                }
                TestBefore.Clear();
                for (int k = 0; k < _state.Files.Length; k++)
                {
                    _state.Files[k] = before.Files[k];
                }
                _state.Anchor = before.Anchor;
                _state.LastDay = before.LastDay;
                _beforeTests = null;
                _testOpening = -1;
                _testMatch = -1;
                _builtStamp++;
                _streetStale = true;
                DistrictBuildings.LiftsChanged();
                if (Instance != null)
                {
                    Instance._crowdKey = -2;
                    Instance._panelLines = -1;
                    Instance.Looks(true);
                    Instance.Traffic(true);
                }
                RefreshSites();
                Written();
                Done("Landmarks check (back): the landmarks as before the first test, " + taken + " building(s) of the district taken back" + (kept > 0 ? ", " + kept
                     + " stood at a load and stay" : string.Empty), kept == 0, LandmarksText.TestBack, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
