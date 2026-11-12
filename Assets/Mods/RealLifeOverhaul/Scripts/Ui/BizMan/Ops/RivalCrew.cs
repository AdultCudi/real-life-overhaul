using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The nearest rival shop of the same kind as the one on the schedule, and how many staff it keeps (game side). A rival shop of the
    /// game's has its crew as AI employees (BuildingRegistration.aiEmployees); the game keeps no rota for them by the hour, so this is
    /// a head count, not who is in at the busy hour. Tycoon-owned shops (Rivals/) are the same registrations and are read the same
    /// way. Made once a game day for a place and kept; a pass over the save's registrations and one lookup of each candidate's
    /// building, never per frame.
    /// </summary>
    internal static class RivalCrew
    {
        private static BuildingRegistration _for;
        private static int _day = -1;
        private static string _name;
        private static int _staff;
        private static readonly List<BuildingRegistration> Found = new List<BuildingRegistration>(32);
        private static float[] _metres = new float[64];
        private static int[] _crew = new int[64];

        /// <summary>The nearest rival shop of the place's kind that keeps a crew: its name and staff. False when there is none, or the city is not there.</summary>
        public static bool Nearest(BuildingRegistration place, out string name, out int staff)
        {
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : -1;
            if (!ReferenceEquals(place, _for) || today != _day)
            {
                _for = place;
                _day = today;
                Read(place);
            }
            name = _name;
            staff = _staff;
            return staff > 0;
        }

        private static void Read(BuildingRegistration place)
        {
            _name = null;
            _staff = 0;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            CityBuildingController here = place != null && city != null ? city.FindCityBuildingController(place.Address) : null;
            if (here == null || save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            Found.Clear();
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (reg != null && !ReferenceEquals(reg, place) && reg.businessTypeName == place.businessTypeName && !string.IsNullOrEmpty(reg.businessOwnerRivalId)
                    && reg.aiEmployees != null && reg.aiEmployees.Count > 0 && reg.HasEstablishedBusiness && !GoingPublic.Mine(reg))
                {
                    Found.Add(reg);
                }
            }
            if (_metres.Length < Found.Count)
            {
                _metres = new float[Found.Count];
                _crew = new int[Found.Count];
            }
            for (int i = 0; i < Found.Count; i++)
            {
                CityBuildingController there = city.FindCityBuildingController(Found[i].Address);
                _metres[i] = there != null ? Vector3.Distance(here.transform.position, there.transform.position) : -1f;
                _crew[i] = Found[i].aiEmployees.Count;
            }
            int best = BizOpsRules.NearestCrew(_metres, _crew, Found.Count);
            if (best >= 0)
            {
                _name = Found[best].BusinessName;
                _staff = _crew[best];
            }
            Found.Clear();
        }
    }
}
