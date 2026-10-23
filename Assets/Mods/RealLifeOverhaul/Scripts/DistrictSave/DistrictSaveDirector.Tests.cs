using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BigAmbitions.Neighborhoods;
using Buildings;
using Entities;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// Options > Tests, "District save: keep a test record out of the save": a building of the test's own at an address
    /// of the mod's (never in the game's lookup), a registration there, a task and a transaction naming it, a district of
    /// the test's with its stats, and the map's destination at it, added to the save inside the mod's last step of the
    /// game's save event (no other handler of the save sees them); a save written by the game into this character's own
    /// saves under the test's name, kept there to be loaded without the mod; the game's lists after the save with the
    /// test's things back where they stood, and whole again once they are taken away; the file read back with the game's
    /// own reader and gone through for anything of the test's, and the mod's section read for the records it should hold.
    /// One log line and one notice. COST: on the button only; at a save, one null test.
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private const string TestOwner = "the district save's test";
        private const string TestStreet = "reallife:street_district_save_test";
        private const int TestNumber = 1;
        private const string TestDistrict = "reallife:district_save_test";
        private const string TestSave = "RealLifeOverhaul district save test";   // the save's name in this character's saves
        private const string TestTask = "district-save-test";                    // the test task's id
        private const string TestTransaction = "reallife_transaction_district_save_test";   // its name in the bank's history, were it ever shown
        private const float TestWait = 60f;   // real seconds the test waits for the game to finish writing the file

        private static bool _testing;
        private static TestThings _test;      // the test's things while its save is written: added in BeforeWrite

        public static void Test()
        {
            try
            {
                string why = TestRefused();
                if (why != null)
                {
                    TestRefusals.TellKey(Logger, "District save", why);
                    return;
                }
                _testing = true;
                Meta.MetaUi.Instance.Run(Guard.Routine(TestRun()));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        // Why the test cannot run now (a sentence's key), or null.
        private static string TestRefused()
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return "reallife_test_refused_not_loaded";
            }
            if (GoingPublic.InSession)
            {
                return DistrictSaveText.TestSession;   // the test business would be sent to the other players
            }
            if (_testing || SaveGameManager.SavingGameInProgress || Waiting)
            {
                return DistrictSaveText.TestBusy;
            }
            if (!GameSaves())
            {
                return "reallife_meta_tool_cannot_save";
            }
            return null;
        }

        // The game's own test of whether saving is allowed here (private: through the contract; "yes" if it cannot be read).
        private static bool GameSaves()
        {
            try
            {
                System.Reflection.MethodInfo method = GameContract.CanSave.Method;
                return method == null || (bool)method.Invoke(null, null);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static IEnumerator TestRun()
        {
            var problems = new List<string>(4);
            string file = null;
            GameInstance save = SaveGameManager.Current;
            try
            {
                file = TestWrite(save, problems);
            }
            catch (Exception e)
            {
                problems.Add("the test's save threw " + e.GetType().Name + ": " + e.Message);
            }
            float until = Time.unscaledTime + TestWait;
            while (file != null && SaveGameManager.SavingGameInProgress && Time.unscaledTime < until)
            {
                yield return null;
            }
            Task<GameInstance> read = null;
            if (file != null && File.Exists(file))
            {
                read = file.EndsWith(".json", StringComparison.Ordinal) ? SaveGameSerializationHelper.DeserializeJsonDataAsync(file) : SaveGameSerializationHelper.DeserializeBinaryDataAsync(file);
                while (!read.IsCompleted && Time.unscaledTime < until)
                {
                    yield return null;
                }
            }
            else if (file != null)
            {
                problems.Add("the game wrote no file at " + file);
            }
            try
            {
                if (read != null)
                {
                    TestRead(read, problems);
                }
            }
            catch (Exception e)
            {
                problems.Add("reading the file back threw " + e.GetType().Name + ": " + e.Message);
            }
            _testing = false;
            bool pass = problems.Count == 0;
            string line = "District save test: a registration, a task, a transaction and the stats of a district of the test's, and the map's destination, kept out of a "
                          + "save written by the game and read back with its own reader" + (pass ? ": PASS" : ": FAIL (" + string.Join("; ", problems) + ")")
                          + (file != null ? ". The save is kept as '" + TestSave + "' in this character's saves (" + file + "), to be loaded without the mod" : string.Empty)
                          + (LastCheck != null ? ". " + LastCheck : string.Empty);
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
            Notifications.Show(pass ? NotificationType.Success : NotificationType.Error, pass ? DistrictSaveText.TestPass : DistrictSaveText.TestFail, null, 10f);
        }

        // The test's things ready, a save of the game's into this character's saves (they are added inside its event), the
        // test's things away again; returns the file, or null when the game did not save.
        private static string TestWrite(GameInstance save, List<string> problems)
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            string folder = SaveGamePathHelper.GetCharacterFolderPath(save.characterId);
            string file = Path.Combine(folder, TestSave + (game != null && game.useSaveGameTypeJson ? ".json" : ".hsg"));
            if (File.Exists(file))
            {
                File.Delete(file);   // the last test's: the file read back is this one's
            }
            Building building = ScriptableObject.CreateInstance<Building>();
            building.name = "RealLifeOverhaul district save test";
            building.StreetName = TestStreet;
            building.StreetNumber = TestNumber;
            building.Neighbourhood = TestDistrict;
            building.BuildingType = "ba:buildingtype_retail";
            var reg = new BuildingRegistration { StreetName = TestStreet, StreetNumber = TestNumber, BusinessName = "District save test" };
            reg.BuildingCached = building;
            var things = new TestThings(reg, new TodoTask { id = TestTask, type = TodoTaskType.MissingSchedule, address = new Address(TestStreet, TestNumber) },
                new Transaction(new TransactionInfo(TestTransaction)) { address = new Address(TestStreet, TestNumber) }, new NeighbourhoodStats { name = TestDistrict },
                new Address(TestStreet, TestNumber));
            string name = save.SaveGameName;
            bool unsaved = SaveGameManager.HasChangesSinceLastSave();
            bool savedOk = false;
            DistrictPlaces.Add(TestOwner, set => set.Street(TestStreet).District(TestDistrict));
            DistrictPlaces.Changed();
            _test = things;
            try
            {
                SaveHook.Keep(null);   // the mod's hook in the game's save event, last
                savedOk = SaveGameManager.Save(SaveGameManager.SaveType.OldAgeBackUp, TestSave, folder);
                if (Waiting)
                {
                    PutBackNow("the test, as the game's line did not come");
                    problems.Add("the game's line that the file is written did not come: the records went back after the save call");
                }
                if (!savedOk)
                {
                    problems.Add("the game did not save (it refused or failed)");
                }
                else if (!things.Added)
                {
                    problems.Add("the district save's step did not run in the game's save event");
                }
                else if (!things.Back(save))
                {
                    problems.Add("after the save the game's lists were not as they were with the test's things where they stood");
                }
            }
            finally
            {
                _test = null;
                things.Remove(save);
                save.SaveGameName = name;   // a save under another name names the game after it, as the player's own does
                DistrictPlaces.Remove(TestOwner);
                DistrictPlaces.Build();   // the places of the features alone again
                UnityEngine.Object.Destroy(building);
                if (unsaved)
                {
                    SaveGameManager.MarkChange();   // the game's own autosave still sees what the player has not saved
                }
            }
            if (things.Added && !things.Same(save))
            {
                problems.Add("the game's lists are not whole again after the test's things were taken away");
            }
            return savedOk ? file : null;
        }

        // The file as the game reads it: nothing of the test's in the game's part, the test's records in the mod's section.
        private static void TestRead(Task<GameInstance> read, List<string> problems)
        {
            if (!read.IsCompleted || read.IsFaulted || read.Result == null)
            {
                problems.Add("the game's reader could not read the file back" + (read.Exception != null ? " (" + read.Exception.GetBaseException().Message + ")" : string.Empty));
                return;
            }
            GameInstance back = read.Result;
            PlaceSet places = new PlaceSet().Street(TestStreet).District(TestDistrict);
            List<string> hits = DistrictSaveScan.Find(back, places, null, MostHits);
            if (hits.Count > 0)
            {
                problems.Add("the game's part of the file still names the test's places at " + string.Join("; ", hits));
            }
            string text = back.modData != null && back.modData.TryGetValue(DistrictSaveStore.Key, out string t) ? t : null;
            StoredLine line = DistrictSaveStore.Read(text, out int _);
            if (line == null)
            {
                problems.Add("the file has no mod's section for the district, or it cannot be read");
                return;
            }
            object[] items = DistrictSaveCodec.Decode(line.Payload);
            bool reg = false, task = false, tx = false, stats = false, guide = false;
            for (int i = 0; i < line.Records.Count; i++)
            {
                StoredRecord r = line.Records[i];
                object item = r.Slot >= 0 && r.Slot < items.Length ? items[r.Slot] : null;
                reg |= r.Kind == SaveKind.Registration && item is BuildingRegistration b && b.StreetName == TestStreet;
                task |= r.Kind == SaveKind.Task && item is TodoTask k && k.id == TestTask;
                tx |= r.Kind == SaveKind.Transaction && item is Transaction x && x.transactionType == TestTransaction;
                stats |= r.Kind == SaveKind.DistrictStats && item is NeighbourhoodStats n && n.name == TestDistrict;
                guide |= r.Kind == SaveKind.Guider && r.Values.Count > 0 && r.Values[0] == TestStreet;
            }
            if (!(reg && task && tx && stats && guide))
            {
                problems.Add("the mod's section lacks the test's " + (reg ? string.Empty : "registration ") + (task ? string.Empty : "task ") + (tx ? string.Empty : "transaction ")
                             + (stats ? string.Empty : "district stats ") + (guide ? string.Empty : "destination"));
            }
        }

        /// <summary>
        /// The test's things, added to the save by the district save's own step (BeforeWrite) after every other handler of
        /// the game's save event has run, and the lists they go into as they stood just before, to tell that the save put
        /// each back where it stood and that the lists are whole again once the test takes them away.
        /// </summary>
        private sealed class TestThings
        {
            private readonly BuildingRegistration _reg;
            private readonly TodoTask _task;
            private readonly Transaction _tx;
            private readonly NeighbourhoodStats _stats;
            private readonly Address _guide;
            private BuildingRegistration[] _regs;
            private TodoTask[] _tasks;
            private Transaction[] _txs;
            private NeighbourhoodStats[] _allStats;
            private Address _guideWas;

            public bool Added { get; private set; }

            public TestThings(BuildingRegistration reg, TodoTask task, Transaction tx, NeighbourhoodStats stats, Address guide)
            {
                _reg = reg;
                _task = task;
                _tx = tx;
                _stats = stats;
                _guide = guide;
            }

            // In the save event, last: the lists as they stand, then the test's things at their ends and the destination.
            public void Add(GameInstance save)
            {
                if (Added)
                {
                    return;
                }
                _regs = save.BuildingRegistrations.ToArray();
                _tasks = save.TodoTasks.ToArray();
                _txs = save.Transactions.ToArray();
                _allStats = save.NeighbourhoodStats.ToArray();
                _guideWas = save.customDestination;
                save.BuildingRegistrations.Add(_reg);
                save.TodoTasks.Add(_task);
                save.Transactions.Enqueue(_tx);
                save.NeighbourhoodStats.Add(_stats);
                save.customDestination = _guide;
                Added = true;
            }

            // After the save: every list as before with the test's thing at its end, where it stood, and the test's destination.
            public bool Back(GameInstance save)
            {
                return Equal(save.BuildingRegistrations.ToArray(), _regs, _reg) && Equal(save.TodoTasks.ToArray(), _tasks, _task)
                       && Equal(save.Transactions.ToArray(), _txs, _tx) && Equal(save.NeighbourhoodStats.ToArray(), _allStats, _stats)
                       && ReferenceEquals(save.customDestination, _guide);
            }

            // The test's things away again, wherever they are.
            public void Remove(GameInstance save)
            {
                if (!Added)
                {
                    return;
                }
                save.BuildingRegistrations.Remove(_reg);
                save.TodoTasks.Remove(_task);
                var none = new List<Taken<Transaction>>(1);
                Transaction tx = _tx;
                DistrictSaveRules.TakeOut(save.Transactions, t => ReferenceEquals(t, tx), none, new List<Transaction>(save.Transactions.Count));
                save.NeighbourhoodStats.Remove(_stats);
                if (ReferenceEquals(save.customDestination, _guide))
                {
                    save.customDestination = _guideWas;
                }
            }

            // Every list as it stood before the test's things were added.
            public bool Same(GameInstance save)
            {
                return Equal(save.BuildingRegistrations.ToArray(), _regs, null) && Equal(save.TodoTasks.ToArray(), _tasks, null)
                       && Equal(save.Transactions.ToArray(), _txs, null) && Equal(save.NeighbourhoodStats.ToArray(), _allStats, null)
                       && ReferenceEquals(save.customDestination, _guideWas);
            }

            private static bool Equal<T>(T[] now, T[] was, T added) where T : class
            {
                int extra = added != null ? 1 : 0;
                if (now.Length != was.Length + extra)
                {
                    return false;
                }
                for (int i = 0; i < was.Length; i++)
                {
                    if (!ReferenceEquals(now[i], was[i]))
                    {
                        return false;
                    }
                }
                return added == null || ReferenceEquals(now[now.Length - 1], added);
            }
        }
    }
}
