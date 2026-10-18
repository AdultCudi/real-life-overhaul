using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Scene control: the props built once (posts, tape, cones, barriers, walls), the cones' taper and the litter at the point of impact.</summary>
    public sealed partial class SceneCordon
    {
        private const float ConeSpacing = 1.9f;   // metres between the cones of a taper

        /// <summary>
        /// Lays the cones: a short diagonal upstream of each end traffic arrives at, beyond any unit standing there, stepping
        /// across to the side traffic is let past on. Laid again on every call; needs the tape up.
        /// </summary>
        /// <param name="near">Metres before the near end taken up by standing vehicles (Reach); negative leaves that end as it is.</param>
        /// <param name="far">The same beyond the far end, which has a taper only if traffic arrives there.</param>
        public void LayTaper(float near, float far)
        {
            if (!Up || Lowered || _failed || _props == null || _cones == null)
            {
                return;
            }
            try
            {
                int nearCones = near >= 0f ? Taper(0, -1f, near) : -1;
                // Traffic reaches the far end by a blocked lane running the other way, or an open one passing the tape.
                int farCones = far >= 0f && (_bothWays || _gateAgainst) ? Taper(TaperCones, 1f, far) : -1;
                if (HasWay)
                {
                    // No cone stands on the ambulance crew's way; measured again with the cones in.
                    CorridorClear = MeasureWay(true, out _, out _);
                }
                if (nearCones > 0 || farCones > 0)
                {
                    Logger?.Info("Cordon: taper laid, " + (nearCones > 0 ? nearCones + " cone(s) " + (near > 0f ? near.ToString("F0") + " m " : "") + "upstream of the near end" : "")
                                 + (nearCones > 0 && farCones > 0 ? " and " : "")
                                 + (farCones > 0 ? farCones + " cone(s) " + (far > 0f ? far.ToString("F0") + " m " : "") + "upstream of the far end" : "") + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                LiftTaper();
            }
        }

        /// <summary>The cones are taken in: a response vehicle is about to leave through where they stand.</summary>
        public void LiftTaper()
        {
            if (_cones != null)
            {
                HideFrom(_cones, 0);
            }
        }

        // One end's taper. Its cones are this end's own (six from 'first'); returns how many stand.
        private int Taper(int first, float end, float clear)
        {
            for (int i = first; i < first + TaperCones; i++)
            {
                _cones[i].gameObject.SetActive(false);
            }
            // Towards the lane traffic is let past in; with none, towards the middle of the road (away from the kerb the entries are on).
            float open = _gateSide != 0f ? _gateSide : -_entry;
            float fromAcross = open > 0f ? _left : _rightEdge;
            float toAcross = open > 0f ? _rightEdge + 0.3f : _left - 0.3f;
            float width = Mathf.Abs(toAcross - fromAcross);
            float run = Mathf.Clamp(width * 1.2f, 4.5f, 9f);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(width * width + run * run) / ConeSpacing) + 1, 4, TaperCones);
            float nearest = (end < 0f ? _back : _front) + end * Mathf.Max(clear, BarrierOut + 1.2f);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);   // 0 at the cone traffic meets first, furthest out
                if (Place(nearest + end * run * (1f - t), Mathf.Lerp(fromAcross, toAcross, t), 0.3f, post: false, out Vector3 ground))
                {
                    Transform cone = _cones[first + placed];
                    cone.SetPositionAndRotation(ground, Quaternion.identity);
                    cone.gameObject.SetActive(true);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>Glass and bits of bumper at the point of impact until Open; only modelled pieces (the wreck has its own debris).</summary>
        public void Litter(Vector3 impact, Vector3 along)
        {
            if (_failed || !PropLibrary.Ready || !Shapes.Available)
            {
                return;
            }
            try
            {
                if (_scatter != null && !_scatterModelled)
                {
                    UnityEngine.Object.Destroy(_scatter);
                    _scatter = null;
                }
                if (_scatter == null && !BuildScatter())
                {
                    return;
                }
                along.y = 0f;
                along = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, along);
                for (int i = 0; i < _litter.Length; i++)
                {
                    // Glass where the cars met, plastic thrown a little further each way.
                    Vector3 at = impact + along * (i == 0 ? 0f : i == 1 ? -1.1f : i == 2 ? 0.5f : 1.5f) + right * (i == 0 ? 0f : i == 1 ? 0.7f : i == 2 ? -1.0f : 0.6f);
                    if (StreetSpots.Ground(at, out float ground))
                    {
                        at.y = ground + 0.01f;
                    }
                    _litter[i].SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                }
                for (int i = 0; i < _markers.Length; i++)
                {
                    _markers[i].gameObject.SetActive(false);
                }
                _scatter.SetActive(true);
                HasImpact = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Where numbered marker 0, 1, ... belongs: beside one of the pieces lying at the point of impact.</summary>
        public Vector3 Evidence(int index)
        {
            if (!HasImpact || _litter == null)
            {
                return _scene;
            }
            Transform piece = _litter[Mathf.Abs(index) % _litter.Length];
            return piece.position + piece.right * 0.55f;
        }

        /// <summary>An officer sets a numbered marker down beside what lies at the point of impact. False if there is none to set.</summary>
        public bool Mark(int index)
        {
            if (!HasImpact || _markers == null || index < 0 || index >= _markers.Length || _markers[index].childCount == 0)
            {
                return false;   // (a marker the bundle does not have has nothing to show)
            }
            Vector3 at = Evidence(index);
            if (StreetSpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            _markers[index].SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            _markers[index].gameObject.SetActive(true);
            return true;
        }

        // Once: two patches of glass, two of plastic and the four numbered markers, switched off until wanted.
        private bool BuildScatter()
        {
            if (!PropLibrary.TryGet("debris_glass", out _, out _) || !PropLibrary.TryGet("debris_plastic", out _, out _))
            {
                return false;
            }
            _scatterModelled = true;
            _scatter = new GameObject("RealLifeOverhaul Impact");
            Transform root = _scatter.transform;
            root.SetParent(_host, false);
            _scatter.SetActive(false);
            _litter = new Transform[4];
            for (int i = 0; i < _litter.Length; i++)
            {
                Transform piece = new GameObject("Debris").transform;
                piece.SetParent(root, false);
                Shapes.Prop(i == 0 || i == 3 ? "debris_glass" : "debris_plastic", piece);
                _litter[i] = piece;
            }
            _markers = new Transform[Markers];
            for (int i = 0; i < Markers; i++)
            {
                Transform marker = new GameObject("Marker").transform;
                marker.SetParent(root, false);
                Shapes.Prop("evidence_marker_" + (i + 1), marker);
                marker.gameObject.SetActive(false);
                _markers[i] = marker;
            }
            return true;
        }

        // Once: every post, length of tape, cone, barrier and wall there will ever be, switched off until wanted.
        private void Build()
        {
            _modelled = PropLibrary.Ready;
            _props = new GameObject("RealLifeOverhaul Cordon");
            Transform root = _props.transform;
            root.SetParent(_host, false);
            _props.SetActive(false);

            Material orange = Shapes.Paint(new Color(0.95f, 0.33f, 0.04f), 1.5f);
            Material dark = Shapes.Paint(new Color(0.05f, 0.05f, 0.05f));
            Material yellow = Shapes.Paint(new Color(0.98f, 0.82f, 0.05f), 4f);
            Material white = Shapes.Paint(new Color(0.92f, 0.92f, 0.92f), 1.5f);
            Material blue = Shapes.Paint(new Color(0.05f, 0.15f, 0.55f), 2f);
            Mesh cube = Shapes.Cube;

            _posts = new Transform[MaxPosts];
            for (int i = 0; i < MaxPosts; i++)
            {
                Transform post = new GameObject("Post").transform;
                post.SetParent(root, false);
                if (!Shapes.Prop("tape_post", post))
                {
                    Shapes.Piece("Foot", post, cube, dark, new Vector3(0f, 0.03f, 0f), new Vector3(0.32f, 0.06f, 0.32f));
                    Shapes.Piece("Pole", post, Shapes.Drum, white, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, PostHeight - 0.06f, 0.05f));
                    Shapes.Piece("Cap", post, Shapes.Drum, orange, new Vector3(0f, PostHeight - 0.14f, 0f), new Vector3(0.07f, 0.14f, 0.07f));
                }
                post.gameObject.SetActive(false);
                _posts[i] = post;
            }

            // The modelled tape if it is to be had (the one that hangs a little, else the straight one), tied on
            // where the modelled post takes it; else the plain strip at the plain post's height.
            string tapeId = "tape_4m_sag";
            if (!PropLibrary.TryGet(tapeId, out Mesh tapeMesh, out Material tapeMaterial) || tapeMesh == null || tapeMaterial == null)
            {
                tapeId = "tape_4m";
                if (!PropLibrary.TryGet(tapeId, out tapeMesh, out tapeMaterial) || tapeMesh == null || tapeMaterial == null)
                {
                    tapeId = null;
                }
            }
            _tapeUnit = tapeId != null ? PropLibrary.SizeOf(tapeId).z : 0f;
            if (tapeId != null && _tapeUnit < 0.1f)
            {
                _tapeUnit = 4f;   // its size is not known: what it is modelled to
            }
            _tapeHeight = PropLibrary.TryGet("tape_post", out _, out _) ? 0.95f : TapeHeight;
            _tapes = new Transform[MaxPosts];
            for (int i = 0; i < MaxPosts; i++)
            {
                _tapes[i] = tapeId != null ? Shapes.Piece("Tape", root, tapeMesh, tapeMaterial, Vector3.zero, Vector3.one)
                    : Shapes.Piece("Tape", root, cube, yellow, Vector3.zero, Vector3.one);
                _tapes[i].gameObject.SetActive(false);
            }

            _cones = new Transform[MaxCones];
            for (int i = 0; i < MaxCones; i++)
            {
                Transform cone = new GameObject("Cone").transform;
                cone.SetParent(root, false);
                if (!Shapes.Prop("cone", cone))
                {
                    Shapes.Piece("Foot", cone, cube, dark, new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.03f, 0.34f));
                    Shapes.Piece("Cone", cone, Shapes.Post, orange, new Vector3(0f, 0.03f, 0f), new Vector3(0.26f, 0.55f, 0.26f));
                }
                cone.gameObject.SetActive(false);
                _cones[i] = cone;
            }

            _barriers = new Transform[MaxBarriers];
            for (int i = 0; i < MaxBarriers; i++)
            {
                Transform barrier = new GameObject("Barrier").transform;
                barrier.SetParent(root, false);
                if (!Shapes.Prop("barrier", barrier))
                {
                    Shapes.Piece("Board", barrier, cube, white, new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 0.22f, 0.04f));
                    Shapes.Piece("Stripe", barrier, cube, blue, new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 0.08f, 0.05f));
                    Shapes.Piece("Leg", barrier, cube, white, new Vector3(-0.8f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f));
                    Shapes.Piece("Leg", barrier, cube, white, new Vector3(0.8f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f));
                }
                barrier.gameObject.SetActive(false);
                _barriers[i] = barrier;
            }

            // The walls have nothing to draw: each cuts its own strip out of the navigation mesh while it is switched on.
            _walls = Obstacles("Wall", Walls, root);
            _bars = Obstacles("Bar", Bars, root);
        }

        private static NavMeshObstacle[] Obstacles(string name, int count, Transform root)
        {
            var made = new NavMeshObstacle[count];
            for (int i = 0; i < count; i++)
            {
                var box = new GameObject(name);
                box.transform.SetParent(root, false);
                box.SetActive(false);
                NavMeshObstacle obstacle = box.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                made[i] = obstacle;
            }
            return made;
        }
    }
}
