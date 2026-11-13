using System;
using System.Collections.Generic;
using Entities;
using Extensions;
using Localizor;
using RealLifeOverhaul.Economy;
using UI.Smartphone.Apps.MyEmployees;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    // The review sheet of a batch on My Employees: a raise for several picked employees, or hiring several picked candidates
    // to a business. The batch itself is chosen in the game's own mass action and its panel of choices; when the choice is
    // confirmed the sheet is raised here (Core/StaffBatch: the raise's from Economy/Company/PickedRaiseGame, the hire's from
    // StaffBatchHire), and Give or Cancel on it runs what the batch says. Nothing is written before Give. The sheet lives
    // only while the game's panel of choices it came from is open, so closing the app, changing tab or ending the picking
    // takes it away. Cost: nothing while no sheet stands; a sheet is read once a layout.
    public sealed partial class EmployeesScreen
    {
        private const string AssignAndHire = "_assignbusinessandhire";   // the end of the game's name for its mass action that hires to a business

        private readonly StaffBatchView _batch = new StaffBatchView();
        private readonly Dictionary<string, EmployeeInstance> _byId = new Dictionary<string, EmployeeInstance>(16);
        private string _massType;           // the game's name for the mass action whose panel of choices is open
        private float _batchOffset;
        private int _batchReveal = -1;

        // The pending sheet as the view reads it; null when none stands, or when the game's panel it came from has shut.
        private void Batch(StaffView v)
        {
            StaffSheet sheet = StaffBatch.Sheet;
            if (sheet != null && !MassPanelOpen())
            {
                StaffBatch.Close();
                sheet = null;
            }
            if (sheet == null)
            {
                v.Batch = null;
                _batchOffset = 0f;
                _batchReveal = -1;
                return;
            }
            _picking = true;
            v.Picking = true;
            _open = -1;
            _byId.Clear();
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            for (int i = 0; picked != null && i < picked.Count; i++)
            {
                if (picked[i] != null && !string.IsNullOrEmpty(picked[i].id))
                {
                    _byId[picked[i].id] = picked[i];
                }
            }
            StaffBatchView b = _batch;
            b.Hire = sheet.Hire;
            b.Subject = sheet.Subject;
            b.Picked = sheet.Picked;
            b.Ready = sheet.Ready;
            b.Left = sheet.Left;
            if (b.Rows.Length != sheet.Lines.Length)
            {
                b.Rows = new StaffBatchRow[sheet.Lines.Length];
            }
            for (int i = 0; i < sheet.Lines.Length; i++)
            {
                StaffSheetLine l = sheet.Lines[i];
                string face = null;
                if (l.Id != null && _byId.TryGetValue(l.Id, out EmployeeInstance who))
                {
                    face = FaceSources.Key(l.Name, who);
                    FaceSources.Employee(l.Name, face, who);   // their face from the game: drawn from their own look, never painted
                }
                bool ready = l.State == BatchState.Ready;
                string why = StaffBatchRules.ReasonKey(l.State);
                b.Rows[i] = new StaffBatchRow
                {
                    Name = l.Name ?? string.Empty, Face = face, Role = string.IsNullOrEmpty(l.RoleKey) ? null : l.RoleKey.GetLocalization(), Business = l.Business,
                    Now = Money(l.Now), After = Money(l.Next), Day = ready && !sheet.Hire ? Money(l.DayNext - l.DayNow) : string.Empty,
                    Result = ready ? UiText.T(sheet.Hire ? "reallife_staff_batch_result_hire" : "reallife_staff_batch_result_raise") : UiText.T(why),
                    Tone = ready ? UiRole.Ok : l.State == BatchState.Failed ? UiRole.Danger : UiRole.Warn
                };
            }
            b.TotalNow = Money(sheet.Now);
            b.TotalAfter = Money(sheet.Next);
            b.TotalDay = Money(sheet.DayNext - sheet.DayNow);
            b.Offset = _batchOffset;
            b.Reveal = _batchReveal;
            v.Batch = b;
        }

        // Hundredths as the game writes money.
        private static string Money(long hundredths) => (hundredths / 100f).ToCurrencyFormat();

        private bool MassPanelOpen()
        {
            var panel = GameContract.StaffMassPanel.Field?.GetValue(_app.massActionsUI) as Transform;
            return panel != null && panel.gameObject.activeSelf;
        }

        // A press while the sheet stands: Cancel (0) or the yes (1); its rows are read only and take a press quietly.
        private bool BatchPress(int command, int arg)
        {
            if (command == UiBatch.ButtonCommand)
            {
                if (arg == 0)
                {
                    return Done(StaffBatch.Cancel(), "the review sheet put away: nothing was changed");
                }
                bool hire = StaffBatch.Sheet != null && StaffBatch.Sheet.Hire;
                _massType = null;
                return Done(StaffBatch.Give(), (hire ? "the hiring confirmed on the review sheet" : "the raise confirmed on the review sheet") + ": handed to the game");
            }
            return command >= UiBatch.TableFirst && command < UiBatch.TableFirst + UiDataTable.Act + 1;
        }

        // The game's panel of choices confirmed for hiring to a business: the kit reviews it first (who can be hired there, who not
        // and why); true when the sheet stands. Anything else the game's own confirm handles.
        private bool ReviewHire()
        {
            if (_massType == null || !_massType.EndsWith(AssignAndHire, StringComparison.Ordinal))
            {
                return false;
            }
            var choices = GameContract.StaffMassOptions.Field?.GetValue(_app.massActionsUI) as UI.Elements.Dropdown;
            int at = choices != null ? choices.SelectedOptionIndex : -1;
            return at >= 0 && StaffBatchHire.Review(at);
        }
    }
}
