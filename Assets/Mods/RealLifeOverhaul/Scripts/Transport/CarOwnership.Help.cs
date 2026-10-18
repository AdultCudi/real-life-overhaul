using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Car ownership's panel over a broken-down or dry car standing still, with roadside assistance on its button.</summary>
    public sealed partial class CarOwnership
    {
        // The game's panel over the car: what is wrong, what help costs, and the button that calls it.
        private void Help(RoadsideJob job)
        {
            float fee = CarRoadside.Fee(job, _record);
            int key = (int)job | (Mathf.RoundToInt(fee) << 3);
            if (key != _helpKey)
            {
                _helpKey = key;
                _helpJob = job;
                bool broken = job == RoadsideJob.Breakdown;
                _help.Lines(_carName, TransitText.T(broken ? "reallife_car_help_breakdown" : "reallife_car_help_fuel"), TransitText.Money(fee), GameUi.WorldPanel.White());
                _help.Buttons(broken ? "reallife_car_button_roadside" : "reallife_car_button_fuel", _onHelp, true, null, null, false);
            }
            Vector3 over = _car.transform.position + Vector3.up * 2.4f;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = camera.transform.position + camera.transform.forward * 6f;   // a camera inside the car: the panel stands ahead of it
            }
            _help.Position = over;
            int state = _help.Show() ? 1 : 2;
            if (state != _helpState)
            {
                _helpState = state;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Car check (roadside panel): " + _carName + " stands still with " + (job == RoadsideJob.Breakdown ? "a breakdown" : "an empty tank") + ": "
                                 + (state == 1 ? "the game's own panel is up over the car, help for " + fee.ToString("F0") + " on the game's interact key: PASS"
                                     : "FAIL (the game's panel is showing another place, a filling station; it is tried again while the car stands)") + ".");
                }
            }
        }

        private void HideHelp(string why)
        {
            if (_helpState != 0)
            {
                bool was = _helpState == 1;
                _helpState = 0;
                _help.Hide();
                if (was && Meta.Dev.Verbose)
                {
                    Logger?.Info("Car check (roadside panel): closed (" + why + ").");
                }
            }
        }

        // The panel's button, on the game's interact key or a click.
        private void HelpClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (_car == null || _record == null || _servicing)
                {
                    return;
                }
                bool called = _roadside.Call(_helpJob, _car, _record, Time.time);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Car check (choice): " + (_helpJob == RoadsideJob.Breakdown ? "roadside assistance" : "a can of fuel") + " from the panel over the car: "
                                 + (called ? "called: PASS" : "not called (the money is short, or help is already on its way)") + ".");
                }
                if (called)
                {
                    HideHelp("help was called");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
