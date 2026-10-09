using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The hose run from the engine to the nozzle: a line of round segments lying on the ground
    /// along a gentle curve, the last of them rising to the nozzle in the firefighter's hands.
    /// Sixteen segments, made once from the shared round shape and one shared paint, kept
    /// switched off and reused; laid on an event, never moved per frame. The run is laid clear
    /// of vehicles and street furniture: the curve is tried bending one way, then the other,
    /// then straight, and if every way passes through something solid no hose is laid at all
    /// (the crew then works from the engine's own reel, and the log says so).
    /// </summary>
    internal sealed class Hose
    {
        private const int Segments = 16;
        private const float Thickness = 0.075f;
        private static readonly Color Jacket = new Color(0.86f, 0.72f, 0.10f);

        private readonly Transform _root;
        private readonly Transform[] _pieces = new Transform[Segments];
        private readonly Vector3[] _points = new Vector3[Segments + 1];
        private bool _built;
        private int _shown;
        private int _solidMask;

        public Hose(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Hose").transform;
            _root.SetParent(parent, false);
            _root.gameObject.SetActive(false);
        }

        public bool Laid => _shown > 0;

        /// <summary>Lays the hose from the engine's outlet (on the ground beside it) to the nozzle. False if no clear run was found.</summary>
        public bool Lay(Vector3 outlet, Vector3 nozzle)
        {
            if (!Shapes.Available)
            {
                return false;
            }
            if (!_built)
            {
                Material paint = Shapes.Paint(Jacket);
                for (int i = 0; i < Segments; i++)
                {
                    _pieces[i] = Shapes.Piece("Segment", _root, Shapes.Drum, paint, Vector3.zero, Vector3.one);
                }
                _built = true;
                _solidMask = TrafficTools.VehicleMask | LayerMask.GetMask("StreetProps");
            }
            Vector3 run = nozzle - outlet;
            run.y = 0f;
            float length = run.magnitude;
            if (length < 1.5f)
            {
                return false;
            }
            Vector3 side = Vector3.Cross(Vector3.up, run / length);
            float bend = Mathf.Min(1.6f, length * 0.14f);
            if (!Trace(outlet, nozzle, side * bend) && !Trace(outlet, nozzle, side * -bend) && !Trace(outlet, nozzle, Vector3.zero))
            {
                Take();
                EventDirector.Logger?.Info("Fire: no clear run for the hose between the engine and the fire (vehicles or street furniture in every line tried); none is laid.");
                return false;
            }
            for (int i = 0; i < Segments; i++)
            {
                Vector3 from = _points[i];
                Vector3 to = _points[i + 1];
                Vector3 along = to - from;
                float span = along.magnitude;
                Transform piece = _pieces[i];
                piece.position = from;
                piece.rotation = Quaternion.FromToRotation(Vector3.up, span > 0.001f ? along / span : Vector3.up);
                piece.localScale = new Vector3(Thickness, span + 0.02f, Thickness);   // a little overlap, so the bends show no gaps
                piece.gameObject.SetActive(true);
            }
            _shown = Segments;
            _root.gameObject.SetActive(true);
            return true;
        }

        /// <summary>Rolls it up one length at a time from the nozzle end. True while there is hose left on the ground.</summary>
        public bool RollUp()
        {
            if (_shown <= 0)
            {
                return false;
            }
            _shown = Mathf.Max(0, _shown - 2);
            for (int i = _shown; i < Segments; i++)
            {
                if (_pieces[i].gameObject.activeSelf)
                {
                    _pieces[i].gameObject.SetActive(false);
                }
            }
            if (_shown == 0)
            {
                _root.gameObject.SetActive(false);
            }
            return _shown > 0;
        }

        /// <summary>Gone at once. Safe at any time.</summary>
        public void Take()
        {
            _shown = 0;
            _root.gameObject.SetActive(false);
        }

        // The points of the run with the curve bent by an offset at its middle; false if any ground stretch passes through something solid.
        private bool Trace(Vector3 outlet, Vector3 nozzle, Vector3 bend)
        {
            // An S: out from the engine one way, in to the nozzle the other, both on the ground; the last segment rises to the hands.
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector3 point = Vector3.Lerp(outlet, nozzle, t) + bend * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t * 0.4f);
                if (i < Segments)
                {
                    point.y = (StreetSpots.Ground(point, out float ground) ? ground : Mathf.Lerp(outlet.y, nozzle.y, t)) + Thickness * 0.5f;
                    if (i > 0 && i < Segments - 1 && _solidMask != 0 && Physics.CheckSphere(point + Vector3.up * 0.3f, 0.22f, _solidMask, QueryTriggerInteraction.Ignore))
                    {
                        return false;
                    }
                }
                else
                {
                    point = nozzle;
                }
                _points[i] = point;
            }
            return true;
        }
    }
}
