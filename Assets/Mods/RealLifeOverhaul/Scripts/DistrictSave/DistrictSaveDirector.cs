using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The district save's state and timing. At every save, as the last of the mod's steps in the game's save event
    /// (Core/SaveHook), every record that names one of the mod's places leaves the game's lists for the mod's section of
    /// modData (DistrictSaveStore), its stand-ins are written, and the self-check reads what is about to be written
    /// (DistrictSaveScan). The records go back where they stood the moment the game has written its file: on the game's
    /// own log line that says so, while its save call is still running, so that nothing of the game's runs on the
    /// shortened lists (the midnight save runs inside the game's hour, before the next one); the frame after if that line
    /// never comes. At a load the kept records go back before the city's scenes are built (DistrictSaveEntry, at the main
    /// menu's unload, after the save is read), or at the city load for a load that did not pass the main menu. With no
    /// places of the mod's and nothing kept, nothing is done. See docs/features/district-save.md.
    /// COST: nothing per frame (DistrictSaveWatch, while places exist: a reference comparison); at a save of a game
    /// with places of the mod's, the strip and the game's serializer writing the payload on the main thread, the payload's
    /// compression and the check on workers alongside (about 20 ms for a district of 537 businesses, against 70 ms with
    /// them all on the main thread, measured under the game's Mono on the owner's save).
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private const int MostHits = 8;   // places still named that the check line lists

        public static IModLogger Logger;

        private static DistrictSaveGame.Strip _pending;     // what the save being written took out, until it goes back
        private static string _prefix;                      // the game's line that the file is written
        private static int _mainThread = -1;
        private static volatile bool _armed;
        private static bool _listening;
        private static readonly Application.LogCallback Heard = OnLog;
        private static readonly Action Join = Guard.Way(KeepHook);

        private static GameInstance _restoredFor;           // the save the last load put back
        private static int _restored;                       // records this load put back: they leave every save of it, whatever names them now
        private static readonly List<StoredRecord> KeptRecords = new List<StoredRecord>();
        private static readonly List<object> KeptItems = new List<object>();

        private static readonly List<string> Said = new List<string>(4);   // lines waiting: the log listener writes none
        private static readonly List<bool> SaidWarn = new List<bool>(4);

        private static DistrictSaveZip _zip;               // made at the first save with a district; its buffers kept for the next
        private static bool _zipped;                       // this save's payload was compressed in pieces (for the check line)

        /// <summary>True while there is anything to keep out of a save: places of the mod's, kept records, or a write going on.</summary>
        public static bool Holds => _pending != null || KeptRecords.Count > 0 || !DistrictPlaces.Current.Empty;

        /// <summary>True between taking records out for a write and putting them back.</summary>
        public static bool Waiting => _pending != null;

        /// <summary>How many records the mod's section keeps for a load where their places are made.</summary>
        public static int Kept => KeptRecords.Count;

        /// <summary>The last save's check line, for the test.</summary>
        public static string LastCheck { get; private set; }

        /// <summary>
        /// True once the district save's city step is done: from then on it alone takes out and puts back whatever of a
        /// save names the mod's places, and a feature's own save guard steps aside (new buildings', MadeGame.Save). False
        /// from a save's reading to its city step, and for a city whose district save step failed, where the guard does
        /// its own. The save tools' stop in a city that stays leaves it true: the district save stays in that city's saves.
        /// </summary>
        public static bool Present { get; private set; }

        /// <summary>
        /// True for a key of the district's records, or of a feature's line that says which of its places stand
        /// (<see cref="KeepsAlso"/>): the save tools leave it, as it holds the player's own things.
        /// </summary>
        public static bool Keeps(string key)
        {
            return key == DistrictSaveStore.Key || key == DistrictSaveStore.UnreadKey || (key != null && AlsoKept.Contains(key));
        }

        // ---------------------------------------------------------------- the save

        /// <summary>
        /// The last step of the mod in the game's save event, after every feature wrote its lines: the records that name
        /// the mod's places leave the game's lists, the mod's section is written, and the check reads the rest.
        /// </summary>
        public static void BeforeWrite()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (_pending != null)
            {
                PutBackNow("before the next save, as the game's line for the last did not come");
            }
            Flush();
            if (_test != null)
            {
                try
                {
                    _test.Add(save);   // the district save test's things: no other handler of the save sees them
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            PlaceSet places = DistrictPlaces.Stale ? DistrictPlaces.Build() : DistrictPlaces.Current;
            if (DistrictSaveRules.Idle(places, KeptRecords.Count + _restored))
            {
                Clean(save);
                CompanionGame.End(save);   // the report's text, written on a worker since the save hook began it (Core/CompanionGame)
                return;   // nothing of the mod's can be in this save: nothing to do
            }
            long began = Stopwatch.GetTimestamp();
            DistrictSaveGame.Strip s = DistrictSaveGame.TakeOut(save, places);
            _pending = s;   // at once: whatever follows, what was taken out goes back
            Arm();
            DistrictSaveWatch.Wake();
            long taken = Stopwatch.GetTimestamp();
            DistrictSaveCheck.Start(save, places, s, MostHits);   // on a worker while the section is written: both only read
            int bytes = Write(save, s, out int records, out string fault);
            long written = Stopwatch.GetTimestamp();
            List<string> hits = DistrictSaveCheck.End(out double checkMs);
            long done = Stopwatch.GetTimestamp();
            bool failed = hits.Count > 0 || fault != null || s.Faults.Count > 0;
            LastCheck = CheckLine(places, s, records, bytes, hits, fault, Parts(Ms(began, done), Ms(began, taken), Ms(taken, written), checkMs, Ms(written, done)));
            Say(LastCheck, failed);
            CompanionGame.End(save);   // the report's text, done on a worker beside the section and the check: set into the save here, nothing waited for
        }

        private static double Ms(long from, long to)
        {
            return (to - from) * 1000.0 / Stopwatch.Frequency;
        }

        // Where the save's time went: the whole, then its parts (the check runs beside the section, so they overlap).
        private static string Parts(double all, double strip, double section, double check, double waited)
        {
            string packed = _zipped
                ? "; " + (_zip.Written / 1024) + " KB of the game's serializer compressed to " + (_zip.Packed / 1024) + " KB on " + (_zip.Workers + 1) + " thread(s)"
                : string.Empty;
            return all.ToString("F1", CultureInfo.InvariantCulture) + " ms: the records out " + strip.ToString("F1", CultureInfo.InvariantCulture) + " ms, the section "
                   + section.ToString("F1", CultureInfo.InvariantCulture) + " ms, the check " + check.ToString("F1", CultureInfo.InvariantCulture)
                   + " ms beside it, then waited for " + waited.ToString("F1", CultureInfo.InvariantCulture) + " ms" + packed;
        }

        // The mod's section: what this save took out of the player's own, and what the load kept.
        private static int Write(GameInstance save, DistrictSaveGame.Strip s, out int records, out string fault)
        {
            fault = null;
            _zipped = false;
            records = s.Records.Count + KeptRecords.Count;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (records == 0)
            {
                save.modData.Remove(DistrictSaveStore.Key);
                return 0;
            }
            try
            {
                var all = new List<StoredRecord>(records);
                var items = new List<object>(s.Payload);
                all.AddRange(s.Records);
                for (int i = 0; i < KeptRecords.Count; i++)
                {
                    StoredRecord kept = KeptRecords[i];
                    if (kept.Slot >= 0)
                    {
                        kept.Slot = items.Count;
                        items.Add(KeptItems[i]);
                    }
                    all.Add(kept);
                }
                string head = DistrictSaveStore.Head(all);
                string line = items.Count > 0 ? Packed(items.ToArray(), head) : head;
                save.modData[DistrictSaveStore.Key] = line;
                return line.Length;
            }
            catch (Exception e)
            {
                fault = "the mod's section could not be written (" + e.GetType().Name + ": " + e.Message + "); the line from before stays";
                return 0;
            }
        }

        // The line with its payload compressed in pieces on the workers; in one stream of the runtime's gzip if a piece failed.
        private static string Packed(object[] items, string head)
        {
            if (_zip == null)
            {
                _zip = new DistrictSaveZip(DistrictSaveZip.WorkersHere);
            }
            string line = DistrictSaveCodec.Line(items, head, _zip);
            if (line != null)
            {
                _zipped = true;
                return line;
            }
            Say("District save: the mod's section was compressed the slower way, in one stream, as " + _zip.Fault + ".", true);
            return head + DistrictSaveCodec.Encode(items);
        }

        // Nothing to keep: a line left from before goes (a line no version could read stays under its own key).
        private static void Clean(GameInstance save)
        {
            if (save.modData != null && save.modData.ContainsKey(DistrictSaveStore.Key))
            {
                save.modData.Remove(DistrictSaveStore.Key);
            }
        }

        private static string CheckLine(PlaceSet places, DistrictSaveGame.Strip s, int records, int bytes, List<string> hits, string fault, string time)
        {
            string counts = s.Left + " record(s) taken out" + (s.NotKept > 0 ? " (" + s.NotKept + " another player's, put back after and not kept)" : string.Empty)
                            + ", " + s.StoodIn + " stand-in(s), " + KeptRecords.Count + " kept from the load; the mod's section " + records + " record(s), "
                            + (bytes / 1024f).ToString("F1", CultureInfo.InvariantCulture) + " KB, " + time;
            string scope = (places.Addresses + places.Streets) + " addresses (" + places.Streets + " whole streets) and " + places.Districts + " districts";
            if (hits.Count == 0 && fault == null && s.Faults.Count == 0)
            {
                return "District save check: the written save names none of the mod's " + scope + " (" + counts + "): PASS";
            }
            var why = new List<string>(hits);
            if (fault != null)
            {
                why.Add(fault);
            }
            why.AddRange(s.Faults);
            return "District save check: the written save still names the mod's " + scope + " at " + string.Join("; ", why) + " (" + counts + "): FAIL";
        }

        // Listens for the game's line that its file is written; the first time a save takes anything out.
        private static void Arm()
        {
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            _prefix = DistrictSaveRules.WrittenPrefix(SaveGamePathHelper.GetTempSavePath());
            if (!_listening)
            {
                _listening = true;
                Application.logMessageReceivedThreaded += Heard;
            }
            _armed = true;
        }

        // From any thread, for every line logged. Nothing here may log (it would come back here); a fault drops the line.
        private static void OnLog(string text, string stack, LogType type)
        {
            try
            {
                if (!_armed || Thread.CurrentThread.ManagedThreadId != _mainThread || !DistrictSaveRules.IsWrittenLine(text, _prefix))
                {
                    return;
                }
                PutBackNow("the game's line that the file is written");
            }
            catch (Exception)
            {
                // (see above: nothing is logged from here; the frame after puts back what is left)
            }
        }

        /// <summary>Puts back what the save took out, where it stood; the lines it has to say wait for the next frame.</summary>
        private static void PutBackNow(string when)
        {
            DistrictSaveGame.Strip s = _pending;
            if (s == null)
            {
                return;
            }
            _pending = null;
            _armed = false;
            int failed = DistrictSaveGame.PutBack(s, null);
            Say("District save: put back after the write (" + when + "): " + s.Left + " record(s) and " + s.StoodIn + " stand-in(s) where they stood"
                + (failed == 0 ? ": PASS" : "; " + failed + " step(s) could not be: FAIL"), failed > 0);
        }

        /// <summary>Once a frame from DistrictSaveWatch while it is awake: a put back the game's line did not bring, the waiting lines, the hook kept last.</summary>
        public static void Frame()
        {
            if (_pending != null)
            {
                PutBackNow("the frame after the save, as the game's line did not come");
            }
            Flush();
            if (!DistrictPlaces.Current.Empty)
            {
                SaveHook.Keep(null);   // last in the save event within a frame of anything that joins it (a car that came into view)
            }
        }

        private static void Say(string line, bool warn)
        {
            if (!warn && !Meta.Dev.Verbose)
            {
                return;
            }
            Said.Add(line);
            SaidWarn.Add(warn);
        }

        private static void Flush()
        {
            for (int i = 0; i < Said.Count; i++)
            {
                if (SaidWarn[i])
                {
                    Logger?.Warn(Said[i]);
                }
                else
                {
                    Logger?.Info(Said[i]);
                }
            }
            Said.Clear();
            SaidWarn.Clear();
        }

        private static void KeepHook()
        {
            SaveHook.Keep(null);
        }

        // ---------------------------------------------------------------- the load

        /// <summary>
        /// The main menu's unload, after the save is read and before the city's scenes are built: the features' early steps
        /// put their buildings' assets into the game's lookup, and the kept records whose places the game knows go back
        /// now, so that every lookup the game builds from the save's lists as the scenes load has them.
        /// </summary>
        public static void Early()
        {
            Present = false;   // a new city: a feature's guard does its own until the district save's city step is done
            GameInstance save = SaveGameManager.Current;
            if (save == null || ReferenceEquals(save, _restoredFor))
            {
                return;
            }
            Start(save);
            DistrictPlaces.Early();
            PlaceSet places = DistrictPlaces.Build();
            Pass(save, places, false);
            Flush();
        }

        /// <summary>
        /// The city load, after every feature has made its places: the rest of the kept records whose places are made now
        /// go back (with the game's lookups that were built from the lists brought up to date); the save hook joins the
        /// game's event after the game's own handlers, before the clock runs; the watch is added.
        /// </summary>
        public static void City(GameObject root)
        {
            _pending = null;   // a write of another save that never came back is not this one's
            _armed = false;
            PlaceSet places = DistrictPlaces.Build();
            for (int i = 0; i < DistrictPlaces.LastFaults.Count; i++)
            {
                Logger?.Warn("District save: a feature's places could not be read (" + DistrictPlaces.LastFaults[i] + "); its places are left out this session.");
            }
            GameInstance save = SaveGameManager.Current;
            if (save != null)
            {
                if (!ReferenceEquals(save, _restoredFor))
                {
                    Start(save);   // a load that did not pass the main menu (a funeral's backup): everything goes back here
                }
                Pass(save, places, true);
            }
            GlobalEvents.RegisterOnGameLoadedLateCallback(Join);
            Guard.Add<DistrictSaveWatch>(root);
            Flush();
            Present = true;   // last: a step that failed above leaves the guards of the features to do their own
        }

        /// <summary>The city is unloading: what this session put back stays in its save; the next load starts afresh.</summary>
        public static void Leave()
        {
            if (_pending != null)
            {
                PutBackNow("as the city unloads");
            }
            Flush();
        }

        /// <summary>
        /// The save tools stop the mod in a city that stays ("Prepare this save for removing the mod"): every record that
        /// names the mod's places leaves the save in memory for good, kept in the mod's section (which the tools leave),
        /// and every stand-in is written for good, so that no later save of this city names them, whatever still runs.
        /// Another player's things stay where they are. The places are the city's as it loaded (its features have stopped:
        /// a made building's door is still known). Returns how many records and stand-ins.
        /// </summary>
        public static int KeepOutForGood()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null)
                {
                    return 0;
                }
                PutBackNow("before the save tools take the mod's places out for good");
                Flush();
                DistrictSaveGame.Strip s = DistrictSaveGame.TakeOut(save, DistrictPlaces.Current, true);
                Write(save, s, out int records, out string fault);
                if (fault != null)
                {
                    DistrictSaveGame.PutBack(s, null);   // not kept, so not out for good: every save goes on taking them out and putting them back
                    Logger?.Warn("District save: the save tools stopped the mod, and the records of the mod's places stay in the save in memory, as " + fault + ": FAIL");
                    return 0;
                }
                s.PutBacks.Clear();   // for good
                for (int i = 0; i < s.Records.Count; i++)
                {
                    StoredRecord r = s.Records[i];
                    KeptRecords.Add(r);
                    KeptItems.Add(r.Slot >= 0 && r.Slot < s.Payload.Count ? s.Payload[r.Slot] : null);
                }
                string line = "District save: the save tools stopped the mod: " + s.Left + " record(s) taken out of the save in memory for good and " + s.StoodIn
                              + " stand-in(s) written for good; the mod's section keeps " + records + " record(s) for a load with the mod"
                              + (s.Faults.Count > 0 ? "; not taken out: " + string.Join("; ", s.Faults) + ": FAIL" : ": PASS");
                if (s.Faults.Count > 0)
                {
                    Logger?.Warn(line);
                }
                else
                {
                    Logger?.Info(line);
                }
                return s.Left + s.StoodIn;
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the save tools go on; every save still takes out what names the mod's places
                return 0;
            }
        }

        // A save's records read for its load: the mod's section's lines, and those an older version could not read.
        private static void Start(GameInstance save)
        {
            _restoredFor = save;
            _restored = 0;
            KeptRecords.Clear();
            KeptItems.Clear();
            if (save.modData == null)
            {
                return;
            }
            string unread = Value(save, DistrictSaveStore.UnreadKey);
            if (!string.IsNullOrEmpty(unread))
            {
                var still = new List<string>(1);
                foreach (string old in unread.Split('\n'))
                {
                    if (!Read(old, KeptRecords, KeptItems))
                    {
                        still.Add(old);   // a line not even this version can read stays as it was read
                    }
                }
                if (still.Count == 0)
                {
                    save.modData.Remove(DistrictSaveStore.UnreadKey);
                }
                else
                {
                    save.modData[DistrictSaveStore.UnreadKey] = string.Join("\n", still);
                }
            }
            string text = Value(save, DistrictSaveStore.Key);
            if (!string.IsNullOrEmpty(text) && !Read(text, KeptRecords, KeptItems))
            {
                save.modData[DistrictSaveStore.UnreadKey] = DistrictSaveStore.KeepUnread(Value(save, DistrictSaveStore.UnreadKey), text);
                save.modData.Remove(DistrictSaveStore.Key);
                Logger?.Warn("District save: the records of the mod's places in this save cannot be read by this version of the mod (a later version's, or damaged); "
                             + "they are kept as they are for the version that can, and nothing of them is put back.");
            }
        }

        // One line of the store, its records with their items; false when it cannot be read at all.
        private static bool Read(string text, List<StoredRecord> records, List<object> items)
        {
            StoredLine line = DistrictSaveStore.Read(text, out int _);
            if (line == null)
            {
                return false;
            }
            object[] payload;
            try
            {
                payload = DistrictSaveCodec.Decode(line.Payload);
            }
            catch (Exception)
            {
                return false;
            }
            DistrictSaveGame.Aligned(line, payload, records, items);
            return true;
        }

        // A pass over what is kept: what the game knows the places of goes back; the city's pass is the last of the load.
        private static void Pass(GameInstance save, PlaceSet places, bool city)
        {
            Retired(save);
            if (KeptRecords.Count == 0)
            {
                return;
            }
            var records = new List<StoredRecord>(KeptRecords);
            var items = new List<object>(KeptItems);
            KeptRecords.Clear();
            KeptItems.Clear();
            DistrictSaveGame.Return r = DistrictSaveGame.Restore(save, records, items, places, city);
            _restored += r.Back;
            if (city)
            {
                DistrictSaveGame.Refresh(r);   // the game built its lookups from the lists as the scenes loaded
            }
            KeptRecords.AddRange(r.KeptRecords);
            KeptItems.AddRange(r.KeptItems);
            if (r.Back + r.Dropped == 0 && r.Faults.Count == 0 && !city)
            {
                return;
            }
            string line = "District save: " + (city ? "at the city load, " : "before the city's scenes, ") + r.Back + " record(s) of the mod's places put back where they "
                          + "stood" + (city ? ", " + KeptRecords.Count + " kept for a load where their places are made, " + r.Dropped + " stand-in(s) left standing" : string.Empty)
                          + (r.Faults.Count > 0 ? "; not put back: " + string.Join("; ", r.Faults) : string.Empty) + ".";
            if (r.Faults.Count > 0 || (city && KeptRecords.Count > 0))
            {
                Logger?.Warn(line);
            }
            else
            {
                Logger?.Info(line);
            }
        }

        private static string Value(GameInstance save, string key)
        {
            return save.modData != null && save.modData.TryGetValue(key, out string text) ? text : null;
        }

        /// <summary>For the save tools' report: how many of the district's records the tool leaves in the mod's section.</summary>
        public static int StoredRecords()
        {
            GameInstance save = SaveGameManager.Current;
            string text = save != null ? Value(save, DistrictSaveStore.Key) : null;
            StoredLine line = DistrictSaveStore.Read(text, out int _);
            return line != null ? line.Records.Count : 0;
        }

        /// <summary>
        /// The registration the mod's section keeps for an address and has not put back yet (it goes back into the save's
        /// list where it stood at the city step, this same object; one a feature put there itself counts as back): for a
        /// feature that makes a building of the mod's with a registration of its own, which must take this one instead of a
        /// new empty one. Null when none is kept for that address. Asked in a load that did not pass the main menu (a
        /// funeral's backup), the save's section is read first, so what is kept is this save's and never the one before's.
        /// </summary>
        public static BuildingRegistration KeptRegistration(string street, int number)
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && !ReferenceEquals(save, _restoredFor))
            {
                Start(save);   // the city step then finds it read and puts back what is still kept
            }
            for (int i = 0; i < KeptRecords.Count && i < KeptItems.Count; i++)
            {
                if (KeptRecords[i].Kind == SaveKind.Registration && KeptItems[i] is BuildingRegistration reg && reg.StreetNumber == number
                    && string.Equals(reg.StreetName, street, StringComparison.Ordinal))
                {
                    return reg;
                }
            }
            return null;
        }
    }
}
