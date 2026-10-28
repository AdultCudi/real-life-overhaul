using System;
using System.Collections;
using System.Collections.Generic;
using Extensions;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The dealership's desk (Crest Motors): the look at a desk gone wrong, once a second while the player is in one of the district's
    /// buildings, and its two tests (Options, Test tools). A desk of the game's seats the player, pauses the game a second later and opens
    /// the dialog its building's service answers with; the dealership copies a service with no vehicle store in it, so the panel came up
    /// empty or not at all and the player stayed seated with the game paused. Now the copy carries the store (MadeGame.Desks), and
    /// whatever else goes wrong at a desk is closed the game's own way. The panel test opens the game's own purchase panel for the
    /// dealership as its desk would and reads its cars and prices; the seat test sits the player down at a chair of the room and
    /// stands them up, then does it once more with a panel that has no dialog, which the look must close. Each writes one
    /// "Uptown check (...)" line, PASS or FAIL, and says why in a notice when it cannot run. Only the player's own seat changes.
    /// COST: nothing per frame; a look a second while indoors in the district.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const float PanelWait = 4f;     // s of real time the game's panel is given to start its page
        private const float SeatSettle = 1f;    // s of real time a player is left seated before they are stood up
        private const float WatchWait = 9f;     // s of real time the look is given to close a panel on no dialog (its grace, a look, and a margin)

        // Once a second, from OnUpdate, while the district lives: the desk's dialog gone wrong closed, a player left seated stood up.
        private static bool _watchOff;   // a game member the look reads is missing in this build: the look stands down, the district goes on
        private static bool _restocked;  // the dealership's list read against the car mods' for this visit

        private static void WatchDesk(float now)
        {
            if (_watchOff)
            {
                return;
            }
            try
            {
                if (!LifeGame.Indoors || !LifeGame.PlayerInside(Standing))
                {
                    LifeGame.WatchReset();
                    _restocked = false;
                    return;
                }
                string did = LifeGame.Watch(now);
                if (did != null)
                {
                    Logger?.Warn("Uptown: a desk went wrong, and " + did + ".");
                }
                RestockDealer();
            }
            catch (Exception e)
            {
                _watchOff = true;
                Logger?.Warn("Uptown: the look at a desk gone wrong stands down for this run (" + e.GetType().Name + "); the district goes on.");
            }
        }

        // Once a visit, as the player walks into the dealership: its list of cars read against the car mods' (a car mod may list late).
        private static void RestockDealer()
        {
            if (_restocked)
            {
                return;
            }
            int at = DealerStanding();
            if (at < 0 || !LifeGame.PlayerIn(SpecialUp[at]))
            {
                return;
            }
            _restocked = true;
            string said = LifeGame.Restock(SpecialUp[at], SpecialsRules.All[at]);
            if (said != null)
            {
                Logger?.Info(said + ".");
            }
        }

        // The place of the table that is a dealership and stands as one at this load, or -1.
        private static int DealerStanding()
        {
            for (int i = 0; i < SpecialsRules.All.Length; i++)
            {
                if (SpecialsRules.All[i].Staff == SpecialStaff.Showroom && SpecialUp[i] != null)
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- the panel

        /// <summary>
        /// "Uptown: the dealership's purchase panel": the game's own panel opened as the dealership's desk opens it, its cars, their prices
        /// and its delivery read, then closed; its check.
        /// </summary>
        public static void TestDealer()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                int at = DealerStanding();
                if (at < 0)
                {
                    Logger?.Warn("Uptown check (dealership): no dealership stands as a special place at this load: FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.DealerNone, null, 12f);
                    return;
                }
                if (LifeGame.DialogOpen || Instance == null)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.Busy, "a dialog panel is open");
                    return;
                }
                _testing = true;
                Instance.StartCoroutine(Guard.Routine(Instance.ReadPanel(SpecialUp[at], SpecialsRules.All[at])));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        private IEnumerator ReadPanel(MadeBuilding made, SpecialPlace place)
        {
            try
            {
                string name = LifeGame.SpecialName(made, place, out string kind);
                string line = "Uptown check (dealership): " + name + " at " + made.Address + ": ";
                if (!LifeGame.OpenStore(made, out string why))
                {
                    Refuse(line + "the game's purchase panel could not be opened (" + why + "): FAIL", name);
                    yield break;
                }
                int cars = 0, warehouses = 0;
                string first = null;
                float price = 0f, fee = 0f;
                bool read = false;
                float until = Time.unscaledTime + PanelWait;
                while (Time.unscaledTime < until && !read)
                {
                    read = LifeGame.ReadStore(made, out cars, out first, out price, out fee, out warehouses);
                    yield return null;
                }
                bool pass = read && cars > 0 && StockRules.Real(cars, price) && fee > 0f;
                string none = warehouses == 0 ? " (none rented with a free slot: its Deliver to list has nothing to pick)" : string.Empty;
                string said = read
                    ? "lists " + cars + " cars, the first " + first + " at " + price.ToCurrencyFormat() + ", with a delivery fee of " + fee.ToCurrencyFormat()
                      + " and " + warehouses + " warehouse(s) of the player's to deliver to" + none
                    : "did not start its page in " + PanelWait.ToString("F0", Plain) + " s";
                string text = line + "the game's purchase panel, opened by the dialog of its service (" + made.Building.SpecialService.dialogType + "), "
                              + said + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(text);
                    Notifications.Show(NotificationType.Info, LifeText.DealerDone, new Dictionary<string, string>
                    {
                        { "name", name }, { "count", cars.ToString(Plain) }, { "price", price.ToCurrencyFormat() }
                    }, 14f);
                }
                else
                {
                    Refuse(text, name);
                }
            }
            finally
            {
                LifeGame.CloseDialog();   // the panel shut the game's way, pass or fail
                _testing = false;
            }
        }

        private static void Refuse(string line, string name)
        {
            Logger?.Warn(line);
            Notifications.Show(NotificationType.Warning, LifeText.DealerFail, new Dictionary<string, string> { { "name", name } }, 12f);
        }

        // ---------------------------------------------------------------- the seat

        /// <summary>
        /// "Uptown: sit at the dealership's desk and stand up": from inside the dealership, the player seated on the nearest free seat
        /// of the room as its desk seats them and stood up again by the game's own call; then seated once more with the game's panel
        /// up on no dialog, which the look must close and stand them up; its check.
        /// </summary>
        public static void TestDealerSit()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                int at = DealerStanding();
                if (at < 0)
                {
                    Logger?.Warn("Uptown check (dealership seat): no dealership stands as a special place at this load: FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.DealerNone, null, 12f);
                    return;
                }
                string name = LifeGame.SpecialName(SpecialUp[at], SpecialsRules.All[at], out string kind);
                if (!LifeGame.PlayerIn(SpecialUp[at]))
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.DealerAway, "the player is not inside " + name);
                    return;
                }
                if (LifeGame.DialogOpen || LifeGame.Seated || Instance == null)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.Busy, "a dialog panel is open or the player is seated");
                    return;
                }
                Transform seat = LifeGame.NearestSeat();
                if (seat == null)
                {
                    Logger?.Warn("Uptown check (dealership seat): " + name + " has no free seat in its room: FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.DealerSitFail, new Dictionary<string, string> { { "name", name } }, 12f);
                    return;
                }
                _testing = true;
                Instance.StartCoroutine(Guard.Routine(Instance.SitAndStand(name, SpecialUp[at], seat)));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        private IEnumerator SitAndStand(string name, MadeBuilding made, Transform seat)
        {
            try
            {
                string line = "Uptown check (dealership seat): " + name + " at " + made.Address + ": ";
                bool sat = LifeGame.SitAtDesk(seat);
                yield return new WaitForSecondsRealtime(SeatSettle);
                bool seated = sat && LifeGame.Seated;
                LifeGame.StandFromDesk();
                yield return new WaitForSecondsRealtime(SeatSettle * 0.5f);
                bool stood = !LifeGame.Seated && !LifeGame.DialogOpen;

                // The desk's fault: the game's panel up on no dialog, as a desk whose service answers with none leaves it.
                bool sat2 = stood && LifeGame.SitAtDesk(seat);
                bool opened = sat2 && LifeGame.OpenNothing();
                float until = Time.unscaledTime + WatchWait;
                while (opened && Time.unscaledTime < until && LifeGame.DialogOpen)
                {
                    yield return null;
                }
                bool closed = opened && !LifeGame.DialogOpen;
                bool free = closed && !LifeGame.Seated;
                if (!free)
                {
                    LifeGame.StandFromDesk();   // never leave the player held by the test
                }
                bool pass = seated && stood && free;
                string text = line + "seated on the room's nearest seat " + Yes(seated) + ", stood up by the game's own call " + Yes(stood)
                              + "; with the game's panel up on no dialog the look closed it and stood the player up " + Yes(free)
                              + (opened ? string.Empty : " (the panel could not be opened)") + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(text);
                    Notifications.Show(NotificationType.Info, LifeText.DealerSitDone, new Dictionary<string, string> { { "name", name } }, 14f);
                }
                else
                {
                    Logger?.Warn(text);
                    Notifications.Show(NotificationType.Warning, LifeText.DealerSitFail, new Dictionary<string, string> { { "name", name } }, 12f);
                }
            }
            finally
            {
                _testing = false;
            }
        }
    }
}
