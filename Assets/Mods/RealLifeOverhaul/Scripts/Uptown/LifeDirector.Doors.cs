using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's warehouses and their garage doors (docs/features/uptown.md, "Garage doors"; the owner, 27 Oct 2026, of a player's
    /// question how to get cars and trucks in and out of the copied warehouses: "this breaks the immersion and the game"): at every
    /// load one "Uptown check (garage doors)" line says whether each warehouse standing keeps its doors whole as the game's own
    /// warehouse has them, a line each with Developer diagnostics on; and the test "Uptown: drive into the nearest warehouse and out"
    /// (Options, Test tools) puts the player's car in front of the nearest warehouse's garage door, takes it in by the game's own call
    /// (what the door's trigger asks when a car drives in), counts the warehouse's vehicle slots and takes the car out again by the
    /// game's own exit. Only the player's own car and place change: safe in a Going Public session. Nothing runs a frame but the
    /// test's few steps.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const float DriveSettle = 0.6f;     // s of real time a car put in front of a door is given to settle on its wheels
        private const float DriveWait = 12f;        // s the game's own fade, load and exit are given before the test calls them failed
        private const float DriveBack = 16f;        // m the car may stand from the door after it came out: it was put out in front of it, or inside it
        private const float DriveRetake = 2.5f;     // s more a car put out is watched: the door's entrance trigger takes a car that touches it straight back in, and the first look would not see it

        // ---------------------------------------------------------------- at every load

        // One line: the warehouses standing, those whose garage doors are whole, the doors and the vehicle slots behind them.
        private static void DoorsCensus(string when)
        {
            int warehouses = 0, whole = 0, doors = 0, bays = 0;
            string first = null;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                CompleteFacts f = LifeGame.EntranceFacts(made);
                if (!f.Warehouse)
                {
                    continue;
                }
                warehouses++;
                doors += f.GatesWhole;
                bays += f.GatesWanted;
                string why = LifeGame.DoorsWhy(made);
                whole += why == null ? 1 : 0;
                first = first ?? (why != null ? made.Address + ": " + why : null);
                if (Meta.Dev.Verbose)
                {
                    Say(why == null, "Uptown check (garage doors, " + when + ", " + made.Address + "): " + made.Design + ", " + f.GatesWhole + " of " + f.GatesWanted
                                     + " garage door(s) whole, the structure's " + LifeGame.StructureSlots(made) + " vehicle slot(s), the mouths "
                                     + (why == null ? "clear" : "not (" + why + ")") + ": " + (why == null ? "PASS" : "FAIL"));
                }
            }
            if (warehouses == 0)
            {
                return;
            }
            bool pass = whole == warehouses;
            Say(pass, "Uptown check (garage doors, " + when + "): " + whole + " of " + warehouses + " warehouses keep their garage doors whole as the game's own (" + doors
                      + " door(s), each with a vehicle slot of its structure behind it, looking out on its street, its mouth clear of lamp posts, furniture and parked cars)"
                      + (first != null ? "; first wrong: " + first : string.Empty) + ": " + (pass ? "PASS" : "FAIL"));
        }

        // ---------------------------------------------------------------- the test: a car in and out

        /// <summary>"Uptown: drive into the nearest warehouse and out": the player's car taken in at the nearest warehouse's garage door and out again, its vehicle slots counted.</summary>
        public static void TestWarehouse()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (Instance == null || !LifeGame.CanMove)
                {
                    TestRefusals.Tell(Logger, TestName, Instance == null ? TestRefusal.NotLoaded : TestRefusal.Indoors);
                    return;
                }
                if (!LifeGame.DrivesCar(out CarController car))
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.WarehouseCar, "the player is not at the wheel of a car");
                    return;
                }
                MadeBuilding made = LifeGame.NearestWarehouse(Standing, car.transform.position, out DriveInEntrance gate);
                if (made == null)
                {
                    Say(false, "Uptown check (warehouse): no warehouse with a garage door stands in the district at this load (Player.log's \"Uptown check (garage doors\" line says why): FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.WarehouseNone, null, 12f);
                    return;
                }
                _testing = true;
                Instance.StartCoroutine(Guard.Routine(Instance.DriveIn(made, gate, car)));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        // The car in front of the door; if the warehouse is the player's, in by the game's own call and out by its own exit.
        private IEnumerator DriveIn(MadeBuilding made, DriveInEntrance gate, CarController car)
        {
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                string why = LifeGame.DoorsWhy(made);
                int structure = LifeGame.StructureSlots(made);
                bool mine = made.Registration != null && made.Registration.RentedByPlayer && !GoingPublic.Foreign(made.Registration);
                Dictionary<string, string> data = new Dictionary<string, string>
                {
                    { "address", made.Address }, { "slots", structure.ToString(Plain) }
                };
                if (!mine)
                {
                    bool whole = why == null;
                    Say(whole, "Uptown check (warehouse): " + made.Address + " (" + made.Design + ") has " + LifeGame.EntranceFacts(made).GatesWhole
                               + " garage door(s) whole and the structure's " + structure + " vehicle slot(s)" + (whole ? string.Empty : "; wrong: " + why)
                               + "; it is not the player's, so its door is shut to a car and none was driven in (rent a warehouse in the district and press the test again): "
                               + (whole ? "PASS" : "FAIL"));
                    Notifications.Show(whole ? NotificationType.Info : NotificationType.Warning, whole ? LifeText.WarehouseRent : LifeText.WarehouseFailed, data, 14f);
                    yield break;
                }
                LifeGame.DrawUpAt(car, gate);
                float until = Time.unscaledTime + DriveSettle;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                if (!LifeGame.DrivesCar(out CarController now) || now != car)
                {
                    Say(false, "Uptown check (warehouse): the player left the car before it was driven in: FAIL");
                    yield break;
                }
                bool asked = gate.TryToEnterWithCar(car);   // what the door's trigger asks of the game when the car drives into it
                until = Time.unscaledTime + DriveWait;
                while (asked && Time.unscaledTime < until && !InWarehouse(manager, made))
                {
                    yield return null;
                }
                bool inside = InWarehouse(manager, made);
                LifeGame.SlotsOf(made, out int slots, out int taken);
                string held = inside ? "inside, " + slots + " vehicle slot(s) of the registration, " + taken + " with a vehicle" : "not inside";
                bool leftAgain = false;
                if (inside)
                {
                    until = Time.unscaledTime + DriveSettle;
                    while (Time.unscaledTime < until)
                    {
                        yield return null;
                    }
                    manager.ExitFromBuilding(gate.doorID);   // the game's own exit for a car: by the door it came in by
                    until = Time.unscaledTime + DriveWait;
                    while (Time.unscaledTime < until && BuildingManager.IsInsideBuilding)
                    {
                        yield return null;
                    }
                    until = Time.unscaledTime + DriveSettle;
                    while (Time.unscaledTime < until)
                    {
                        yield return null;
                    }
                    leftAgain = !BuildingManager.IsInsideBuilding && car != null && LifeGame.Away(car, gate) <= DriveBack;
                    until = Time.unscaledTime + DriveRetake;
                    while (leftAgain && Time.unscaledTime < until)
                    {
                        yield return null;
                    }
                    leftAgain = leftAgain && !BuildingManager.IsInsideBuilding && !manager.enteringBuilding;   // still out: the door did not take the car back in
                }
                bool pass = why == null && asked && inside && leftAgain && slots == structure;
                Say(pass, "Uptown check (warehouse): the player's car put in front of the garage door of " + made.Address + " (" + made.Design
                          + "), the game's own call to take a car in " + (asked ? "accepted it" : "refused it (a notice says why)") + ", " + held
                          + (inside ? ", " + (leftAgain ? "out again by the same door" : "not out again") : string.Empty) + "; the structure has " + structure
                          + " vehicle slot(s)" + (why != null ? "; wrong: " + why : string.Empty) + ": " + (pass ? "PASS" : "FAIL"));
                data["used"] = taken.ToString(Plain);
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, pass ? LifeText.WarehouseDone : LifeText.WarehouseFailed, data, 14f);
            }
            finally
            {
                _testing = false;
            }
        }

        private static bool InWarehouse(BuildingManager manager, MadeBuilding made)
        {
            return BuildingManager.IsInsideBuilding && manager != null && manager.building != null && manager.building.Address == made.Building.Address;
        }
    }
}
