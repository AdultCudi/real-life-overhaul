using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The two newspaper kiosks near the player's home, against a wall on the pavement (CitySpots.Shopfront), found
    /// once for each home and kept in the save. The cost: once a second a distance to each (and three buildings in a
    /// search); per frame nothing unless the player is near one, then the counter's panel, with nothing allocated.
    /// </summary>
    public sealed class LedgerKiosks
    {
        private const string PropId = "newspaper_kiosk";
        private const float WallGap = 0.80f;       // metres from the wall to the kiosk's middle: its back stands 0.2 m off
        private const float CounterOut = 1.25f;    // metres from its middle to where a customer stands
        private const float HalfWidth = 0.70f;     // metres along the wall
        private const float HalfDepth = 0.60f;     // metres
        private const float Aside = 4.5f;          // metres along the frontage from a building's door
        private const float Reach = 1.3f;          // metres from the customer's place for the counter's panel
        private const float CloseBy = 15f;         // metres: within this at the last look, the counter is looked at every frame
        private const float PanelUp = 2.05f;       // metres over the ground
        private const int TriesPerSecond = 3;      // buildings looked at a second during a search
        private const int CandidatesMost = 80;     // the nearest buildings a search looks at
        private const float RetrySeconds = 300f;   // real seconds before a search that found nothing begins again

        private readonly LedgerDirector _director;
        private readonly List<Transform> _props = new List<Transform>(LedgerCatalogue.KiosksWanted);
        private readonly List<CullEntry> _culls = new List<CullEntry>(LedgerCatalogue.KiosksWanted);   // not drawn beyond the far band (Performance/ModCulling)
        private readonly List<SolidFootprint> _solid = new List<SolidFootprint>(LedgerCatalogue.KiosksWanted);
        private readonly List<Vector3> _fronts = new List<Vector3>(LedgerCatalogue.KiosksWanted);
        private readonly List<Vector3> _outward = new List<Vector3>(LedgerCatalogue.KiosksWanted);
        private readonly List<CityBuildingController> _buildings = new List<CityBuildingController>(LedgerCatalogue.KiosksWanted);
        private readonly List<string> _names = new List<string>(LedgerCatalogue.KiosksWanted);
        private readonly List<CityBuildingController> _candidates = new List<CityBuildingController>(CandidatesMost);
        private readonly List<float> _distances = new List<float>(CandidatesMost);
        private readonly List<KioskSpot> _found = new List<KioskSpot>(LedgerCatalogue.KiosksWanted);
        private readonly WorldPanel _panel = new WorldPanel { Name = "newspaper kiosk" };
        private readonly Action _onUse;
        private Transform _root;
        private string _searchFor;      // the home the search under way is for
        private int _next;
        private string _standingFor;    // the home the kiosks standing are for
        private bool _close;
        private int _near = -1;
        private int _offer = -1;        // what the panel offers: 0 closed, 1 not in yet, 2 buy, 3 read, 4 too little money
        private int _keyFrame = -1;
        private bool _toldNoProp;
        private Transform _test;        // the test button's kiosk, not in the save
        private float _retryAt;         // a search that found nothing is not begun again before this
        private string _homeKey, _homeKeyStreet;   // the home's key, and the street and number it was made from
        private int _homeKeyNumber;

        public LedgerKiosks(LedgerDirector director)
        {
            _director = director;
            _onUse = Use;
            PanelPlaces.Set("newspaper kiosk", KiosksNear);   // somebody waiting for the player is stood clear of them
        }

        // The kiosks standing near a point, with the reach of the counter's panel from where a customer stands (Core/GameUi/PanelPlaces).
        private int KiosksNear(Vector3 around, float within, float[] x, float[] z, float[] reach, int count)
        {
            for (int i = 0; i < _fronts.Count; i++)
            {
                count = PanelPlaces.Add(around, within, _fronts[i], Reach, x, z, reach, count);
            }
            return count;
        }

        /// <summary>True while the counter's panel sells or shows the paper on the interact key, and in the frame it was pressed.</summary>
        public bool TakesInteractKey => (_offer >= 2 && _panel.Showing) || _keyFrame == Time.frameCount;

        public int Count => _props.Count;

        /// <summary>A number that changes whenever the kiosks standing change (for the city map).</summary>
        public long Stamp { get; private set; }

        /// <summary>Where a customer stands at a kiosk's counter (the place on the map).</summary>
        public Vector3 Front(int i) => _fronts[i];

        /// <summary>The building a kiosk stands in front of: it lights up on the map. Null if the game has no such building now.</summary>
        public CityBuildingController Building(int i) => _buildings[i];

        /// <summary>A kiosk's place in words: the address of the building it stands in front of.</summary>
        public string Address(int i) => _names[i];

        public void Slow(BuildingRegistration home, bool skipping)
        {
            LedgerState state = _director.State;
            if (state == null || !Settings.LedgerKiosks || home == null || !PropLibrary.Ready || GameManager.isCitySceneBeingUnloaded)
            {
                Clear();
                if (_test != null)
                {
                    Look();   // the test kiosk still sells the paper
                }
                return;
            }
            if (!PropLibrary.Has(PropId))
            {
                if (!_toldNoProp)
                {
                    _toldNoProp = true;
                    LedgerDirector.Logger?.Info("City Ledger: the prop bundle has no newspaper kiosk; the paper is delivered to subscribers only.");
                }
                Clear();
                return;
            }
            string key = HomeKey(home.StreetName, home.StreetNumber);
            if (state.KiosksFor != key)
            {
                Clear();
                if (!skipping)
                {
                    Search(home, key, state);
                }
                return;
            }
            if (_standingFor != key)
            {
                Stand(state, key);
            }
            Look();
        }

        // Near the player on foot: solid, and the counter looked at every frame.
        private void Look()
        {
            PlayerController player = PlayerHelper.PlayerController;
            bool onFoot = player != null && !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding;
            Vector3 at = player != null ? player.transform.position : Vector3.zero;
            float nearest = float.MaxValue;
            float now = Time.time;
            for (int i = 0; i < _props.Count; i++)
            {
                float sqr = CitySpots.Flat(_fronts[i] - at);
                nearest = Mathf.Min(nearest, sqr);
                SolidFootprint solid = _solid[i];
                bool want = onFoot && (solid.IsSolid ? sqr < SolidFootprint.Keep * SolidFootprint.Keep : sqr < SolidFootprint.Within * SolidFootprint.Within);
                solid.Solid(want, now);
                Vector3 beside = _props[i].position + Vector3.Cross(Vector3.up, _outward[i]) * (HalfWidth + 0.8f) + _outward[i] * 0.4f;
                if (solid.Verify(now, beside, _fronts[i], out bool cut, out bool kept, out bool wayRound) && Meta.Dev.Verbose)
                {
                    LedgerDirector.Logger?.Info("City Ledger check (kiosk solid): the pavement is " + (cut ? "cut round it" : "NOT cut") + ", the counter and the way past "
                                                 + (kept && wayRound ? "can be reached: PASS" : "were lost, so the cut is taken back: FAIL") + ".");
                }
            }
            _close = onFoot && nearest < CloseBy * CloseBy;
        }

        public void Frame()
        {
            if (!_close || _props.Count == 0)
            {
                Leave();
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || ThirdPersonCamera.IsUiOpen())
            {
                Leave();
                return;
            }
            Vector3 at = player.transform.position;
            int near = -1;
            for (int i = 0; i < _fronts.Count && near < 0; i++)
            {
                Vector3 offset = _fronts[i] - at;
                if (offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < Reach * Reach)
                {
                    near = i;
                }
            }
            if (near < 0)
            {
                Leave();
                return;
            }
            Offer(near, at);
        }

        private void Offer(int kiosk, Vector3 player)
        {
            LedgerState state = _director.State;
            GameInstance save = SaveGameManager.Current;
            if (state == null || save == null)
            {
                Leave();
                return;
            }
            int price = _director.CopyPrice;
            int offer = !LedgerRules.KioskOpen(save.Hour) ? 0 : !LedgerRules.OnSale(state, save.Day, save.Hour) ? 1 : LedgerRules.BoughtCopy(state) ? 3 : save.Money < price ? 4 : 2;
            if (offer != _offer || kiosk != _near)
            {
                if (_offer < 0 && Meta.Dev.Verbose)
                {
                    LedgerDirector.Logger?.Info("City Ledger check (kiosk counter): the player is " + Mathf.Sqrt(CitySpots.Flat(_fronts[kiosk] - player)).ToString("F1", CultureInfo.InvariantCulture)
                                                + " m from the counter (offered within " + Reach.ToString("F1", CultureInfo.InvariantCulture) + " m); it offers "
                                                + (offer == 0 ? "nothing (shut)" : offer == 1 ? "nothing (not in yet)"
                                                : offer == 3 ? "the copy bought" : offer == 4 ? "the paper, but there is not the money" : "today's paper") + ": PASS.");
                }
                _offer = offer;
                _near = kiosk;
                string title = LedgerText.T("reallife_ledger_kiosk_title");
                switch (offer)
                {
                    case 0:
                        _panel.Lines(title, LedgerText.Fill("reallife_ledger_kiosk_closed", "time", LedgerText.Hour(LedgerCatalogue.KioskOpenHour)), null, WorldPanel.Red());
                        _panel.Buttons(null, null, false, null, null, false);
                        break;
                    case 1:
                        _panel.Lines(title, LedgerText.T("reallife_ledger_kiosk_not_in"), null, WorldPanel.White());
                        _panel.Buttons(null, null, false, null, null, false);
                        break;
                    case 3:
                        _panel.Lines(title, LedgerText.T("reallife_ledger_kiosk_have"), null, WorldPanel.White());
                        _panel.Buttons("reallife_ledger_kiosk_read", _onUse, true, null, null, false);
                        break;
                    default:
                        _panel.Lines(title, LedgerText.T("reallife_ledger_kiosk_paper"), LedgerText.Money(price), offer == 4 ? WorldPanel.Red() : WorldPanel.Green());
                        _panel.Buttons("reallife_ledger_kiosk_buy", _onUse, offer == 2, null, null, false);
                        break;
                }
            }
            // Over the counter; over the player's own head while the counter is behind the camera.
            Vector3 over = _props[kiosk].position + _outward[kiosk] * 0.55f + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = player + Vector3.up * 2.1f;
            }
            _panel.For(_props[kiosk].position);
            _panel.Position = over;
            _panel.Show();
        }

        private void Leave()
        {
            if (_near >= 0 || _offer >= 0)
            {
                _near = -1;
                _offer = -1;
                _panel.Hide();
            }
        }

        private void Use()
        {
            _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
            Leave();
            try
            {
                _director.BuyCopy();
            }
            catch (Exception e)
            {
                LedgerDirector.Logger?.Error(e);
            }
        }

        private void Search(BuildingRegistration home, string key, LedgerState state)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null || city.cityBuildingControllers.Length == 0)
            {
                return;
            }
            if (_searchFor != key)
            {
                if (Time.unscaledTime < _retryAt || !Candidates(city, home))
                {
                    return;   // the home's own building is not in the city yet: next second
                }
                _searchFor = key;
                _next = 0;
                _found.Clear();
            }
            for (int tries = 0; tries < TriesPerSecond && _next < _candidates.Count && _found.Count < LedgerCatalogue.KiosksWanted; tries++)
            {
                CityBuildingController building = _candidates[_next++];
                if (building != null && TryAt(building, out KioskSpot spot))
                {
                    _found.Add(spot);
                }
            }
            if (_next < _candidates.Count && _found.Count < LedgerCatalogue.KiosksWanted)
            {
                return;
            }
            if (_found.Count == 0)
            {
                // Nowhere fits (or the walkways were not there to be asked): nothing kept, looked for again later.
                LedgerDirector.Logger?.Info("City Ledger: no place for a newspaper kiosk was found near the home at " + home.Address.ToFormattedString() + "; looked for again later.");
                _searchFor = null;
                _retryAt = Time.unscaledTime + RetrySeconds;
                _candidates.Clear();
                return;
            }
            state.KiosksFor = key;
            state.Kiosks.Clear();
            state.Kiosks.AddRange(_found);
            LedgerDirector.Logger?.Info("City Ledger: " + _found.Count + " newspaper kiosk(s) found near the home at " + home.Address.ToFormattedString() + " (" + _next + " of "
                                        + _candidates.Count + " buildings looked at).");
            _searchFor = null;
            _found.Clear();
            _candidates.Clear();
        }

        private bool Candidates(CityManager city, BuildingRegistration home)
        {
            CityBuildingController own = city.FindCityBuildingController(home.Address);
            if (own == null || !Door(own, out Vector3 door))
            {
                return false;
            }
            _candidates.Clear();
            _distances.Clear();
            CityBuildingController[] all = city.cityBuildingControllers;
            float nearest = LedgerCatalogue.KioskNearest * LedgerCatalogue.KioskNearest, furthest = LedgerCatalogue.KioskFurthest * LedgerCatalogue.KioskFurthest;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b == null || b == own || b.building == null || GameMembers.Fenced(b.building) || !Door(b, out Vector3 at))
                {
                    continue;
                }
                float sqr = CitySpots.Flat(at - door);
                if (sqr < nearest || sqr > furthest)
                {
                    continue;
                }
                Candidate(b, sqr, CandidatesMost);
            }
            return true;
        }

        private void Candidate(CityBuildingController b, float sqr, int most)
        {
            int place = _distances.Count;
            while (place > 0 && _distances[place - 1] > sqr)
            {
                place--;
            }
            if (place >= most)
            {
                return;
            }
            _distances.Insert(place, sqr);
            _candidates.Insert(place, b);
            if (_candidates.Count > most)
            {
                _distances.RemoveAt(most);
                _candidates.RemoveAt(most);
            }
        }

        // True once the pavement can be found at the home's door: the city's walkways are there to be asked.
        private bool WalkwaysThere()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            BuildingRegistration home = _director.Home();
            CityBuildingController own = city != null && home != null ? city.FindCityBuildingController(home.Address) : null;
            return own != null && Door(own, out Vector3 door) && CitySpots.Pavement(door, 6f, out _);
        }

        private static bool Door(CityBuildingController b, out Vector3 door)
        {
            door = default;
            if (b.entranceDoors == null || b.entranceDoors.Length == 0 || b.entranceDoors[0] == null || b.entranceDoors[0].doorTransform == null)
            {
                return false;
            }
            door = b.entranceDoors[0].doorTransform.position;
            return true;
        }

        // A place for a kiosk a few metres aside from a building's door, either way along its frontage.
        private bool TryAt(CityBuildingController building, out KioskSpot spot)
        {
            spot = default;
            if (!Door(building, out Vector3 door) || !CitySpots.Pavement(door, 4f, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (!CitySpots.Shopfront(front + along * (side * Aside), WallGap + CounterOut, 0.45f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 middle = stand - outward * CounterOut;
                if (!CitySpots.Ground(middle, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f)
                {
                    continue;
                }
                middle.y = ground;
                if (!Fits(middle, outward, ground) || CitySpots.Flat(door - middle) < 3f * 3f)
                {
                    continue;
                }
                if (LedgerBridges.Taken != null && LedgerBridges.Taken(middle, LedgerCatalogue.KioskKeepClear))
                {
                    continue;
                }
                bool apart = true;
                for (int i = 0; i < _found.Count && apart; i++)
                {
                    apart = CitySpots.Flat(new Vector3(_found[i].X, 0f, _found[i].Z) - middle) >= LedgerCatalogue.KioskApart * LedgerCatalogue.KioskApart;
                }
                if (!apart)
                {
                    continue;
                }
                spot = new KioskSpot
                {
                    X = middle.x, Y = ground, Z = middle.z, Heading = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg,
                    Building = Place(building.building.StreetName, building.building.StreetNumber)
                };
                return true;
            }
            return false;
        }

        // Its front on the pavement, its back on level ground, nothing in it, no door near and nothing overhead.
        private static bool Fits(Vector3 middle, Vector3 outward, float ground)
        {
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            for (int c = 0; c < 2; c++)
            {
                if (!CitySpots.OnPavement(middle + outward * HalfDepth + right * (c == 0 ? -HalfWidth : HalfWidth), out _))
                {
                    return false;
                }
                if (!CitySpots.Ground(middle - outward * HalfDepth + right * (c == 0 ? -HalfWidth : HalfWidth), out float back) || Mathf.Abs(back - ground) > 0.2f)
                {
                    return false;
                }
            }
            if (!CitySpots.ClearQuietly(middle, 0.6f))
            {
                return false;
            }
            int overhead = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "Default");
            return !Physics.CheckCapsule(middle + Vector3.up * 1.95f, middle + Vector3.up * 2.5f, 0.55f, overhead, QueryTriggerInteraction.Ignore);
        }

        private void Stand(LedgerState state, string key)
        {
            Clear();
            if (!PropLibrary.TryGet(PropId, out Mesh mesh, out Material material))
            {
                return;
            }
            // A kept place whose counter is off the pavement now starts a new search, once the walkways can be asked.
            for (int i = 0; i < state.Kiosks.Count && WalkwaysThere(); i++)
            {
                KioskSpot k = state.Kiosks[i];
                Vector3 front = new Vector3(k.X, k.Y, k.Z) + Quaternion.Euler(0f, k.Heading, 0f) * Vector3.forward * CounterOut;
                if (!CitySpots.Pavement(front, 0.6f, out _))
                {
                    LedgerDirector.Logger?.Info("City Ledger: a kiosk's place near the home is no longer on the pavement; the kiosks are looked for again.");
                    state.KiosksFor = null;
                    state.Kiosks.Clear();
                    return;
                }
            }
            for (int i = 0; i < state.Kiosks.Count; i++)
            {
                Add(state.Kiosks[i], mesh, material);
            }
            _standingFor = key;
            Stamp++;
        }

        private Transform Add(KioskSpot k, Mesh mesh, Material material)
        {
            if (_root == null)
            {
                _root = new GameObject("RealLifeOverhaul Ledger Kiosks").transform;
                _root.SetParent(_director.transform, false);
            }
            var made = new GameObject(PropId);
            made.transform.SetParent(_root, false);
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            Quaternion facing = Quaternion.Euler(0f, k.Heading, 0f);
            made.transform.SetPositionAndRotation(new Vector3(k.X, k.Y, k.Z), facing);
            Vector3 outward = facing * Vector3.forward;
            var solid = new SolidFootprint();
            solid.Fit(made.transform, HalfWidth, HalfDepth, CounterOut);
            _props.Add(made.transform);
            _culls.Add(ModCulling.Add(CullRules.Kind.NewsKiosk, made.transform, made.transform.position, HalfWidth + HalfDepth));
            _solid.Add(solid);
            _outward.Add(outward);
            _fronts.Add(made.transform.position + outward * CounterOut);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            Address address = AddressOf(k.Building);
            CityBuildingController building = address != null && city != null ? city.FindCityBuildingController(address) : null;
            _buildings.Add(building);
            _names.Add(address != null ? address.ToFormattedString() : string.Empty);
            return made.transform;
        }

        private void RemoveAt(int i)
        {
            ModCulling.Remove(_culls[i]);
            _culls.RemoveAt(i);
            _solid[i].Clear();
            if (_props[i] != null)
            {
                UnityEngine.Object.Destroy(_props[i].gameObject);
            }
            _props.RemoveAt(i);
            _solid.RemoveAt(i);
            _fronts.RemoveAt(i);
            _outward.RemoveAt(i);
            _buildings.RemoveAt(i);
            _names.RemoveAt(i);
        }

        /// <summary>A kiosk on the nearest shopfront round a place, not kept in the save; false if none fits within metres.</summary>
        public bool TestHere(Vector3 at, out float metres)
        {
            metres = 70f;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null || !PropLibrary.Ready || !PropLibrary.TryGet(PropId, out Mesh mesh, out Material material))
            {
                return false;
            }
            TestBack();
            _candidates.Clear();
            _distances.Clear();
            CityBuildingController[] all = city.cityBuildingControllers;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b == null || b.building == null || GameMembers.Fenced(b.building) || !Door(b, out Vector3 door))
                {
                    continue;
                }
                float sqr = CitySpots.Flat(door - at);
                if (sqr > metres * metres)
                {
                    continue;
                }
                Candidate(b, sqr, int.MaxValue);
            }
            bool placed = false;
            for (int i = 0; i < _candidates.Count && i < 30 && !placed; i++)
            {
                if (TryAt(_candidates[i], out KioskSpot spot))
                {
                    _test = Add(spot, mesh, material);
                    metres = Mathf.Sqrt(CitySpots.Flat(_test.position - at));
                    placed = true;
                }
            }
            _candidates.Clear();
            _distances.Clear();
            _searchFor = null;   // a search under way starts again
            Stamp++;
            Look();
            return placed;
        }

        /// <summary>The kiosks near the home are looked for afresh from the next second (the test button).</summary>
        public void Again()
        {
            Clear();
            _searchFor = null;
            _retryAt = 0f;
            _candidates.Clear();
            _found.Clear();
        }

        /// <summary>The test kiosk goes. False if there was none.</summary>
        public bool TestBack()
        {
            int i = _test != null ? _props.IndexOf(_test) : -1;
            _test = null;
            if (i < 0)
            {
                return false;
            }
            Leave();
            RemoveAt(i);
            Stamp++;
            return true;
        }

        /// <summary>Takes the home's kiosks away (they stand again on the next second if they should); the test kiosk stays.</summary>
        public void Clear()
        {
            if (_standingFor == null && (_props.Count == 0 || (_props.Count == 1 && _test != null)))
            {
                return;
            }
            Leave();
            _close = false;
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_props[i], _test))
                {
                    RemoveAt(i);
                }
            }
            _standingFor = null;
            Stamp++;
        }

        public void Dispose()
        {
            TestBack();
            Clear();
            _searchFor = null;
            _candidates.Clear();
            _found.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        // The home's place key, asked every second: made again only when the home changed (no string otherwise).
        private string HomeKey(string street, int number)
        {
            if (_homeKey == null || !ReferenceEquals(street, _homeKeyStreet) || number != _homeKeyNumber)
            {
                _homeKeyStreet = street;
                _homeKeyNumber = number;
                _homeKey = Place(street, number);
            }
            return _homeKey;
        }

        /// <summary>A building by its address, as the mod's other features write a place: the street, a tilde, the number.</summary>
        public static string Place(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(CultureInfo.InvariantCulture);
        }

        private static Address AddressOf(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            return new Address(Uri.UnescapeDataString(place.Substring(0, at)), number);
        }
    }
}
