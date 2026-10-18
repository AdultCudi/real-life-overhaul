using System;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The director's part that offers the routines at the furniture and the doctor's panel in a hospital, and shows the needs bars.</summary>
    public sealed partial class HomeDirector
    {
        private const float DoctorReach = 7f;        // metres from the doctor's desk at which the game's panel is offered
        private const string ButtonDoctor = "reallife_home_button_doctor";
        private Buildings.Building _doctorIn;   // the hospital the desk was looked for in
        private Vector3 _doctorAt;
        private bool _doctorDesk;
        private int _doctorKey = int.MinValue;
        private int _doctorState;            // 0 not up, 1 up, 2 refused by the game's own panel
        private bool _offersTold;
        private int _noteCode = -1;
        private string _note;

        // At home: a click on the cookers and coffee machines and a button in the fridge's panel; in a hospital, the game's panel at the doctor's desk.
        private void Offer(PlayerController player, bool free, bool atHome)
        {
            if (Settings.HomeRoutines && atHome)
            {
                HomePlace.Scan();
                bool clicks = HomeOffers.Mark(HomePlace.Cookers, HomePlace.CoffeeMachines);
                bool button = HomeFridgeButton.Install();
                if (!_offersTold && Meta.Dev.Verbose)
                {
                    _offersTold = true;
                    Logger?.Info("Home check (offer): at home with " + HomePlace.Cookers.Count + " cooker(s), " + HomePlace.CoffeeMachines.Count + " coffee machine(s) and "
                                 + HomePlace.Fridges.Count + " fridge(s): click actions " + (clicks ? "on" : "NOT on") + ", the fridge panel's button " + (button ? "in" : "NOT in") + ": "
                                 + (clicks && button ? "PASS" : "FAIL") + ".");
                }
            }
            else if (!atHome)
            {
                _offersTold = false;
            }
            if (free && Settings.HomeIllness && HomePlace.InHospital())
            {
                Doctor(player);
                return;
            }
            HideDoctor();
            if (!BuildingManager.IsInsideBuilding)
            {
                _doctorIn = null;
            }
        }

        // The game's panel over a place, at the doctor's desk (or, in a clinic without one, where the player came in).
        private void Doctor(PlayerController player)
        {
            Vector3 at = player.transform.position;
            Buildings.Building building = InstanceBehavior<BuildingManager>.Instance.building;
            if (!ReferenceEquals(building, _doctorIn))
            {
                _doctorIn = building;
                _doctorDesk = HomePlace.DoctorDesk(out _doctorAt);
                if (!_doctorDesk)
                {
                    _doctorAt = at;
                }
                _doctorKey = int.MinValue;
            }
            if (CitySpots.Flat(_doctorAt - at) > DoctorReach * DoctorReach || Mathf.Abs(_doctorAt.y - at.y) > 3f)
            {
                HideDoctor();
                return;
            }
            bool ill = HomeHealth.Ill || ModState.IsSick;
            int key = (ill ? 1 : 0) | ((int)HomeState.Ill << 1);
            if (key != _doctorKey)
            {
                _doctorKey = key;
                _doctor.Lines(JobText.T("reallife_home_doctor_title"),
                    JobText.T(HomeHealth.Ill ? HomeHealth.NameKey(HomeState.Ill) : ill ? "reallife_home_doctor_unwell" : "reallife_home_doctor_checkup"),
                    JobText.Money(HomeBalance.ConsultationFee), GameUi.WorldPanel.White());
                _doctor.Buttons(ButtonDoctor, _onDoctor, true, null, null, false);
            }
            // Over the desk; over the player's own head while the desk is behind the camera.
            Vector3 over = _doctorAt + Vector3.up * (_doctorDesk ? 2.2f : 2.1f);
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = at + Vector3.up * 2.1f;
            }
            _doctor.For(_doctorAt);
            _doctor.Position = over;
            int state = _doctor.Show() ? 1 : 2;
            if (state != _doctorState)
            {
                _doctorState = state;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (doctor panel): " + Mathf.Sqrt(CitySpots.Flat(_doctorAt - at)).ToString("F1", CultureInfo.InvariantCulture) + " m from "
                                 + (_doctorDesk ? "the doctor's desk" : "where the player came in (no desk found)") + ", offered within " + DoctorReach.ToString("F0") + " m: "
                                 + (state == 1 ? "the game's own panel is up (" + (ill ? "ill" : "well") + ", fee " + HomeBalance.ConsultationFee + "): PASS"
                                     : "FAIL (the game's panel is showing another place, a lift; it is tried again while the player stands here)") + ".");
                }
            }
        }

        private void HideDoctor()
        {
            if (_doctorState != 0)
            {
                bool was = _doctorState == 1;
                _doctorState = 0;
                _doctor.Hide();
                if (was && Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (doctor panel): closed (its button was used, the player walked away or left, or another screen is open).");
                }
            }
        }

        private void DoctorClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (choice): 'See a doctor' at the doctor's panel.");
                }
                HideDoctor();
                Act(HomeWish.Doctor, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Strip(bool atHome)
        {
            bool show = Settings.HomeStrip && Settings.HomeNeeds && (atHome || HomeState.Hygiene < 35f || HomeState.Stress > 65f || HomeHealth.Ill);
            if (!show)
            {
                HomeNeedsBar.Hide();
                return;
            }
            string note = null;
            if (HomeHealth.Ill)
            {
                int hoursLeft = Mathf.Max(0, Mathf.CeilToInt((HomeState.IllUntil - TimeHelper.NowInMinutes()) / 60f));
                int code = (int)HomeState.Ill * 100000 + hoursLeft * 2 + (HomeState.IllTreated ? 1 : 0);
                if (code != _noteCode)
                {
                    _noteCode = code;
                    _note = JobText.T("reallife_home_strip_ill", "illness", JobText.T(HomeHealth.NameKey(HomeState.Ill)), "days", HomeHealth.Days(hoursLeft * 60f));
                }
                note = _note;
            }
            HomeNeedsBar.Show(HomeState.Hygiene, HomeState.Stress, HomeState.Tidy, HomeTidy.HasHome, note);
        }
    }
}
