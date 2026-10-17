using System;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a drive shows: the game's job panel (or notifications without it), the guide marker, a beacon at the
    /// checkpoint and the parked panel at the box. Each panel line is rewritten only when its words change.
    /// </summary>
    internal sealed partial class PracticalTest
    {
        private readonly DoorPanel _park;
        private readonly Action _onParked;
        private ShiftCard _card;
        private bool _parkOffered;          // the car stands still near the box: the parked panel is up
        private bool _parkWritten;
        private string _title, _objective, _markerLabel, _toldObjective;
        private Vector3 _markerAt;
        private int _hudNext = -1, _hudMinor = -1, _hudSerious = -1, _hudSecs = -1;
        private Transform _beacon;
        private Transform _bay;

        private void Hud(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
            if (_step == Step.Result || _step == Step.Leaving)
            {
                _card?.Show(false);
                _mark.Hide();
                _park.Hide("the drive is over");
                return;
            }
            bool driving = _step == Step.Drive || _step == Step.Park;
            if (_card != null)
            {
                _card.Title(_title);
                _card.Line(0, _objective, WorldPanel.White());
                if (driving)
                {
                    int minor = Faults(false);
                    int serious = Faults(true);
                    int done = _step == Step.Park ? _route.Checkpoints.Count : _next;
                    if (done != _hudNext)
                    {
                        _hudNext = done;
                        _card.Line(1, Licences.T("reallife_lic_card_checkpoints", "done", done.ToString(), "of", _route.Checkpoints.Count.ToString()), WorldPanel.White());
                    }
                    if (minor != _hudMinor || serious != _hudSerious)
                    {
                        _hudMinor = minor;
                        _hudSerious = serious;
                        if (_lesson)
                        {
                            _card.Line(2, Licences.T("reallife_lic_card_faults", "faults", (minor + serious).ToString()), serious > 0 ? WorldPanel.Red() : WorldPanel.White());
                            _card.Line(3, null, WorldPanel.White());
                        }
                        else
                        {
                            _card.Line(2, Licences.T("reallife_lic_card_minor", "minor", minor.ToString(), "allowed", LicenceLaw.MinorsAllowed.ToString()), WorldPanel.White());
                            _card.Line(3, Licences.T("reallife_lic_card_serious", "serious", serious.ToString()), serious > 0 ? WorldPanel.Red() : WorldPanel.White());
                        }
                    }
                }
                int secs = Mathf.Max(0, Mathf.CeilToInt(_stepEnd - now));
                if (secs != _hudSecs)
                {
                    _hudSecs = secs;
                    _card.Line(4, Licences.T("reallife_lic_card_time", "time", (secs / 60).ToString() + ":" + (secs % 60).ToString("00")), secs < 30 ? WorldPanel.Red() : WorldPanel.White());
                }
                _card.Show(true);
            }
            else if (!ReferenceEquals(_objective, _toldObjective))
            {
                _toldObjective = _objective;
                Licences.Toast(NotificationType.Info, "reallife_lic_toast", "text", _objective ?? string.Empty, 6f);
            }
            if (_markerLabel != null)
            {
                _mark.Show(_markerAt, _markerLabel);
            }
            else
            {
                _mark.Hide();
            }
            if (_parkOffered && _body != null && _step == Step.Park)
            {
                if (!_parkWritten)
                {
                    _parkWritten = true;
                    WorldPanel panel = _park.Panel;
                    panel.Lines(Licences.T("reallife_lic_park_title"), Licences.T("reallife_lic_park_line"), null, WorldPanel.White());
                    panel.Buttons("reallife_lic_park_button", _onParked, true, null, null, false);
                    panel.Button(2, null, null, false, false);
                    panel.Button(3, null, null, false, false);
                }
                _park.Show(_body.position, _body.position, 2.0f, null);
            }
            else
            {
                _park.Hide("the car is not still near the bay");
            }
        }

        private void ParkedClicked()
        {
            try
            {
                if (_step == Step.Park && _parkOffered)
                {
                    _park.Hide("the player said they have parked");
                    JudgeParking();
                    Conclude(Time.time, null);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Show(string objective, Vector3 markerAt, string markerLabel)
        {
            _objective = objective;
            _markerAt = markerAt;
            _markerLabel = markerLabel;
        }

        private static void Say(string key)
        {
            Licences.Toast(NotificationType.Info, key, 7f);
        }

        // On the kerb side of the checkpoint, clear of the lane.
        private void Beacon(Vector3 at, Vector3 runs)
        {
            if (_beacon == null)
            {
                _beacon = LicenceProps.Make(LicenceProps.Beacon, _parent);
            }
            if (_beacon == null)
            {
                return;
            }
            Vector3 place = at + Vector3.Cross(Vector3.up, runs) * 2.4f;
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            // The model's flag is on its +X: turned to face the driver with the flag pointing in at the checkpoint.
            Vector3 back = new Vector3(-runs.x, 0f, -runs.z);
            _beacon.SetPositionAndRotation(place, back.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(back, Vector3.up) : Quaternion.identity);
            _beacon.gameObject.SetActive(true);
        }

        private void HideBeacon()
        {
            if (_beacon != null)
            {
                _beacon.gameObject.SetActive(false);
            }
        }

        private void HideProps()
        {
            HideBeacon();
            if (_bay != null)
            {
                _bay.gameObject.SetActive(false);
            }
        }
    }
}
