using System;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A call's form on the call card (a bank's loan, an agency's campaign, a delivery asked for by phone), read from the
    /// game's own widgets as the dialogue panel's are (DialogForm) and pressed through them, so the call's dialog reads its
    /// values back as it always has; the mood the game shows by a call's line as a small glyph before it. A form the kit
    /// cannot draw (a casino's game, nothing it knows) is answered in the game's own view as before. Cost: a walk of the
    /// form's pieces with the screen's look four times a second while it shows; a read only when that changed.
    /// </summary>
    public sealed partial class ContactsScreen : IUiDragging
    {
        private const float CallFormShare = 0.45f;   // of the room over the status bar, the most a call's form takes before it scrolls

        private readonly DialogForm _callForm = new DialogForm();
        private int _callDue;            // the frame after a press on the form, when the game's answer to it is read
        private int _callDragging = -1;

        // The call card shows a form the kit draws.
        private bool CallFormShown => _m.Call != null && _callForm.Count > 0;

        private static bool FormCommand(int command) =>
            command == UiFormRows.FieldCommand || command == UiFormRows.LessCommand || command == UiFormRows.MoreCommand || command == UiFormRows.OptionCommand
            || command == UiFormRows.RowCommand || command == UiFormRows.SwatchCommand || command == UiFormRows.RemoveCommand;

        // The game answers a press in its own frame (a list's lines made again): read in the next.
        private void CallFormDue()
        {
            if (_callDue > 0 && Time.frameCount >= _callDue)
            {
                _callDue = 0;
                _dirty = true;
            }
        }

        private long CallFormSignature() => _callForm.Form != null ? DialogForm.Signature(_callForm.Form) : 0L;

        /// <summary>The line's form into the call card; false where the kit cannot draw it, so the game's own view answers it.</summary>
        private bool CallForm(UiCallModel call)
        {
            DialogEntry e = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null;
            if (e == null || e.Template != DialogEntry.TemplateType.Input)
            {
                _callForm.Clear();
                return true;   // a line without a form: nothing to answer elsewhere
            }
            Transform form = GameContract.TalkForm.Field != null ? GameContract.TalkForm.Field.GetValue(_app) as Transform : null;
            bool casino = _app.dialog != null && DialogRules.Casino(_app.dialog.GetType().Name);
            if (form == null || !form.gameObject.activeInHierarchy || casino || DialogRules.CasinoForm(form.name) || !_callForm.Read(form, null))
            {
                _callForm.Clear();
                return false;
            }
            call.Rows = _callForm.Rows;
            call.RowCount = _callForm.Count;
            call.Pick = _callForm.Pick;
            return true;
        }

        /// <summary>The mood the game shows by the line now (a negotiation's), as its glyph; none where it shows none.</summary>
        private UiIcon LineMood()
        {
            DialogEntry e = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null;
            if (e == null || e.SecondaryIcon == null)
            {
                return UiIcon.None;
            }
            try
            {
                return DialogRules.MoodIcon(MoodOf(e.SecondaryIcon));
            }
            catch (Exception ex) when (Guard.Missing(ex))
            {
                return UiIcon.None;
            }
        }

        // A press on the call's form, through its widget; true when the press was the form's (done: it reached the game).
        private bool CallFormPress(int command, int arg, out bool done)
        {
            done = false;
            if (!CallFormShown || !FormCommand(command))
            {
                return false;
            }
            bool kit = command == UiFormRows.FieldCommand && arg >= 0 && arg < _callForm.Count
                       && (_callForm.Rows[arg].Kind == UiFormKind.Choice || _callForm.Rows[arg].Kind == UiFormKind.Slider);
            done = _callForm.Press(command, arg);
            if (!kit)
            {
                _callDue = Time.frameCount + 1;   // an open list or a held slider is the kit's own; the rest the game answers
            }
            return true;
        }

        private bool CallFormTyped(int row) => CallFormShown && _callForm.Typed(row);

        private void CallFormType(int row, string text)
        {
            if (_callForm.Type(row, text ?? string.Empty))
            {
                _callDue = Time.frameCount + 1;
            }
        }

        // A step on a row: an amount's minus or plus, the choice before or after; a slider only while the keys hold it.
        private void CallFormStep(int row, int step)
        {
            if (row < 0 || row >= _callForm.Count || (_callForm.Rows[row].Kind == UiFormKind.Slider && _callForm.Pick.Held != row))
            {
                return;
            }
            if (_callForm.Step(row, step))
            {
                _callDue = Time.frameCount + 1;
            }
        }

        private bool CallFormBack() => CallFormShown && _callForm.Back();

        private bool CallFormWheel(int clip, float notches)
        {
            if (!CallFormShown || clip < 0)
            {
                return false;
            }
            UiFormPick pick = _callForm.Pick;
            float by = -notches * UiTokens.S16;
            if (clip == pick.ListClip)
            {
                pick.ListOffset = Math.Max(0f, pick.ListOffset + by);
                return true;
            }
            if (clip == pick.Clip)
            {
                pick.Offset = Math.Max(0f, pick.Offset + by);
                return true;
            }
            return false;
        }

        private bool CallFormReveal(int list, int index)
        {
            if (!CallFormShown || list < 0 || list != _callForm.Pick.List)
            {
                return false;
            }
            _callForm.Pick.Reveal = index;
            return true;
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (CallFormShown && command == UiFormRows.FieldCommand)
            {
                CallSlide(arg, x);
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            _callDragging = CallFormShown && command == UiFormRows.FieldCommand && arg >= 0 && arg < _callForm.Count && _callForm.Rows[arg].Kind == UiFormKind.Slider ? arg : -1;
            if (_callDragging >= 0)
            {
                CallSlide(_callDragging, x);
            }
            return _callDragging >= 0;
        }

        public void DragMove(float x, float y)
        {
            if (_callDragging >= 0)
            {
                CallSlide(_callDragging, x);
            }
        }

        public void DragEnd(bool cancelled)
        {
            _callDragging = -1;
        }

        // A slider at the pointer's share of its track, as the kit draws it.
        private void CallSlide(int row, float x)
        {
            UiFormPick pick = _callForm.Pick;
            if (row < 0 || row >= _callForm.Count || _callForm.Rows[row].Kind != UiFormKind.Slider || pick.ControlW <= 0f)
            {
                return;
            }
            float face = Mathf.Clamp01((x - pick.ControlX) / pick.ControlW);
            if (_callForm.Slide(row, UiDraw.SliderShare(pick.ControlW, _callForm.Rows[row].Value ?? string.Empty, face)))
            {
                _callDue = Time.frameCount + 1;
                _dirty = true;
            }
        }
    }
}
