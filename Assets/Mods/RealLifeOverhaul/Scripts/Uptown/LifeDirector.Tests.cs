using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's test entries (Options, Test tools): the district's census, a business the game's own daily pass opens there,
    /// a unit rented through the game's own BizMan, and a save written and read back that names nothing of it. Each works
    /// when pressed or says why not in a notice, and writes one "Uptown check (...)" line per event in Player.log with
    /// Developer diagnostics on (a FAIL always). The load's own census is the same line, once a load. Nothing here runs a
    /// frame unless a test is under way.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const string TestName = "Uptown";
        private const float RentWatch = 1200f;   // s of real time the rent test watches for the player renting the unit
        private const float SaveWait = 60f;      // s a save and its reading back are waited for

        private static bool _testing;            // a test with steps of its own is under way
        private static readonly Dictionary<string, string> Before = new Dictionary<string, string>(64);
        private static readonly Dictionary<string, string> After = new Dictionary<string, string>(64);

        // ---------------------------------------------------------------- the census

        /// <summary>"Uptown: district census": the census line, and a notice of its counts.</summary>
        public static void TestCensus()
        {
            try
            {
                if (Refused(false))
                {
                    return;
                }
                bool pass = Census("the test", true);
                if (!Lives)
                {
                    Notifications.Show(NotificationType.Info, LifeText.NotLiving, null, 10f);
                    return;
                }
                LifeGame.Counted(out int inCity, out int _);
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.CensusDone, new Dictionary<string, string>
                {
                    { "buildings", Standing.Count.ToString(Plain) }, { "empty", (Plan.Count - Standing.Count).ToString(Plain) }, { "listed", inCity.ToString(Plain) }
                }, 12f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The census line: at the load with Developer diagnostics on, and on the test's asking (a FAIL always).
        private static bool Census(string when, bool asked = false)
        {
            GameInstance save = SaveGameManager.Current;
            bool found = LifeGame.GetDataFinds(), listed = LifeGame.Listed();
            int built = Standing.Count, dayOne = 0, kept = 0, finished = 0, looks = 0;
            string lookWhy = null;
            for (int i = 0; i < Standing.Count; i++)
            {
                string id = Standing[i].Spec.Id;
                bool keptOne = KeptLots.Contains(id);
                kept += keptOne ? 1 : 0;
                dayOne += BuiltLots.Contains(id) && !keptOne ? 1 : 0;
                finished += _state.On(id) != null ? 1 : 0;
                if (_down || MadeBuildings.LooksAsGame(Standing[i], out string why))
                {
                    looks++;
                }
                else
                {
                    lookWhy = lookWhy ?? Standing[i].Address + ": " + why;
                }
            }
            int open = 0;
            for (int i = 0; i < Plan.Count; i++)
            {
                open += !StandsOn(Plan[i].Id) && _state.Covering(Plan[i].Id) == null && BuildingsRules.Clear(Plan[i].Id) ? 1 : 0;
            }
            int leases = 0, leased = 0;
            for (int i = 0; i < LeasedUp.Length; i++)
            {
                leases += Lessee(i) != null ? 1 : 0;
                leased += LeasedUp[i] != null ? 1 : 0;
            }
            LifeGame.Counted(out int inCity, out int inDistrict);
            int registrations = LifeGame.InSaveList(Standing);
            string rowWhy = null;
            bool zone = LifeGame.Zoned, row = true, placed = true;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                bool rowOf = LifeGame.Rowed(d);
                row &= rowOf;
                string oneWhy = null;
                bool placedOne = !rowOf || LifeGame.RowPlaced(d, out oneWhy);
                placed &= placedOne;
                rowWhy = rowWhy ?? oneWhy;
            }
            bool stats = LifeGame.HasStats(save, DistrictSplit.Uptown) && LifeGame.HasStats(save, DistrictSplit.CedarHeights);
            bool pass = !_known ? !Lives
                        : _down ? found && !listed && inCity == 0 && inDistrict == 0 && registrations == 0   // out of the game's lists, its record kept for what holds its id
                        : found && listed && built > 0 && zone && placed && inCity == built && inDistrict == built && registrations == built && stats && looks == built
                          && leased == leases;
            string line = "Uptown check (census, " + when + "): " + (!_known
                              ? "not a district of the game at this load (" + (_notWhy ?? "not asked for") + ")"
                              : "the districts " + DistrictSplit.UptownId + " and " + DistrictSplit.CedarId + " found by the game's GetData " + Yes(found) + ", in its list of districts " + Yes(listed)
                                + ", its zone on the city map " + Yes(zone) + ", its filter row " + Yes(row) + (row ? " under the map's districts " + Yes(placed) : string.Empty)
                                + (placed ? string.Empty : " (" + rowWhy + ")") + ", " + built + " building(s) built (" + dayOne + " from the first day, " + kept
                                + " kept from an earlier load, " + finished + " finished by the construction game), " + open + " lot(s) open of " + Plan.Count + ", "
                                + leased + " of " + leases + " leased offices with their businesses, " + looks + " of " + built + " looking as the game's own (the city map's box "
                                + "drawn only as a shadow while the map is shut" + (lookWhy != null ? "; not: " + lookWhy : string.Empty) + "), in the game's lists "
                                + inCity + " in allBuildings and " + inDistrict + " in the district's, " + registrations + " registration(s) in the save's list, its stats " + Yes(stats)
                                + (_down ? ", stood down in this city" : string.Empty) + "; Uptown's figures: " + DistrictValues.Describe(LifeGame.Figures(DistrictSplit.Uptown))
                                + "; Cedar Heights': " + DistrictValues.Describe(LifeGame.Figures(DistrictSplit.CedarHeights))
                                + (built == 0 && _notWhy != null ? "; no building because " + _notWhy : string.Empty))
                          + ": " + (pass ? "PASS" : "FAIL");
            if (!pass)
            {
                Logger?.Warn(line);
            }
            else if (asked || Meta.Dev.Verbose)
            {
                Logger?.Info(line);
            }
            return pass;
        }

        private static string Yes(bool yes)
        {
            return yes ? "YES" : "NO";
        }

        // Why a test cannot run now, said in a notice; false when it can.
        private static bool Refused(bool needsLife)
        {
            if (LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return true;
            }
            if (_testing)
            {
                TestRefusals.TellKey(Logger, TestName, LifeText.Busy, "another of its tests is under way");
                return true;
            }
            if (needsLife && !Lives)
            {
                TestRefusals.TellKey(Logger, TestName, LifeText.NotLiving, _down ? "it stood down in this city" : _notWhy ?? "it has no building standing at this load");
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- a business opens there

        /// <summary>"Uptown: a business opens there": the game's own daily pass for a new business, run for Uptown alone.</summary>
        public static void TestBusiness()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (GoingPublic.InSession)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.Session, "in a Going Public session");   // a business started on one machine alone: the others would never have it
                    return;
                }
                int empty = LifeGame.EmptyUnits(Standing), open = LifeGame.Businesses(Standing);
                LifeGame.BusinessesBy(Standing, Before);
                int next = -1;
                string why = null;
                for (int d = 0; d < DistrictSplit.Count; d++)
                {
                    int dayOf = LifeGame.StartBusiness(d, out string whyOf);   // the game's daily pass, once for each of the two districts
                    next = dayOf >= 0 ? dayOf : next;
                    why = why ?? whyOf;
                }
                LifeGame.BusinessesBy(Standing, After);
                string started = null, kind = null;
                foreach (KeyValuePair<string, string> now in After)
                {
                    if (Before.TryGetValue(now.Key, out string was) && was != now.Value && !string.IsNullOrEmpty(now.Value) && now.Value != "ba:businesstype_empty")
                    {
                        started = now.Key;
                        kind = now.Value;
                        break;
                    }
                }
                bool pass = started != null;
                string line = "Uptown check (a business opens): the game's own daily pass for each of the band's two districts (CompetitionHelper.StartNewBusiness over its "
                              + Standing.Count + " building(s)) " + (why != null ? "could not run (" + why + ")"
                                  : pass ? "started a business of the type " + kind + " at " + started + " (empty units " + empty + " before, " + LifeGame.EmptyUnits(Standing)
                                           + " after; businesses " + open + " before, " + LifeGame.Businesses(Standing) + " after; the game's next new business there on day " + next + ")"
                                  : "started none (" + empty + " empty unit(s); the game starts one only where a demanded product has room and a layout fits)")
                              + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line);
                    Notifications.Show(NotificationType.Info, LifeText.BusinessDone, new Dictionary<string, string> { { "address", started } }, 12f);
                }
                else
                {
                    Logger?.Warn(line);
                    Notifications.Show(NotificationType.Warning, LifeText.BusinessNone, null, 12f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- rent a unit there

        /// <summary>"Uptown: rent a unit there": the nearest unit to let made the destination; the player rents it through the game's own BizMan.</summary>
        public static void TestRent()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                MadeBuilding unit = LifeGame.ToLet(Standing);
                if (unit == null)
                {
                    Logger?.Warn("Uptown check (rent): no unit of Uptown is to let and empty just now (of " + Standing.Count + " building(s)): FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.RentNone, null, 10f);
                    return;
                }
                LifeGame.Destination(unit);
                Notifications.Show(NotificationType.Info, LifeText.RentReady, new Dictionary<string, string> { { "address", unit.Address } }, 16f);
                Logger?.Info("Uptown test: " + unit.Address + " is to let; the destination is set there, and the test watches for " + (RentWatch / 60f).ToString("F0", Plain)
                             + " minutes for the player renting it through BizMan.");
                if (Instance != null)
                {
                    _testing = true;
                    Instance.StartCoroutine(Guard.Routine(Instance.WatchRent(unit)));
                }
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        private IEnumerator WatchRent(MadeBuilding unit)
        {
            float until = Time.unscaledTime + RentWatch;
            var wait = new WaitForSecondsRealtime(1f);
            try
            {
                while (Time.unscaledTime < until && unit.Registration != null && !unit.Registration.RentedByPlayer && Lives)
                {
                    yield return wait;
                }
                bool rented = unit.Registration != null && unit.Registration.RentedByPlayer;
                List<MadeBuilding> one = new List<MadeBuilding>(1) { unit };
                bool inSave = LifeGame.InSaveList(one) == 1;
                string line = "Uptown check (rent): " + (rented
                                  ? "the player rented " + unit.Address + " through the game's own BizMan: its rent $" + LifeGame.RentOf(unit).ToString("F0", Plain)
                                    + " a day, its registration in the save's list " + Yes(inSave) + ", the district "
                                    + (unit.Building != null ? unit.Building.Neighbourhood : DistrictValues.Id) + ": " + (inSave ? "PASS" : "FAIL")
                                  : "not rented within " + (RentWatch / 60f).ToString("F0", Plain) + " minutes" + (Lives ? string.Empty : " (Uptown stood down)") + ": FAIL");
                if (rented && inSave)
                {
                    Logger?.Info(line);
                    Notifications.Show(NotificationType.Info, LifeText.RentDone, new Dictionary<string, string> { { "address", unit.Address } }, 12f);
                }
                else
                {
                    Logger?.Warn(line);
                }
            }
            finally
            {
                _testing = false;
            }
        }

        // ---------------------------------------------------------------- save and read back

        /// <summary>
        /// "Uptown: save and read back": one of the game's own recovery saves written, read back by the game's own reader and
        /// gone through for anything of Uptown (the district save's self-check over the whole file, and its own counts), its
        /// records in the mod's section counted, and the save in memory compared with how it was before. The save is kept:
        /// loaded with the mod switched off, it has no Uptown and the player outside it.
        /// </summary>
        public static void TestSave()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (GoingPublic.InSession)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.Session, "in a Going Public session");   // the session's saves are Going Public's own
                    return;
                }
                if (MadeGame.Saving || DistrictSaveDirector.Waiting)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.Busy, "a save is being written");
                    return;
                }
                if (Instance != null)
                {
                    _testing = true;
                    Instance.StartCoroutine(Guard.Routine(Instance.SaveAndRead()));
                }
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        private IEnumerator SaveAndRead()
        {
            try
            {
                GameInstance live = SaveGameManager.Current;
                LifeGame.Holds(live, out int regs, out int stats, out int demand, out int rows, out bool _, out int _);
                string path = MadeGame.SaveRecovery(out string name);
                if (path == null)
                {
                    TestRefusals.TellKey(Logger, TestName, "reallife_meta_tool_cannot_save", "the game would not save here");
                    yield break;
                }
                Notifications.Show(NotificationType.Info, LifeText.Saving, null, 6f);
                float until = Time.unscaledTime + SaveWait;
                while (MadeGame.Saving && Time.unscaledTime < until)
                {
                    yield return null;
                }
                Task<GameInstance> reading = MadeGame.ReadBack(path);
                while (!reading.IsCompleted && Time.unscaledTime < until)
                {
                    yield return null;
                }
                GameInstance read = reading.IsCompleted && !reading.IsFaulted ? reading.Result : null;
                if (read == null)
                {
                    string fault = reading.Exception != null ? reading.Exception.GetBaseException().Message : "not in time";
                    Logger?.Warn("Uptown check (save and read back): the save " + name + " could not be read back (" + fault + "): FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.NotSaved, null, 12f);
                    yield break;
                }
                LifeGame.Holds(read, out int fileRegs, out int fileStats, out int fileDemand, out int fileRows, out bool filePlayer, out int kept);
                List<string> hits = DistrictSaveScan.Find(read, DistrictPlaces.Current, null, 8);
                LifeGame.Holds(live, out int nowRegs, out int nowStats, out int nowDemand, out int nowRows, out bool _, out int _);
                bool clean = fileRegs == 0 && fileStats == 0 && fileDemand == 0 && fileRows == 0 && !filePlayer && hits.Count == 0;
                bool back = nowRegs == regs && nowStats == stats && nowDemand == demand && nowRows == rows;
                bool keeps = kept >= regs + stats;
                bool pass = clean && back && keeps;
                string line = "Uptown check (save and read back): the save " + name + " written by the game and read back by its own reader names "
                              + (clean ? "nothing of Uptown" : "Uptown's places")
                              + " (registrations " + fileRegs + ", its stats " + fileStats + ", products' demand rows " + fileDemand + ", ticked map rows " + fileRows
                              + ", the player's address or destination "
                              + Yes(filePlayer) + "; the district save's check over the whole file: " + (hits.Count == 0 ? "none" : string.Join("; ", hits)) + "), the mod's section keeps "
                              + kept + " record(s) for " + regs + " registration(s) and " + stats + " stats, and after the save the game's own part holds Uptown's as before (registrations "
                              + nowRegs + " of " + regs + ", stats " + nowStats + " of " + stats + ", demand rows " + nowDemand + " of " + demand + ", map rows " + nowRows + " of " + rows + "): "
                              + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line + ". Load " + name + " with the mod switched off: no Uptown, the player outside it.");
                    Notifications.Show(NotificationType.Info, LifeText.Saved, new Dictionary<string, string> { { "save", name } }, 16f);
                }
                else
                {
                    Logger?.Warn(line);
                    Notifications.Show(NotificationType.Warning, LifeText.NotSaved, null, 12f);
                }
            }
            finally
            {
                _testing = false;
            }
        }
    }
}
