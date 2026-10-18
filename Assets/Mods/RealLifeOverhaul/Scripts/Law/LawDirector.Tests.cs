using System;
using System.Collections.Generic;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The law's test buttons (Options > Tests).</summary>
    public sealed partial class LawDirector
    {
        private TestRefusal Refusal(bool needsCustody)
        {
            if (!ModState.Loaded || !LawState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (!enabled || (needsCustody ? !Settings.LawCustody : !Settings.LawPrecincts))
            {
                return TestRefusal.FeatureOff;
            }
            if (_precincts.Count == 0)
            {
                return TestRefusal.NoPrecinct;
            }
            if (!needsCustody)
            {
                return TestRefusal.None;
            }
            if (Custody.Active || PoliceAttention.IsBusy)
            {
                return TestRefusal.PoliceBusy;
            }
            // Not over a time skip or a conversation; a menu is no reason: the button is in one.
            var uis = InstanceBehavior<UI.UIs>.Instance;
            return (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning) || DialogController.current != null
                ? TestRefusal.ScreenBusy : TestRefusal.None;
        }

        /// <summary>Test button: the whole arrest from where the player stands. 0 minor, 1 serious, 2 fatal.</summary>
        public void TestArrest(int kind)
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: true);
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Arrest", why, 0, _precincts.Count + " precinct(s)");
                    return;
                }
                int charges = kind <= 0 ? LawBook.Bit(Charge.FledStop)
                    : kind == 1 ? LawBook.Bit(Charge.HitAndRun) | LawBook.Bit(Charge.InjuryFled)
                    : LawBook.Bit(Charge.DeathFled);
                // No fines: in play they are charged by whoever stops the player.
                if (!Custody.Arrest(charges, 0, null))
                {
                    TestRefusals.Tell(Logger, "Arrest", TestRefusal.PoliceBusy);
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_law_test_fine", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test button: marks the nearest precinct and says how far it is.</summary>
        public void TestNearest()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Nearest precinct", why);
                    return;
                }
                Precinct precinct = GuideToNearest();
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Nearest precinct", TestRefusal.NoPrecinct);
                    return;
                }
                Vector3 offset = precinct.Door - PlayerHelper.GetCityPosition();
                offset.y = 0f;
                var data = new Dictionary<string, string>(3)
                {
                    { "precinct", precinct.Name },
                    { "metres", Mathf.RoundToInt(offset.magnitude).ToString() },
                    { "address", precinct.Building.building.Address.ToFormattedString() }
                };
                Notifications.Show(NotificationType.Info, "reallife_law_nearest", data, 12f);
                Logger?.Info($"Law: nearest precinct is {precinct.Name} at {precinct.Building.building.Address.ToFormattedString()}, {offset.magnitude:F0} m.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test button: opens the nearest precinct's front desk wherever the player stands.</summary>
        public void TestDesk()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why == TestRefusal.None && (Custody.Active || DialogController.current != null))
                {
                    why = Custody.Active ? TestRefusal.PoliceBusy : TestRefusal.ScreenBusy;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Front desk", why);
                    return;
                }
                Precinct precinct = Inside ?? _precincts.Nearest(PlayerHelper.GetCityPosition());
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Front desk", TestRefusal.NoPrecinct);
                    return;
                }
                Idle();
                _desk.Open(precinct, Inside == precinct);
                Notifications.Show(NotificationType.Info, "reallife_law_test_desk_done", null, 6f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test button: dresses the nearest precinct's frontage again and logs what fitted.</summary>
        public void TestDress()
        {
            try
            {
                TestRefusal why = Refusal(needsCustody: false);
                if (why == TestRefusal.None && BuildingManager.IsInsideBuilding)
                {
                    why = TestRefusal.Indoors;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Dress the precinct", why);
                    return;
                }
                Vector3 at = PlayerHelper.GetPosition();
                Precinct precinct = _precincts.Nearest(at);
                if (precinct == null)
                {
                    TestRefusals.Tell(Logger, "Dress the precinct", TestRefusal.NoPrecinct);
                    return;
                }
                float metres = JobSpots.Flat(at, precinct.Door);
                if (metres > 250f)
                {
                    TestRefusals.TellKey(Logger, "Dress the precinct", "reallife_law_test_dress_far", $"{precinct.Name} is {metres:F0} m away");
                    return;
                }
                _precincts.Undress();
                _precincts.DressNow(precinct);
                string report = _precincts.Report();
                Logger?.Info("Law: dressed again by the test button: " + report + ". " + _interior.Report() + ".");
                var data = new Dictionary<string, string>(1) { { "precinct", precinct.Name } };
                Notifications.Show(NotificationType.Info, "reallife_law_test_dress_done", data, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test button: wipes the record and puts both reputations back to a new save's.</summary>
        public void TestClearRecord()
        {
            if (!LawState.Loaded)
            {
                TestRefusals.Tell(Logger, "Clear record", TestRefusal.NotLoaded);
                return;
            }
            if (Custody.Active)
            {
                TestRefusals.Tell(Logger, "Clear record", TestRefusal.PoliceBusy);
                return;
            }
            Record.Clear();
            Reputation.Set(Reputation.Start, Reputation.Start);
            _level = Reputation.ScrutinyLevel;
            Notifications.Show(NotificationType.Success, "reallife_law_record_cleared", null, 7f);
            Logger?.Info("Law: record cleared by the test button.");
        }
    }
}
