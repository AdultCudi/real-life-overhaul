using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>The bus zones' part for street furniture: a piece switched off whole, and put back where its hole in the mesh is baked in.</summary>
    public static partial class BusZones
    {
        private const float ProbeAfter = 2.5f;    // seconds after a piece is switched off that the mesh is asked about its footprint (the cut closes in about half a second)
        private const float ObstacleMost = 2.6f;  // metres across an obstacle of the game's may be at the most to go with a piece (TransitMap.BoardingPieceMost)
        private static readonly List<NavMeshObstacle> Carving = new List<NavMeshObstacle>(4);

        // Switches a piece off with everything under it, and the game's obstacles of it that hang elsewhere, and asks the walkable
        // ground about its footprint a moment later (Probe).
        private static void Hide(GameObject piece, TransitStop stop)
        {
            Bounds bounds = Footprint(piece);
            var hid = new Hid
            {
                Piece = piece, Where = stop.Name, Foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                Reach = Mathf.Max(bounds.extents.x, bounds.extents.z), ProbeAt = Time.unscaledTime + ProbeAfter
            };
            // What of the walkable ground changes: the piece's own obstacles that are on, and those of the game's beside it.
            piece.GetComponentsInChildren(false, Carving);
            int changed = 0;
            for (int i = 0; i < Carving.Count; i++)
            {
                changed += Carving[i] != null && Carving[i].enabled && Carving[i].carving ? 1 : 0;
            }
            Carving.Clear();
            piece.SetActive(false);
            if (_obstacles == null)
            {
                Prewarm();   // listed behind the loading screen as a rule; this is for a city where that could not be done
            }
            Bounds near = bounds;
            near.Expand(new Vector3(0.6f, 2f, 0.6f));
            for (int i = 0; i < _obstacles.Length; i++)
            {
                NavMeshObstacle obstacle = _obstacles[i];
                if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
                {
                    continue;   // one under the piece went with it
                }
                Transform at = obstacle.transform;
                Vector3 middle = at.TransformPoint(obstacle.center);
                if (!WithinFlat(middle, near))
                {
                    continue;
                }
                Vector3 scale = at.lossyScale;
                float across = obstacle.shape == NavMeshObstacleShape.Box
                    ? Mathf.Max(Mathf.Abs(obstacle.size.x * scale.x), Mathf.Abs(obstacle.size.z * scale.z))
                    : obstacle.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                Transform root = at.root;
                if (across > ObstacleMost || (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal)))
                {
                    continue;   // a building's, or the mod's own
                }
                if (obstacle.GetComponentInParent<TreeController>() != null || obstacle.GetComponentInChildren<Renderer>(false) != null)
                {
                    continue;   // something still to be seen (a street tree beside it) has its own obstacle
                }
                obstacle.enabled = false;
                hid.Off.Add(obstacle);
                changed += obstacle.carving ? 1 : 0;
            }
            MeshCuts.Note("street furniture switched off at a bus stop", changed);   // a piece with no obstacle changes nothing and is not counted
            Hidden.Add(hid);
        }

        // The piece's footprint by its colliders (by its models where it has none), while it is still switched on.
        private static Bounds Footprint(GameObject piece)
        {
            piece.GetComponentsInChildren(false, Colliders);
            bool any = false;
            var bounds = new Bounds(piece.transform.position, Vector3.zero);
            for (int i = 0; i < Colliders.Count; i++)
            {
                if (Colliders[i] != null && Colliders[i].enabled)
                {
                    if (any)
                    {
                        bounds.Encapsulate(Colliders[i].bounds);
                    }
                    else
                    {
                        bounds = Colliders[i].bounds;
                        any = true;
                    }
                }
            }
            Colliders.Clear();
            if (!any)
            {
                Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(false);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (any)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    else
                    {
                        bounds = renderers[i].bounds;
                        any = true;
                    }
                }
            }
            return bounds;
        }

        // The pieces switched off a moment ago: where the walkable ground still has a hole under one that the ground round it
        // does not (the hole is baked in), the piece is put back and stays, so the player sees what they cannot walk through.
        private static void Probe(float now)
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                Hid hid = Hidden[i];
                if (hid.ProbeAt <= 0f || now < hid.ProbeAt || hid.Piece == null)
                {
                    continue;
                }
                hid.ProbeAt = 0f;
                if (OnMesh(hid.Foot))
                {
                    continue;
                }
                int around = 0;
                float reach = hid.Reach + 0.45f;
                around += OnMesh(hid.Foot + Vector3.forward * reach) ? 1 : 0;
                around += OnMesh(hid.Foot - Vector3.forward * reach) ? 1 : 0;
                around += OnMesh(hid.Foot + Vector3.right * reach) ? 1 : 0;
                around += OnMesh(hid.Foot - Vector3.right * reach) ? 1 : 0;
                if (around < 3)
                {
                    continue;   // the edge of the walkable ground: no hole in a way anybody walks
                }
                hid.Piece.SetActive(true);
                for (int k = 0; k < hid.Off.Count; k++)
                {
                    if (hid.Off[k] != null)
                    {
                        hid.Off[k].enabled = true;
                    }
                }
                MeshCuts.Note("street furniture put back at a bus stop (its hole is baked in)", Carves(hid));
                Stay.Add(hid.Piece);
                Hidden.RemoveAt(i--);
                _putBack++;
                Logger?.Info("Transit: '" + hid.Piece.name + "' at " + hid.Where
                             + " is put back: the walkable ground under it is baked without it, so with it switched off it would stop the player unseen.");
            }
        }

        // How many carving obstacles switching a piece back on changes: its own that are on, and the game's beside it.
        private static int Carves(Hid hid)
        {
            int count = 0;
            if (hid.Piece != null)
            {
                hid.Piece.GetComponentsInChildren(false, Carving);
                for (int i = 0; i < Carving.Count; i++)
                {
                    count += Carving[i] != null && Carving[i].enabled && Carving[i].carving ? 1 : 0;
                }
                Carving.Clear();
            }
            for (int k = 0; k < hid.Off.Count; k++)
            {
                count += hid.Off[k] is NavMeshObstacle obstacle && obstacle != null && obstacle.carving ? 1 : 0;
            }
            return count;
        }

        private static bool OnMesh(Vector3 place)
        {
            return NavMesh.SamplePosition(place, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && CitySpots.Flat(hit.position - place) < 0.1f * 0.1f;
        }
    }
}
