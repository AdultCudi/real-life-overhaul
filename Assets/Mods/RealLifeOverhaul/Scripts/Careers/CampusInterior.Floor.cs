using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.Items;
using Controllers;
using LayoutItem = BusinessLayoutSets.Item;
using Helpers;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A floor's furniture: the school's layout laid in, the classroom doors found, named and signed, and their marks.</summary>
    internal sealed partial class CampusInterior
    {
        // The school's layout laid in over the frames the game keeps the screen dark; the classroom doors last.
        private IEnumerator Lay(int built, int goCourse, bool begin)
        {
            var watch = Stopwatch.StartNew();
            BuildingManager manager = Manager;
            List<LayoutItem> items = _set.Items;
            var order = new List<LayoutItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (!IsDoor(items[i].itemName))
                {
                    order.Add(items[i]);
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (IsDoor(items[i].itemName))
                {
                    order.Add(items[i]);
                }
            }
            var made = new Dictionary<string, KeyValuePair<ItemController, List<AttachableChild>>>(order.Count);
            try
            {
                var elements = new List<InteriorElement>(InteriorElementsHelper.InteriorElementsCache.Values);
                BuildingManager.ApplyInteriorDesign(_set.interiorDesigns, elements.ToArray());
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            int per = (order.Count + Chunks - 1) / Chunks, at = 0, frames = 0, skipped = 0;
            while (at < order.Count)
            {
                yield return null;
                frames++;
                if (built != _built || !BuildingManager.IsInsideBuilding)
                {
                    yield break;
                }
                long before = watch.ElapsedMilliseconds;
                for (int n = 0; n < per && at < order.Count; n++, at++)
                {
                    try
                    {
                        ItemController made1 = Place(order[at], manager.IndoorItemContainer);
                        if (made1 != null)
                        {
                            made[order[at].id] = new KeyValuePair<ItemController, List<AttachableChild>>(made1, order[at].stackedItems);
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        _logger?.Error(e);
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Careers check (university floor): frame " + frames + ", " + at + " of " + order.Count + " pieces, " + (watch.ElapsedMilliseconds - before) + " ms: PASS");
                }
            }
            try
            {
                foreach (KeyValuePair<string, KeyValuePair<ItemController, List<AttachableChild>>> pair in made)
                {
                    ItemController parent = pair.Value.Key;
                    List<AttachableChild> children = pair.Value.Value;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        if (made.TryGetValue(children[i].childId, out KeyValuePair<ItemController, List<AttachableChild>> child) && parent.AttachmentPoints.Length > children[i].attachmentIndex)
                        {
                            child.Key.SetToParentPlaceableItem(parent, parent.AttachmentPoints[children[i].attachmentIndex]);
                        }
                    }
                }
                manager.ForceUpdateAvailableProducers();   // the game reads the pieces again: the classroom doors find their chairs
                Rooms(manager);
                Label();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _logger?.Info("Careers: the university's floor " + Floor + " is laid: " + made.Count + " pieces of the game's school" + (skipped > 0 ? " (" + skipped + " left out)" : string.Empty)
                          + " over " + frames + " frames, " + watch.ElapsedMilliseconds + " ms in all; rooms: " + RoomsWords() + ".");
            Tell(NotificationType.Info, "reallife_campus_note_floor", "floor", Floor.ToString(), "courses", RoomsList());
            if (goCourse >= 0)
            {
                yield return null;   // the doors have found their chairs: their Start has run
                GoToRoom(goCourse, begin);
            }
        }

        // One piece of a layout, the way BuildingManager.LoadBusinessLayoutSet makes it.
        private static ItemController Place(LayoutItem item, Transform container)
        {
            BigAmbitions.Items.Item data = ItemsGetter.GetByName(item.itemName);
            if (data == null || data.isSeasonalForSale || (!data.IsAvailableInCurrentSeason() && !BuildingManager.ignoreSeasons))
            {
                return null;
            }
            ItemController c = PrefabHelper.CreatePrefabItem(item.itemName, container);
            if (c == null)
            {
                return null;
            }
            c.playerItemPurchaserSettings = new PlayerItemPurchaserSettings
            {
                enabled = item.playerItemPurchaserSettings != null && item.playerItemPurchaserSettings.enabled,
                itemName = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemName : string.Empty,
                itemQuantity = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemQuantity : 1
            };
            c.itemName = item.itemName;
            c.customPositions = item.customPositions;
            c.customColors = item.customColors;
            c.customValue = item.customValue;
            c.transform.position = item.position;
            c.transform.rotation = item.rotation;
            c.ItemInstance = ItemHelper.InitializeNewInstance(item.itemName);
            c.ItemInstance.id = item.id;
            c.ItemInstance.worldSpaceTextValue = item.worldSpaceTextValue;
            c.ItemInstance.linkedItemName = item.linkedItemName;
            c.gameObject.SetActive(true);
            return c;
        }

        // The floor's classroom doors by room, and each room's chair found as the game's door finds its own (the first seat named like it).
        private void Rooms(BuildingManager manager)
        {
            Array.Clear(_rooms, 0, _rooms.Length);
            Array.Clear(_chairs, 0, _chairs.Length);
            List<ItemController> all = manager.allItemControllers;
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
