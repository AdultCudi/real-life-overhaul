using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The copy at home: the <c>newspaper_ledger</c> prop open inside the front door while the player is home with a
    /// copy, a click away (<see cref="LedgerPointer"/>). The cost: nothing per frame; a few comparisons a second.
    /// </summary>
    public sealed class LedgerHome
    {
        private const string PropId = "newspaper_ledger";
        private const string PlainPropId = "newspaper";   // for a prop bundle without the Ledger's own

        private readonly LedgerDirector _director;
        private Transform _paper;
        private LedgerPointer _pointer;
        private BuildingRegistration _placedIn;   // the home it lies in
        private int _placedFor;

        public LedgerHome(LedgerDirector director)
        {
            _director = director;
        }

        public void Slow(bool atHomeWithCopy)
        {
            LedgerState state = _director.State;
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!atHomeWithCopy || state == null || state.Edition == null || manager == null || !PropLibrary.Ready)
            {
                Dispose();
                return;
            }
            if (_paper != null && ReferenceEquals(_placedIn, manager.buildingRegistration) && _placedFor == state.Edition.Day && _paper.gameObject.activeInHierarchy)
            {
                return;
            }
            Dispose();
            Place(manager, state.Edition.Day);
        }

        private void Place(BuildingManager manager, int edition)
        {
            if (!Spot(manager, out Vector3 floor, out float heading))
            {
                return;
            }
            string id = PropLibrary.Has(PropId) ? PropId : PlainPropId;
            if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material))
            {
                return;
            }
            var made = new GameObject("RealLifeOverhaul Ledger Copy");
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            // The prop stands upright, front page to +Z: laid on its back it lies front page up, 0.4 m long.
            made.transform.SetPositionAndRotation(floor + Vector3.up * 0.004f, Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(-90f, 0f, 0f));
            _paper = made.transform;
            _pointer = LedgerPointer.Make(_paper, new Vector3(0f, 0.2f, 0.03f), new Vector3(0.66f, 0.46f, 0.16f));
            _placedIn = manager.buildingRegistration;
            _placedFor = edition;
            if (Meta.Dev.Verbose)
            {
                LedgerDirector.Logger?.Info("City Ledger check (the copy at home): today's paper lies inside the door at " + floor.ToString("F1", CultureInfo.InvariantCulture) + ", "
                                            + (_pointer != null ? "a click away: PASS" : "but cannot be pointed at (the game has no layer for it): FAIL") + ".");
            }
        }

        // On the floor a little ahead of where the player comes in and to one side, where the player can walk.
        private static bool Spot(BuildingManager manager, out Vector3 floor, out float heading)
        {
            floor = default;
            heading = 0f;
            List<ExitZone> exits = manager.exitZones;
            ExitZone entry = null;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.playerSpawnPoint == null || exit.isDriveInBay || exit.isElevator)
                {
                    continue;
                }
                if (entry == null || exit.isPrimarySpawnPoint)
                {
                    entry = exit;
                }
                if (exit.isPrimarySpawnPoint)
                {
                    break;
                }
            }
            if (entry == null)
            {
                return false;
            }
            Vector3 at = entry.playerSpawnPoint.position;
            Vector3 inward = entry.playerSpawnPoint.forward;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            inward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, inward);
            float[] ahead = { 0.85f, 0.85f, 1.25f, 1.25f, 0.6f };
            float[] aside = { 0.55f, -0.55f, 0.45f, -0.45f, 0.75f };
            for (int i = 0; i < ahead.Length; i++)
            {
                Vector3 wanted = at + inward * ahead[i] + right * aside[i];
                if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - at.y) < 0.4f)
                {
                    floor = hit.position;
                    heading = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg + 180f + (aside[i] > 0f ? -14f : 14f);   // read from the door: the page's foot towards the room
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (_paper != null)
            {
                UnityEngine.Object.Destroy(_paper.gameObject);
            }
            _paper = null;
            _pointer = null;
            _placedIn = null;
            _placedFor = 0;
        }
    }
}
