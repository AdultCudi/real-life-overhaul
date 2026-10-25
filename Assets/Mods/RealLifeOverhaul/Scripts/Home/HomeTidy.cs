using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>How tidy the player's home is: the game's own floor dirt where the home has it, else <see cref="HomeState.Tidy"/>.</summary>
    public static class HomeTidy
    {
        private const string Residential = "ba:buildingtype_residential";

        private static BuildingRegistration _home;
        private static float _nextLook;
        private static float _owed;          // tidiness lost that is still too little to put on the floor

        public static bool HasHome => Home() != null;

        public static void Reset()
        {
            _home = null;
            _nextLook = 0f;
            _owed = 0f;
        }

        /// <summary>Whether a home has power, and water, now (Utilities, through Wiring/UtilityLinks); null: always.</summary>
        public static Func<BuildingRegistration, bool> HasPower, HasWater;

        public static bool Dark(BuildingRegistration home)
        {
            Func<BuildingRegistration, bool> has = HasPower;
            return home != null && has != null && !has(home);
        }

        public static bool Dry(BuildingRegistration home)
        {
            Func<BuildingRegistration, bool> has = HasWater;
            return home != null && has != null && !has(home);
        }

        /// <summary>True while the player stands in a home of their own.</summary>
        public static bool AtHome()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && GoingPublic.Mine(manager.buildingRegistration) && manager.building.BuildingType == Residential;
        }

        /// <summary>The home whose tidiness counts: the one the player is in, else the first they rent. Looked up at most every few seconds.</summary>
        public static BuildingRegistration Home()
        {
            if (AtHome())
            {
                _home = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
                return _home;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return _home != null && GoingPublic.Mine(_home) ? _home : null;
            }
            _nextLook = Time.unscaledTime + 5f;
            if (_home != null && GoingPublic.Mine(_home))
            {
                return _home;
            }
            _home = null;
            try
            {
                List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (all[i] != null && GoingPublic.Mine(all[i]) && GameRegistrations.BuildingType(all[i]) == Residential)
                    {
                        _home = all[i];
                        break;
                    }
                }
            }
            catch (Exception)
            {
                _home = null;
            }
            return _home;
        }

        private static bool UsesFloor(BuildingRegistration home)
        {
            return home != null && home.dirtSpots != null && home.dirtSpots.Count > 0;
        }

        public static float Read()
        {
            BuildingRegistration home = Home();
            if (UsesFloor(home))
            {
                HomeState.Tidy = Mathf.Clamp(home.GetCleanliness(), 0f, 100f);
            }
            return HomeState.Tidy;
        }

        /// <summary>Tidier (positive) or messier (negative) by so many points of a hundred.</summary>
        public static void Change(float points)
        {
            Change(Home(), points);
        }

        /// <summary>The same for one home of the player's in particular (the one household staff work at); null: the figure alone.</summary>
        public static void Change(BuildingRegistration home, float points)
        {
            if (!UsesFloor(home))
            {
                HomeState.Tidy = HomeState.Clamp(HomeState.Tidy + points, 0f, 100f);
                return;
            }
            // The game's cleanliness is 100 less about twice the average dirt of the cells; small losses are saved up
            // so that a cell gets dirt it can show (the game draws it from 5 up) rather than a film over everything.
            _owed += points;
            if (_owed > -2f && _owed < 2f)
            {
                return;
            }
            float perCell = -_owed * 0.5f;
            _owed = 0f;
            List<DirtSpot> cells = home.dirtSpots;
            if (perCell < 0f)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    cells[i].dirtiness = Mathf.Max(0f, cells[i].dirtiness + perCell);
                }
            }
            else
            {
                // On a third of the cells, three times as much: patches, not an even grey.
                for (int i = 0; i < cells.Count; i++)
                {
                    if (UnityEngine.Random.value < 0.34f && cells[i].dirtiness < HomeBalance.DirtMost)
                    {
                        cells[i].dirtiness = Mathf.Min(HomeBalance.DirtMost, cells[i].dirtiness + perCell * 3f);
                    }
                }
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && manager != null && ReferenceEquals(manager.buildingRegistration, home))
            {
                manager.UpdateDirtinessInCurrentBuilding();
            }
            Read();
        }
    }
}
