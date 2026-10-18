using System;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The dispatcher's planning board in the window the game lists its subway stations in (<see cref="DeskList"/>): the
    /// parcels waiting, with how far and which way from the depot, then each van's load and round with a button to load the
    /// next parcel, and one to take the last back. The panel over the desk has the same choices. Opened with each batch; the
    /// player may close it. Written when a parcel moves, never per frame; without the window the panel is all there is.
    /// </summary>
    internal sealed class DispatchList
    {
        private const int VanIds = DispatchPlan.Vans;     // ids 0 to 2 are the vans; this one takes a parcel back
        private const string ButtonSend = "reallife_job_dispatch_list_send";
        private const string ButtonBack = "reallife_job_dispatch_list_back_button";
        private static readonly string[] DirKeys =
        {
            "reallife_job_dispatch_dir_0", "reallife_job_dispatch_dir_1", "reallife_job_dispatch_dir_2", "reallife_job_dispatch_dir_3",
            "reallife_job_dispatch_dir_4", "reallife_job_dispatch_dir_5", "reallife_job_dispatch_dir_6", "reallife_job_dispatch_dir_7"
        };

        private readonly IModLogger _logger;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Dispatch", 900f, 124f);
        private readonly StringBuilder _text = new StringBuilder(160);
        private readonly StringBuilder _parcels = new StringBuilder(40);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action<int> _assign;
        private Action _takeBack;
        private bool _dismissed;
        private bool _said;

        public DispatchList(IModLogger logger)
        {
            _logger = logger;
            _onPick = Picked;
            _onClosed = Closed;
            TravelList.Logger = logger;
        }

        /// <summary>A new batch: the window may open again even if the last one was closed.</summary>
        public void Reopen()
        {
            _dismissed = false;
        }

        /// <summary>Writes the plan. <paramref name="current"/> is the parcel to place next, or -1 when the batch is over.</summary>
        public void Draw(string title, string foot, Vector2[] where, int[] van, int[,] rounds, int[] load, int current, Action<int> assign, Action takeBack)
        {
            if (_dismissed)
            {
                return;
            }
            TravelList list = _window.Take(this);
            if (list == null)
            {
                if (!_said)
                {
                    _said = true;
                    _logger?.Info("Jobs check (dispatch list): the game's list could not be made; the panel over the desk is all there is: FAIL.");
                }
                return;
            }
            _assign = assign;
            _takeBack = takeBack;
            list.Begin(title ?? string.Empty, foot ?? string.Empty);

            // The parcels not yet on a van.
            _text.Length = 0;
            int waiting = 0;
            for (int i = 0; i < DispatchPlan.Parcels; i++)
            {
                if (van[i] >= 0)
                {
                    continue;
                }
                waiting++;
                float km = where[i].magnitude * DispatchPlan.Kilometres;
                float angle = Mathf.Atan2(where[i].y, where[i].x) * Mathf.Rad2Deg;
                int dir = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
                _text.Append(i == current ? "> " : string.Empty)
                    .Append(JobText.T("reallife_job_dispatch_list_parcel").Replace("{n}", (i + 1).ToString()).Replace("{km}", km.ToString("F1")).Replace("{dir}", JobText.T(DirKeys[dir])))
                    .Append('\n');
            }
            if (waiting == 0)
            {
                _text.Append(JobText.T("reallife_job_dispatch_list_none"));
            }
            else
            {
                _text.Length--;
            }
            list.AddNote(JobText.T("reallife_job_dispatch_list_waiting"), _text.ToString(), list.HeightFor(_text.Length + 40 * Math.Max(1, waiting), false));

            // What each van carries, and how long its round is.
            for (int v = 0; v < DispatchPlan.Vans; v++)
            {
                _parcels.Length = 0;
                for (int s = 0; s < DispatchPlan.Parcels && rounds[v, s] >= 0; s++)
                {
                    _parcels.Append(_parcels.Length > 0 ? ", " : string.Empty).Append(rounds[v, s] + 1);
                }
                string detail;
                if (_parcels.Length == 0)
                {
                    detail = JobText.T("reallife_job_dispatch_list_van_empty");
                }
                else
                {
                    float round = 0f;
                    Vector2 at = Vector2.zero;
                    for (int s = 0; s < DispatchPlan.Parcels && rounds[v, s] >= 0; s++)
                    {
                        round += (where[rounds[v, s]] - at).magnitude;
                        at = where[rounds[v, s]];
                    }
                    round += at.magnitude;
                    detail = JobText.T("reallife_job_dispatch_list_van_load").Replace("{parcels}", _parcels.ToString()).Replace("{load}", load[v].ToString())
                        .Replace("{cap}", DispatchPlan.Capacity.ToString()).Replace("{km}", (round * DispatchPlan.Kilometres).ToString("F1"));
                }
                list.Add(v, JobText.T("reallife_job_dispatch_list_van_name").Replace("{letter}", ((char)('A' + v)).ToString()), detail, ButtonSend,
                    current >= 0 && load[v] < DispatchPlan.Capacity);
            }
            list.Add(VanIds, JobText.T("reallife_job_dispatch_list_back_name"), JobText.T("reallife_job_dispatch_list_back_detail"), ButtonBack, current > 0);
            list.Open(_onPick, _onClosed);
            list.Locked = false;
            if (Meta.Dev.Verbose && !_said)
            {
                _said = true;
                _logger?.Info("Jobs check (dispatch list): the game's list is open, " + list.Made + " entries made so far: PASS.");
            }
        }

        public void Hide()
        {
            _window.Close();
        }

        public void Dispose()
        {
            _window.Destroy(e => _logger?.Error(e));
        }

        private void Picked(int id)
        {
            if (id >= 0 && id < DispatchPlan.Vans)
            {
                _assign?.Invoke(id);
            }
            else if (id == VanIds)
            {
                _takeBack?.Invoke();
            }
        }

        private void Closed()
        {
            _dismissed = true;
        }
    }
}
