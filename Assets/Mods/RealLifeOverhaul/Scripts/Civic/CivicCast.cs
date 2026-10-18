using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Civic life's own people, five at most: a second instance of city life's pool, made a person at a time once something
    /// first needs people. Civic life asks city life's code for people only through this class and <see cref="CivicCrew"/>.
    /// </summary>
    public sealed class CivicCast
    {
        public const int Most = 5;

        private readonly CityPeople _people;
        private float _nextWarm;

        public CivicCast(GameObject host)
        {
            Tools = new CityProps(host.transform);
            _people = host.AddComponent<CityPeople>();
            _people.Setup(Tools);
        }

        /// <summary>The shelf the tools in their hands come from.</summary>
        public CityProps Tools { get; }

        /// <summary>True once this many people can be had. Until then it makes one more out of sight, two seconds apart, unless city life has seven of its eight out.</summary>
        public bool Ready(int needed, Vector3 near, float now)
        {
            if (_people == null)
            {
                return false;
            }
            needed = Mathf.Min(needed, Most);
            if (_people.Free >= needed)
            {
                return true;
            }
            if (_people.Count < Most && now >= _nextWarm && CivicBridges.PeopleOut() < CityPeople.MaxPeople - 1)
            {
                _nextWarm = now + 2f;
                _people.Warm(near + Vector3.down * 40f);
            }
            return false;
        }

        public CityPerson Take(CityLook look, Vector3 spot, Vector3 face)
        {
            return _people != null ? _people.Take(look, spot, face) : null;
        }

        public void Release(CityPerson person)
        {
            if (_people != null && person != null)
            {
                _people.Release(person);
            }
        }

        public void ReleaseAll()
        {
            if (_people != null)
            {
                _people.ReleaseAll();
            }
        }

        /// <summary>Tools dropped by somebody who was knocked down go back on the shelf once nobody is looking at them. Usually there is nothing to do.</summary>
        public void ClearFallen()
        {
            if (_people == null || _people.Fallen.Count == 0)
            {
                return;
            }
            List<Transform> fallen = _people.Fallen;
            for (int i = fallen.Count - 1; i >= 0; i--)
            {
                if (fallen[i] == null || !CitySpots.InView(fallen[i].position))
                {
                    Tools.Return(fallen[i]);
                    fallen.RemoveAt(i);
                }
            }
        }
    }
}
