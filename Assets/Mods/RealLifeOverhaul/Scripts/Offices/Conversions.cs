using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Blueprints;
using Buildings;
using RealLifeOverhaul.BizPace;
using Streets;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// More offices to rent: some empty shops are offices while the mod runs, as the mod's claims, the game's businesses and
    /// its rivals can leave no office to let. Which shops is <see cref="ConversionRules"/>'s; to the game such a shop is an
    /// office in every way (<see cref="ConversionGame"/>). The held ones come back at load (<see cref="ConversionStore"/>);
    /// the empty ones are chosen once the other features have chosen (<see cref="OthersPicked"/>) and again on a change.
    /// Going Public: every machine chooses the same from the city, nothing is sent, and another player's building is never
    /// made an office. Cost: no frame work, a one-second timer reading a few fields; the choice only on a change.
    /// </summary>
    public sealed class Conversions : MonoBehaviour
    {
        private const int WaitForOthers = 120;   // seconds at most for the other features' choices

        public static IModLogger Logger;
        public static Conversions Instance { get; private set; }

        /// <summary>The other features that choose buildings have chosen (Wiring/OfficeLinks); null for "go ahead".</summary>
        public static Func<bool> OthersPicked;

        private sealed class Made
        {
            public Building B;      // the city scene's copy of the building: what the map card, the filters and the marker read
            public Building G;      // the game's list's copy (BuildingHelper.GetBuilding), what BizMan and every lookup by address read;
                                    // kept, so it is put back though the scene's is gone; null where it is the same object
            public CityBuildingController C;
            public string Was;      // the type as the game made it
            public string Holder;   // its claim with the held addresses (Core/HeldUnits), so the game's own letting leaves it to the player
        }

        // Static, so another feature's choice can ask without a reference: the buildings that are offices because of this.
        private static readonly Dictionary<Building, Made> Converted = new Dictionary<Building, Made>(32);
        // The game's list's copies of the same buildings, so a feature that walks that list asks the same question.
        private static readonly HashSet<Building> GameCopies = new HashSet<Building>();

        private readonly List<CityBuildingController> _shops = new List<CityBuildingController>(128);
        private readonly List<CityBuildingController> _offices = new List<CityBuildingController>(96);
        private readonly HashSet<Address> _watched = new HashSet<Address>();
        // Each watched building's controller, to read its record when the game says it changed; and the facts the choice keys on as the last event for
        // the building left them (BizPaceRules.Signature).
        private readonly Dictionary<Address, CityBuildingController> _at = new Dictionary<Address, CityBuildingController>(256);
        private readonly Dictionary<Address, int> _looked = new Dictionary<Address, int>(256);
        private readonly HashSet<Address> _forSale = new HashSet<Address>();
        private readonly HashSet<string> _kept = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<Building> _refused = new HashSet<Building>();   // shops the game's list would not read as offices
        private readonly List<ConversionRow> _rows = new List<ConversionRow>(224);
        private readonly List<CityBuildingController> _rowOf = new List<CityBuildingController>(224);
        private bool[] _want = new bool[0];
        private GameInstance _save;
        private bool _indexed, _heldDone, _picked, _dirty, _failed, _said;
        private int _waited, _day = -1, _stamp = int.MinValue, _free;
        private Action<Address> _onChange, _changeSeen;
        private Action _onSave;

        /// <summary>
        /// True for a building that is an office because of this, whichever copy of it is asked (the city scene's or the game's list's):
        /// other features' choices of an office leave it alone.
        /// </summary>
        public static bool Holds(Building building) => building != null && (Converted.ContainsKey(building) || GameCopies.Contains(building));

        private void Awake()
        {
            Instance = this;
            _onChange = Guard.Wrap<Address>(OnChange);   // a fault here never reaches the game's own handlers
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GlobalEvents.onBuildingRegistrationChange -= _onChange;
            SaveHook.Drop(_onSave);
            StopAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_failed)
                {
                    continue;
                }
                while (StartLine.Holds(StartSeats.Offices))
                {
                    yield return null;   // its first load after a save loads waits for a frame of its own
                }
                try
                {
                    Slow();
                }
                catch (Exception e) when (Guard.StandsDown(e, "empty shops as offices"))
                {
                    _failed = true;
                    StopAll();
                }
                catch (Exception e)
                {
                    _failed = true;
                    Logger?.Error(e);
                    Logger?.Warn("Offices: the shops made offices are shops again after the error above, for the rest of this session.");
                    StopAll();
                }
            }
        }

        private static readonly int BizChange = HotSpots.Slot("Offices: Conversions.OnChange");

        // The game's own event, for any building (BizMan raises it each time a business view or the Schedule tab is left): only a change
        // in the facts the choice keys on, at one of the watched buildings, asks for a new look. The first event for a building has
        // nothing to be compared with and asks; once asked, the flag stays until the look is made, so no change is missed.
        private void OnChange(Address address)
        {
            using var timed = BizPaceWatch.Time(BizChange);   // a step of BizMan closing a business or leaving a tab (BizPace)
            try
            {
                if (address != null && _at.TryGetValue(address, out CityBuildingController c) && c != null)
                {
                    int now = Facts(c);
                    if (!_looked.TryGetValue(address, out int was) || BizPaceRules.Moved(was, now))
                    {
                        _looked[address] = now;
                        _dirty = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Offices: Conversions.Tick");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || Meta.SaveTools.Dormant)
            {
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                StopAll();   // another save in the same city: what the last one made is put back first
                _save = save;
                ReadStore(save);
            }
            if (!Settings.OfficeConversion)
            {
                if (Converted.Count > 0)
                {
                    Logger?.Info("Offices: the option is off; the " + Converted.Count + " shops made offices are shops again.");
                    StopAll();
                    _save = save;
                }
                return;
            }
            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onChange, ref _changeSeen);
            SaveHook.Keep(_onSave ?? (_onSave = OnSave));
            if (!_indexed && !Index())
            {
                return;
            }
            int stamp = SpecialBuildings.Stamp;
            if (stamp != _stamp)
            {
                _stamp = stamp;
                _dirty = true;
            }
            if (save.Day != _day)
            {
                _day = save.Day;
                _dirty = true;
            }
            if (!_heldDone)
            {
                _heldDone = true;
                Choose(false);   // the offices somebody has, at once: an office business is never left in a shop
            }
            if (!_picked)
            {
                Func<bool> others = OthersPicked;
                if (others != null && !others() && ++_waited < WaitForOthers)
                {
                    return;
                }
                _picked = true;
                _dirty = true;
            }
            if (_dirty)
            {
                _dirty = false;
                Choose(true);
            }
        }

        // The facts of one watched building that the choice keys on (see Choose): to let and empty, an office business, rented, owned, another
        // player's, held by a feature of the mod's, listed for sale.
        private static int Facts(CityBuildingController c)
        {
            BuildingRegistration reg = c.buildingRegistration;
            return BizPaceRules.Signature(ConversionGame.Vacant(reg), ConversionGame.OfficeBusiness(reg), reg != null && reg.RentedByPlayer, reg != null && reg.BuildingOwnedByPlayer,
                GoingPublic.Foreign(reg), SpecialBuildings.Holds(c), ConversionGame.OnSale(SaveGameManager.Current, c.building.Address));
        }

        // The city's shops and offices that may take part, once a city load, by the types as the game made them.
        private bool Index()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = GameDlc.Open(city);
            if (buildings == null || buildings.Length == 0)
            {
                return false;
            }
            List<BuildingSizeInfo> officeSizes = ConversionGame.OfficeSizes();
            _shops.Clear();
            _offices.Clear();
            _watched.Clear();
            _at.Clear();
            _looked.Clear();
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController c = buildings[i];
                if (!ConversionGame.Usable(c) || Converted.ContainsKey(c.building))
                {
                    continue;
                }
                if (c.building.BuildingType == ConversionGame.Office)
                {
                    _offices.Add(c);
                }
                else if (c.building.BuildingType == ConversionGame.Retail && ConversionGame.SharedStructure(c.building, officeSizes))
                {
                    _shops.Add(c);
                }
                else
                {
                    continue;
                }
                _watched.Add(c.building.Address);
                _at[c.building.Address] = c;
            }
            _indexed = true;
            Logger?.Info("Offices: " + _offices.Count + " office buildings and " + _shops.Count + " shops that may be offices (the game's shared shop and office structures).");
            return true;
        }

        private static readonly int BizChoose = HotSpots.Slot("Offices: Conversions.Choose");

        // The choice made again from the city as it is now; only what changed is changed. picks false: only the held offices.
        private void Choose(bool picks)
        {
            using var timed = BizPaceWatch.Time(BizChoose);   // what a BizMan step leaves for the next tick (BizPace)
            ConversionGame.ForSale(_save, _forSale);
            _rows.Clear();
            _rowOf.Clear();
            for (int i = 0; i < _offices.Count; i++)
            {
                CityBuildingController c = _offices[i];
                _rows.Add(new ConversionRow
                {
                    Street = c.building.StreetName, Number = c.building.StreetNumber, District = c.building.Neighbourhood, Office = true,
                    Free = ConversionGame.Vacant(c.buildingRegistration) && !SpecialBuildings.Holds(c)
                });
                _rowOf.Add(c);
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                CityBuildingController c = _shops[i];
                BuildingRegistration reg = c.buildingRegistration;
                bool vacant = ConversionGame.Vacant(reg);
                bool made = Converted.ContainsKey(c.building);
                bool foreign = GoingPublic.Foreign(reg);
                // A shop's business in it (a save made while the game still offered shop businesses in such an office) keeps it a shop.
                string key = ConversionRules.Key(c.building.StreetName, c.building.StreetNumber);
                bool held = !ConversionGame.OtherBusiness(reg)
                            && ((made && !vacant)
                                || (!foreign && reg != null && (ConversionGame.OfficeBusiness(reg) || (!vacant && _kept.Contains(key)))));
                // A rival landlord stays one: the held addresses (Core/HeldUnits) keep the game's letting off without its ignore list.
                bool free = picks && vacant && !foreign && !_forSale.Contains(c.building.Address) && !SpecialBuildings.Holds(c);
                _rows.Add(new ConversionRow
                {
                    Street = c.building.StreetName, Number = c.building.StreetNumber, District = c.building.Neighbourhood, Office = false, Free = free, Held = held
                });
                _rowOf.Add(c);
            }
            if (_want.Length < _rows.Count)
            {
                _want = new bool[_rows.Count];
            }
            int toLet = ConversionRules.Decide(_rows, _want);
            if (picks)
            {
                _kept.Clear();   // what the save kept is an office by now, or somebody let it go
            }
            int added = 0, removed = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Office)
                {
                    continue;
                }
                CityBuildingController c = _rowOf[i];
                bool made = Converted.TryGetValue(c.building, out Made m);
                if (_want[i] && !made)
                {
                    if (_refused.Contains(c.building))
                    {
                        continue;
                    }
                    m = new Made
                    {
                        B = c.building, G = ConversionGame.GameCopy(c.building), C = c, Was = c.building.BuildingType,
                        Holder = HeldUnitsRules.OfficesPrefix + ConversionRules.Key(c.building.StreetName, c.building.StreetNumber)
                    };
                    ConversionGame.SetType(m.B, m.G, c, ConversionGame.Office);
                    string why = ConversionGame.Disagreement(m.B, m.G, ConversionGame.Office);
                    if (why != null)
                    {
                        // Never a shop the map calls an office and BizMan a shop: put back, and not tried again this session.
                        ConversionGame.SetType(m.B, m.G, c, m.Was);
                        _refused.Add(c.building);
                        Logger?.Warn("Offices: " + c.building.Address.ToFormattedString() + " stays a shop: " + why + ".");
                        continue;
                    }
                    Converted[c.building] = m;
                    if (m.G != null)
                    {
                        GameCopies.Add(m.G);
                    }
                    HeldUnits.Hold(m.Holder, c.building.StreetName, c.building.StreetNumber, HeldKind.Conversions);
                    added++;
                }
                else if (!_want[i] && made && (!picks || ConversionGame.Vacant(c.buildingRegistration) || ConversionGame.OtherBusiness(c.buildingRegistration)))
                {
                    if (!picks)
                    {
                        continue;   // the first look only adds
                    }
                    Restore(m);
                    Converted.Remove(c.building);
                    removed++;
                }
            }
            if (added > 0 || removed > 0)
            {
                ConversionGame.Filters();
            }
            if (added > 0 || removed > 0 || (picks && (toLet != _free || !_said)))
            {
                Say(picks, added, removed, toLet);
            }
            if (picks && (added > 0 || removed > 0 || !_said))
            {
                TypeCheck(Meta.Dev.Verbose);
            }
            if (picks)
            {
                _free = toLet;
                _said = true;
            }
        }

        private void Say(bool picks, int added, int removed, int free)
        {
            var said = new StringBuilder(512);
            said.Append("Offices: ").Append(Converted.Count).Append(" shops are offices (").Append(added).Append(" made, ").Append(removed).Append(" shops again");
            said.Append(picks
                ? "); " + free + " offices to let in the office districts (at least " + ConversionRules.DistrictFloor + " a district and " + ConversionRules.CityFloor
                  + " in the city where there are shops to spare):"
                : "; the ones somebody has, at load):");
            foreach (Made m in Converted.Values)
            {
                Building b = m.C.building;
                said.Append(' ').Append(b.StreetName).Append(' ').Append(b.StreetNumber).Append(" (").Append(b.Neighbourhood)
                    .Append(ConversionGame.Vacant(m.C.buildingRegistration) ? ", to let)" : ", taken)");
            }
            Logger?.Info(said.ToString());
            if (picks && Meta.Dev.Verbose)
            {
                Logger?.Info("Offices: conversion check (" + free + " offices to let against " + ConversionRules.CityFloor + " wanted in the city): "
                             + (free >= ConversionRules.CityFloor ? "PASS" : "short: the office districts have no more empty shops to spare under the cap of half their shops"));
            }
        }

        // Every building of the city reads one type in each copy the game keeps (the map's and BizMan's), and every business the game
        // offers in a building of a type is one made for that type: the office a player rents on the map offers offices. Once a change, a
        // pass over the buildings the conversion looked at and over the game's list; a failure is always a warning, a pass said with
        // Developer diagnostics (or the test button).
        private void TypeCheck(bool say)
        {
            int looked = 0, apart = 0;
            string first = null;
            for (int i = 0; i < _rowOf.Count; i++)
            {
                CityBuildingController c = _rowOf[i];
                if (c == null || c.building == null)
                {
                    continue;
                }
                looked++;
                Converted.TryGetValue(c.building, out Made m);
                string why = ConversionGame.Disagreement(c.building, m != null ? m.G : null, c.building.BuildingType);
                if (why != null)
                {
                    apart++;
                    first = first ?? c.building.Address.ToFormattedString() + ": " + why;
                }
            }
            string wrong = ConversionGame.WrongOfferInCity(out int types);
            string line = "Offices: type check (" + looked + " buildings, " + Converted.Count + " of them shops made offices; each reads one type on the map "
                          + "and in BizMan, and the businesses offered in each of the city's " + types + " building types are made for it): ";
            if (apart == 0 && wrong == null)
            {
                if (say)
                {
                    Logger?.Info(line + "PASS");
                }
                return;
            }
            Logger?.Warn(line + "FAIL (" + (apart > 0 ? apart + " read two types, first " + first : string.Empty) + (apart > 0 && wrong != null ? "; " : string.Empty)
                         + (wrong != null ? "the game offers " + wrong : string.Empty) + ")");
        }

        private static void Restore(Made m)
        {
            HeldUnits.Release(m.Holder);   // never throws: the game's letting may take the shop again at once
            ConversionGame.SetType(m.B, m.G, m.C, m.Was);   // the game's list's copy outlives the scene's, so it goes back even when the scene's is gone
            if (m.G != null)
            {
                GameCopies.Remove(m.G);
            }
        }

        private void ReadStore(GameInstance save)
        {
            _kept.Clear();
            string text = null;
            if (save.modData != null && save.modData.TryGetValue(ConversionStore.Key, out text) && !ConversionStore.Read(text, _kept))
            {
                SaveLines.Unreadable(save.modData, ConversionStore.Key, "it could not be read");   // put back as it was read at every save
                Logger?.Warn("Offices: the save's line of shops kept as offices could not be read (" + ConversionStore.Key + "); only shops with an office business "
                             + "in them are offices at load, and the line is kept in the save as it is.");
            }
        }

        // Inside the game's save event: the shops that are offices and the player keeps (rented, owned, listed for sale), by address.
        private void OnSave()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.modData == null || !ReferenceEquals(save, _save))
            {
                return;
            }
            var places = new List<string>(Converted.Count);
            ConversionGame.ForSale(save, _forSale);
            foreach (Made m in Converted.Values)
            {
                if (!ConversionGame.Vacant(m.C.buildingRegistration) && ConversionGame.Keeps(m.C.buildingRegistration, _forSale))
                {
                    places.Add(ConversionRules.Key(m.C.building.StreetName, m.C.building.StreetNumber));
                }
            }
            if (places.Count == 0 && _kept.Count == 0)
            {
                save.modData.Remove(ConversionStore.Key);
                return;
            }
            foreach (string kept in _kept)
            {
                if (!places.Contains(kept))
                {
                    places.Add(kept);   // a save written before the first full look keeps what it read
                }
            }
            save.modData[ConversionStore.Key] = ConversionStore.Write(places);
        }

        /// <summary>The feature's data is reset: what the save kept is forgotten.</summary>
        public void Forget()
        {
            _kept.Clear();
        }

        /// <summary>The player's businesses in a shop that is an office because of this (an office business there), each "name (address)".</summary>
        public static List<string> PlayersBusinesses()
        {
            var names = new List<string>();
            foreach (Made m in Converted.Values)
            {
                BuildingRegistration reg = m.C != null ? m.C.buildingRegistration : null;
                if (reg != null && (GoingPublic.Mine(reg) || reg.BuildingOwnedByPlayer) && ConversionGame.OfficeBusiness(reg))
                {
                    names.Add(ConversionGame.Named(reg, m.C.building));
                }
            }
            names.Sort(string.CompareOrdinal);
            return names;
        }

        /// <summary>The test button: the shops that are offices, in the log, and the nearest one to let in a notification.</summary>
        public void TestList()
        {
            if (!Settings.OfficeConversion || Converted.Count == 0)
            {
                Notifications.Show(NotificationType.Warning, Settings.OfficeConversion ? "reallife_offices_test_none" : "reallife_offices_test_off", null, 8f, trackOnSaveGame: false);
                return;
            }
            TypeCheck(true);
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            Made nearest = null;
            float best = float.MaxValue;
            int open = 0;
            foreach (Made m in Converted.Values)
            {
                bool vacant = ConversionGame.Vacant(m.C.buildingRegistration);
                Logger?.Info("Offices test: " + m.C.building.Address.ToFormattedString() + ", " + m.C.building.Neighbourhood + (vacant ? ", to let." : ", taken."));
                if (!vacant)
                {
                    continue;
                }
                open++;
                float sqr = player != null ? (m.C.transform.position - player.transform.position).sqrMagnitude : open;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = m;
                }
            }
            if (nearest == null)
            {
                Notifications.Show(NotificationType.Info, "reallife_offices_test_taken", new Dictionary<string, string> { { "count", Converted.Count.ToString() } }, 10f, trackOnSaveGame: false);
                return;
            }
            var data = new Dictionary<string, string>(2)
            {
                { "count", open.ToString() }, { "address", nearest.C.building.Address.ToFormattedString() }
            };
            Notifications.Show(NotificationType.Info, "reallife_offices_test_done", data, 12f, trackOnSaveGame: false);
        }

        /// <summary>Every shop made an office is a shop again (the feature's clean-up, the option off, another save, the city going).</summary>
        public void StopAll()
        {
            bool any = Converted.Count > 0;
            foreach (Made m in Converted.Values)
            {
                try
                {
                    if (_save != null && m.C != null && !ConversionGame.Vacant(m.C.buildingRegistration) && ConversionGame.Keeps(m.C.buildingRegistration, null))
                    {
                        _kept.Add(ConversionRules.Key(m.B.StreetName, m.B.StreetNumber));   // an office the player has is one again if the option comes back on
                    }
                    Restore(m);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Converted.Clear();
            GameCopies.Clear();
            _refused.Clear();
            if (any)
            {
                try
                {
                    ConversionGame.Filters();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _indexed = false;
            _heldDone = false;
            _picked = false;
            _said = false;
            _waited = 0;
            _day = -1;
            _stamp = int.MinValue;
            _save = null;
        }
    }
}
