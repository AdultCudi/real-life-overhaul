using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Going for a run: a loop of six checkpoints on the pavement (or on the paths of a park,
    /// when the player starts in one), run by the player themselves with the game's own
    /// running. The checkpoint ahead has a marker and a ring on the ground; reaching it moves
    /// both on. Fitness, a calmer head and a sweaty shirt come from the game minutes spent
    /// actually moving (the director counts them while <see cref="Moving"/>); a park is worth a
    /// quarter more, rain a fifth less, and soaks the runner as rain does.
    ///
    /// Ends on the last checkpoint, on going indoors or getting into a vehicle, when the player
    /// is out of puff, after twenty-five minutes, and from the phone. Everything it put on
    /// screen and on the ground goes on every one of those.
    /// </summary>
    public sealed class JogRun
    {
        private const int Checkpoints = 6;
        private const float Reach = 4.5f;
        private const float StreetRadius = 55f;
        private const float ParkRadius = 34f;
        private const float MostSeconds = 25f * 60f;
        private const float MovingSpeed = 2.2f;      // metres a second: faster than the game's walk

        private readonly IModLogger _logger;
        private readonly Vector3[] _route = new Vector3[Checkpoints];
        private readonly string[] _lines = new string[Checkpoints];
        private Transform _ring;
        private int _next;
        private float _endsAt;
        private float _startMinutes;
        private float _startFitness;
        private Vector3 _lastAt;
        private float _lastTime;
        private string _title;
        private string _label;

        private static Bounds[] _parks;

        public bool Active { get; private set; }
        /// <summary>True while the runner is actually running (not standing at a kerb): what the director counts as exercise.</summary>
        public bool Moving { get; private set; }
        /// <summary>1.25 in a park, 0.8 in the rain, both multiplied.</summary>
        public float Boost { get; private set; } = 1f;

        public JogRun(IModLogger logger)
        {
            _logger = logger;
        }

        public static void ForgetCity()
        {
            _parks = null;
        }

        /// <summary>Lays the loop out from where the player stands. False, with the reason, if it cannot be run here.</summary>
        public bool Begin(PlayerController player, Transform root, out string refusal)
        {
            refusal = null;
            Vector3 from = player.transform.position;
            if (!CitySpots.Pavement(from, 12f, out Vector3 start))
            {
                refusal = "reallife_home_refused_no_pavement";
                return false;
            }
            bool park = InPark(from);
            float radius = park ? ParkRadius : StreetRadius;
            // A circle through the start, tried in eight directions: the first that gives a whole loop of pavement is the route.
            bool found = false;
            float turn = Random.Range(0f, 360f);
            for (int attempt = 0; attempt < 8 && !found; attempt++)
            {
                Vector3 centre = start + Quaternion.Euler(0f, turn + attempt * 45f, 0f) * Vector3.forward * radius;
                Vector3 previous = start;
                found = true;
                for (int i = 0; i < Checkpoints; i++)
                {
                    bool last = i == Checkpoints - 1;
                    float angle = turn + attempt * 45f + 180f + (i + 1) * (360f / Checkpoints);
                    Vector3 want = last ? start : centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    if (!CitySpots.Pavement(want, last ? 2f : 16f, out Vector3 spot) || CitySpots.Flat(spot - previous) < 12f * 12f
                        || !CitySpots.Reachable(previous, spot))
                    {
                        found = false;
                        break;
                    }
                    _route[i] = spot;
                    previous = spot;
                }
            }
            if (!found)
            {
                refusal = "reallife_home_refused_no_route";
                return false;
            }
            bool rain = BigAmbitions.DayNightCycle.RainHelper.AreRainDropsFalling;
            Boost = (park ? 1.25f : 1f) * (rain ? 0.8f : 1f);
            _title = JobText.T(park ? "reallife_home_run_title_park" : "reallife_home_run_title");
            _label = JobText.T("reallife_home_run_marker");
            for (int i = 0; i < Checkpoints; i++)
            {
                _lines[i] = JobText.T("reallife_home_run_line", "done", i.ToString(), "all", Checkpoints.ToString());
            }
            _next = 0;
            _endsAt = Time.unscaledTime + MostSeconds;
            _startMinutes = TimeHelper.NowInMinutes();
            _startFitness = HomeState.Fitness;
            _lastAt = from;
            _lastTime = Time.time;
            Moving = false;
            Active = true;
            Ring(root);
            Notifications.Show(NotificationType.Info, rain ? "reallife_home_run_started_rain" : park ? "reallife_home_run_started_park" : "reallife_home_run_started", null, 8f);
            _logger?.Info("Run: started, " + (park ? "park" : "street") + " loop of " + Checkpoints + " checkpoints, radius " + radius + " m" + (rain ? ", raining" : string.Empty) + ".");
            return true;
        }

        /// <summary>Four times a second while a run is on. Returns false when the run has ended.</summary>
        public bool Tick(PlayerController player)
        {
            if (!Active)
            {
                return false;
            }
            Vector3 at = player.transform.position;
            float elapsed = Time.time - _lastTime;
            if (elapsed > 0.2f)
            {
                Moving = CitySpots.Flat(at - _lastAt) > MovingSpeed * elapsed * MovingSpeed * elapsed;
                _lastAt = at;
                _lastTime = Time.time;
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || JobDirector.OnShift)
            {
                End("reallife_home_run_ended_left");
                return false;
            }
            if (SaveGameManager.Current.Energy <= HomeBalance.EnergyFloor)
            {
                End("reallife_home_run_ended_tired");
                return false;
            }
            if (Time.unscaledTime >= _endsAt)
            {
                End("reallife_home_run_ended_time");
                return false;
            }
            if (CitySpots.Flat(at - _route[_next]) < Reach * Reach)
            {
                _next++;
                if (_next >= Checkpoints)
                {
                    End(null);
                    return false;
                }
                PlaceRing();
            }
            return true;
        }

        /// <summary>Per frame while a run is on: the marker over the checkpoint ahead. Nothing is allocated.</summary>
        public void Draw(PlayerController player)
        {
            if (!Active || ThirdPersonCamera.IsUiOpen())
            {
                if (JobHud.Exists)
                {
                    JobHud.Instance.HideMarker();
                    JobHud.Instance.HideObjective();
                }
                return;
            }
            JobHud hud = JobHud.Instance;
            hud.ShowObjective(_title, _lines[_next]);
            hud.ShowMarkerAt(_label, _route[_next], player.transform.position);
        }

        /// <summary>Stops the run. With no reason it was run to the end; otherwise the reason is said.</summary>
        public void End(string reasonKey)
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            Moving = false;
            if (_ring != null)
            {
                _ring.gameObject.SetActive(false);
            }
            if (JobHud.Exists)
            {
                JobHud.Instance.HideMarker();
                JobHud.Instance.HideObjective();
            }
            int minutes = Mathf.Max(0, Mathf.RoundToInt(TimeHelper.NowInMinutes() - _startMinutes));
            float gained = HomeState.Fitness - _startFitness;
            var data = new Dictionary<string, string>(3)
            {
                { "minutes", minutes.ToString() }, { "legs", _next.ToString() }, { "fitness", gained.ToString("F1") }
            };
            Notifications.Show(reasonKey == null ? NotificationType.Success : NotificationType.Info, reasonKey ?? "reallife_home_run_done", data, 10f);
            if (reasonKey == null)
            {
                ModState.AddModifier(ModState.MoodUp);
            }
            _logger?.Info("Run: ended (" + (reasonKey ?? "done") + "), " + _next + "/" + Checkpoints + " checkpoints, " + minutes + " game minutes, fitness +" + gained.ToString("F1") + ".");
        }

        public void Dispose()
        {
            Active = false;
            Moving = false;
            if (_ring != null)
            {
                Object.Destroy(_ring.gameObject);
                _ring = null;
            }
        }

        // The ring on the ground at the checkpoint ahead: one object, made once, moved.
        private void Ring(Transform root)
        {
            if (_ring == null && Shapes.Available)
            {
                var made = new GameObject("RealLifeOverhaul Run Checkpoint");
                _ring = made.transform;
                _ring.SetParent(root, false);
                if (Shapes.Prop("run_checkpoint", _ring))
                {
                    PlaceRing();
                    return;
                }
                Material paint = Shapes.Paint(new Color(0.20f, 0.62f, 0.95f), 1.5f);
                Shapes.Piece("Disc", _ring, Shapes.Drum, paint, new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.02f, 1.6f));
                Shapes.Piece("Post", _ring, Shapes.Post, paint, Vector3.zero, new Vector3(0.12f, 1.4f, 0.12f));
            }
            PlaceRing();
        }

        private void PlaceRing()
        {
            if (_ring != null && _next < Checkpoints)
            {
                _ring.position = _route[_next];
                _ring.gameObject.SetActive(true);
            }
        }

        // The game marks its parks with trigger volumes that give the "walk in the park" mood. Found once per city, on the first run.
        private static bool InPark(Vector3 at)
        {
            if (_parks == null)
            {
                LocationHappinessTrigger[] triggers = Object.FindObjectsOfType<LocationHappinessTrigger>();
                var bounds = new List<Bounds>(triggers.Length);
                foreach (LocationHappinessTrigger trigger in triggers)
                {
                    Collider collider = trigger != null ? trigger.GetComponent<Collider>() : null;
                    if (collider != null)
                    {
                        bounds.Add(collider.bounds);
                    }
                }
                _parks = bounds.ToArray();
            }
            for (int i = 0; i < _parks.Length; i++)
            {
                Bounds park = _parks[i];
                if (at.x > park.min.x - 10f && at.x < park.max.x + 10f && at.z > park.min.z - 10f && at.z < park.max.z + 10f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
