using System;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The dispatcher's sums: what a plan costs, and the best and worst plan for a batch. A van drives from the depot to its
    /// nearest parcel, on to the nearest of the rest and home, and costs a fixed amount for going out at all. Six parcels on
    /// three vans are 729 ways, so the bounds are found by trying every one, once a batch.
    /// </summary>
    internal static class DispatchPlan
    {
        public const int Parcels = 6;
        public const int Vans = 3;
        public const int Capacity = 3;            // parcels a van takes
        public const float VanOut = 0.45f;        // what sending a van out costs, in map units of driving
        public const float Kilometres = 2.4f;     // kilometres in a map unit, as the board words it

        private static readonly bool[] Visited = new bool[Parcels];
        private static readonly int[] Trial = new int[Parcels];

        /// <summary>The driving a plan comes to, in map units. Parcels on no van (-1) are not driven to.</summary>
        public static float Cost(Vector2[] where, int[] van, int[,] rounds = null)
        {
            float total = 0f;
            for (int v = 0; v < Vans; v++)
            {
                for (int i = 0; i < Parcels; i++)
                {
                    Visited[i] = van[i] != v;
                }
                Vector2 at = Vector2.zero;
                int stops = 0;
                while (true)
                {
                    int next = -1;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < Parcels; i++)
                    {
                        float d = Visited[i] ? float.MaxValue : (where[i] - at).sqrMagnitude;
                        if (d < nearest)
                        {
                            nearest = d;
                            next = i;
                        }
                    }
                    if (next < 0)
                    {
                        break;
                    }
                    Visited[next] = true;
                    total += Mathf.Sqrt(nearest);
                    at = where[next];
                    if (rounds != null)
                    {
                        rounds[v, stops] = next;
                    }
                    stops++;
                }
                if (rounds != null && stops < Parcels)
                {
                    rounds[v, stops] = -1;
                }
                if (stops > 0)
                {
                    total += at.magnitude + VanOut;
                }
            }
            return total;
        }

        /// <summary>The cheapest and the dearest way to load every parcel, no van over its capacity.</summary>
        public static void Bounds(Vector2[] where, out float best, out float worst)
        {
            best = float.MaxValue;
            worst = 0f;
            int ways = 1;
            for (int i = 0; i < Parcels; i++)
            {
                ways *= Vans;
            }
            for (int code = 0; code < ways; code++)
            {
                int rest = code;
                int a = 0, b = 0, c = 0;
                for (int i = 0; i < Parcels; i++)
                {
                    int v = rest % Vans;
                    rest /= Vans;
                    Trial[i] = v;
                    if (v == 0) { a++; } else if (v == 1) { b++; } else { c++; }
                }
                if (a > Capacity || b > Capacity || c > Capacity)
                {
                    continue;
                }
                float cost = Cost(where, Trial);
                best = Mathf.Min(best, cost);
                worst = Mathf.Max(worst, cost);
            }
        }

        /// <summary>How good a plan is, 0 (the worst there was) to 1 (the best).</summary>
        public static float Quality(float cost, float best, float worst)
        {
            return worst - best < 0.01f ? 1f : Mathf.Clamp01((worst - cost) / (worst - best));
        }
    }

    /// <summary>
    /// A shift at the dispatch desk, a courier firm's third rank. Three batches of six parcels come in; each parcel goes on
    /// one of three vans (three at most each) against the clock, on a small map (<see cref="DispatchList"/>) that lists each
    /// van's round as it grows, or with the buttons of the game's panel over the desk (<see cref="JobPanel"/>). A plan is
    /// paid by how near it came to the best there was; two poor ones make a bad shift. Worked on foot at the depot; walking
    /// off ends it. A plan is a decision, so there is no clock to leave it to.
    /// </summary>
    public sealed class DispatchJob : Job
    {
        private enum Step { Planning, Result }

        /// <summary>Poor plans that make a bad shift (more than half the batches).</summary>
        public const int PoorAllowed = Plans / 2 + 1;

        private const float Debounce = 0.7f;        // seconds after a batch begins in which the buttons do nothing (a key meant for the last parcel)
        private const float DeskReach = 8f;         // metres from the desk within which the panel stands over it
        private const float PanelUp = 1.8f;         // metres
        private static readonly string[] ButtonVan = { "reallife_job_dispatch_button_van_a", "reallife_job_dispatch_button_van_b", "reallife_job_dispatch_button_van_c" };
        private const string ButtonBack = "reallife_job_dispatch_button_back";
        private const int Plans = 3;
        private const float PlanSeconds = 55f;    // seconds a batch
        private const float ResultSeconds = 6f;   // seconds the result is shown
        private const float LeaveReach = 45f;   // metres from the desk before the shift counts as walked off
        private const float PoorBelow = 0.4f;   // a plan's quality below which it is poor
        private const string DeskClip = "clipboard_write";
        private const string WishClip = "desk_plan";

        private static readonly string[] Great = { "reallife_job_dispatch_great_1", "reallife_job_dispatch_great_2" };
        private static readonly string[] Fair = { "reallife_job_dispatch_fair_1", "reallife_job_dispatch_fair_2" };
        private static readonly string[] Poor = { "reallife_job_dispatch_poor_1", "reallife_job_dispatch_poor_2" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Dispatch);
        private readonly Vector2[] _where = new Vector2[DispatchPlan.Parcels];
        private readonly int[] _van = new int[DispatchPlan.Parcels];
        private readonly int[] _load = new int[DispatchPlan.Vans];
        private readonly int[,] _rounds = new int[DispatchPlan.Vans, DispatchPlan.Parcels];

        private Step _step;
        private Vector3 _desk;
        private int _plan;            // batches finished
        private int _current;         // the parcel being decided
        private float _stepEnd;
        private float _best;
        private float _worst;
        private int _earned;
        private int _poor;
        private int _great;
        private bool _paid;
        private int _line = -1;
        private string _title;
        private float _fullSaid = -10f;

        // The game's own interface.
        private readonly JobPanel _panel = new JobPanel("dispatch panel");
        private readonly Action[] _onVan = new Action[DispatchPlan.Vans];
        private readonly string[] _asks = new string[DispatchPlan.Parcels];
        private Action _onBack;
        private ShiftFigures _figures;
        private DispatchList _list;
        private Action<int> _assign;
        private string _panelLoads;
        private float _batchAt;
        private int _panelState = int.MinValue;
        private int _shownSeconds = int.MinValue;

        public override JobDef Def => _def;

        /// <summary>While the panel is up its first button has the interact key.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        public override bool QuitIsFailure => _plan < Plans && _plan > 0 && _poor * 2 >= _plan;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            Vector3 player = Ctx.PlayerPosition;
            _desk = Ctx.Shift != null && Ctx.Shift.HasDepot && JobSpots.Flat(Ctx.Shift.Depot, player) < LeaveReach ? Ctx.Shift.Depot : player;
            // At the desk if the bundle has that clip, else a clipboard in hand; either way they stay free to step about.
            if (!(ClipLibrary.Has(WishClip) && Ctx.Self.Over(WishClip, BodyMask.Upper)))
            {
                Ctx.Self.Over(DeskClip, BodyMask.Upper);
            }
            for (int v = 0; v < DispatchPlan.Vans; v++)
            {
                int van = v;
                _onVan[v] = () => Choose(van, Time.time);
            }
            _onBack = TakeBack;
            _assign = AssignFromList;
            _list = new DispatchList(Ctx.Logger);
            for (int i = 0; i < DispatchPlan.Parcels; i++)
            {
                _asks[i] = JobText.T("reallife_job_dispatch_panel_ask", "parcel", (i + 1).ToString(), "count", DispatchPlan.Parcels.ToString());
            }
            _panelLoads = JobText.T("reallife_job_dispatch_panel_loads");
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_dispatch_card_done");
            NewBatch(now);
            Figures(now);
            Ctx.Logger?.Info("Jobs: dispatcher shift, " + Plans + " batches of " + DispatchPlan.Parcels + ".");
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: dispatch: the desk is " + JobSpots.Flat(_desk, Ctx.PlayerPosition).ToString("F1", CultureInfo.InvariantCulture)
                                 + " m from the player; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panel over the desk is all there is)") + ", no clock offered (a plan is a decision, not a wait).");
            }
            return true;
        }

        // Six drops round the depot, in two or three loose clusters, so a good plan can be seen.
        private void NewBatch(float now)
        {
            int clusters = Random.Range(2, 4);
            float turn = Random.value * 360f;
            for (int i = 0; i < DispatchPlan.Parcels; i++)
            {
                float angle = (turn + (i % clusters) * (360f / clusters) + Random.Range(-38f, 38f)) * Mathf.Deg2Rad;
                float radius = Random.Range(0.35f, 0.98f);
                _where[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                _van[i] = -1;
            }
            // Never two squares on top of each other.
            for (int i = 1; i < DispatchPlan.Parcels; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if ((_where[i] - _where[j]).sqrMagnitude < 0.05f)
                    {
                        _where[i] += new Vector2(0.24f, -0.2f);
                        _where[i] = Vector2.ClampMagnitude(_where[i], 1f);
                    }
                }
            }
            for (int v = 0; v < DispatchPlan.Vans; v++)
            {
                _load[v] = 0;
            }
            DispatchPlan.Bounds(_where, out _best, out _worst);
            _list?.Reopen();
            _current = 0;
            _step = Step.Planning;
            _stepEnd = now + PlanSeconds;
            _title = JobText.T("reallife_job_dispatch_board", "plan", (_plan + 1).ToString(), "count", Plans.ToString());
            _batchAt = now;
            _panelState = int.MinValue;
            Refresh();
        }

        // Everything on screen that depends on the plan, rebuilt when a parcel moves, never on the tick.
        private void Refresh()
        {
            float cost = DispatchPlan.Cost(_where, _van, _rounds);
            string foot = JobText.T("reallife_job_dispatch_foot", "km", (cost * DispatchPlan.Kilometres).ToString("F1"));
            _list?.Draw(_title, foot, _where, _van, _rounds, _load, _step == Step.Planning && _current < DispatchPlan.Parcels ? _current : -1, _assign, _onBack);
            _panelState = int.MinValue;
            if (_step == Step.Planning && _current < DispatchPlan.Parcels)
            {
                Show(JobText.T("reallife_job_dispatch_obj", "parcel", (_current + 1).ToString(), "count", DispatchPlan.Parcels.ToString()));
            }
        }

        public override void Tick(float now)
        {
            Work(now);
            if (!Over)
            {
                Figures(now);
                Panel(now);
            }
        }

        // The shift's figures on the card, each line written only when it changes.
        private void Figures(float now)
        {
            _figures.Objective(Objective);
            _figures.Units(_plan, Plans);
            int seconds = _step == Step.Planning ? Mathf.Max(0, Mathf.CeilToInt(_stepEnd - now)) : -1;
            int key = (seconds + 1) | (_poor << 12);
            if (key != _shownSeconds)
            {
                _shownSeconds = key;
                string poor = JobText.T("reallife_job_dispatch_card_poor", "poor", _poor.ToString(), "most", PoorAllowed.ToString());
                _figures.Limit(seconds < 0 ? poor : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)) + ", " + poor,
                    _poor >= PoorAllowed - 1 ? WorldPanel.Yellow() : _panel.White);
            }
            _figures.Money(_earned, 0);
            _figures.Time(-1);
        }

        // A button for each van and Take one back, rebuilt only when a parcel, a load or the second changes.
        private void Panel(float now)
        {
            if (_step != Step.Planning || _current >= DispatchPlan.Parcels)
            {
                _panel.Hide();
                _panelState = int.MinValue;
                return;
            }
            int seconds = Mathf.Clamp(Mathf.CeilToInt(_stepEnd - now), 0, 99);
            int state = _current | (seconds << 4) | (_load[0] << 12) | (_load[1] << 14) | (_load[2] << 16);
            if (state != _panelState)
            {
                _panelState = state;
                _panel.Panel.Lines(_asks[_current], _panelLoads.Replace("{a}", _load[0].ToString()).Replace("{b}", _load[1].ToString()).Replace("{c}", _load[2].ToString()),
                    ShiftFigures.Clock(seconds), seconds <= 10 ? WorldPanel.Red() : seconds <= 20 ? WorldPanel.Yellow() : _panel.White);
                for (int v = 0; v < DispatchPlan.Vans; v++)
                {
                    _panel.Panel.Button(v, ButtonVan[v], _onVan[v], _load[v] < DispatchPlan.Capacity, true);
                }
                _panel.Panel.Button(3, ButtonBack, _onBack, _current > 0, false);
            }
            if (JobSpots.Flat(Ctx.PlayerPosition, _desk) < DeskReach)
            {
                _panel.Show(_desk + Vector3.up * PanelUp, Ctx.Player);
            }
            else
            {
                _panel.Show(Ctx.Player.transform, 2.1f, Ctx.Player);
            }
        }

        private void Work(float now)
        {
            if (Ctx.Driving || JobSpots.Flat(Ctx.PlayerPosition, _desk) > LeaveReach)
            {
                Ctx.Say("reallife_job_dispatch_left", 8f);
                Settle();
                Finish(JobEnd.Failed);
                return;
            }
            if (_step == Step.Planning)
            {
                if (now >= _stepEnd)
                {
                    // Out of time: what is left goes on whichever van has room, in order. Rarely a good plan.
                    for (int i = _current; i < DispatchPlan.Parcels; i++)
                    {
                        for (int v = 0; v < DispatchPlan.Vans; v++)
                        {
                            if (_load[v] < DispatchPlan.Capacity)
                            {
                                _van[i] = v;
                                _load[v]++;
                                break;
                            }
                        }
                    }
                    _current = DispatchPlan.Parcels;
                    Ctx.Say("reallife_job_dispatch_timeout", 6f);
                    Score(now);
                }
                return;
            }
            if (now >= _stepEnd)
            {
                if (_plan >= Plans)
                {
                    Settle();
                    Finish(_poor * 2 > Plans ? JobEnd.Failed : JobEnd.Done);
                }
                else
                {
                    NewBatch(now);
                }
            }
        }

        public override void Choose(int choice, float now)
        {
            if (Ctx == null || Over || _step != Step.Planning || _current >= DispatchPlan.Parcels || choice < 0 || choice >= DispatchPlan.Vans || now - _batchAt < Debounce)
            {
                return;
            }
            if (_load[choice] >= DispatchPlan.Capacity)
            {
                if (now - _fullSaid > 3f)
                {
                    _fullSaid = now;
                    Ctx.Say("reallife_job_dispatch_full", 4f);
                }
                return;
            }
            _van[_current] = choice;
            _load[choice]++;
            _current++;
            if (_current >= DispatchPlan.Parcels)
            {
                Score(now);
            }
            else
            {
                Refresh();
            }
        }

        // A click on a van in the list does what its button does.
        private void AssignFromList(int van)
        {
            Choose(van, Time.time);
        }

        // The panel's Take one back: the last parcel comes off its van.
        private void TakeBack()
        {
            if (Ctx == null || Over || _step != Step.Planning || _current <= 0)
            {
                return;
            }
            _current--;
            _load[_van[_current]]--;
            _van[_current] = -1;
            Refresh();
        }

        private void Score(float now)
        {
            float cost = DispatchPlan.Cost(_where, _van, _rounds);
            float quality = DispatchPlan.Quality(cost, _best, _worst);
            int pay = Mathf.RoundToInt(Def.UnitPay * Ctx.Rate * (0.25f + 0.75f * quality));
            _earned += pay;
            _plan++;
            if (quality < PoorBelow)
            {
                _poor++;
            }
            else if (quality >= 0.85f)
            {
                _great++;
            }
            _step = Step.Result;
            _stepEnd = now + ResultSeconds;
            string km = (cost * DispatchPlan.Kilometres).ToString("F1");
            string best = (_best * DispatchPlan.Kilometres).ToString("F1");
            _list?.Draw(_title, JobText.T("reallife_job_dispatch_foot_done", "km", km, "best", best), _where, _van, _rounds, _load, -1, _assign, _onBack);
            Show(JobText.T("reallife_job_dispatch_obj_done", "km", km, "best", best));
            JobText.Toast(quality >= 0.85f ? NotificationType.Success : quality >= PoorBelow ? NotificationType.Info : NotificationType.Warning,
                JobText.Pick(quality >= 0.85f ? Great : quality >= PoorBelow ? Fair : Poor, ref _line), "amount", JobText.Money(pay), 6f);
            Ctx.Logger?.Info("Jobs: dispatch plan " + _plan + ": " + cost.ToString("F2", CultureInfo.InvariantCulture) + " against best " + _best.ToString("F2", CultureInfo.InvariantCulture)
                             + " and worst " + _worst.ToString("F2", CultureInfo.InvariantCulture)
                             + ", quality " + quality.ToString("F2", CultureInfo.InvariantCulture) + ", paid " + pay + ".");
        }

        private void Settle()
        {
            if (!Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_dispatch_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _plan <= 0 || Ctx == null)
            {
                return false;
            }
            slip = new PaySlip
            {
                Units = _plan,
                Base = _earned,
                Bonus = _great >= Plans ? Mathf.RoundToInt(_earned * 0.1f) : 0   // every batch planned as well as it could be
            };
            return true;
        }

        public override void End(JobEnd why)
        {
            _list?.Hide();
            _list?.Dispose();
            _list = null;
            if (Ctx == null)
            {
                return;
            }
            _panel.Hide();
            Ctx.Self.StopOver();
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
