using System;
using System.Collections.Generic;
using System.IO;
using BigAmbitions.SaveSystem;
using Blueprints;
using BusinessLayoutSets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>The game's own design for the unit: an office design's walls, floors and furniture, or a shop design's look alone.</summary>
    internal sealed partial class StationInterior
    {
        private static readonly string[] OfficeKinds =
        {
            "LawFirm", "Headquarters", "TravelAgency", "Bank", "IRS", "RecruitmentAgency", "WebDevelopmentAgency", "EventPlanningAgency",
            "MarketingAgency", "GraphicDesigner"
        };

        private static readonly string[] LookOnlyKinds = { "ElectronicsStore", "Bookstore", "GiftShop", "JewelryStore", "Supermarket" };

        // A design's items that are a business's trade, not an office's furniture.
        private static readonly string[] NotForAStation =
        {
            "forsale", "jobboard", "signwithtext", "productsign", "menuboard", "deliverysign", "deliveryspot", "cashregister", "checkout",
            "shoppingbasket", "spawner", "showcase", "cardboardcutout", "christmas", "halloween", "jackolantern", "seasonal", "neon", "candycane"
        };

        private static readonly Dictionary<string, BusinessLayoutSet> Designs = new Dictionary<string, BusinessLayoutSet>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> DesignFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private string _designName;
        private InteriorElement[] _elements;
        private Buildings.Building _lookOn;

        private static List<string> FilesFor(string kind, string size)
        {
            string key = kind + "|" + size;
            if (DesignFiles.TryGetValue(key, out List<string> files))
            {
                return files;
            }
            files = new List<string>(4);
            try
            {
                string root = Application.streamingAssetsPath;
                string designs = Path.Combine(root, "InteriorDesigns");
                if (Directory.Exists(designs))
                {
                    foreach (string type in Directory.GetDirectories(designs))
                    {
                        string folder = Path.Combine(type, size, kind);
                        if (Directory.Exists(folder))
                        {
                            files.AddRange(Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly));
                        }
                    }
                }
                string layouts = Path.Combine(root, "BusinessLayouts", kind, size);
                if (Directory.Exists(layouts))
                {
                    files.AddRange(Directory.GetFiles(layouts, "*.json", SearchOption.TopDirectoryOnly));
                }
            }
            catch (Exception)
            {
                files.Clear();
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            DesignFiles[key] = files;
            return files;
        }

        private static BusinessLayoutSet Read(string path)
        {
            if (Designs.TryGetValue(path, out BusinessLayoutSet set))
            {
                return set;
            }
            try
            {
                set = BusinessLayoutSetHelper.Deserialize(path);
            }
            catch (Exception)
            {
                set = null;
            }
            Designs[path] = set;
            return set;
        }

        // The design for this unit: an office one if the size has one (each precinct its own pick, so two stations of one
        // size differ), otherwise a shop one for the look alone. Null if the game has none for this size.
        private BusinessLayoutSet FindDesign(out bool furnish)
        {
            furnish = false;
            Buildings.Building building = _building != null ? _building.building : null;
            if (building == null || string.IsNullOrEmpty(building.BuildingSize))
            {
                return null;
            }
            string size = new BuildingSizeInfo(building).ToString();
            for (int pass = 0; pass < 2; pass++)
            {
                string[] kinds = pass == 0 ? OfficeKinds : LookOnlyKinds;
                for (int k = 0; k < kinds.Length; k++)
                {
                    List<string> files = FilesFor(kinds[k], size);
                    for (int tried = 0; tried < files.Count; tried++)
                    {
                        string path = files[(Precinct.Index + tried) % files.Count];
                        BusinessLayoutSet set = Read(path);
                        if (set == null || set.BuildingSize != building.BuildingSize || set.BuildingVersion != building.BuildingVersion)
                        {
                            continue;   // a design for another version of the size: another room
                        }
                        furnish = pass == 0;
                        _designName = kinds[k] + "/" + Path.GetFileNameWithoutExtension(path) + " (" + size + ")";
                        return set;
                    }
                }
            }
            return null;
        }

        private BusinessLayoutSet _design;
        private bool _designFurnishes;

        private void PlaceDesign()
        {
            _design = FindDesign(out _designFurnishes);
            if (_design == null)
            {
                _fitted.Append("no design of the game's for this unit; ");
                return;
            }
            if (!_designFurnishes || _design.Items == null || _design.Items.Count == 0)
            {
                _fitted.Append("the walls and floors of ").Append(_designName).Append("; ");
                return;
            }
            // Does the design stand in this room? Its floor pieces must be on this room's floor.
            int floorItems = 0, onFloor = 0;
            for (int i = 0; i < _design.Items.Count; i++)
            {
                Item item = _design.Items[i];
                Vector3 at = item.position;
                if (item.itemName == null || at.y - _floor > 0.15f || at.y - _floor < -0.3f)
                {
                    continue;
                }
                floorItems++;
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, 1.0f, _filter) && Mathf.Abs(hit.position.y - _floor) < 0.3f)
                {
                    onFloor++;
                }
            }
            if (floorItems < 4 || onFloor < floorItems * 0.7f)
            {
                _logger?.Info($"Law: {Precinct.Name}: the game's design {_designName} does not stand in this room ({onFloor} of {floorItems} pieces on its floor); only its own furniture.");
                _designFurnishes = false;
                return;
            }
            int first = _staged.Count, staged = 0, clash = 0, hungBefore = _hung.Count;
            for (int i = 0; i < _design.Items.Count; i++)
            {
                Item item = _design.Items[i];
                if (string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                string id = item.itemName.GetIdWithoutType();
                if (!ForAStation(id))
                {
                    continue;
                }
                Vector3 at = item.position;
                Quaternion facing = item.rotation;
                float height = at.y - _floor;
                bool lamp = id.Contains("ceilinglamp") || id.Contains("chandelier");
                bool flat = id.Contains("carpet") || id.Contains("rug");
                Transform piece = Piece(id, out Bounds size);
                if (piece == null)
                {
                    continue;
                }
                bool floor = !lamp && !flat && height < 0.15f;
                int index = Stage(piece, size, at, facing, floor, -1, SeatUse.None);
                staged++;
                Staged s = _staged[index];
                if (floor)
                {
                    // Not on anything the station has placed, nor on a way it keeps.
                    FloorRect r = s.Rect;
                    if (IsTaken(r.A, r.D, r.HalfA, r.HalfD) || OverlapsFixed(r, 0.05f) || OverlapsStagedBefore(r, first, 0.05f))
                    {
                        s.Dropped = true;
                        clash++;
                    }
                }
                else if (flat)
                {
                    // A carpet: anywhere but under the counter, the bench or in the cell.
                    s.Dropped = OverlapsFixed(s.Rect, 0f);
                    _designCarpet |= !s.Dropped;
                }
                else if (lamp)
                {
                    _designLamps = true;
                }
                _staged[index] = s;
            }
            // What stands on something goes with it (a computer on a desk); a chair goes with the desk or table it is at.
            for (int i = first; i < _staged.Count; i++)
            {
                Staged s = _staged[i];
                float height = s.At.y - _floor;
                string name = s.Piece.name;
                if (!s.Floor && height >= 0.15f && !IsLamp(name))
                {
                    s.Anchor = Under(s, first);
                    if (s.Anchor < 0)
                    {
                        // On a wall: not where the notice board or the evidence door hangs.
                        bool near = (_board != null && _board.gameObject.activeSelf && CitySpots.Flat(_board.position - s.At) < 1.5f * 1.5f)
                                    || (_evidence != null && _evidence.gameObject.activeSelf && CitySpots.Flat(_evidence.position - s.At) < 1.4f * 1.4f);
                        for (int h = 0; h < hungBefore && !near; h++)
                        {
                            near = CitySpots.Flat(_hung[h] - s.At) < 1.2f * 1.2f && Mathf.Abs(_hung[h].y - s.At.y) < 1.4f;   // the station's own screen
                        }
                        s.Dropped |= near;
                        if (!s.Dropped)
                        {
                            _hung.Add(s.At);
                        }
                    }
                }
                else if (s.Floor && IsSeat(name))
                {
                    s.Anchor = Beside(s, first, out bool desk);
                    s.Use = desk ? SeatUse.Desk : SeatUse.Break;
                }
                else if (s.Floor && (name.IndexOf("sofa", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("armchair", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    s.Use = SeatUse.Break;
                }
                _staged[i] = s;
            }
            // Whatever is kept is taken: what goes along the walls afterwards keeps off it.
            int kept = 0;
            for (int i = first; i < _staged.Count; i++)
            {
                if (_staged[i].Anchor >= 0 && _staged[_staged[i].Anchor].Dropped)
                {
                    Drop(i);
                }
                if (_staged[i].Dropped)
                {
                    continue;
                }
                kept++;
                if (_staged[i].Floor)
                {
                    FloorRect r = _staged[i].Rect;
                    Take(r.A, r.D, r.HalfA, r.HalfD);
                }
            }
            _fitted.Append("the game's design ").Append(_designName).Append(": ").Append(kept).Append(" of ").Append(staged)
                .Append(" pieces (").Append(clash).Append(" in the station's way); ");
        }

        private static bool ForAStation(string id)
        {
            for (int i = 0; i < NotForAStation.Length; i++)
            {
                if (id.IndexOf(NotForAStation[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsLamp(string name)
        {
            return name.IndexOf("CeilingLamp", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Chandelier", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSeat(string name)
        {
            return (name.IndexOf("chair", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("stool", StringComparison.OrdinalIgnoreCase) >= 0)
                   && name.IndexOf("hairdresser", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("cinema", StringComparison.OrdinalIgnoreCase) < 0
                   && name.IndexOf("theater", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("lounger", StringComparison.OrdinalIgnoreCase) < 0
                   && name.IndexOf("deckchair", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private bool OverlapsStagedBefore(FloorRect rect, int before, float margin)
        {
            for (int i = 0; i < before; i++)
            {
                if (_staged[i].Floor && !_staged[i].Dropped && _staged[i].Rect.Overlaps(rect, margin))
                {
                    return true;
                }
            }
            return false;
        }

        // The floor piece a raised piece stands on: the smallest whose floor it is over. -1 if none (it hangs on a wall).
        private int Under(Staged s, int first)
        {
            float a = AsideOf(s.At), d = DepthOf(s.At);
            int best = -1;
            float lowest = float.MaxValue;
            for (int i = first; i < _staged.Count; i++)
            {
                Staged other = _staged[i];
                if (!other.Floor || !other.Rect.Contains(a, d, 0.05f))
                {
                    continue;
                }
                float area = other.Rect.HalfA * other.Rect.HalfD;
                if (area < lowest)
                {
                    lowest = area;
                    best = i;
                }
            }
            return best;
        }

        private int Beside(Staged chair, int first, out bool desk)
        {
            desk = false;
            int best = -1;
            float bestSqr = 1.3f * 1.3f;
            for (int i = first; i < _staged.Count; i++)
            {
                Staged other = _staged[i];
                string name = other.Piece != null ? other.Piece.name : string.Empty;
                bool isDesk = name.IndexOf("desk", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isTable = name.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!other.Floor || (!isDesk && !isTable))
                {
                    continue;
                }
                float sqr = CitySpots.Flat(other.At - chair.At);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                    desk = isDesk;
                }
            }
            return best;
        }

        private void ApplyLook()
        {
            if (_design == null || _design.interiorDesigns == null || _design.interiorDesigns.Count == 0 || _building == null || _building.currentBuildingVersion == null)
            {
                return;
            }
            try
            {
                _elements = _building.currentBuildingVersion.GetComponentsInChildren<InteriorElement>(true);
                if (_elements.Length == 0)
                {
                    return;
                }
                BuildingManager.ApplyInteriorDesign(_design.interiorDesigns, _elements);
                _lookOn = _building.building;
                _fitted.Append("walls and floors of ").Append(_designName).Append("; ");
            }
            catch (Exception e)
            {
                _logger?.Warn("Law: the game's walls and floors could not be put on the station (" + e.Message + "); it keeps the unit's own.");
                _lookOn = null;
            }
        }

        // The unit's own look back if the player is still in it; out of it the game puts it back whenever it loads the unit.
        private void RestoreLook()
        {
            Buildings.Building on = _lookOn;
            InteriorElement[] elements = _elements;
            _lookOn = null;
            _elements = null;
            _design = null;
            if (on == null || elements == null)
            {
                return;
            }
            try
            {
                BuildingManager buildings = _building;
                if (BuildingManager.IsInsideBuilding && buildings != null && buildings.building == on && !buildings.exitingBuilding)
                {
                    BuildingManager.ApplyInteriorDesign(on, elements);
                }
            }
            catch (Exception e)
            {
                _logger?.Warn("Law: the unit's own walls and floors could not be put back (" + e.Message + "); the game puts them back when it next loads the unit.");
            }
        }
    }
}
