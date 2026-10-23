using System;
using System.Collections;
using System.Globalization;
using BAModAPI;
using Controllers;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A floor's classrooms: once the floors core has laid the school's layout, its doors found, named and signed, and their marks.</summary>
    internal sealed partial class CampusInterior
    {
        // The school's layout stands (the floors core laid it over the frames the game keeps the screen dark): its rooms.
        private void Laid(FloorVisit v, FloorLaid laid)
        {
            if (!ReferenceEquals(v, _visit))
            {
                return;
            }
            try
            {
                Rooms(Manager);
                Label();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _logger?.Info("Careers: the university's floor " + Floor + " is laid: " + laid.Made + " pieces of the game's school"
                          + (laid.Skipped > 0 ? " (" + laid.Skipped + " left out)" : string.Empty)
                          + " over " + laid.Frames + " frames, " + laid.Ms + " ms in all; rooms: " + RoomsWords() + ".");
            Tell(NotificationType.Info, "reallife_campus_note_floor", "floor", Floor.ToString(), "courses", RoomsList());
            int course = _layCourse;
            bool begin = _layBegin;
            _layCourse = -1;
            _layBegin = false;
            if (course >= 0)
            {
                _director.StartCoroutine(Guard.Routine(ToRoom(v, course, begin)));
            }
        }

        private IEnumerator ToRoom(FloorVisit v, int course, bool begin)
        {
            yield return null;   // the doors have found their chairs: their Start has run
            if (ReferenceEquals(v, _visit))
            {
                GoToRoom(course, begin);
            }
        }

        // The lift to another floor, under the dark screen: the same five rooms given that floor's courses.
        private void Moved(FloorVisit v, int fromFloor)
        {
            Floor = v.Floor;
            Label();
            _logger?.Info("Careers: the player takes the lift from floor " + fromFloor + " to floor " + v.Floor + " of the university (at " + Clock() + "); rooms: " + RoomsWords() + ".");
        }

        // The lift is over and the screen light again: the floor's courses said, and on to the classroom asked for.
        private void Shown(FloorVisit v)
        {
            Tell(NotificationType.Info, "reallife_campus_note_floor", "floor", Floor.ToString(), "courses", RoomsList());
            int course = _goCourse;
            bool begin = _goBegin;
            _goCourse = -1;
            _goBegin = false;
            if (course >= 0)
            {
                GoToRoom(course, begin);
            }
        }

        // The floor's classroom doors by room, and each room's chair found as the game's door finds its own (the first seat named like it).
        private void Rooms(BuildingManager manager)
        {
            Array.Clear(_rooms, 0, _rooms.Length);
            Array.Clear(_chairs, 0, _chairs.Length);
            System.Collections.Generic.List<ItemController> all = manager != null ? manager.allItemControllers : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] is EducationDoorController door)
                {
                    int room = CampusRules.RoomOfDoor(door.customValue);
                    if (room > 0)
                    {
                        _rooms[room - 1] = door;
                    }
                }
                else if (all[i] is SeatController seat)
                {
                    int room = CampusRules.RoomOfDoor(seat.customValue);
                    if (room > 0 && _chairs[room - 1] == null)
                    {
                        _chairs[room - 1] = seat.transform;
                    }
                }
            }
        }

        // Each classroom door named with its course where the game names what is under the pointer, and a sign over it.
        private void Label()
        {
            TMP_FontAsset font = GameFont.Asset;
            for (int r = 0; r < _rooms.Length; r++)
            {
                EducationDoorController door = _rooms[r];
                if (door == null)
                {
                    continue;
                }
                int course = CampusRules.CourseAt(Floor, r + 1);
                door.customOverlayHeaderKey = course >= 0 ? "reallife" + "_study_course_" + StudyCatalogue.Find(course).Key : "reallife_campus_room_free";
                string words = course >= 0
                    ? CareersText.T("reallife_campus_room_sign", "room", (r + 1).ToString(), "course", CareersText.Course(course))
                    : CareersText.T("reallife_campus_room_sign", "room", (r + 1).ToString(), "course", CareersText.T("reallife_campus_room_free"));   // "Room 3: Free classroom"
                if (_signs[r] == null && font != null)
                {
                    _signs[r] = Sign(door.transform, _chairs[r], font, r + 1, _logger);
                }
                TextMeshPro text = _signs[r] != null ? _signs[r].GetComponentInChildren<TextMeshPro>() : null;
                if (text != null)
                {
                    text.text = words;
                }
            }
            Marks();
        }

        // Once a second on a floor, and when a floor is laid or changed: the game's mark over each classroom door.
        private void Marks()
        {
            for (int r = 0; r < _rooms.Length; r++)
            {
                if (_rooms[r] != null)
                {
                    _marks.Set(r, _rooms[r], _director.RoomMarkOf(CampusRules.CourseAt(Floor, r + 1)));
                }
            }
        }

        private const float SignHeight = 2.45f;   // metres over the door's foot: above the doorway
        private const float SignOut = 0.2f;       // metres out from the door's plane, clear of its frame

        // The room's sign over its door, facing out of the room (CampusRules.SignSide) and drawn from the front only, so never mirrored.
        private static GameObject Sign(Transform door, Transform chair, TMP_FontAsset font, int room, IModLogger logger)
        {
            Vector3 forward = door.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 at = door.position, from = chair != null ? chair.position : at;
            int side = CampusRules.SignSide(forward.x, forward.z, at.x, at.z, from.x, from.z, chair != null);
            Vector3 outward = forward * side;
            var root = new GameObject("RealLifeOverhaul University Room Sign");
            Transform place = root.transform;
            place.SetParent(door, false);
            place.SetPositionAndRotation(at + Vector3.up * SignHeight + outward * SignOut, Quaternion.LookRotation(outward, Vector3.up));
            Material plaque = Shapes.Paint(new Color(0.93f, 0.92f, 0.89f));
            if (plaque != null)
            {
                Shapes.Piece("Plaque", place, Shapes.Cube, plaque, new Vector3(0f, 0f, -0.015f), new Vector3(1.62f, 0.36f, 0.02f));
            }
            var label = new GameObject("Lettering");
            Transform letters = label.transform;
            letters.SetParent(place, false);
            letters.localPosition = Vector3.zero;
            letters.localRotation = Quaternion.Euler(0f, 180f, 0f);   // lettering is read from behind its own forward: from outside the room
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            Material look = SignLook(font);
            if (look != null)
            {
                text.fontSharedMaterial = look;
            }
            text.color = new Color(0.12f, 0.12f, 0.14f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(1.5f, 0.3f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 1.6f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (Meta.Dev.Verbose)
            {
                logger?.Info("Careers check (university room sign): room " + room + ": one face, out of the room ("
                             + (chair != null
                                 ? "the room's chair " + new Vector2(from.x - at.x, from.z - at.z).magnitude.ToString("0.0", CultureInfo.InvariantCulture) + " m away on the other side"
                                 : "no chair found, the door's own forward")
                             + "), " + (plaque != null ? "on its plaque" : "no plaque") + ", " + (look != null ? "drawn from the front only" : "the font's own material") + ": PASS");
            }
            return root;
        }

        private string RoomsWords()
        {
            var b = new System.Text.StringBuilder(160);
            for (int r = 1; r <= CampusRules.RoomsPerFloor; r++)
            {
                int course = CampusRules.CourseAt(Floor, r);
                b.Append(r == 1 ? string.Empty : "; ").Append(r).Append(' ').Append(course >= 0 ? StudyCatalogue.Find(course).Key : "free")
                 .Append(_rooms[r - 1] != null ? string.Empty : " (no door)");
            }
            return b.ToString();
        }

        private string RoomsList()
        {
            var b = new System.Text.StringBuilder(160);
            for (int r = 1; r <= CampusRules.RoomsPerFloor; r++)
            {
                int course = CampusRules.CourseAt(Floor, r);
                if (course >= 0)
                {
                    b.Append(b.Length > 0 ? ", " : string.Empty).Append(r).Append(' ').Append(CareersText.Course(course));
                }
            }
            return b.ToString();
        }

        private void Tell(NotificationType type, string key, string a, string av, string b, string bv)
        {
            _director.Tell(type, key, CareersDirector.Data(a, av, b, bv), CareersDirector.TabStudying, null);
        }
    }
}
