using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The path a bus's front axle drives through a route's points, and its pace along it, numbers only so NavCheck drives a
    /// model bus with the very code <see cref="BusDriver"/> uses: a centripetal Catmull-Rom spline (no loops or overshoot)
    /// begun the way the bus faces, each point's speed limit from its bend planned back at <see cref="Braking"/>, the rear
    /// axle trailing (<see cref="Trail"/>) so its wheels cut inside a bend as a bus's do.
    /// </summary>
    public sealed class BusPath
    {
        public const float Spacing = 0.5f;          // metres between the points of the path
        public const float FinalShort = 1.2f;       // the bus stands this far before a route's last point
        public const float Pickup = 1.1f;           // m/s2 it gathers speed with
        public const float PickupJerk = 1.4f;       // m/s3 it comes up to that with: it does not lurch away
        public const float Braking = 1.5f;          // m/s2 every stop is planned with
        public const float BrakingMost = 3.5f;      // m/s2 at the most, for what comes into its way
        public const float BendGrip = 1.3f;         // m/s2 sideways through a bend
        public const float Crawl = 1.5f;            // m/s no bend holds it under
        public const float CapReach = 8f;           // metres either side of a route point that point's own limit holds over

        public readonly List<float> X = new List<float>(512);
        public readonly List<float> Y = new List<float>(512);
        public readonly List<float> Z = new List<float>(512);
        public readonly List<float> Arc = new List<float>(512);       // metres along the path to each point
        public readonly List<float> Limit = new List<float>(512);     // m/s, the most it may be doing at each point
        public readonly List<float> PointArc = new List<float>(128);  // metres along the path to each point of the route as given

        private readonly List<float> _cx = new List<float>(130);
        private readonly List<float> _cy = new List<float>(130);
        private readonly List<float> _cz = new List<float>(130);
        private readonly List<int> _kept = new List<int>(128);
        private readonly List<float> _controlArc = new List<float>(130);
        private int _cursor;

        public int Count => X.Count;

        public float End { get; private set; }   // metres from the start of the path to where the bus is to stand

        /// <summary>Metres from the start of the path to its last point.</summary>
        public float Total => Arc.Count > 0 ? Arc[Arc.Count - 1] : 0f;

        /// <summary>
        /// Builds the path from the front axle's place, facing (hx, hz), through the route's points; <paramref name="caps"/> (or
        /// null) is the most the bus may be doing at each route point, a junction, held over CapReach either side.
        /// </summary>
        public void Build(IList<float> rx, IList<float> ry, IList<float> rz, int count, float fx, float fy, float fz, float hx, float hz, IList<float> caps = null)
        {
            X.Clear();
            Y.Clear();
            Z.Clear();
            Arc.Clear();
            Limit.Clear();
            PointArc.Clear();
            _cx.Clear();
            _cy.Clear();
            _cz.Clear();
            _kept.Clear();
            _controlArc.Clear();
            _cursor = 0;
            _cx.Add(fx);
            _cy.Add(fy);
            _cz.Add(fz);
            for (int i = 0; i < count; i++)
            {
                int last = _cx.Count - 1;
                float dx = rx[i] - _cx[last], dz = rz[i] - _cz[last];
                if (dx * dx + dz * dz > 0.09f)
                {
                    _cx.Add(rx[i]);
                    _cy.Add(ry[i]);
                    _cz.Add(rz[i]);
                }
                _kept.Add(_cx.Count - 1);
            }
            float h = (float)Math.Sqrt(hx * hx + hz * hz);
            if (h < 0.0001f)
            {
                hx = 0f;
                hz = 1f;
            }
            else
            {
                hx /= h;
                hz /= h;
            }
            int controls = _cx.Count;
            X.Add(fx);
            Y.Add(fy);
            Z.Add(fz);
            Arc.Add(0f);
            _controlArc.Add(0f);
            for (int k = 0; k < controls - 1; k++)
            {
                float x1 = _cx[k], y1 = _cy[k], z1 = _cz[k];
                float x2 = _cx[k + 1], y2 = _cy[k + 1], z2 = _cz[k + 1];
                float span = Flat(x2 - x1, z2 - z1);
                float x0, y0, z0;
                if (k > 0)
                {
                    x0 = _cx[k - 1];
                    y0 = _cy[k - 1];
                    z0 = _cz[k - 1];
                }
                else
                {
                    float back = Math.Max(1f, span);   // begun the way it faces
                    x0 = x1 - hx * back;
                    y0 = y1;
                    z0 = z1 - hz * back;
                }
                float x3, y3, z3;
                if (k + 2 < controls)
                {
                    x3 = _cx[k + 2];
                    y3 = _cy[k + 2];
                    z3 = _cz[k + 2];
                }
                else
                {
                    x3 = x2 + (x2 - x1);
                    y3 = y2 + (y2 - y1);
                    z3 = z2 + (z2 - z1);
                }
                int steps = Math.Max(1, (int)Math.Ceiling(span / Spacing));
                for (int j = 1; j <= steps; j++)
                {
                    Spline(x0, z0, x1, z1, x2, z2, x3, z3, (float)j / steps, out float px, out float pz);
                    float py = y1 + (y2 - y1) * j / steps;
                    int lastPoint = X.Count - 1;
                    Arc.Add(Arc[lastPoint] + Flat(px - X[lastPoint], pz - Z[lastPoint]));
                    X.Add(px);
                    Y.Add(py);
                    Z.Add(pz);
                }
                _controlArc.Add(Arc[Arc.Count - 1]);
            }
            for (int i = 0; i < _kept.Count; i++)
            {
                PointArc.Add(_controlArc[_kept[i]]);
            }
            End = Math.Max(0f, Total - FinalShort);

            // The pace each bend allows (the turn between neighbouring points over the distance), planned back from each.
            int n = X.Count;
            for (int i = 0; i < n; i++)
            {
                float most = float.MaxValue;
                if (i > 0 && i < n - 1)
                {
                    float ax = X[i] - X[i - 1], az = Z[i] - Z[i - 1];
                    float bx = X[i + 1] - X[i], bz = Z[i + 1] - Z[i];
                    float la = Flat(ax, az), lb = Flat(bx, bz);
                    if (la > 0.0001f && lb > 0.0001f)
                    {
                        float cos = Math.Max(-1f, Math.Min(1f, (ax * bx + az * bz) / (la * lb)));
                        float bend = (float)Math.Acos(cos) / Math.Max(0.05f, (la + lb) * 0.5f);   // 1 / radius
                        if (bend > 0.0001f)
                        {
                            most = Math.Max(Crawl, (float)Math.Sqrt(BendGrip / bend));
                        }
                    }
                }
                Limit.Add(most);
            }
            for (int r = 0; caps != null && r < count && r < caps.Count && r < PointArc.Count; r++)
            {
                float cap = caps[r];
                if (cap >= float.MaxValue)
                {
                    continue;
                }
                float at = PointArc[r];
                for (int i = 0; i < n; i++)
                {
                    if (Arc[i] >= at - CapReach && Arc[i] <= at + CapReach)
                    {
                        Limit[i] = Math.Min(Limit[i], cap);
                    }
                }
            }
            for (int i = n - 2; i >= 0; i--)
            {
                float step = Arc[i + 1] - Arc[i];
                Limit[i] = Math.Min(Limit[i], (float)Math.Sqrt(Limit[i + 1] * Limit[i + 1] + 2f * Braking * step));
            }
        }

        /// <summary>The place a distance along the path falls at, and its flat direction there; quickest asked in order (a cursor is kept).</summary>
        public void At(float metres, out float x, out float y, out float z, out float dx, out float dz)
        {
            int n = X.Count;
            if (n == 0)
            {
                x = y = z = dx = 0f;
                dz = 1f;
                return;
            }
            metres = Math.Max(0f, Math.Min(metres, Arc[n - 1]));
            if (_cursor >= n - 1 || Arc[_cursor] > metres)
            {
                _cursor = 0;
            }
            while (_cursor < n - 2 && Arc[_cursor + 1] < metres)
            {
                _cursor++;
            }
            int a = _cursor, b = Math.Min(_cursor + 1, n - 1);
            float span = Arc[b] - Arc[a];
            float t = span > 0.0001f ? (metres - Arc[a]) / span : 0f;
            x = X[a] + (X[b] - X[a]) * t;
            y = Y[a] + (Y[b] - Y[a]) * t;
            z = Z[a] + (Z[b] - Z[a]) * t;
            dx = X[b] - X[a];
            dz = Z[b] - Z[a];
            float d = Flat(dx, dz);
            if (d > 0.000001f)
            {
                dx /= d;
                dz /= d;
            }
            else
            {
                dx = 0f;
                dz = 1f;
            }
        }

        public int Cursor => _cursor;   // the cursor At keeps, to be put back (Seek) after looking ahead

        public void Seek(int cursor)
        {
            _cursor = Math.Max(0, Math.Min(cursor, Math.Max(0, X.Count - 1)));
        }

        /// <summary>The most it may be doing where the cursor is.</summary>
        public float LimitHere => Limit.Count > 0 ? Limit[Math.Min(_cursor, Limit.Count - 1)] : float.MaxValue;

        /// <summary>The first route point whose place along the path is beyond <paramref name="metres"/>.</summary>
        public int NextPoint(float metres)
        {
            int i = 0;
            while (i < PointArc.Count && PointArc[i] <= metres + 0.01f)
            {
                i++;
            }
            return i;
        }

        /// <summary>
        /// Seconds from <paramref name="from"/> metres along to where it is to stand, driven as it is driven, no faster than
        /// <paramref name="slowSpeed"/> from <paramref name="slowAt"/> (a bay). Lights and traffic not counted. A few hundred sums.
        /// </summary>
        public float SecondsLeft(float from, float speed, float cruise, float slowAt, float slowSpeed)
        {
            int n = X.Count;
            if (n < 2 || from >= End - 0.01f)
            {
                return 0f;
            }
            float seconds = 0f;
            float v = Math.Max(0f, speed);
            float at = from;
            int i = 1;
            while (i < n && Arc[i] <= from)
            {
                i++;
            }
            while (at < End - 0.01f && i <= n)
            {
                float next = i < n ? Math.Min(Arc[i], End) : End;
                float step = next - at;
                if (step > 0.0001f)
                {
                    float most = Math.Min(cruise, Math.Min(i < n ? Limit[i] : float.MaxValue, StopIn(End - next)));
                    if (slowAt < float.MaxValue)
                    {
                        most = Math.Min(most, next >= slowAt ? slowSpeed : (float)Math.Sqrt(slowSpeed * slowSpeed + 2f * Braking * (slowAt - next)));
                    }
                    float gathered = (float)Math.Sqrt(v * v + 2f * Pickup * step);
                    float braked = (float)Math.Sqrt(Math.Max(0f, v * v - 2f * BrakingMost * step));   // it cannot lose speed faster than it brakes
                    float end = Math.Min(gathered, Math.Max(most, braked));
                    seconds += step / Math.Max(0.3f, (v + end) * 0.5f);   // the last metres of a stop come to a mean, not to nothing
                    v = end;
                    at = next;
                }
                i++;
            }
            return seconds;
        }

        /// <summary>The fastest it may be doing with so many metres to a place it must stand at.</summary>
        public static float StopIn(float metres)
        {
            return (float)Math.Sqrt(2f * Braking * Math.Max(0f, metres));
        }

        /// <summary>One step of its speed towards <paramref name="wanted"/>: up with a pull that eases in, or down as its stops are planned.</summary>
        public static float Pace(float speed, ref float pull, float wanted, float dt)
        {
            if (wanted >= speed)
            {
                pull = Math.Min(Pickup, pull + PickupJerk * dt);
                return Math.Min(wanted, speed + pull * dt);
            }
            pull = 0f;
            float hard = speed - wanted > 1f ? BrakingMost : Braking * 1.3f;
            return Math.Max(wanted, speed - hard * dt);
        }

        /// <summary>The rear axle drawn straight towards the front axle's new place, at the wheelbase.</summary>
        public static void Trail(float fx, float fz, ref float rx, ref float rz, float wheelbase)
        {
            float dx = fx - rx, dz = fz - rz;
            float d = Flat(dx, dz);
            if (d < 0.0001f)
            {
                return;
            }
            rx = fx - dx / d * wheelbase;
            rz = fz - dz / d * wheelbase;
        }

        // A point of a centripetal Catmull-Rom segment from p1 to p2 (Barry and Goldman's form), on the ground.
        private static void Spline(float x0, float z0, float x1, float z1, float x2, float z2, float x3, float z3, float u, out float x, out float z)
        {
            float t0 = 0f;
            float t1 = t0 + Knot(x0, z0, x1, z1);
            float t2 = t1 + Knot(x1, z1, x2, z2);
            float t3 = t2 + Knot(x2, z2, x3, z3);
            float t = t1 + (t2 - t1) * u;
            Mix(x0, z0, x1, z1, t0, t1, t, out float a1x, out float a1z);
            Mix(x1, z1, x2, z2, t1, t2, t, out float a2x, out float a2z);
            Mix(x2, z2, x3, z3, t2, t3, t, out float a3x, out float a3z);
            Mix(a1x, a1z, a2x, a2z, t0, t2, t, out float b1x, out float b1z);
            Mix(a2x, a2z, a3x, a3z, t1, t3, t, out float b2x, out float b2z);
            Mix(b1x, b1z, b2x, b2z, t1, t2, t, out x, out z);
        }

        private static float Knot(float ax, float az, float bx, float bz)
        {
            return Math.Max(0.01f, (float)Math.Sqrt(Flat(bx - ax, bz - az)));
        }

        private static void Mix(float ax, float az, float bx, float bz, float ta, float tb, float t, out float x, out float z)
        {
            float span = tb - ta;
            if (span < 0.0001f)
            {
                x = ax;
                z = az;
                return;
            }
            float wa = (tb - t) / span, wb = (t - ta) / span;
            x = ax * wa + bx * wb;
            z = az * wa + bz * wb;
        }

        private static float Flat(float dx, float dz)
        {
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
