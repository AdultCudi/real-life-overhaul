using System;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The wall measured from the door, and the pieces put up on it: fascia, sign, canopy, lamps, shield, board, lettering.</summary>
    internal sealed partial class PrecinctFrontage
    {
        // The door's own frame, and how far out from the door's plane the wall face stands, where most rays agree.
        private void Measure()
        {
            _measured = true;
            if (_solidMask == 0)
            {
                _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
                _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
                if (_wallMask == 0)
                {
                    _wallMask = _solidMask;
                }
            }
            Vector3 toFront = _front - _dressed.Door;
            toFront.y = 0f;
            Vector3 outward = Precincts.Outward(Precincts.DoorOf(_dressed.Building), toFront);
            _out = outward.sqrMagnitude < 0.01f ? Vector3.forward : outward.normalized;
            _across = Vector3.Cross(Vector3.up, _out);
            float ground = CitySpots.Ground(_front, out float found) ? found : _front.y;
            _doorGround = new Vector3(_dressed.Door.x, ground, _dressed.Door.z);
            _doorSpot = StreetSpots.OnPavement(_doorGround + _out * 0.7f, 1.5f, out Vector3 step) ? step : _front;

            int count = 0;
            for (int i = 0; i < 6 && count < _depths.Length - 1; i++)
            {
                float aside = (1.4f + (i / 2) * 0.6f) * ((i & 1) == 0 ? -1f : 1f);
                if (Wall(aside, 1.3f, out float low))
                {
                    _depths[count++] = low;
                }
                if (Wall(aside, 2.2f, out float high))
                {
                    _depths[count++] = high;
                }
            }
            int best = 0;
            _wallKnown = false;
            for (int i = 0; i < count; i++)
            {
                int agree = 0;
                for (int j = 0; j < count; j++)
                {
                    agree += Mathf.Abs(_depths[i] - _depths[j]) < 0.12f ? 1 : 0;
                }
                if (agree > best)
                {
                    best = agree;
                    _wall = _depths[i];
                    _wallKnown = agree >= 2;
                }
            }
        }

        // How far out from the door's plane the wall face stands here. False with no upright wall facing the street (a gap, glass).
        private bool Wall(float aside, float height, out float depth)
        {
            Vector3 from = _doorGround + _across * aside + _out * RayOut + Vector3.up * height;
            if (Physics.Raycast(from, -_out, out RaycastHit hit, RayOut + 1.6f, _wallMask, QueryTriggerInteraction.Ignore) && Vector3.Dot(hit.normal, _out) > 0.88f)
            {
                depth = RayOut - hit.distance;
                return true;
            }
            depth = 0f;
            return false;
        }

        // The middle of a flat patch of wall of a size, with a clear box in front of it and nothing of ours on it already.
        private bool Patch(float aside, float height, float halfWide, float halfHigh, float proud, out Vector3 at, out string why)
        {
            at = default;
            if (!_wallKnown)
            {
                why = "no wall could be measured beside the door";
                return false;
            }
            if (Taken(aside - halfWide, aside + halfWide, height - halfHigh, height + halfHigh))
            {
                why = "that wall is taken by another piece";
                return false;
            }
            if (!Wall(aside, height, out float depth) || Mathf.Abs(depth - _wall) > 0.6f)
            {
                why = "no wall there";
                return false;
            }
            float inWide = halfWide * 0.9f;
            float inHigh = halfHigh * 0.9f;
            for (int corner = 0; corner < 4; corner++)
            {
                if (!Wall(aside + ((corner & 1) == 0 ? -inWide : inWide), height + (corner < 2 ? -inHigh : inHigh), out float other) || Mathf.Abs(other - depth) > FlatWithin)
                {
                    why = "the wall is not flat there";
                    return false;
                }
            }
            at = _doorGround + _across * aside + _out * depth + Vector3.up * height;
            if (Physics.CheckBox(at + _out * (proud * 0.5f + 0.03f), new Vector3(halfWide, halfHigh, proud * 0.5f), Quaternion.LookRotation(_out, Vector3.up),
                    _solidMask, QueryTriggerInteraction.Ignore))
            {
                why = "something is in the way";
                return false;
            }
            why = null;
            return true;
        }

        private bool Taken(float from, float to, float low, float high)
        {
            for (int i = 0; i < _patches.Count; i++)
            {
                Vector4 patch = _patches[i];
                if (from < patch.y + 0.08f && to > patch.x - 0.08f && low < patch.w + 0.08f && high > patch.z - 0.08f)
                {
                    return true;
                }
            }
            return false;
        }

        private void Take(float aside, float height, float halfWide, float halfHigh)
        {
            _patches.Add(new Vector4(aside - halfWide, aside + halfWide, height - halfHigh, height + halfHigh));
        }

        private void PlaceStatics()
        {
            using var piece = SessionWatch.Piece("law: a precinct's front being put out");
            if (!Shapes.Available)
            {
                _statics = true;
                for (int i = 0; i < _why.Length; i++)
                {
                    _why[i] = "the game's lit shader was not found";
                }
                return;
            }
            if (!_built)
            {
                _built = true;
                _builtModelled = PropLibrary.Ready;
            }
            _statics = true;
            _patches.Clear();
            Quaternion facing = Quaternion.LookRotation(_out, Vector3.up);
            PlaceFascia(facing);
            PlaceSign();
            PlaceAwning(facing);
            PlaceLamps(facing);
            PlaceWallPiece(Piece.Emblem, ref _emblem, EmblemPlaces, 0.42f, 0.42f, 0.07f, facing);
            PlaceWallPiece(Piece.Board, ref _board, BoardPlaces, 0.64f, 0.50f, 0.08f, facing);
            PlaceDoorText();
            PlaceKerb();
            _nightKnown = false;
            Light();
            if (_cull == null)
            {
                _cull = ModCulling.Add(CullRules.Kind.StationFront, _root, _dressed.Door, CullRadius);
            }
            _logger?.Info("Law: frontage: " + Report() + ".");
        }

        private void PlaceFascia(Quaternion facing)
        {
            HideSigns(out BuildingSignController nearest);
            if (nearest == null)
            {
                Skip(Piece.Fascia, "the building has no sign band of its own");
                return;
            }
            MeshFilter filter = nearest.signMeshRenderer != null ? nearest.signMeshRenderer.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                Skip(Piece.Fascia, "its sign band has no shape to measure");
                return;
            }
            Bounds box = mesh.bounds;
            Transform frame = filter.transform;
            float fromAside = float.MaxValue, toAside = float.MinValue, low = float.MaxValue, high = float.MinValue, back = float.MaxValue, face = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = frame.TransformPoint(box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                Vector3 offset = corner - _doorGround;
                float aside = Vector3.Dot(offset, _across);
                float depth = Vector3.Dot(offset, _out);
                fromAside = Mathf.Min(fromAside, aside);
                toAside = Mathf.Max(toAside, aside);
                low = Mathf.Min(low, offset.y);
                high = Mathf.Max(high, offset.y);
                back = Mathf.Min(back, depth);
                face = Mathf.Max(face, depth);
            }
            float wide = toAside - fromAside;
            float tall = high - low;
            if (wide < 1.2f || wide > 16f || tall < 0.25f || tall > 2.4f || low < 1.9f || face - back > 1.2f || Mathf.Abs(fromAside + toAside) * 0.5f > 12f)
            {
                Skip(Piece.Fascia, FormattableString.Invariant($"its sign band measures {wide:F1} x {tall:F1} m, {low:F1} m up: not a board over a shopfront"));
                return;
            }
            if (_fascia == null)
            {
                BuildFascia();
            }
            float thick = Mathf.Max(0.06f, face - back);
            _fascia.gameObject.SetActive(false);
            _fascia.SetPositionAndRotation(_doorGround + _across * ((fromAside + toAside) * 0.5f) + Vector3.up * ((low + high) * 0.5f) + _out * (face - thick * 0.5f), facing);
            _fasciaBoard.localScale = new Vector3(wide, tall, thick);
            float front = thick * 0.5f + 0.006f;
            bool both = wide >= 3.2f;
            if (_fasciaPolice != null)
            {
                _fasciaPolice.rectTransform.sizeDelta = new Vector2(both ? wide * 0.34f : wide * 0.86f, tall * 0.66f);
                _fasciaPolice.transform.localPosition = new Vector3(both ? wide * 0.27f : 0f, 0f, front);   // x runs to the reader's left
            }
            if (_fasciaName != null)
            {
                _fasciaName.gameObject.SetActive(both);
                _fasciaName.rectTransform.sizeDelta = new Vector2(wide * 0.50f, tall * 0.42f);
                _fasciaName.transform.localPosition = new Vector3(-wide * 0.20f, 0f, front);
                _fasciaName.text = _dressed.Name.ToUpperInvariant();
            }
            _fascia.gameObject.SetActive(true);
            Take((fromAside + toAside) * 0.5f, (low + high) * 0.5f, wide * 0.5f, tall * 0.5f);
            Fit(Piece.Fascia);
        }

        private void BuildFascia()
        {
            var holder = new GameObject("RealLifeOverhaul Precinct Fascia");
            _fascia = holder.transform;
            _fascia.SetParent(_root, false);
            _fasciaBoard = Shapes.Piece("Board", _fascia, Shapes.Cube, Shapes.Paint(StationProps.PoliceBlue, 0.5f), Vector3.zero, Vector3.one);
            _fasciaPolice = StationProps.Letter(_fascia, "POLICE", Vector3.zero, 1f, 0.5f);
            _fasciaName = StationProps.Letter(_fascia, string.Empty, Vector3.zero, 1f, 0.3f);
            holder.SetActive(false);
        }

        // Every sign of the building off for the visit (a station is not "for rent"); the wide one nearest the door is handed back.
        private void HideSigns(out BuildingSignController nearest)
        {
            nearest = null;
            CityBuildingController building = _dressed.Building;
            if (building == null)
            {
                return;
            }
            float best = float.MaxValue;
            BuildingSignController[] wide = building.GetComponentsInChildren<BuildingSignController>(true);
            for (int i = 0; i < wide.Length; i++)
            {
                BuildingSignController sign = wide[i];
                if (sign == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(sign.transform.position - _dressed.Door);
                if (sqr < best && sign.signMeshRenderer != null)
                {
                    best = sqr;
                    nearest = sign;
                }
                if (!_signsOff.Contains(sign))
                {
                    _signsOff.Add(sign);
                }
                if (sign.gameObject.activeSelf)
                {
                    sign.gameObject.SetActive(false);
                }
            }
            BuildingLogoSignController[] logos = building.GetComponentsInChildren<BuildingLogoSignController>(true);
            for (int i = 0; i < logos.Length; i++)
            {
                BuildingLogoSignController logo = logos[i];
                if (logo == null)
                {
                    continue;
                }
                if (!_logosOff.Contains(logo))
                {
                    _logosOff.Add(logo);
                }
                if (logo.gameObject.activeSelf)
                {
                    logo.gameObject.SetActive(false);
                }
            }
        }

        // The game switches a building's signs back on whenever its registration changes: looked at once a second.
        private void KeepSignsOff()
        {
            for (int i = 0; i < _signsOff.Count; i++)
            {
                if (_signsOff[i] != null && _signsOff[i].gameObject.activeSelf)
                {
                    _signsOff[i].gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < _logosOff.Count; i++)
            {
                if (_logosOff[i] != null && _logosOff[i].gameObject.activeSelf)
                {
                    _logosOff[i].gameObject.SetActive(false);
                }
            }
        }

        // The building's signs as the game would have them now, and the mod's front for its buildings after (Core/FrontSigns).
        private void RestoreSigns()
        {
            if (_signsOff.Count == 0 && _logosOff.Count == 0)
            {
                return;
            }
            CityBuildingController building = null;
            for (int i = 0; i < _signsOff.Count && building == null; i++)
            {
                building = _signsOff[i] != null ? _signsOff[i].GetComponentInParent<CityBuildingController>() : null;
            }
            for (int i = 0; i < _logosOff.Count && building == null; i++)
            {
                building = _logosOff[i] != null ? _logosOff[i].GetComponentInParent<CityBuildingController>() : null;
            }
            _signsOff.Clear();
            _logosOff.Clear();
            try
            {
                if (building != null && building.buildingRegistration != null && !GameManager.isCitySceneBeingUnloaded)
                {
                    FrontSigns.Refresh(building);   // the game's own call, then the mod's front for its buildings
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void PlaceSign()
        {
            if (!CitySpots.Along(_front, out Vector3 along))
            {
                along = _across;
            }
            string why = "no clear stretch of wall beside the door";
            for (int attempt = 0; attempt < SignAside.Length * 2; attempt++)
            {
                float aside = SignAside[attempt / 2] * ((attempt & 1) == 0 ? -1f : 1f);
                // Measured 0.57 m out from the wall with 0.6 m of room: the sign's strip is inside what was found clear.
                if (!CitySpots.Shopfront(_front + along * aside, SignFromWall + 0.45f, 0.6f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || CitySpots.Flat(at - _dressed.Door) < 1.3f * 1.3f)
                {
                    continue;
                }
                at.y = ground;
                float where = Vector3.Dot(at - _doorGround, _across);
                if (!SignClear(at, outward) || Taken(where - 0.95f, where + 0.95f, 0f, 3.0f))
                {
                    continue;
                }
                if (_sign == null)
                {
                    BuildSign();
                }
                _sign.gameObject.SetActive(false);
                _sign.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                if (_signName != null)
                {
                    _signName.text = _dressed.Name.ToUpperInvariant();
                }
                _sign.gameObject.SetActive(true);
                _signOut = true;
                _signAt = at;
                _signAcross = Vector3.Cross(Vector3.up, outward);
                Take(where, 1.5f, 0.95f, 1.5f);
                Fit(Piece.Sign);
                return;
            }
            Skip(Piece.Sign, why);
        }

        // The whole sign clear: the board over everybody's head, each post from the ground, the lamp on top.
        private bool SignClear(Vector3 at, Vector3 outward)
        {
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            if (Physics.CheckBox(at + Vector3.up * 2.45f, new Vector3(0.84f, 0.42f, 0.045f), Quaternion.LookRotation(outward, Vector3.up), _solidMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 post = at + across * (side * 0.86f);
                if (Physics.CheckCapsule(post + Vector3.up * 0.2f, post + Vector3.up * 3.1f, 0.05f, _solidMask, QueryTriggerInteraction.Ignore)
                    || !CitySpots.Ground(post, out float ground) || Mathf.Abs(ground - at.y) > 0.12f)
                {
                    return false;   // something in the way of a post, or the ground steps there
                }
            }
            return true;
        }

        private void BuildSign()
        {
            var holder = new GameObject("RealLifeOverhaul Precinct Sign");
            _sign = holder.transform;
            _sign.SetParent(_root, false);
            if (!Shapes.Prop(StationProps.Sign, _sign))
            {
                Material steel = Shapes.Paint(StationProps.Steel);
                Shapes.Piece("Post L", _sign, Shapes.Cube, steel, new Vector3(-0.86f, 1.45f, 0f), new Vector3(0.07f, 2.90f, 0.07f));
                Shapes.Piece("Post R", _sign, Shapes.Cube, steel, new Vector3(0.86f, 1.45f, 0f), new Vector3(0.07f, 2.90f, 0.07f));
                Shapes.Piece("Board", _sign, Shapes.Cube, Shapes.Paint(StationProps.PoliceBlue), new Vector3(0f, 2.45f, 0f), new Vector3(1.65f, 0.80f, 0.05f));
                Shapes.Piece("Band", _sign, Shapes.Cube, Shapes.Paint(StationProps.White), new Vector3(0f, 2.27f, 0.002f), new Vector3(1.65f, 0.025f, 0.05f));
                // The panel faces -Z as made (for a back): turned to face the pavement.
                if (PropLibrary.TryGet("back_panel_police", out Mesh letters, out Material lettering) && letters != null && lettering != null)
                {
                    Transform panel = Shapes.Piece("POLICE", _sign, letters, lettering, new Vector3(0f, 2.56f, 0.028f), new Vector3(5f, 5f, 1f));
                    panel.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                else
                {
                    StationProps.Letter(_sign, "POLICE", new Vector3(0f, 2.56f, 0.027f), 1.3f, 0.46f);
                }
            }
            var socket = new GameObject("Lamp");
            socket.transform.SetParent(_sign, false);
            socket.transform.localPosition = new Vector3(0.86f, 2.90f, 0f);
            if (!Shapes.Prop(StationProps.Lamp, socket.transform))
            {
                _lampGlass[2] = StationProps.LampStandIn(socket.transform);
            }
            _signName = StationProps.Letter(_sign, string.Empty, new Vector3(0f, 2.15f, 0.027f), 1.5f, 0.17f);
            holder.SetActive(false);
        }

        private void PlaceAwning(Quaternion facing)
        {
            const float under = 2.5f;
            if (_awning == null)
            {
                _awning = StationProps.Holder("RealLifeOverhaul Precinct Canopy", _root, StationProps.Awning, Pivot.Wall, new Vector3(2.4f, 0.5f, 1.1f), out _, out bool modelled);
                if (!modelled)
                {
                    // The stand-in hangs from its underside's back edge; a model from the middle of its back face.
                    var hung = new GameObject("Hung");
                    hung.transform.SetParent(_awning, false);
                    hung.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                    StationProps.AwningStandIn(hung.transform);
                }
            }
            if (!Patch(0f, under + 0.25f, 1.22f, 0.27f, 1.12f, out Vector3 at, out string why))
            {
                Skip(Piece.Awning, why);
                return;
            }
            // Clear above the heads of people coming out, and not over the road.
            if (!StreetSpots.OnPavement(_doorGround + _out * (_wall + 1.0f), 0.8f, out _))
            {
                Skip(Piece.Awning, "the pavement is not that deep at the door");
                return;
            }
            _awning.gameObject.SetActive(false);
            _awning.SetPositionAndRotation(at, facing);
            _awning.gameObject.SetActive(true);
            Take(0f, under + 0.25f, 1.24f, 0.30f);
            Fit(Piece.Awning);
        }

        private void PlaceLamps(Quaternion facing)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_lamps[i] == null)
                {
                    var holder = new GameObject("RealLifeOverhaul Precinct Lamp " + (i + 1));
                    _lamps[i] = holder.transform;
                    _lamps[i].SetParent(_root, false);
                    Transform socket = StationProps.LampBracket(_lamps[i]);
                    if (!Shapes.Prop(StationProps.Lamp, socket))
                    {
                        _lampGlass[i] = StationProps.LampStandIn(socket);
                    }
                    holder.SetActive(false);
                }
            }
            const float height = 2.2f;
            // Together first, then each by itself.
            for (int pass = 0; pass < 2 && !(_fits[(int)Piece.LampA] && _fits[(int)Piece.LampB]); pass++)
            {
                for (int step = 0; step < LampAside.Length; step++)
                {
                    float aside = LampAside[step];
                    bool left = Patch(-aside, height + 0.08f, 0.13f, 0.22f, 0.36f, out Vector3 leftAt, out string leftWhy);
                    bool right = Patch(aside, height + 0.08f, 0.13f, 0.22f, 0.36f, out Vector3 rightAt, out string rightWhy);
                    if (pass == 0 && !(left && right))
                    {
                        _why[(int)Piece.LampA] = leftWhy ?? rightWhy;
                        _why[(int)Piece.LampB] = rightWhy ?? leftWhy;
                        continue;
                    }
                    if (left && !_fits[(int)Piece.LampA])
                    {
                        PutLamp(0, leftAt - Vector3.up * 0.08f, facing, -aside, height);
                        Fit(Piece.LampA);
                    }
                    if (right && !_fits[(int)Piece.LampB])
                    {
                        PutLamp(1, rightAt - Vector3.up * 0.08f, facing, aside, height);
                        Fit(Piece.LampB);
                    }
                    if (_fits[(int)Piece.LampA] && _fits[(int)Piece.LampB])
                    {
                        break;
                    }
                }
            }
        }

        private void PutLamp(int index, Vector3 at, Quaternion facing, float aside, float height)
        {
            _lamps[index].gameObject.SetActive(false);
            _lamps[index].SetPositionAndRotation(at, facing);
            _lamps[index].gameObject.SetActive(true);
            Take(aside, height + 0.08f, 0.13f, 0.22f);
        }

        private void PlaceWallPiece(Piece piece, ref Transform holder, Vector2[] places, float halfWide, float halfHigh, float proud, Quaternion facing)
        {
            if (holder == null)
            {
                if (piece == Piece.Emblem)
                {
                    holder = StationProps.Holder("RealLifeOverhaul Precinct Shield", _root, StationProps.Emblem, Pivot.Wall, new Vector3(0.8f, 0.8f, 0.05f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.EmblemStandIn(holder);
                    }
                }
                else
                {
                    holder = StationProps.Holder("RealLifeOverhaul Precinct Notices", _root, StationProps.NoticeBoard, Pivot.Wall, new Vector3(1.2f, 0.9f, 0.05f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.NoticeBoardStandIn(holder);
                    }
                }
            }
            string why = "no flat wall";
            for (int i = 0; i < places.Length; i++)
            {
                if (!Patch(places[i].x, places[i].y, halfWide, halfHigh, proud, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                holder.gameObject.SetActive(false);
                holder.SetPositionAndRotation(at + _out * 0.004f, facing);
                holder.gameObject.SetActive(true);
                Take(places[i].x, places[i].y, halfWide, halfHigh);
                Fit(piece);
                return;
            }
            Skip(piece, why);
        }

        // POLICE on the door: only where the rays find the door itself, flat, at most a little behind the wall face.
        private void PlaceDoorText()
        {
            if (!_wallKnown || !Wall(0f, 1.55f, out float depth) || depth > _wall + 0.1f || depth < _wall - 0.8f
                || !Wall(-0.28f, 1.55f, out float left) || !Wall(0.28f, 1.55f, out float right) || Mathf.Abs(left - depth) > 0.04f || Mathf.Abs(right - depth) > 0.04f)
            {
                Skip(Piece.Door, "the door's face could not be found flat");
                return;
            }
            if (_doorText == null)
            {
                _doorText = StationProps.Letter(_root, "POLICE", Vector3.zero, 0.74f, 0.15f);
                if (_doorText == null)
                {
                    Skip(Piece.Door, "no font to letter it in");
                    return;
                }
                _doorText.gameObject.name = "RealLifeOverhaul Precinct Door";
            }
            _doorText.gameObject.SetActive(false);
            // Read from the street: the text's own forward points into the wall.
            _doorText.transform.SetPositionAndRotation(_doorGround + _out * (depth + 0.012f) + Vector3.up * 1.55f, Quaternion.LookRotation(-_out, Vector3.up));
            _doorText.gameObject.SetActive(true);
            Fit(Piece.Door);
        }

        private void Fit(Piece piece)
        {
            _fits[(int)piece] = true;
            _why[(int)piece] = null;
        }

        private void Skip(Piece piece, string why)
        {
            _fits[(int)piece] = false;
            _why[(int)piece] = why;
        }

        // Dusk and dawn: the lamps' glass changes between two shared paints. Looked at once a second.
        private void Light()
        {
            bool night;
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                night = gm != null && gm.timeOfDayController != null ? gm.timeOfDayController.ShouldOutdoorLightsBeOn() : Crime.IsNight;
            }
            catch (Exception)
            {
                night = Crime.IsNight;
            }
            if (_nightKnown && night == _night)
            {
                return;
            }
            _nightKnown = true;
            _night = night;
            Material paint = StationProps.LampPaint(night);
            for (int i = 0; i < _lampGlass.Length; i++)
            {
                if (_lampGlass[i] != null && paint != null)
                {
                    _lampGlass[i].sharedMaterial = paint;
                }
            }
        }
    }
}
