using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Options > Tests: the player seated at the nearest free desk of a business of theirs by the pose the own firm's stint and
    /// the stations use (Careers/DeskGame.SitAt), kept there a game minute, and stood up by its release (StandFrom), with a
    /// "Desk check (seat)" line saying whether they walk again. Cost: nothing per frame while idle; while it runs, one clock
    /// read a frame for at most two real minutes.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const int SeatTestMinutes = 1;            // game minutes the test keeps the player at the desk
        private const float SeatTestMostSeconds = 120f;   // real seconds it waits for that minute at most (the options open, the game paused)

        private readonly List<EmployeeStationController> _seatDesks = new List<EmployeeStationController>(16);
        private bool _seatTesting;
        private EmployeeStationController _seatDesk;      // the desk the test holds the player at, for the firms standing down meanwhile

        /// <summary>For Options > Tests: sits the player at the nearest free desk of the business they stand in for a game minute, then stands them up.</summary>
        public void TestDeskSeat()
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Desk seat", TestRefusal.NotLoaded);
                return;
            }
            if (_seatTesting || _stint != null || DeskGame.Busy || !isActiveAndEnabled)
            {
                TestRefusals.Tell(Logger, "Desk seat", TestRefusal.ScreenBusy, 0, _seatTesting ? "the test is running" : _stint != null ? "a stint at a desk is on" : "the work panel is up or the player is in a vehicle");
                return;
            }
            EmployeeStationController desk = null;
            if (FirmsGame.InsideOwn() != null)
            {
                FirmsGame.Desks(_seatDesks);
                desk = FirmsGame.NearestFree(_seatDesks);
                _seatDesks.Clear();
            }
            if (desk == null)
            {
                TestRefusals.TellKey(Logger, "Desk seat", "reallife_tech_test_desk_seat_none", "no free desk with its chair in a business of the player's here");
                return;
            }
            _seatTesting = true;
            _seatDesk = desk;
            StartCoroutine(Guard.Routine(SeatTest(desk)));
        }

        private IEnumerator SeatTest(EmployeeStationController desk)
        {
            string name = desk.name;
            bool seated = false;
            int from = FirmsGame.Clock;
            try
            {
                seated = DeskGame.SitAt(desk) && DeskGame.SeatedAt(desk);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            float until = Time.unscaledTime + SeatTestMostSeconds;
            while (seated && FirmsGame.Clock - from < SeatTestMinutes && Time.unscaledTime < until && desk != null)
            {
                yield return null;
            }
            int minutes = FirmsGame.Clock - from;
            if (!_seatTesting)
            {
                yield break;   // the firms stood down meanwhile and stood the player up (SeatTestDown)
            }
            DeskGame.StandFrom(desk);
            yield return null;   // as the stint's end: the desk's own employee seated a frame after
            bool walks = false, given = false;
            string why = null;
            try
            {
                walks = DeskGame.Walks(out why);
                given = desk == null || !FirmsGame.PlayerAt(desk);
                if (desk != null)
                {
                    DeskGame.Restaff(desk);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _seatTesting = false;
            _seatDesk = null;
            bool pass = seated && walks && given;
            Logger?.Info("Desk check (seat): the player seated at the " + name + " by the own desk's pose (" + (seated ? "its employee, on its chair, held" : "NOT seated") + ") for "
                         + minutes + " game minute(s), then stood up by its release: " + (walks ? "free to walk" : why) + (given ? ", the desk let go" : ", still the desk's employee")
                         + ": " + (pass ? "PASS" : "FAIL"));
            Notifications.Show(pass ? NotificationType.Success : NotificationType.Error, pass ? "reallife_tech_test_desk_seat_pass" : "reallife_tech_test_desk_seat_fail", null, 10f);
        }

        // The firms stand down while the test holds the player: stood up at once, the test called off.
        private void SeatTestDown()
        {
            if (!_seatTesting)
            {
                return;
            }
            _seatTesting = false;
            EmployeeStationController desk = _seatDesk;
            _seatDesk = null;
            DeskGame.StandFrom(desk);
            Logger?.Info("Desk check (seat): the test was called off as the firms stood down; the player was stood up.");
        }
    }
}
