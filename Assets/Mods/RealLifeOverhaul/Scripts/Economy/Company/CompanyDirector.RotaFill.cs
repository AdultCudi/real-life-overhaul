using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rota planner's buttons: a rota filled or a plan used, and the week as it was put back.</summary>
    public sealed partial class CompanyDirector
    {
        internal bool RotaHasUndo(string place) => RotaUndoAt(place) >= 0;

        private int RotaUndoAt(string place)
        {
            for (int i = 0; i < _rotaUndo.Count; i++)
            {
                if (_rotaUndo[i].Place == place)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Why a business's rota cannot be filled, as a text key that may take {stations} and {staff}; null if it can.</summary>
        internal static string RotaRefusal(RotaSheet sheet)
        {
            return sheet == null ? "reallife_rota_off"
                : sheet.Place.Type == RotaType.Warehouse ? "reallife_rota_warehouse"
                : sheet.TooLarge ? "reallife_rota_too_large"
                : sheet.Stations.Count == 0 ? "reallife_rota_no_stations"
                : sheet.Workers.Count == 0 ? "reallife_rota_no_staff"
                : null;
        }

        /// <summary>The ladder's plan that may be written (never one that takes a hire, see RotaLadder.Writable); null otherwise.</summary>
        internal static RotaRung RotaRungOf(RotaSheet sheet, int rung)
        {
            RotaLadderResult ladder = sheet != null ? sheet.Ladder : null;
            RotaRung r = ladder != null && rung > 0 && rung < ladder.Count ? ladder.Rungs[rung] : null;
            return r != null && RotaLadder.Writable(r) && r.Workers == sheet.Workers.Count ? r : null;
        }

        /// <summary>"Fill the rota" (rung -1) and "Use this plan": the game's confirm dialogue first, as before its auto fill.</summary>
        public void FillRota(string place, int rung)
        {
            bool useHours = rung >= 0;
            if (!RotaOn)
            {
                return;
            }
            try
            {
                RotaPlace at = RotaPlaceAt(place);
                RotaSheet sheet = at != null && RotaFresh(at) ? _rotaSheet : null;
                if (sheet == null && at != null && !RotaUnread(place))
                {
                    RotaAsk(place, JobShow, false);   // it changed since the page was written: the page and its buttons are written again
                    Changed();
                    return;
                }
                string refusal = sheet == null ? "reallife_rota_cannot"
                    : RotaRefusal(sheet) ?? (useHours && RotaRungOf(sheet, rung) == null ? "reallife_rota_no_hours" : null);
                if (refusal != null)
                {
                    Notify(NotificationType.Warning, refusal, new Dictionary<string, string>
                    {
                        { "stations", RotaCatalogue.StationsMost.ToString() }, { "staff", RotaCatalogue.WorkersMost.ToString() }
                    }, 8f);
                    return;
                }
                int kind = useHours ? JobFillHours : JobFill;
                if (!useHours && !RotaGame.HasRota(sheet) && sheet.Ladder.FillLeft.Count == 0)
                {
                    RotaAsk(place, kind, false, -1);
                    return;
                }
                long stamp = sheet.Stamp;   // what is put to the player: only that is written
                RotaRung plan = useHours ? RotaRungOf(sheet, rung) : null;
                if (plan == null && sheet.Ladder.FillLeft.Count > 0)
                {
                    // The hours kept cannot hold somebody's least hours: said with the figures, and the plan that can is offered.
                    RotaLeft left = sheet.Ladder.FillLeft[0];
                    RotaRung can = RotaRungOf(sheet, 1);
                    can = can != null && can.Left.Count == 0 ? can : null;
                    string who = left.Worker < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[left.Worker]) : "?";
                    GameConfirm.Show("reallife_rota_tab".Localize(), (can != null ? "reallife_rota_confirm_short_plan" : "reallife_rota_confirm_short").Localize(new
                    {
                        name = who, least = left.Least, room = left.Why == RotaClash.Shared ? left.Held : left.Room, count = sheet.Ladder.FillLeft.Count,
                        now = sheet.Ladder.Rungs[0].Sum.Hours, after = can != null ? can.Sum.Hours : sheet.Ladder.Rungs[0].Sum.Hours
                    }), () => RotaConfirmed(place, can != null ? JobFillHours : kind, can != null ? 1 : -1, stamp), null, null, null, false, false);
                    return;
                }
                if (plan == null)
                {
                    GameConfirm.Show(null, "reallife_rota_confirm", () => RotaConfirmed(place, kind, -1, stamp), null, null, null, true, false);
                    return;
                }
                // A plan that changes the opening hours is always asked about, with the hours now and after and what it brings;
                // one that keeps more people within their hours may bring less, and says so.
                long gain = plan.Sum.Net - sheet.Ladder.Rungs[0].Sum.Net;
                GameConfirm.Show("reallife_rota_tab".Localize(), (gain < 0 ? "reallife_rota_confirm_plan_less" : "reallife_rota_confirm_plan").Localize(new
                {
                    now = sheet.Ladder.Rungs[0].Sum.Hours, after = plan.Sum.Hours, gain = CompanyText.Money(Math.Abs(gain) / 100f)
                }), () => RotaConfirmed(place, kind, rung, stamp), null, null, null, false, false);
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        private void RotaConfirmed(string place, int kind, int rung, long stamp)
        {
            try
            {
                if (RotaOn)
                {
                    RotaAsk(place, kind, false, rung, stamp);
                    Changed();
                }
            }
            catch (Exception e)
            {
                RotaFault("the fill button", e);
            }
        }

        // In one frame, on a sheet just found true. The week as it was is kept first: the oldest, if filled twice. False
        // with nothing written: never a rota with people in it who are not there.
        private bool RotaWrite(RotaJob job, RotaPlace at, RotaSheet sheet, RotaPlan plan, RotaRung rung)
        {
            bool useHours = rung != null;
            if (plan.Stations != sheet.Stations.Count || (rung != null && (rung.Hires > 0 || rung.Workers != sheet.Workers.Count)))
            {
                return false;
            }
            string place = job.Place, name = at.Name;   // the places are read again below, and their entries with them
            int kept = RotaUndoAt(place);
            if (kept < 0)
            {
                _rotaUndo.Add(RotaGame.Keep(at, job.Test));
            }
            else
            {
                _rotaUndo[kept].Kept |= job.Test;
            }
            double before = _rotaClock.Elapsed.TotalMilliseconds;
            _rotaWriting = place;
            int shifts = RotaGame.Write(sheet, plan, useHours ? rung.Open : null);
            _rotaWriting = null;
            double wrote = _rotaClock.Elapsed.TotalMilliseconds - before;
            _rotaSheetGood = false;
            _rotaOwnOf = null;
            RotaRead();
            at = RotaPlaceAt(place);
            _rotaMade = at != null ? place : null;
            _rotaMadeStamp = at != null ? RotaGame.Stamp(at, RotaDays(at)) : 0;
            _rotaTestFilled += job.Test ? 1 : 0;
            _rotaTestShifts += job.Test ? shifts : 0;
            Logger?.Info("Company: rota written for " + name
                         + (useHours ? ", with the plan's opening hours (" + rung.Sum.Hours + " a week, " + sheet.Ladder.Rungs[0].Sum.Hours + " before)" : string.Empty)
                         + ": " + shifts + " shift(s), " + plan.CoveredHours + " of " + plan.WantedHours + " wanted hours covered, " + plan.UnmetCount
                         + " demand(s) unmet" + (plan.Level > 0 ? " (optional demands gave way)" : string.Empty) + ", " + sheet.FixedCount
                         + " employee(s) left as they were, wages " + plan.Wage / 100 + " a week; worked out in "
                         + _rotaSpent.ToString("0.0", CultureInfo.InvariantCulture) + " ms over " + (_rotaFrames + 1)
                         + " frame(s), the longest share of a frame " + _rotaLongest.ToString("0.0", CultureInfo.InvariantCulture) + " ms; written in "
                         + wrote.ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
            if (!job.Test)
            {
                Notify(plan.Full ? NotificationType.Success : NotificationType.Info, plan.Full ? "reallife_rota_note_filled" : "reallife_rota_note_partly",
                    new Dictionary<string, string>
                    {
                        { "business", name }, { "covered", plan.CoveredHours.ToString() }, { "wanted", plan.WantedHours.ToString() }, { "shifts", shifts.ToString() }
                    }, 9f);
                Changed();
            }
            return true;
        }

        /// <summary>"Put the old rota back": the week as it was before the first fill since the app was opened.</summary>
        public void RotaBack(string place)
        {
            if (!RotaOn)
            {
                return;
            }
            try
            {
                RotaPlace at = RotaPlaceAt(place);
                string name = at != null ? at.Name : "?";
                if (RotaPutBack(place) >= 0)
                {
                    Notify(NotificationType.Success, "reallife_rota_note_back", new Dictionary<string, string> { { "business", name } }, 7f);
                    Changed();
                }
            }
            catch (Exception e)
            {
                RotaFault("putting a rota back", e);
            }
        }

        private int RotaPutBack(string place)
        {
            int at = RotaUndoAt(place);
            if (at < 0)
            {
                return -1;
            }
            RotaPlace where = RotaPlaceAt(place);
            RotaUndo undo = _rotaUndo[at];
            _rotaUndo.RemoveAt(at);
            for (int i = _rotaJobs.Count - 1; i >= 0; i--)
            {
                if (_rotaJobs[i].Place == place && _rotaJobs[i].Kind != JobShow && (i > 0 || _rotaStage == StageRead))
                {
                    _rotaJobs.RemoveAt(i);   // a fill of it still waiting is not wanted any more
                }
            }
            if (where == null)
            {
                return -1;   // no longer the player's: nothing to put a rota back into
            }
            string name = where.Name;   // the places are read again below, and their entries with them
            int shifts = RotaGame.PutBack(where, undo);
            _rotaSheetGood = false;
            _rotaOwnOf = null;
            _rotaMade = null;
            RotaRead();
            Logger?.Info("Company: the rota and opening hours of " + name + " are as they were before the planner wrote them: " + shifts + " shift(s) back.");
            return shifts;
        }

        /// <summary>The Company app has closed: the weeks kept to put back are let go, but those a test tool kept.</summary>
        internal void RotaLeft()
        {
            for (int i = _rotaUndo.Count - 1; i >= 0; i--)
            {
                if (!_rotaUndo[i].Kept)
                {
                    _rotaUndo.RemoveAt(i);
                }
            }
        }
    }
}
