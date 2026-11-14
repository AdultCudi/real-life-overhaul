using System.Collections.Generic;
using BAModAPI;
using Controllers;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Inside Manhattan Business School while an exam of the player's is open there: every study desk where it can be sat carries the
    /// game's own mark over it and says "Exam desk" where the game names what is under the pointer, and the game's destination marker
    /// is on the nearest desk. Everything is put back as it was when the exam is over or the player leaves. The cost: once a second
    /// in the school with an exam open, a flag test; the building's items are looked through when the player comes in or its furniture changes.
    /// </summary>
    internal sealed class ExamDesks
    {
        private const string HeaderKey = "reallife_study_desk_exam";
        private const int Most = 24;   // desks marked at once

        private readonly CampusMarks _marks;
        private readonly GuideMark _mark;
        private readonly List<EducationDoorController> _doors = new List<EducationDoorController>(8);
        private readonly string[] _headers = new string[Most];
        private BuildingManager _manager;
        private int _items = -1;
        private bool _up;
        private string _name;

        public ExamDesks(IModLogger logger, CareersDirector director)
        {
            _marks = new CampusMarks(logger, Most);
            _mark = new GuideMark("the school's study desks while an exam is open").Keep(director).While(() => _up);
        }

        /// <summary>True while the desks are marked.</summary>
        public bool Up => _up;

        /// <summary>Once a second: marks the desks while <paramref name="wanted"/> (an exam open at the school, the player inside it); takes the marks off otherwise.</summary>
        public void Tick(bool wanted)
        {
            if (!wanted)
            {
                if (_up)
                {
                    Clear();
                }
                return;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            List<ItemController> all = manager != null ? manager.allItemControllers : null;
            if (all == null)
            {
                return;
            }
            if (!_up || !ReferenceEquals(manager, _manager) || all.Count != _items)
            {
                Find(manager, all);
            }
            PlayerController player = PlayerHelper.PlayerController;
            EducationDoorController near = null;
            float best = float.MaxValue;
            for (int i = 0; i < _doors.Count; i++)
            {
                EducationDoorController door = _doors[i];
                if (door == null)
                {
                    continue;
                }
                if (player != null)
                {
                    float d = (door.transform.position - player.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        near = door;
                    }
                }
            }
            _name = _name ?? CareersText.T(HeaderKey);
            if (near != null && best > NearMeters * NearMeters)
            {
                _mark.Show(near.transform.position, _name);   // the desks in sight need no marker
            }
            else
            {
                _mark.Hide();
            }
        }

        private const float NearMeters = 4f;

        // The study desks of the building: the doors that open the game's study panel and have their chair, marked and named.
        private void Find(BuildingManager manager, List<ItemController> all)
        {
            Clear();
            _manager = manager;
            _items = all.Count;
            for (int i = 0; i < all.Count && _doors.Count < Most; i++)
            {
                if (all[i] is EducationDoorController door && door.gameObject.activeInHierarchy && door.StudyDiploma != null && door.StudyDiploma.chair != null)
                {
                    _headers[_doors.Count] = door.customOverlayHeaderKey;
                    door.customOverlayHeaderKey = HeaderKey;
                    _marks.Set(_doors.Count, door, RoomMark.Soon);
                    _doors.Add(door);
                }
            }
            _up = true;
            CareersDirector.Logger?.Info("Careers: an exam of the player's is open at the school; " + _doors.Count + " study desk(s) marked as exam desks.");
        }

        /// <summary>Every mark taken off and every desk's own name given back; safe when the building is gone.</summary>
        public void Clear()
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                if (_doors[i] != null)
                {
                    _doors[i].customOverlayHeaderKey = _headers[i];
                }
                _headers[i] = null;
            }
            _doors.Clear();
            _marks.Clear();
            _mark.Hide();
            _manager = null;
            _items = -1;
            _up = false;
        }
    }
}
