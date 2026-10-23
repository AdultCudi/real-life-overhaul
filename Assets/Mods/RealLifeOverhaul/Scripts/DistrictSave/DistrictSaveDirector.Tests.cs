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
    /// the test's with its stats, and the map's destination at it; a save written by the game into a folder of the test's
    /// own; the game's lists then without the test's things and compared with before; the file read back with the game's
    /// own reader and gone through for anything of the test's, and the mod's section read for the records it should
    /// hold; the folder removed. One log line and one notice. COST: on the button only.
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private const string TestOwner = "the district save's test";
        private const string TestStreet = "reallife:street_district_save_test";
        private const int TestNumber = 1;
        private const string TestDistrict = "reallife:district_save_test";
        private const string TestSave = "RealLifeOverhaul district save test";
        private const float TestWait = 60f;   // real seconds the test waits for the game to finish writing the file

        private static bool _testing;

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
            string folder = Path.Combine(Application.temporaryCachePath, "RealLifeOverhaul", "district-save-test");
            string file = null;
            GameInstance save = SaveGameManager.Current;
            try
            {
                file = TestWrite(save, folder, problems);
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
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception e)
            {
                problems.Add("the test's folder could not be removed (" + e.Message + ")");
            }
            _testing = false;
            bool pass = problems.Count == 0;
            string line = "District save test: a registration, a task, a transaction and the stats of a district of the test's, and the map's destination, kept out of a "
                          + "save written by the game and read back with its own reader" + (pass ? ": PASS" : ": FAIL (" + string.Join("; ", problems) + ")")
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

        // The test's things in, a save of the game's into the test's folder, the test's things out; returns the file.
        private static string TestWrite(GameInstance save, string folder, List<string> problems)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);
            var before = new Snapshot(save);
            Building building = ScriptableObject.CreateInstance<Building>();
            building.name = "RealLifeOverhaul district save test";
            building.StreetName = TestStreet;
            building.StreetNumber = TestNumber;
            building.Neighbourhood = TestDistrict;
            building.BuildingType = "ba:buildingtype_retail";
            var reg = new BuildingRegistration { StreetName = TestStreet, StreetNumber = TestNumber, BusinessName = "District save test" };
            reg.BuildingCached = building;
            var task = new TodoTask { id = "reallife_district_save_test", type = TodoTaskType.MissingSchedule, address = new Address(TestStreet, TestNumber) };
            var tx = new Transaction(new TransactionInfo("reallife_transaction_district_save_test")) { address = new Address(TestStreet, TestNumber) };
            var stats = new NeighbourhoodStats { name = TestDistrict };
            Address guide = save.customDestination;
            string name = save.SaveGameName;
            bool savedOk = false;
            DistrictPlaces.Add(TestOwner, set => set.Street(TestStreet).District(TestDistrict));
            DistrictPlaces.Changed();
            try
            {
                save.BuildingRegistrations.Add(reg);
                save.TodoTasks.Add(task);
                save.Transactions.Enqueue(tx);
                save.NeighbourhoodStats.Add(stats);
                save.customDestination = new Address(TestStreet, TestNumber);
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
                else if (!before.Back(save, reg, task, tx, stats))
                {
                    problems.Add("after the save the game's lists were not as they were with the test's things where they stood");
                }
            }
            finally
            {
                save.SaveGameName = name;   // a save under another name names the game after it, as the player's own does
                save.BuildingRegistrations.Remove(reg);
                save.TodoTasks.Remove(task);
                Drop(save.Transactions, tx);
                save.NeighbourhoodStats.Remove(stats);
                save.customDestination = guide;
                DistrictPlaces.Remove(TestOwner);
                DistrictPlaces.Build();   // the places of the features alone again
                UnityEngine.Object.Destroy(building);
            }
            if (!before.Same(save))
            {
                problems.Add("the game's lists are not whole again after the test's things were taken out");
            }
            if (!savedOk)
            {
                return null;
            }
            bool json = SaveGameManager.Current != null && InstanceBehavior<GameManager>.Instance != null && InstanceBehavior<GameManager>.Instance.useSaveGameTypeJson;
            return Path.Combine(folder, TestSave + (json ? ".json" : ".hsg"));
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
                task |= r.Kind == SaveKind.Task && item is TodoTask k && k.id == "reallife_district_save_test";
                tx |= r.Kind == SaveKind.Transaction && item is Transaction x && x.transactionType == "reallife_transaction_district_save_test";
                stats |= r.Kind == SaveKind.DistrictStats && item is NeighbourhoodStats n && n.name == TestDistrict;
                guide |= r.Kind == SaveKind.Guider && r.Values.Count > 0 && r.Values[0] == TestStreet;
            }
            if (!(reg && task && tx && stats && guide))
            {
                problems.Add("the mod's section lacks the test's " + (reg ? string.Empty : "registration ") + (task ? string.Empty : "task ") + (tx ? string.Empty : "transaction ")
                             + (stats ? string.Empty : "district stats ") + (guide ? string.Empty : "destination"));
            }
        }

        private static void Drop(Queue<Transaction> queue, Transaction tx)
        {
            var none = new List<Taken<Transaction>>(1);
            DistrictSaveRules.TakeOut(queue, t => ReferenceEquals(t, tx), none, new List<Transaction>(queue.Count));
        }

        // The lists the test touches, as they stood before: to tell they are whole again.
        private sealed class Snapshot
        {
            private readonly BuildingRegistration[] _regs;
            private readonly TodoTask[] _tasks;
            private readonly Transaction[] _txs;
            private readonly NeighbourhoodStats[] _stats;
            private readonly Address _guide;

            public Snapshot(GameInstance save)
            {
                _regs = save.BuildingRegistrations.ToArray();
                _tasks = save.TodoTasks.ToArray();
                _txs = save.Transactions.ToArray();
                _stats = save.NeighbourhoodStats.ToArray();
                _guide = save.customDestination;
            }

            // Every list as before with the test's thing at its end, where it stood, and the destination the test's.
            public bool Back(GameInstance save, BuildingRegistration reg, TodoTask task, Transaction tx, NeighbourhoodStats stats)
            {
                Address g = save.customDestination;
                return Equal(save.BuildingRegistrations.ToArray(), _regs, reg) && Equal(save.TodoTasks.ToArray(), _tasks, task)
                       && Equal(save.Transactions.ToArray(), _txs, tx) && Equal(save.NeighbourhoodStats.ToArray(), _stats, stats)
                       && g != null && g.streetName == TestStreet;
            }

            public bool Same(GameInstance save)
            {
                return Equal(save.BuildingRegistrations.ToArray(), _regs, null) && Equal(save.TodoTasks.ToArray(), _tasks, null)
                       && Equal(save.Transactions.ToArray(), _txs, null) && Equal(save.NeighbourhoodStats.ToArray(), _stats, null)
                       && ReferenceEquals(save.customDestination, _guide);
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
