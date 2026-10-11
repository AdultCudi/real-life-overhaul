using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own dirt on a client's floor, for the length of one cleaning job.
    ///
    /// How the game does dirt. A building's floor is a grid of <c>DirtSpotObject</c>s that every interior carries (the
    /// player's shop and anybody else's alike); each reads its dirtiness, 0 to 100, from its place in the registration's
    /// <c>dirtSpots</c> list. Only a business the player rents is given that list (and dirtied, and cleaned by staff), so a
    /// client's list is empty or missing. The game's cleanliness (<c>BuildingCleanliness.GetCleanliness</c>) is
    /// 100 minus the floor's mean dirtiness minus the mean over the visible spots (5 or more), and exactly 100 when no
    /// spot shows dirt.
    ///
    /// What this does. It builds a list of its own for the client's floor (same cells, same order), puts dirt in it
    /// with the game's own call (<c>BuildingCleanliness.SetDirtiness</c>) and hangs the list on the client's registration while the
    /// player is inside, so the game's floor objects draw it, the game's mop cleans it and the game's own percentage
    /// reads it. Nothing else is touched:
    ///  - never a business the player rents (refused in <see cref="Lay"/>);
    ///  - the registration's own list is put back whenever the player leaves, when the job ends and while the game writes
    ///    a save (<see cref="Suspend"/>), so no job dirt can reach a save file;
    ///  - the job's list lives in this object only.
    /// </summary>
    public sealed class JobDirt
    {
        private const int FewestCells = 8;          // a floor with fewer cells than this is not a floor to clean
        private const int CellsPerUnit = 6;         // dirty cells at most, for each unit of the job
        private const float CoverLeast = 0.25f;     // share of the floor that is dirty, at least...
        private const float CoverMost = 0.55f;      // ...and at most (before the cap on cells)
        private const float StrengthBase = 55f;     // dirtiness of a cell in an eight-unit job, before its own spread
        private const float StrengthPerUnit = 4f;   // ...more for each unit above eight
        private const float Spread = 0.25f;         // each cell is this much dirtier or cleaner than that, at random
        private const float Visible = BuildingCleanliness.DirtinessNeededToShowVisualDirt;

        private BuildingRegistration _registration;
        private List<DirtSpot> _original;           // what the registration held before the job's list went on it
        private List<DirtSpot> _spots;              // the job's list
        private int[] _order;                       // the dirty cells, middle of a patch first
        private bool _wanted;                       // the job wants its list on the registration
        private bool _inPlace;                      // ...and it is there now

        /// <summary>True once dirt has been put down.</summary>
        public bool Laid => _spots != null;

        /// <summary>True while the job's list is on the client's registration.</summary>
        public bool InPlace => _inPlace;

        /// <summary>Cells the floor has, and how many of them were made dirty.</summary>
        public int Cells => _spots != null ? _spots.Count : 0;
        public int Dirtied => _order != null ? _order.Length : 0;

        /// <summary>The mean dirtiness of the cells that were dirtied, as laid.</summary>
        public float Mean { get; private set; }

        /// <summary>The game's own cleanliness of the floor now, 0 to 100. Only while the list is in place.</summary>
        public float Cleanliness => _inPlace ? _registration.Cleanliness.GetCleanliness() : 100f;

        /// <summary>True if the premises are somebody else's and the player is inside them.</summary>
        private bool Here()
        {
            return _registration != null && !_registration.RentedByPlayer && BuildingManager.IsInsideBuilding
                   && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.buildingRegistration, _registration);
        }

        /// <summary>
        /// Makes the client's floor dirty: <paramref name="units"/> sizes the job (an eight-unit job is the table's), the
        /// dice give where and how thickly. False, with the reason, if the game's dirt cannot be had here; nothing is
        /// then changed.
        /// </summary>
        public bool Lay(BuildingRegistration registration, int units, System.Random dice, out string why)
        {
            why = null;
            if (registration == null || registration.RentedByPlayer)
            {
                why = registration == null ? "no registration" : "the premises are the player's own";
                return false;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || !BuildingManager.IsInsideBuilding || !ReferenceEquals(manager.buildingRegistration, registration))
            {
                why = "the player is not inside the client's premises";
                return false;
            }
            List<DirtSpot> cells = registration.dirtSpots != null && registration.dirtSpots.Count > 0
                ? registration.dirtSpots
                : BuildingCleanlinessHelper.GetDirtSpotsForBuilding(manager.building);
            if (cells == null || cells.Count < FewestCells)
            {
                why = "the floor has " + (cells == null ? 0 : cells.Count) + " cells";
                return false;
            }
            if (!HasFloorObject())
            {
                why = "no dirt spot object under the player";
                return false;
            }
            _registration = registration;
            _spots = new List<DirtSpot>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                _spots.Add(new DirtSpot { x = cells[i].x, z = cells[i].z, dirtiness = 0f });
            }
            Adopt();
            if (!_inPlace)
            {
                why = "the list could not be put on the registration";
                Discard();
                return false;
            }
            Spoil(Mathf.Max(1, units), dice);
            Refresh();
            return true;
        }

        // The floor objects the player stands on: the game's own cleaning finds a cell by the same ray.
        private static bool HasFloorObject()
        {
            Vector3 at = PlayerHelper.GetPosition();
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero : new Vector3(i < 3 ? 1.5f : -1.5f, 0f, i % 2 == 0 ? 1.5f : -1.5f);
                if (Physics.Raycast(new Ray(at + offset + Vector3.up, Vector3.down), out RaycastHit hit, 600f, LayerHelper.groundLayerMask)
                    && hit.collider.TryGetComponent<DirtSpotObject>(out _))
                {
                    return true;
                }
            }
            return false;
        }

        // Patches rather than single cells, as spills are: some centres at random and the cells nearest to them, so a
        // click of the mop (which takes the cells round the one clicked) cleans several at once.
        private void Spoil(int units, System.Random dice)
        {
            int count = _spots.Count;
            int cap = Mathf.Max(FewestCells, units * CellsPerUnit);
            float cover = CoverLeast + (float)dice.NextDouble() * (CoverMost - CoverLeast);
            int cells = Mathf.Clamp(Mathf.RoundToInt(count * cover), FewestCells, Mathf.Min(cap, count));
            int centres = Mathf.Max(2, cells / 5);
            var at = new int[centres];
            for (int c = 0; c < centres; c++)
            {
                at[c] = dice.Next(count);
            }
            var distance = new float[count];
            var index = new int[count];
            for (int i = 0; i < count; i++)
            {
                float nearest = float.MaxValue;
                for (int c = 0; c < centres; c++)
                {
                    float dx = _spots[i].x - _spots[at[c]].x;
                    float dz = _spots[i].z - _spots[at[c]].z;
                    nearest = Mathf.Min(nearest, dx * dx + dz * dz);
                }
                distance[i] = nearest + (float)dice.NextDouble() * 0.5f;   // a little give, so the edge of a patch is not a ruler
                index[i] = i;
            }
            Array.Sort(distance, index);
            _order = new int[cells];
            float strength = StrengthBase + StrengthPerUnit * (units - 8);
            float total = 0f;
            BuildingCleanliness game = _registration.Cleanliness;
            for (int k = 0; k < cells; k++)
            {
                int cell = index[k];
                float dirt = Mathf.Clamp(strength * (1f - Spread + 2f * Spread * (float)dice.NextDouble()), Visible + 5f, BuildingCleanliness.MaxDirtiness);
                game.SetDirtiness(cell, dirt);   // the game's own call
                _order[k] = cell;
                total += dirt;
            }
            Mean = total / cells;
        }

        // ---------------------------------------------------------------- in and out of the registration

        /// <summary>Puts the job's list on the client's registration (the player is inside) and has the floor redrawn.</summary>
        public void Adopt()
        {
            _wanted = _spots != null;
            Put();
        }

        private void Put()
        {
            if (_wanted && !_inPlace && Here())
            {
                _original = _registration.dirtSpots;   // whatever the game holds now: it may have made a new list on entering
                _registration.dirtSpots = _spots;
                _inPlace = true;
                Refresh();
            }
        }

        private void Pull()
        {
            if (_inPlace)
            {
                _inPlace = false;
                _registration.dirtSpots = _original;
                _original = null;
            }
        }

        /// <summary>The player has left, or the job is over: the registration is as the game had it, the floor is drawn from that.</summary>
        public void Release()
        {
            _wanted = false;
            bool was = _inPlace;
            Pull();
            if (was)
            {
                Refresh();
            }
        }

        /// <summary>The game is writing a save: the registration is as the game had it until <see cref="Resume"/>.</summary>
        public void Suspend()
        {
            Pull();
        }

        /// <summary>Called on the job's tick: puts the list back after a save. Nothing to do (a flag test) otherwise.</summary>
        public void Resume()
        {
            if (_wanted && !_inPlace)
            {
                Put();
            }
        }

        /// <summary>Everything let go: nothing of the job is left on the registration.</summary>
        public void Discard()
        {
            Release();
            _spots = null;
            _order = null;
            _registration = null;
        }

        /// <summary>
        /// The clock cleans a share of the dirt that is left: that many of the dirty cells go to nothing, the patches'
        /// middles first, through the game's own call.
        /// </summary>
        public void Clean(float share)
        {
            if (_order == null)
            {
                return;
            }
            int dirty = 0;
            for (int k = 0; k < _order.Length; k++)
            {
                if (_spots[_order[k]].dirtiness > 0f)
                {
                    dirty++;
                }
            }
            int clean = share >= 1f ? dirty : Mathf.CeilToInt(Mathf.Clamp01(share) * dirty);
            BuildingCleanliness game = _inPlace ? _registration.Cleanliness : null;
            for (int k = 0; k < _order.Length && clean > 0; k++)
            {
                int cell = _order[k];
                if (_spots[cell].dirtiness <= 0f)
                {
                    continue;
                }
                if (game != null)
                {
                    game.SetDirtiness(cell, 0f);
                }
                else
                {
                    _spots[cell].dirtiness = 0f;
                }
                clean--;
            }
            Refresh();
        }

        // The game's own redraw of the floor of the building the player is in.
        private void Refresh()
        {
            if (Here())
            {
                InstanceBehavior<BuildingManager>.Instance.UpdateDirtinessInCurrentBuilding();
            }
        }
    }
}
