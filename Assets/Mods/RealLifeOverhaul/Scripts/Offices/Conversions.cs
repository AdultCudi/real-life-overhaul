using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Blueprints;
using Buildings;
using Streets;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// More offices to rent: some of the city's empty shops are offices while the mod runs (option "Empty shops as
    /// offices"). The mod's claims (the tech companies' headquarters, the utilities office, the courthouse, the
    /// university, the law chambers) take the game's empty office buildings, and the game's own businesses and its rivals
    /// hold the rest, so a player could find none to let. Which shops and how many is <see cref="ConversionRules"/>'s:
    /// at least two offices to let in each district with offices and eight in the city, the same on every machine. To the
    /// game such a shop is an office: listed and filtered as one on the map and in BizMan, rented at an office's rent,
    /// started as any office business (the mod's tech firms' too), with the game's office interior of that size. How,
    /// and what a building's type governs, is in <see cref="ConversionGame"/>.
    ///
    /// WHEN. At a city load the shops that are offices and somebody has (an office business in one, or one the save
    /// keeps, <see cref="ConversionStore"/>) are offices again at the first look; the empty ones are chosen once the
    /// other features have chosen their buildings (<see cref="OthersPicked"/>, two minutes at most), and chosen again
    /// once a game day, when a building of the city's office districts changes hands (the game's own event,
    /// GlobalEvents.onBuildingRegistrationChange, for one of the buildings it looks at) and when another feature claims
    /// or lets go of a building (SpecialBuildings.Stamp). An office that somebody has is never a shop again while the mod
    /// runs; an empty one no longer needed is.
    ///
    /// NOT SAVED. The type is the city's building asset's, never the save's: a save without the mod has every building
    /// as the game made it. Everything is put back when the city goes, the option is switched off, or the save is
    /// prepared for removing the mod (which says which of the player's businesses are in such a building, Meta/SaveTools).
    ///
    /// GOING PUBLIC. The choice is made from the city and who rents what, which are the same for every player in a
    /// session, so every machine with the mod has the same offices; nothing is sent. Another player's building is never
    /// made an office: only an empty one is, and one that becomes another player's while an office stays as it is.
    ///
    /// Other features' choices of an office building skip these (<see cref="Holds"/>), so none of them takes the
    /// player's offices.
    ///
    /// The cost: no Update. A one-second timer on the real clock that reads a few fields of the converted buildings, an
    /// int and the day; the choice itself (one pass over the city's shops and offices, a few hundred) only on a change.
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
            public Building B;      // the city's building asset: what is put back, whatever became of the scene's object
            public CityBuildingController C;
            public string Was;      // the type as the game made it
            public bool Ignored;    // on the game's list of addresses its rivals leave alone
        }

        // Static, so that another feature's choice can ask without a reference: the buildings that are offices because of this.
        private static readonly Dictionary<Building, Made> Converted = new Dictionary<Building, Made>(32);

        private readonly List<CityBuildingController> _shops = new List<CityBuildingController>(128);
        private readonly List<CityBuildingController> _offices = new List<CityBuildingController>(96);
        private readonly HashSet<Address> _watched = new HashSet<Address>();
        private readonly HashSet<Address> _forSale = new HashSet<Address>();
        private readonly HashSet<string> _kept = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ConversionRow> _rows = new List<ConversionRow>(224);
        private readonly List<CityBuildingController> _rowOf = new List<CityBuildingController>(224);
        private bool[] _want = new bool[0];
        private GameInstance _save;
        private bool _indexed, _heldDone, _picked, _dirty, _failed, _said;
        private int _waited, _day = -1, _stamp = int.MinValue, _free;
        private Action<Address> _onChange, _changeSeen;
        private Action _onSave;

        /// <summary>True for a building that is an office because of this: other features' choices of an office leave it alone.</summary>
        public static bool Holds(Building building) => building != null && Converted.ContainsKey(building);

        /// <summary>The shops that are offices now.</summary>
        public static int Count => Converted.Count;

        private void Awake()
        {
            Instance = this;
            _onChange = Guard.Wrap<Address>(OnChange);   // (a fault here never reaches the game's own handlers: Core/Guard)
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

        // The game's own event, for any building: only one of the buildings looked at asks for a new look (a set's look-up).
        private void OnChange(Address address)
        {
            try
            {
                if (address != null && _watched.Contains(address))
                {
                    _dirty = true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Slow()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || Meta.SaveTools.Dormant)
            {
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                StopAll();   // (another save in the same city: what the last one had made is put back first)
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
            // A converted shop let since the last look leaves the rivals' list at once (another player's id on it must stay).
            foreach (Made m in Converted.Values)
            {
                if (m.Ignored && !ConversionGame.Vacant(m.C.buildingRegistration))
                {
                    m.Ignored = ConversionGame.Ignore(m.B.Address, false);
                    _dirty = true;
                }
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

        // The city's shops and offices that may take part, once a city load (the types as the game made them).
        private bool Index()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || buildings.Length == 0)
            {
                return false;
            }
            List<BuildingSizeInfo> officeSizes = ConversionGame.OfficeSizes();
            _shops.Clear();
            _offices.Clear();
            _watched.Clear();
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
            }
            _indexed = true;
            Logger?.Info("Offices: " + _offices.Count + " office buildings and " + _shops.Count + " shops that may be offices (the game's shared shop and office structures).");
            return true;
        }

        // The choice made again from the city as it is now; only what changed is changed. picks false: only the offices somebody has.
        private void Choose(bool picks)
        {
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
                bool held = (made && !vacant)
                            || (!foreign && reg != null && (ConversionGame.OfficeBusiness(reg) || (!vacant && _kept.Contains(ConversionRules.Key(c.building.StreetName, c.building.StreetNumber)))));
                bool free = picks && vacant && !foreign && ConversionGame.NoLandlord(reg) && !_forSale.Contains(c.building.Address) && !SpecialBuildings.Holds(c);
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
                _kept.Clear();   // (what the save kept is an office by now, or somebody let it go)
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
                    m = new Made { B = c.building, C = c, Was = c.building.BuildingType };
                    Converted[c.building] = m;
                    ConversionGame.SetType(m.B, c, ConversionGame.Office);
                    added++;
                }
                else if (!_want[i] && made && (!picks || ConversionGame.Vacant(c.buildingRegistration)))
                {
                    if (!picks)
                    {
                        continue;   // (the first look only adds)
                    }
                    Restore(m);
                    Converted.Remove(c.building);
                    removed++;
                    continue;
                }
                if (_want[i])
                {
                    BuildingRegistration reg = c.buildingRegistration;
                    bool ignore = ConversionGame.Vacant(reg) && ConversionGame.NoLandlord(reg);
                    if (ignore != m.Ignored)
                    {
                        m.Ignored = ConversionGame.Ignore(c.building.Address, ignore);
                    }
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
            said.Append(picks ? "); " + free + " offices to let in the office districts (at least " + ConversionRules.DistrictFloor + " a district and " + ConversionRules.CityFloor + " in the city where there are shops to spare):" : "; the ones somebody has, at load):");
            foreach (Made m in Converted.Values)
            {
                Building b = m.C.building;
                said.Append(' ').Append(b.StreetName).Append(' ').Append(b.StreetNumber).Append(" (").Append(b.Neighbourhood).Append(ConversionGame.Vacant(m.C.buildingRegistration) ? ", to let)" : ", taken)");
            }
            Logger?.Info(said.ToString());
            if (picks && Meta.Dev.Verbose)
            {
                Logger?.Info("Offices: conversion check (" + free + " offices to let against " + ConversionRules.CityFloor + " wanted in the city): "
                             + (free >= ConversionRules.CityFloor ? "PASS" : "short: the office districts have no more empty shops to spare under the cap of half their shops"));
            }
        }

        private static void Restore(Made m)
        {
            try
            {
                if (m.Ignored)
                {
                    m.Ignored = ConversionGame.Ignore(m.B.Address, false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            ConversionGame.SetType(m.B, m.C, m.Was);
        }

        // ---------------------------------------------------------------- the save

        private void ReadStore(GameInstance save)
        {
            _kept.Clear();
            string text = null;
            if (save.modData != null && save.modData.TryGetValue(ConversionStore.Key, out text) && !ConversionStore.Read(text, _kept))
            {
                SaveLines.Unreadable(save.modData, ConversionStore.Key, "it could not be read");   // (put back as it was read at every save: Core/SaveLines)
                Logger?.Warn("Offices: the save's line of shops kept as offices could not be read (" + ConversionStore.Key + "); only shops with an office business in them are offices at load, and the line is kept in the save as it is.");
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
                    places.Add(kept);   // (a save written before the first full look keeps what it read)
                }
            }
            save.modData[ConversionStore.Key] = ConversionStore.Write(places);
        }

        /// <summary>The feature's data is reset: what the save kept is forgotten.</summary>
        public void Forget()
        {
            _kept.Clear();
        }

        // ---------------------------------------------------------------- the save tools, the test

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
                        _kept.Add(ConversionRules.Key(m.B.StreetName, m.B.StreetNumber));   // (an office the player has is one again if the option comes back on)
                    }
                    Restore(m);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Converted.Clear();
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
