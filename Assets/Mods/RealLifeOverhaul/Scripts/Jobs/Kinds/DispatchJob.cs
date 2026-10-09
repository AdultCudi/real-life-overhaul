using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The sums of the dispatcher's desk: what a plan costs and what the best and the worst plan for a
    /// batch would have cost. A van drives from the depot to the nearest of its parcels, on to the
    /// nearest of the rest, and home; a van that goes out at all costs a fixed amount on top. With six
    /// parcels and three vans there are 729 ways to load them, so the best and worst are simply found
    /// by trying every one, once per batch.
    /// </summary>
    internal static class DispatchPlan
    {
        public const int Parcels = DispatchBoard.Parcels;
        public const int Vans = DispatchBoard.Vans;
        public const int Capacity = 3;            // parcels a van takes
        public const float VanOut = 0.45f;        // what sending a van out costs, in map units of driving
        public const float Kilometres = 2.4f;     // a map unit, as the board words it

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
    /// A shift at the dispatch desk, a courier firm's third rank. Three batches of six parcels come
    /// in; each parcel is put on one of three vans (three parcels a van at most) with the choice
    /// keys, against the clock, on a small map (<see cref="DispatchBoard"/>) that draws each van's
    /// round as it grows. The interaction key takes the last parcel back. A plan is paid by how near
    /// it came to the best plan there was for that batch (found by trying all 729): better plans
    /// pay more, and two poor ones in a shift are a bad shift. Worked on foot at the depot; walking
    /// off ends it.
    /// </summary>
    public sealed class DispatchJob : Job
    {
        private enum Step { Planning, Result }

        private const int Plans = 3;
        private const float PlanSeconds = 55f;
        private const float ResultSeconds = 6f;
        private const float LeaveReach = 45f;
        private const float PoorBelow = 0.4f;
        private const string DeskClip = "clipboard_write";
        private const string WishClip = "desk_plan";

        private static readonly string[] VanLabels = { "reallife_job_dispatch_van_a", "reallife_job_dispatch_van_b", "reallife_job_dispatch_van_c" };
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

        public override JobDef Def => _def;
        public override bool OwnsInteractKey => _step == Step.Planning && !Over;
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
            // At the desk, if the bundle has that clip (wish-list); else a clipboard in hand. Either way they stay free to step about.
            if (!(ClipLibrary.Has(WishClip) && Ctx.Self.Over(WishClip, BodyMask.Upper)))
            {
                Ctx.Self.Over(DeskClip, BodyMask.Upper);
            }
            NewBatch(now);
            Ctx.Logger?.Info("Jobs: dispatcher shift, " + Plans + " batches of " + DispatchPlan.Parcels + ".");
            return true;
        }

        // Six drops round the depot, in two or three loose clusters so that a good plan can be seen.
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
            _current = 0;
            _step = Step.Planning;
            _stepEnd = now + PlanSeconds;
            _title = JobText.T("reallife_job_dispatch_board", "plan", (_plan + 1).ToString(), "count", Plans.ToString());
            Status = null;
            Refresh();
        }

        // Everything on screen that depends on the plan: rebuilt when a parcel moves, never on the tick.
        private void Refresh()
        {
            float cost = DispatchPlan.Cost(_where, _van, _rounds);
            string foot = JobText.T("reallife_job_dispatch_foot", "km", (cost * DispatchPlan.Kilometres).ToString("F1"));
            DispatchBoard.Instance.Draw(_title, foot, _where, _van, _rounds, _step == Step.Planning && _current < DispatchPlan.Parcels ? _current : -1);
            if (_step == Step.Planning && _current < DispatchPlan.Parcels)
            {
                var labels = new string[JobKeys.Choices];
                for (int v = 0; v < DispatchPlan.Vans; v++)
                {
                    labels[v] = JobText.T(VanLabels[v], "load", _load[v].ToString(), "room", DispatchPlan.Capacity.ToString());
                }
                Choices = labels;
                ChoiceLit = -1;
                Show(JobText.T("reallife_job_dispatch_obj", "parcel", (_current + 1).ToString(), "count", DispatchPlan.Parcels.ToString()));
            }
            else
            {
                Choices = null;
            }
        }

        public override void Tick(float now)
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
                ClockSeconds = Mathf.CeilToInt(_stepEnd - now);
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
            if (_step != Step.Planning || _current >= DispatchPlan.Parcels || choice < 0 || choice >= DispatchPlan.Vans)
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

        // The interaction key takes the last parcel back off its van.
        public override void Interact(float now)
        {
            if (_step != Step.Planning || _current <= 0)
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
            ClockSeconds = -1;
            Status = JobText.Money(pay);
            Choices = null;
            string km = (cost * DispatchPlan.Kilometres).ToString("F1");
            string best = (_best * DispatchPlan.Kilometres).ToString("F1");
            DispatchBoard.Instance.Draw(_title, JobText.T("reallife_job_dispatch_foot_done", "km", km, "best", best), _where, _van, _rounds, -1);
            Show(JobText.T("reallife_job_dispatch_obj_done", "km", km, "best", best));
            JobText.Toast(quality >= 0.85f ? NotificationType.Success : quality >= PoorBelow ? NotificationType.Info : NotificationType.Warning,
                JobText.Pick(quality >= 0.85f ? Great : quality >= PoorBelow ? Fair : Poor, ref _line), "amount", JobText.Money(pay), 6f);
            Ctx.Logger?.Info("Jobs: dispatch plan " + _plan + ": " + cost.ToString("F2") + " against best " + _best.ToString("F2") + " and worst " + _worst.ToString("F2")
                             + ", quality " + quality.ToString("F2") + ", paid " + pay + ".");
        }

        private void Settle()
        {
            if (_paid || _plan <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            var slip = new PaySlip
            {
                Units = _plan,
                Base = _earned,
                Bonus = _great >= Plans ? Mathf.RoundToInt(_earned * 0.1f) : 0   // every batch planned as well as it could be
            };
            Ctx.Pay(this, slip, "reallife_job_dispatch_slip");
        }

        public override void End(JobEnd why)
        {
            if (DispatchBoard.Exists)
            {
                DispatchBoard.Instance.Hide();
            }
            if (Ctx == null)
            {
                return;
            }
            Choices = null;
            Prompt = null;
            Ctx.Self.StopOver();
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
