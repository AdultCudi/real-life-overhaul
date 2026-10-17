using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The rota planner's jobs: the game read in one frame, then the advice and the ladder a few steps a frame, by a
    /// coroutine that lives only while a job waits. The steps do not depend on the cut, so the result is the checks'.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        // A business worked out for its page, a rota filled, or the notice that ends a run of test fills.
        private struct RotaJob
        {
            public string Place;
            public int Kind;
            public bool Test;
            public int Rung;        // the ladder's plan to write; -1: a rota for the hours kept now
            public long Stamp;      // the sheet the player confirmed (RotaSheet.Stamp); 0: nothing was put to them
        }

        private void RotaAsk(string place, int kind, bool test, int rung = -1, long stamp = 0)
        {
            for (int i = 0; i < _rotaJobs.Count; i++)
            {
                if (_rotaJobs[i].Place == place && _rotaJobs[i].Kind == kind)
                {
                    return;
                }
            }
            _rotaJobs.Add(new RotaJob { Place = place, Kind = kind, Test = test, Rung = rung, Stamp = stamp });
            if (!_rotaWorking)
            {
                _rotaWorking = true;
                RotaBegin();
                StartCoroutine(Guard.Routine(RotaWork()));
            }
        }

        private void RotaBegin()
        {
            _rotaStage = StageRead;
            _rotaRestarts = _rotaFrames = 0;
            _rotaSpent = _rotaLongest = 0;
        }

        private IEnumerator RotaWork()
        {
            try
            {
                while (RotaSlice())
                {
                    yield return null;
                }
            }
            finally
            {
                _rotaWorking = false;   // however it ends, a fault the guard caught included: the next job starts a coroutine again
            }
        }

        // One frame's share of the first job. False when there is nothing left to do.
        private bool RotaSlice()
        {
            if (!RotaOn || Save == null || _rotaJobs.Count == 0)
            {
                _rotaJobs.Clear();
                return false;
            }
            RotaJob job = _rotaJobs[0];
            bool done;
            _rotaClock.Restart();
            try
            {
                do
                {
                    done = RotaStep(job);
                }
                while (!done && _rotaClock.Elapsed.TotalMilliseconds < RotaSliceMs);
            }
            catch (Exception e)
            {
                done = true;
                RotaPlace at = RotaPlaceAt(job.Place);
                if (_rotaStage == StageWrite || at == null)
                {
                    RotaFault("a rota for " + (at != null ? at.Name : job.Place), e);
                    if (_rotaWriting == job.Place)
                    {
                        RotaRescue(job.Place);
                    }
                }
                else
                {
                    RotaUnreadable(at, e);
                }
            }
            double took = _rotaClock.Elapsed.TotalMilliseconds;
            _rotaFrames++;
            _rotaSpent += took;
            _rotaLongest = Math.Max(_rotaLongest, took);
            if (done)
            {
                if (_rotaJobs.Count > 0)
                {
                    _rotaJobs.RemoveAt(0);
                }
                RotaBegin();
                if (_view != null && job.Kind != JobTell)
                {
                    try
                    {
                        _view.Changed();
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);   // the page is written again on its next change; the planner goes on
                    }
                }
            }
            return _rotaJobs.Count > 0;
        }

        // The next piece of a job; true when it is finished. Reading the game and writing to it are one piece each.
        private bool RotaStep(RotaJob job)
        {
            if (job.Kind == JobTell)
            {
                Logger?.Warn("Company test: " + _rotaTestFilled + " rota(s) filled, " + _rotaTestShifts
                             + " shift(s) written; the weeks as they were are kept for 'Test: put every rota back'.");
                Notifications.Show(NotificationType.Info, "reallife_rota_test_filled",
                    new Dictionary<string, string> { { "count", _rotaTestFilled.ToString() }, { "shifts", _rotaTestShifts.ToString() } },
                    9f, null, Guard.Wrap(() => Open(TabRota, null)), true, false);
                Changed();
                return true;
            }
            RotaPlace place = RotaPlaceAt(job.Place);
            RotaSheet sheet = _rotaSheet;
            if (place == null || _rotaUnread.Contains(job.Place))
            {
                return true;   // no longer the player's, or unreadable
            }
            switch (_rotaStage)
            {
                case StageRead:
                    if (RotaFresh(place))
                    {
                        sheet.Place = place;
                        _rotaStage = StageWrite;
                        return RotaToFill(job, sheet);
                    }
                    _rotaSheetGood = false;
                    _rotaRaise = _rotaRaise ?? RaiseOf;
                    int days = RotaDays(place);
                    RotaGame.Read(sheet, place, _rotaPromotion, _rotaRaise);
                    RotaGame.ReadDeep(sheet, RotaMarket(place));
                    sheet.Stamp = RotaGame.Stamp(place, days);
                    sheet.Permit = (days & 2) != 0;
                    if (!sheet.Permit)
                    {
                        RotaAdvisor.LateHours(_rotaBarred);
                    }
                    _rotaAdvisor.Begin(sheet.Shop, sheet.Problem, _rotaSolver, sheet.Now.At, sheet.Permit ? null : _rotaBarred, sheet.Outlook);
                    _rotaStage = StageAdvise;
                    return false;
                case StageAdvise:
                    _rotaStage = _rotaAdvisor.Step(RotaStepsAtOnce) ? StageRate : StageAdvise;
                    return false;
                case StageRate:
                    _rotaAdvisor.Grid(sheet.Shop, sheet.Shop.Open, sheet.Problem);
                    _rotaSolver.Rate(sheet.Problem, sheet.Now);
                    _rotaLadder.Begin(sheet.Shop, sheet.Problem, _rotaSolver, _rotaAdvisor, sheet.Outlook, sheet.Now, sheet.Permit ? null : _rotaBarred,
                        RotaModeOf(place.Place), sheet.Ladder);
                    _rotaStage = StageLadder;
                    return false;
                case StageLadder:
                    if (!_rotaLadder.Step(RotaStepsAtOnce))
                    {
                        return false;
                    }
                    if (RotaGame.Stamp(place, RotaDays(place)) != sheet.Stamp)
                    {
                        return RotaAgain(job, place);   // the business changed while it was worked out
                    }
                    sheet.Late = !sheet.Permit && RotaLate(sheet.Shop.Open);
                    sheet.Shown = _rotaMade == place.Place && _rotaMadeStamp == sheet.Stamp ? sheet.Now : null;
                    _rotaSheetGood = true;
                    RotaCheck(place, sheet);
                    RotaPlansLog(place, sheet, false);
                    _rotaStage = StageWrite;
                    return RotaToFill(job, sheet);
                default:
                    if (!RotaFresh(place))
                    {
                        return RotaAgain(job, place);   // never a rota from a reading that is no longer true
                    }
                    if (job.Stamp != 0 && sheet.Stamp != job.Stamp)
                    {
                        return RotaUnwritten(job, place, "it changed after the plan was confirmed, so what was confirmed is no longer the plan");
                    }
                    RotaRung rung = job.Kind == JobFillHours ? RotaRungOf(sheet, job.Rung) : null;
                    if (job.Kind == JobFillHours && rung == null)
                    {
                        return RotaUnwritten(job, place, "the plan is no longer one that can be written");   // one that takes a hire, or no longer there
                    }
                    return RotaWrite(job, place, sheet, rung != null ? rung.Plan : sheet.Ladder.Fill, rung)
                           || RotaUnwritten(job, place, "the plan no longer fits the stations and people there");
            }
        }

        private bool RotaToFill(RotaJob job, RotaSheet sheet)
        {
            if (job.Kind == JobShow && job.Test)
            {
                RotaPlansLog(sheet.Place, sheet, true);
            }
            if (job.Kind == JobShow)
            {
                return true;
            }
            if (RotaRefusal(sheet) != null || (job.Kind == JobFillHours && RotaRungOf(sheet, job.Rung) == null))
            {
                string refusal = RotaRefusal(sheet);
                return RotaUnwritten(job, sheet.Place, refusal != null ? "it cannot be filled (" + refusal + ")" : "the plan is no longer one that can be written");
            }
            _rotaStage = StageWrite;
            return false;
        }

        // A fill that ends with nothing written: said in the log, and to the player unless a test asked for it.
        private bool RotaUnwritten(RotaJob job, RotaPlace place, string why)
        {
            string name = place != null ? place.Name : job.Place;
            Logger?.Info("Company: the rota of " + name + " was not written: " + why + ".");
            if (!job.Test)
            {
                Notify(NotificationType.Warning, "reallife_rota_changed", new Dictionary<string, string> { { "business", name } }, 8f);
            }
            return true;
        }

        // After a fault in the middle of a write: the week kept before it is put back, so nothing half written stays.
        private void RotaRescue(string place)
        {
            _rotaWriting = null;
            try
            {
                RotaPlace at = RotaPlaceAt(place);
                string name = at != null ? at.Name : place;
                if (RotaPutBack(place) >= 0)
                {
                    Notify(NotificationType.Success, "reallife_rota_note_back", new Dictionary<string, string> { { "business", name } }, 7f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Company: the week of " + place + " as it was before the planner wrote it could not be put back after the error above.");
            }
        }

        private bool RotaAgain(RotaJob job, RotaPlace place)
        {
            _rotaSheetGood = false;
            _rotaStage = StageRead;
            if (++_rotaRestarts <= RotaRestartsMost)
            {
                return false;
            }
            Logger?.Warn("Company: " + place.Name + " kept changing while its rota was worked out (" + _rotaRestarts + " readings); nothing was written.");
            if (job.Kind != JobShow && !job.Test)
            {
                Notify(NotificationType.Warning, "reallife_rota_changed", new Dictionary<string, string> { { "business", place.Name } }, 8f);
            }
            return true;
        }

        private void RotaCheck(RotaPlace place, RotaSheet sheet)
        {
            if (!Verbose)
            {
                return;
            }
            RotaOutlook o = sheet.Outlook;
            double now = _rotaClock.Elapsed.TotalMilliseconds, longest = Math.Max(_rotaLongest, now);
            Logger?.Info("Company check (rota, " + place.Name + "): " + sheet.Stations.Count + " station(s) in " + sheet.Shop.Groups + " group(s), "
                         + sheet.Workers.Count + " employee(s), " + sheet.FixedCount + " left as they are, " + o.CustomersWeek
                         + " customers a week by the game's calculator, " + sheet.Shop.Margin + " a customer in hundredths ("
                         + (sheet.FromBooks ? "the books" : "the prices") + "); hours now " + o.HoursNow + ", with the staff " + o.HoursStaffed
                         + ", with all it takes " + o.HoursBest + (o.Barred ? ", with a late opening permit " + o.HoursFree : string.Empty)
                         + "; a week in hundredths " + o.ProfitNow + ", " + o.ProfitStaffed + ", " + o.ProfitBest + "; the rota it has covers "
                         + sheet.Now.CoveredHours + " of " + sheet.Now.WantedHours + " wanted hours with " + sheet.Now.UnmetCount
                         + " demand(s) unmet; worked out in " + (_rotaSpent + now).ToString("0.0", CultureInfo.InvariantCulture) + " ms over "
                         + (_rotaFrames + 1) + " frame(s), the longest share of a frame " + longest.ToString("0.0", CultureInfo.InvariantCulture)
                         + " ms (wanted: about " + RotaSliceMs.ToString("0") + " ms, the frame that reads the game apart): "
                         + (longest <= RotaSliceMs * 4 ? "PASS" : "SLOW"));
        }

        // One rung in a line of the log: its hours, what its week comes to and what that is made of, in whole money.
        private static string RotaRungLog(RotaSheet sheet, RotaRung r)
        {
            RotaWeekSum s = r.Sum;
            string what = r.Kind == RotaRungKind.AsItIs ? "as it is"
                : r.Kind == RotaRungKind.Best ? "best with the people (" + (r.Source == 0 ? "the week as it is" : r.Source == 1 ? "the hours kept, a new rota"
                    : r.Source == 2 ? "the hours kept less those nobody can keep" : r.Source == 4 ? "the hours kept and more, so everybody has their hours"
                    : "the hours worth opening") + ")"
                : "hire " + r.Hires;
            for (int i = 0; i < r.Hires; i++)
            {
                what += (i == 0 ? " (" : ", ") + RotaGame.GroupSkill(sheet, r.Hire[i]) + " at "
                        + (RotaLadder.Going(sheet.Shop, r.Hire[i]) / 100f).ToString("0.00", CultureInfo.InvariantCulture) + (i == r.Hires - 1 ? ")" : string.Empty);
            }
            return what + ": " + s.Hours + " h open, " + s.Manned + " manned, " + s.StaffHours + " staff h (" + s.CleanHours + " cleaning), " + s.Served
                   + " of " + s.Come + " customers served (" + s.LostUnmanned + " found nobody, " + s.LostFull + " over what the stations serve), sales "
                   + s.Sales / 100 + " (dirt takes " + s.Clean / 100 + "), wages " + s.Wages / 100 + ", fees " + s.Fees / 100 + ", theft " + s.Theft / 100
                   + ", night risk " + s.Night / 100 + ", demands " + s.Demands / 100 + ", net " + s.Net / 100
                   + (r.Kind != RotaRungKind.AsItIs ? ", gain " + r.Gain / 100 : string.Empty) + ", " + r.Plan.UnmetCount + " demand(s) unmet";
        }

        // A business's ladder in the log: the self-check line of each working out (verbose), and every rung for the test.
        private void RotaPlansLog(RotaPlace place, RotaSheet sheet, bool always)
        {
            if (!always && !Verbose)
            {
                return;
            }
            RotaLadderResult l = sheet.Ladder;
            RotaShop shop = sheet.Shop;
            bool pass = l.Count >= 1;
            int left = l.FillLeft.Count;
            for (int i = 1; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                left += r.Left.Count;
                for (int w = 0; w < sheet.Workers.Count && w < r.Workers; w++)
                {
                    pass &= RotaFit.Inside(sheet.Problem.Worker[w], r.HoursOf[w]);
                }
            }
            string hours = string.Empty, rungs = string.Empty;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                hours += (h > 0 ? " " : string.Empty) + l.Hourly[h];
            }
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                pass &= r.Kind != RotaRungKind.Hire || r.Gain >= RotaLadder.GainLeast;
                rungs += (i > 0 ? " > " : string.Empty) + (r.Kind == RotaRungKind.AsItIs ? "now" : r.Kind == RotaRungKind.Best ? "best" : "hire " + r.Hires)
                         + " " + r.Sum.Hours + " h " + r.Sum.Net / 100;
            }
            string stop = l.Stop == RotaEnd.TooLittle ? "one more for " + RotaGame.GroupSkill(sheet, l.StopGroup) + " would add " + l.StopGain / 100
                                                        + ", under the " + l.StopNeed / 100 + " a rung must"
                : l.Stop == RotaEnd.Most ? "as many people as one ladder advises"
                : l.Stop == RotaEnd.NoAdvice ? "no hours are advised here"
                : l.Stop == RotaEnd.Stock ? "one more for " + RotaGame.GroupSkill(sheet, l.StopGroup) + " would add " + l.StopGain / 100 + ", under the "
                                            + l.StopNeed / 100 + " a rung must, because the goods run out first"
                : "everybody the paying hours take is there";
            string line = "Company check (rota plans, " + place.Name + "): " + rungs + "; recommended rung " + l.Recommended + "; ends because " + stop + "; "
                          + (l.Mode == RotaMode.StaffFirst ? "staff first" : "lean")
                          + (l.ModeAuto ? " (chosen by the week: lean " + l.LeanNet / 100 + ", staff first " + l.StaffNet / 100 + ")" : " (the player's choice)")
                          + (shop.Cleaned ? "; the cleaning service cleans it" : string.Empty)
                          + "; everybody in every plan inside the hours a week they ask for or without hours (" + left + " left without, over the plans and the fill)"
                          + "; customers a week by the hour of the day (the game's calculator): " + hours + "; a customer " + shop.Margin
                          + " hundredths, satisfaction " + shop.Content + ", cleanliness " + shop.CleanNow + ", dirt a customer " + shop.DirtPerCustomer
                          + " and a cleaner's hour " + shop.CleanPerHour + " (thousandths of a point), theft a customer " + shop.TheftRate
                          + " hundredths up to " + shop.TheftLimit + " an hour, a night shut " + shop.NightRisk + " hundredths; " + l.Steps + " step(s): "
                          + (pass ? "PASS" : "FAIL");
            if (always)
            {
                Logger?.Warn(line);
            }
            else
            {
                Logger?.Info(line);
            }
            for (int i = 0; always && i < l.Count; i++)
            {
                Logger?.Warn("Company test (rota plans, " + place.Name + "), rung " + i + ", " + RotaRungLog(sheet, l.Rungs[i]) + ".");
            }
        }
    }
}
