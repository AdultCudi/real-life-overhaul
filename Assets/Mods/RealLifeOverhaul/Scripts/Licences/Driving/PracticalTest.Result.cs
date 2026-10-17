using System;
using System.Text;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The end of a drive: the result on the record, the examiner's sheet, and the examiner walking off.</summary>
    internal sealed partial class PracticalTest
    {
        private readonly StringBuilder _text = new StringBuilder(512);
        private readonly Action _onSheetClosed;
        private bool _closingSheet;

        // The drive is over, one way or another. Cut short (endedKey), a test fails whatever was marked.
        private void Conclude(float now, string endedKey)
        {
            Unhook();
            _parkOffered = false;
            HideProps();
            bool ended = endedKey != null;
            int minor = Faults(false);
            int serious = Faults(true);
            _passed = !_lesson && !ended && serious == 0 && minor <= LicenceLaw.MinorsAllowed;
            int day = TimeHelper.CurrentDay;
            int index = _class == LicenceClass.Commercial ? 1 : 0;
            if (_lesson)
            {
                if (!ended)
                {
                    LicenceState.Lessons[index]++;
                }
            }
            else if (_passed)
            {
                LicenceState.Grant(_class, day);
                LicenceState.RetestDue[index] = false;
                LicenceState.RetestDay[index] = 0;
                LicenceState.TestsPassed++;
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else
            {
                LicenceState.RetestDue[index] = true;
                LicenceState.RetestDay[index] = day + LicenceLaw.RetestWaitDays;
                LicenceState.TestsFailed++;
                ModState.AddModifier(ModState.MoodDown);
            }

            _text.Length = 0;
            if (ended)
            {
                _text.Append(Licences.T(endedKey)).Append('\n').Append('\n');
            }
            else if (_endWaived)
            {
                _text.Append(Licences.T("reallife_lic_sheet_end_closed")).Append('\n').Append('\n');
            }
            if (_lesson)
            {
                _text.Append(Licences.T(ended ? "reallife_lic_sheet_lesson_cut" : "reallife_lic_sheet_lesson_done")).Append('\n');
            }
            else
            {
                _text.Append(Licences.T("reallife_lic_sheet_counts", "minor", minor.ToString(), "serious", serious.ToString())
                    .Replace("{allowed}", LicenceLaw.MinorsAllowed.ToString())).Append('\n');
            }
            bool any = false;
            for (int i = 0; i < _faults.Length; i++)
            {
                if (_faults[i] > 0)
                {
                    any = true;
                    _text.Append("  ").Append(_faults[i]).Append(" x  ").Append(Licences.T(FaultKeys[i])).Append('\n');
                }
            }
            if (!any)
            {
                _text.Append("  ").Append(Licences.T("reallife_lic_sheet_clean")).Append('\n');
            }
            if (!_lesson)
            {
                _text.Append('\n').Append(_passed
                    ? Licences.T("reallife_lic_sheet_pass", "class", Licences.ClassName(_class), "days", LicenceLaw.ValidDays.ToString())
                    : Licences.T("reallife_lic_sheet_fail", "amount", Licences.Money(LicenceLaw.RetestFee)));
            }
            string title = _lesson ? Licences.T("reallife_lic_sheet_title_lesson")
                : Licences.T(_passed ? "reallife_lic_sheet_title_pass" : "reallife_lic_sheet_title_fail");
            if (!_screens.Report(this, title, string.Empty, _text.ToString(), _onSheetClosed))
            {
                Licences.Toast(_passed ? NotificationType.Success : NotificationType.Info, "reallife_lic_toast", "text", title + "\n" + _text, 20f);
            }
            _objective = Licences.T("reallife_lic_test_over");
            _markerLabel = null;
            _step = Step.Result;
            _resultAt = now;
            _logger?.Info("Licences: " + (_lesson ? "lesson" : "practical test") + " over" + (ended ? " (" + endedKey + ")" : string.Empty) + ": " + minor + " minor, "
                          + serious + " serious" + (_lesson ? "." : _passed ? ": PASSED, " + _class + " licence issued." : ": failed."));
        }

        // The sheet was closed by its Close entry or the cancel key: the examiner may go.
        private void SheetClosed()
        {
            if (_step == Step.Result && !_closingSheet)
            {
                CloseSheet(Time.time);
            }
        }

        // The examiner gets out and walks back towards the desk; the car goes back once the player has left it.
        private void CloseSheet(float now)
        {
            _closingSheet = true;
            if (_screens.Window.OpenFor(this))
            {
                _screens.Window.Close();
            }
            _closingSheet = false;
            Licences.Toast(_passed ? NotificationType.Success : NotificationType.Info,
                _lesson ? "reallife_lic_test_bye_lesson" : _passed ? "reallife_lic_test_bye_pass" : "reallife_lic_test_bye_fail", 8f);
            JobCast cast = _cast();
            bool walking = false;
            try
            {
                if (_examiner != null && _aboard && cast != null && _car != null && JobSpots.BesideCar(_car, _desk, out Vector3 spot)
                    && cast.Unseat(_examiner, spot, true))
                {
                    JobCast.DoorSound(spot);
                    _aboard = false;
                    Vector3 away = StreetSpots.PavementAround(spot, 30f, 55f, true, out Vector3 found) ? found : _desk;
                    walking = _examiner.MoveTo(away, Pace.Walk);
                }
                else if (_examiner != null && !_aboard)
                {
                    walking = _examiner.MoveTo(_desk, Pace.Walk);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            if (!walking && _examiner != null)
            {
                cast?.Release(_examiner);   // no room to get out or no way on: gone unseen with the car
                _examiner = null;
            }
            _lender.Return();
            _step = Step.Leaving;
            _stepEnd = now + 40f;
        }
    }
}
