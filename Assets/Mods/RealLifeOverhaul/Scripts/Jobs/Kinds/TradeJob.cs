using System;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A building trade at a works site (Scripts/BuildTrades): taken at the site's notice, worked where the player stands
    /// facing the site, in the hi-vis, in the trade's working pose. The trade's game is played on the game's own panel over
    /// the work and the bar on the shift card (<see cref="TradeMode"/>): a brick laid in time, a wire to its terminal, a
    /// corner surveyed, a checklist judged, a night watched. Paid by the unit by how it went; the rest can be left to the
    /// game's clock at a fair unit's pay. Cost: the job's 0.2 s tick; while a timing bar runs, one marker write a frame.
    /// </summary>
    public sealed partial class TradeJob : Job, IClockedShift
    {
        private const float SiteReach = 30f;       // metres: a works site this near the player is where the shift is worked
        private const float LeaveReach = 32f;      // metres from the place of work at which the shift counts as walked off
        private const float PostReach = 1.2f;      // metres: the stand's zone
        private const float PanelLift = 2.1f;      // metres over the player the panel stands
        private const float Debounce = 0.6f;       // seconds after a stroke in which the buttons do nothing (a key meant for the last)
        private const float FairQuality = 0.7f;    // the quality a unit left to the clock is paid at

        private readonly JobDef _def;
        private readonly TradeDef _trade;
        private readonly TradeSite _site = new TradeSite();
        private readonly JobPanel _panel;
        private ShiftFigures _figures;
        private ShiftClock _clock;
        private GameBar _bar;
        private bool _barTried;
        private TradeDice _dice;

        private Vector3 _post, _face;
        private int _units;            // the shift's units
        private int _done;             // done by hand
        private int _perfect;          // ...of those at the best quality
        private int _base;             // what they have earned
        private int _clocked;          // done by the game's clock...
        private int _clockPay;         // ...and what it paid
        private int _skill;            // the player's points of the trade's helping skill
        private int _learnt;           // units that taught the trade's skill, settled at the end
        private bool _paid;
        private bool _dressed;
        private bool _onSite;          // a works site was found where the shift was taken (a test may have none)
        private float _quiet;          // Time.time until which the panel's buttons do nothing
        private int _panelState = int.MinValue;
        private string _objective;

        private const string ButtonSkip = "reallife_job_button_skip";
        private const string ButtonAway = "reallife_job_button_away";

        public TradeJob(string id)
        {
            _def = JobCatalogue.Find(id);
            _trade = TradeCatalogue.Find(id);
            _panel = new JobPanel(id + " panel");
        }

        public override JobDef Def => _def;

        public override bool OwnsInteractKey => _panel.Up;

        public override bool PassingTime => _clock != null && _clock.Working;

        public override bool QuitIsFailure => DoneCount > 0 && DoneCount * 3 < _units;

        private int DoneCount => _done + _clocked;

        int IClockedShift.ClockLeft => _trade.Mode == TradeMode.Judge ? 0 : Mathf.Max(0, _units - DoneCount);

        float IClockedShift.ClockPay => TradeRules.UnitPay(Def.UnitPay * Ctx.Rate, FairQuality);

        void IClockedShift.ClockStarted()
        {
            _panel.Hide();
            Unmark();
            ClockStartedMode();
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            _clocked += count;
            _clockPay += pay;
            _learnt += count / 2;   // the clock's units teach half what the hand's do
            _quiet = Time.time + Debounce;
            _panelState = int.MinValue;
            ClockEndedMode();
        }

        protected override bool OnBegin(float now)
        {
            if (_def == null || _trade == null)
            {
                Refusal = "reallife_job_no_now";
                return false;
            }
            if (!Settings.Trades)
            {
                Refusal = "reallife_trades_off";
                return false;
            }
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            if (!Ctx.Test && !TradesBridges.Holds(_trade.Needs))
            {
                Refusal = _trade.Needs.LacksKey;   // the site's notice said so already; a rota shift by phone hears it here
                return false;
            }
            Vector3 player = Ctx.Player.transform.position;
            _onSite = TradesBridges.NearestSite(player, SiteReach, _site);
            if (!_onSite && !Ctx.Test)
            {
                Refusal = "reallife_trades_no_site";
                return false;
            }
            if (_onSite && !Ctx.Test && Ctx.Shift == null && !_trade.OnAt(_site.Kind, _site.Phase))
            {
                Refusal = "reallife_trades_not_here";   // freelance work is what the site has on; an employer's shift is its own
                return false;
            }
            if (_trade.AfterHours && _onSite && _site.Working && !Ctx.Test)
            {
                Refusal = "reallife_trades_security_day";
                return false;
            }
            Vector3 towards = _onSite ? _site.At - player : Ctx.Player.transform.forward;
            towards.y = 0f;
            _face = towards.sqrMagnitude > 0.25f ? towards.normalized : Ctx.Player.transform.forward;
            _post = player;
            _units = ShiftSize();
            _skill = TradesBridges.PointsOf(_trade.Helps);
            _dice = new TradeDice(TradeDice.Mix(TimeHelper.CurrentDay, Mathf.RoundToInt(TimeHelper.NowInMinutes()), _onSite ? _site.Id : 0, 0x7A4D3E11u));
            _figures = new ShiftFigures(Ctx, Def, TradeCatalogue.Key + _def.Id + "_card_done");
            if (_trade.Mode != TradeMode.Judge)
            {
                _clock = new ShiftClock(Ctx, this, this, _def.Id, TradeCatalogue.Key + _def.Id + "_unit", TradeCatalogue.Key + _def.Id + "_units", "reallife_trades_slip");
            }
            if (StandsStill)
            {
                Ctx.Stand.Place(_post, _face, PostReach, ClipLibrary.Has(_trade.Clip) ? _trade.Clip : ClipLibrary.Has(_trade.ClipElse) ? _trade.ClipElse : null);
            }
            BeginMode(now);
            Figures();
            Ctx.Logger?.Info("Jobs: trade '" + _def.Id + "' (" + _trade.Mode + ") begun " + (_onSite
                ? "at site " + _site.Id + " (kind " + _site.Kind + ", stage " + (_site.Phase + 1) + ", " + _site.Address + ")"
                : "with no works site near (a test)") + ", " + _units + " unit(s), skill " + _skill + ", rate " + Ctx.Rate.ToString("F2", CultureInfo.InvariantCulture) + ".");
            return true;
        }

        // The trades worked in one place, held there in their working pose; the survey and the watch walk the site.
        private bool StandsStill => _trade.Mode == TradeMode.Timing || _trade.Mode == TradeMode.Match || _trade.Mode == TradeMode.Judge;

        public override void Tick(float now)
        {
            if (_clock != null && _clock.Hold())
            {
                Figures();
                return;
            }
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            if (!_dressed && onFoot && Ctx.Player.Character.visible)
            {
                _dressed = true;
                Ctx.Uniform.Wear(Ctx.Player, _trade.Painter ? CityLook.Painter : CityLook.RoadWorker);   // the director takes it off however the shift ends
            }
            if (Ctx.Driving || JobSpots.Flat(Ctx.PlayerPosition, _post) > LeaveReach)
            {
                Ctx.Say("reallife_trades_left", 8f);
                Finish(DoneCount * 3 < _units && DoneCount > 0 ? JobEnd.Failed : JobEnd.Quit);
                return;
            }
            if (StandsStill)
            {
                Ctx.Stand.Tick(Ctx.Player, onFoot, now);
            }
            if (DoneCount >= _units && Settled())
            {
                _panel.Hide();
                Finish(JobEnd.Done);
                return;
            }
            TickMode(now);
            Figures();
        }

        /// <summary>The per-frame part: the marker on the bar while a timing runs (one number written on a visible step).</summary>
        public override void Frame(float now)
        {
            if (_bar != null && _timing && !PassingTime)
            {
                _bar.Mark(MarkerNow(now));
            }
        }

        // The shift's card: the units, the money, what to do, and the bar while a timing runs.
        private void Figures()
        {
            if (_figures == null)
            {
                return;
            }
            _figures.Objective(_objective);
            _figures.Units(DoneCount, _units);
            int earned = _base + _clockPay + Bonus();
            if (earned != _shownEarned)
            {
                _shownEarned = earned;
                _figures.Limit(JobText.T("reallife_job_card_earned", "earned", JobText.Money(earned)), _panel.White);
            }
        }

        // The card's last two lines: the trade's own words while no bar stands there (a pick's time, a reading), written on a change only.
        private void Lines(string third, string fourth)
        {
            ShiftCard card = Ctx.Card;
            if (card == null)
            {
                return;
            }
            card.Line(3, third ?? string.Empty, _panel.White);
            card.Line(4, fourth ?? string.Empty, _panel.White);
        }

        private int _shownEarned = int.MinValue;

        private void Say(string objective)
        {
            _objective = objective;
            Show(objective);
        }

        // The bar on the card, made the first time a timing wants it; null (with one line) if the game's bar could not be copied.
        private GameBar Bar()
        {
            if (!_barTried)
            {
                _barTried = true;
                ShiftCard card = Ctx.Card;
                _bar = card != null ? card.Bar() : null;
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Trades check (bar): " + (_bar != null ? "the game's bar on the shift card, for the " + _def.Id + "'s timing: PASS"
                        : "FAIL (the game's bar could not be copied; the zone is given in words on the card)") + ".");
                }
            }
            return _bar;
        }

        // A unit's pay by its quality; the shift's tally.
        private void Laid(float quality, float now)
        {
            int pay = TradeRules.UnitPay(Def.UnitPay * Ctx.Rate, quality);
            _base += pay;
            _done++;
            _learnt++;
            _perfect += quality >= 0.999f ? 1 : 0;
            UnitDone(_done, pay);   // the offer's big tipper, if a career shift ever has one
            JobText.Toast(quality >= 0.85f ? NotificationType.Success : quality >= 0.5f ? NotificationType.Info : NotificationType.Warning,
                TradeCatalogue.Key + _def.Id + "_unit_done", "quality", Percent(quality), "amount", JobText.Money(pay), 5f);
            Ctx.Logger?.Info("Jobs: trade '" + _def.Id + "' unit " + _done + " of " + _units + " done by hand at quality " + quality.ToString("F2", CultureInfo.InvariantCulture)
                             + ", paid " + pay + ".");
            _quiet = now + Debounce;
            _panelState = int.MinValue;
        }

        private int Bonus() => TradeRules.TrueBonus(_base, _done, _units, _perfect);

        private static string Percent(float share) => Mathf.RoundToInt(share * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        // "Let time pass": the rest of the shift by the game's clock, from the panel.
        private void OfferSkip()
        {
            if (Over || Time.time < _quiet || _clock == null || !_clock.Offer())
            {
                return;
            }
            _panel.Hide();
        }

        // "Step away": the stand lets the player go; walking off ends the shift.
        private void StepAway()
        {
            if (!Over && Time.time >= _quiet)
            {
                Ctx.Stand.StepAway();
                Ctx.Say("reallife_trades_stepped_away", 6f);
            }
        }

        public override void End(JobEnd why)
        {
            _timing = false;
            _bar?.Show(false);
            _panel.Hide();
            Unmark();
            Ctx.Stand.Clear();
            _clock?.Close();
            EndMode(why);
            if (_learnt > 0 && !Ctx.Test && why != JobEnd.Fault)
            {
                TradesBridges.Learnt(_trade.Learns, _learnt * _trade.LearnEach);   // the trade learnt on the job: a labourer's masonry, an apprenticeship
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Trades check (learnt on the job): " + _learnt + " unit(s) of '" + _def.Id + "' taught skill " + _trade.Learns + " "
                                     + (_learnt * _trade.LearnEach) + " thousandths: PASS.");
                }
            }
            if (why == JobEnd.Fault || !Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_trades_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _done <= 0)
            {
                return false;
            }
            slip = new PaySlip { Units = _done, Base = _base, Bonus = Bonus() };
            return true;
        }

        protected override ShiftClock Clocked => _clock;
    }
}
