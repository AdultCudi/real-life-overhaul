using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The building inspector's round for the city: the site's checklist (the same for everyone who looks at it that day),
    /// each item's reading against its limit on the game's panel, Sound or Fault. A fault called right is written up; a site
    /// failed that is unsafe is reported to the city as the notice's own report is. With faults found the contractor may offer
    /// a bribe to sign it off: taken, the money now and a chance it comes out in the next days; refused, nothing.
    /// </summary>
    public sealed partial class TradeJob
    {
        private readonly int[] _kinds = new int[TradeRules.Items];
        private readonly bool[] _faults = new bool[TradeRules.Items];
        private int _item;             // the item being judged
        private int _found;            // real faults called faults
        private int _wrong;            // items called wrongly
        private int _bribe;            // the amount offered, 0 for none; then -1 once answered
        private Action _onSound, _onFault, _onTake, _onRefuse;
        private bool _judging;

        private void BeginJudge(float now)
        {
            int day = TimeHelper.CurrentDay;
            TradeRules.Checklist(_onSite ? _site.Id : 0, day, _onSite && _site.Unsafe, _kinds, _faults);
            _units = TradeRules.Items;   // a checklist is its items, whatever the shift's size
            _onSound = () => Judge(false, Time.time);
            _onFault = () => Judge(true, Time.time);
            _onTake = () => Bribe(true);
            _onRefuse = () => Bribe(false);
            _onAway = StepAway;
            _item = 0;
            _judging = true;
            Say(JobText.T("reallife_trades_inspector_obj"));
        }

        // An inspection is over only once the bribe, if one was offered, is answered.
        private bool JudgeSettled() => _bribe <= 0;

        private void TickJudge(float now)
        {
            if (!Ctx.Stand.Ready)
            {
                _panel.Hide();
                Say(JobText.T("reallife_trades_take_place"));
                Mark(_post, JobText.T(Def.NameKey));
                return;
            }
            Unmark();
            int state = _item | (_bribe > 0 ? 1 << 6 : 0) | (_judging ? 1 << 7 : 0);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                if (_bribe > 0)
                {
                    p.Lines(JobText.T("reallife_trades_inspector_bribe", "amount", JobText.Money(_bribe)),
                        JobText.T("reallife_trades_inspector_found", "found", _found.ToString(CultureInfo.InvariantCulture)),
                        null, _panel.White);
                    p.Button(0, "reallife_trades_inspector_take", _onTake, true, false);
                    p.Button(1, "reallife_trades_inspector_refuse", _onRefuse, true, true);
                    p.Button(2, null, null, false, false);
                    p.Button(3, null, null, false, false);
                    Lines(JobText.T("reallife_trades_inspector_bribe_line"), null);
                }
                else if (_item < TradeRules.Items)
                {
                    TradeRules.Reading(_onSite ? _site.Id : 0, TimeHelper.CurrentDay, _item, _kinds[_item], _faults[_item], out int measured, out int limit, out bool atLeast);
                    string reading = JobText.T(TradeCatalogue.Key + "check_" + _kinds[_item], "measured", measured.ToString(CultureInfo.InvariantCulture), "limit",
                        limit.ToString(CultureInfo.InvariantCulture));
                    p.Lines(reading, JobText.T("reallife_trades_inspector_item", "item", (_item + 1).ToString(CultureInfo.InvariantCulture), "count",
                        TradeRules.Items.ToString(CultureInfo.InvariantCulture)), JobText.T(atLeast ? "reallife_trades_inspector_at_least" : "reallife_trades_inspector_at_most"), _panel.White);
                    p.Button(0, "reallife_trades_inspector_sound", _onSound, true, true);
                    p.Button(1, "reallife_trades_inspector_fault", _onFault, true, true);
                    p.Button(2, ButtonAway, _onAway, true, false);
                    p.Button(3, null, null, false, false);
                    Lines(reading, null);
                }
            }
            if (_item < TradeRules.Items || _bribe > 0)
            {
                _panel.Show(Ctx.Player.transform, PanelLift);
            }
            else
            {
                _panel.Hide();
            }
        }

        // Sound or Fault for the item on the panel.
        private void Judge(bool fault, float now)
        {
            if (Over || _item >= TradeRules.Items || now < _quiet || _bribe > 0)
            {
                return;
            }
            TradeRules.Reading(_onSite ? _site.Id : 0, TimeHelper.CurrentDay, _item, _kinds[_item], _faults[_item], out int measured, out int limit, out bool atLeast);
            bool right = fault != TradeRules.Sound(measured, limit, atLeast);
            if (right)
            {
                _base += Mathf.RoundToInt(Def.UnitPay * Ctx.Rate);
                _done++;
                _learnt++;
                _perfect++;
                _found += fault ? 1 : 0;
            }
            else
            {
                _wrong++;
                _done++;   // judged, if wrongly: it is the city's word on it all the same, and pays nothing
            }
            JobText.Toast(right ? NotificationType.Info : NotificationType.Warning, right ? "reallife_trades_inspector_right" : "reallife_trades_inspector_wrong", 3f);
            Ctx.Logger?.Info("Jobs: inspection item " + (_item + 1) + " (kind " + _kinds[_item] + "): " + measured + " against " + (atLeast ? "at least " : "at most ") + limit
                             + ", called " + (fault ? "a fault" : "sound") + ", " + (right ? "right" : "wrong") + ".");
            _item++;
            _quiet = now + Debounce;
            _panelState = int.MinValue;
            if (_item >= TradeRules.Items)
            {
                Inspected();
            }
        }

        // The checklist is done: with faults found the contractor may offer to make it worth the inspector's while first.
        private void Inspected()
        {
            _judging = false;
            int day = TimeHelper.CurrentDay;
            Ctx.Logger?.Info("Jobs: inspection of site " + (_onSite ? _site.Id.ToString(CultureInfo.InvariantCulture) : "(none)") + " done: " + _found + " fault(s) found, "
                             + _wrong + " item(s) called wrongly.");
            if (_found > 0 && (Ctx.Test || TradeRules.BribeOffered(_onSite ? _site.Id : 0, day, _found)))   // a test always sees the offer
            {
                _bribe = TradeRules.Bribe(Def.UnitPay * Ctx.Rate, TradeRules.Items);
                Say(JobText.T("reallife_trades_inspector_obj_bribe"));
                return;
            }
            Verdict();
        }

        // The site passed, or failed with its faults; failed and unsafe, it is reported to the city as a report at its notice is.
        private void Verdict()
        {
            bool reported = _found > 0 && _onSite && !Ctx.Test && _site.Unsafe && TradesBridges.Report(_site.Id);
            JobText.Toast(_found > 0 ? NotificationType.Warning : NotificationType.Success,
                reported ? "reallife_trades_inspector_stopped" : _found > 0 ? "reallife_trades_inspector_failed" : "reallife_trades_inspector_passed",
                "found", _found.ToString(CultureInfo.InvariantCulture), 8f);
            if (reported)
            {
                Ctx.Logger?.Info("Jobs: the inspector reported site " + _site.Id + " unsafe to the city: it stops for a working day.");
            }
        }

        // The contractor's envelope, taken or refused.
        private void Bribe(bool take)
        {
            if (Over || _bribe <= 0)
            {
                return;
            }
            int amount = _bribe;
            _bribe = -1;
            _panelState = int.MinValue;
            if (take)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, new TransactionInfo("reallife_transaction_trades_bribe"), null, null, force: true);
                if (!Ctx.Test)
                {
                    DesignBook.Bribed(TimeHelper.CurrentDay, _onSite ? _site.Id : 0, amount);   // it may come out (TradesDirector, at the day)
                }
                JobText.Toast(NotificationType.Info, "reallife_trades_inspector_took", "amount", JobText.Money(amount), 8f);
                Ctx.Logger?.Info("Jobs: the inspector took the contractor's " + amount + " to sign the site off.");
            }
            else
            {
                Ctx.Logger?.Info("Jobs: the inspector refused the contractor's " + amount + "; the site is failed with " + _found + " fault(s).");
                Verdict();
            }
        }
    }
}
